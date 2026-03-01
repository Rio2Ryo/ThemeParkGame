// ============================================================
// ThemeParkGame - StaffManager
// スタッフ管理マネージャー
// ============================================================

using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using ThemeParkGame.Core;
using ThemeParkGame.UI;

namespace ThemeParkGame.Staff
{
    /// <summary>
    /// 全スタッフの雇用・解雇・給与・訓練・配置を一元管理するマネージャー。
    /// GameManagerのサブシステムとして動作する。
    ///
    /// 【ゲームデザイン】
    /// ・スタッフの雇用にはコストがかかり、月給が毎月の固定費として発生する
    /// ・適切な人数・配置・訓練の管理がパーク運営の成否を分ける
    /// ・スタッフ不足は直接的にパーク品質の低下を招く
    ///   - メカニック不足 → 故障・事故増加
    ///   - クリーナー不足 → 衛生悪化
    ///   - エンターテイナー不足 → 行列不満増加
    ///   - ガードマン不足 → 治安悪化
    ///   - サイエンティスト不足 → 研究停滞
    /// ・訓練への投資は長期的なリターンが大きい
    /// ・ストライキが発生するとスタッフが機能停止し、連鎖的に問題が拡大する
    /// </summary>
    public class StaffManager : MonoBehaviour
    {
        // ============================================================
        // 定数
        // ============================================================

        /// <summary>スタッフID生成用のカウンター初期値</summary>
        private const int InitialStaffIdCounter = 1000;

        /// <summary>
        /// スタッフ種別ごとの基本雇用コスト。
        /// 雇用時に一度だけ支払う初期費用。
        /// </summary>
        private static readonly Dictionary<StaffType, float> BaseHiringCosts = new Dictionary<StaffType, float>
        {
            { StaffType.Mechanic, 800f },
            { StaffType.Cleaner, 500f },
            { StaffType.Entertainer, 600f },
            { StaffType.Guard, 700f },
            { StaffType.Scientist, 1000f },
            { StaffType.Doctor, 900f },
            { StaffType.Vendor, 550f },
            { StaffType.Gardener, 450f }
        };

        /// <summary>
        /// スタッフ種別ごとの基本月給。
        /// 毎月の固定費として支払われる。
        /// </summary>
        private static readonly Dictionary<StaffType, float> BaseMonthlySalaries = new Dictionary<StaffType, float>
        {
            { StaffType.Mechanic, 400f },
            { StaffType.Cleaner, 300f },
            { StaffType.Entertainer, 350f },
            { StaffType.Guard, 350f },
            { StaffType.Scientist, 500f },
            { StaffType.Doctor, 450f },
            { StaffType.Vendor, 280f },
            { StaffType.Gardener, 250f }
        };

        // ============================================================
        // フィールド
        // ============================================================

        [Header("Staff Prefabs")]
        [SerializeField] private GameObject mechanicPrefab;
        [SerializeField] private GameObject cleanerPrefab;
        [SerializeField] private GameObject entertainerPrefab;
        [SerializeField] private GameObject guardPrefab;
        [SerializeField] private GameObject scientistPrefab;
        [SerializeField] private GameObject doctorPrefab;
        [SerializeField] private GameObject vendorPrefab;
        [SerializeField] private GameObject gardenerPrefab;

        [Header("Staff Room")]
        [SerializeField] private List<Transform> staffRooms = new List<Transform>();

        [Header("Settings")]
        [SerializeField] private int maxStaffPerType = 20;
        [SerializeField] private float strikeResolutionSalaryBonus = 100f;

        /// <summary>全スタッフのマスターリスト</summary>
        private readonly Dictionary<int, StaffMember> allStaff = new Dictionary<int, StaffMember>();

        /// <summary>種別ごとのスタッフリスト</summary>
        private readonly Dictionary<StaffType, List<StaffMember>> staffByType
            = new Dictionary<StaffType, List<StaffMember>>();

        /// <summary>スタッフID生成用カウンター</summary>
        private int nextStaffId;

        /// <summary>初期化済みフラグ</summary>
        private bool isInitialized;

        // ============================================================
        // プロパティ
        // ============================================================

        /// <summary>全スタッフ数</summary>
        public int TotalStaffCount => allStaff.Count;

        /// <summary>現在ストライキ中のスタッフ数</summary>
        public int StrikingStaffCount => allStaff.Values.Count(s => s.IsOnStrike);

        /// <summary>月間総人件費</summary>
        public float TotalMonthlySalary => allStaff.Values.Sum(s => s.Salary);

        // ============================================================
        // 初期化
        // ============================================================

        /// <summary>
        /// スタッフ管理システムを初期化する。GameManager.StartNewGame() から呼ばれる。
        /// </summary>
        public void Initialize()
        {
            nextStaffId = InitialStaffIdCounter;
            allStaff.Clear();

            // 種別ごとのリストを初期化
            foreach (StaffType type in Enum.GetValues(typeof(StaffType)))
            {
                staffByType[type] = new List<StaffMember>();
            }

            isInitialized = true;
            WebGLOptimizer.LogVerbose("[StaffManager] 初期化完了");
        }

        // ============================================================
        // イベント購読
        // ============================================================

        private void OnEnable()
        {
            GameEvents.OnParkYearPassed += HandleYearPassed;
            GameEvents.OnStaffWentOnStrike += HandleStaffStrike;
        }

        private void OnDisable()
        {
            GameEvents.OnParkYearPassed -= HandleYearPassed;
            GameEvents.OnStaffWentOnStrike -= HandleStaffStrike;
        }

        /// <summary>
        /// 年度末処理。月給の支払いは実際にはTimeManagerのサイクルで行うが、
        /// 年度統計のリセットなどをここで行う。
        /// </summary>
        private void HandleYearPassed(int year)
        {
            WebGLOptimizer.LogVerbose($"[StaffManager] {year}年度末: スタッフ数={TotalStaffCount}"
                      + $", ストライキ中={StrikingStaffCount}");
        }

        /// <summary>ストライキ発生時のハンドラ</summary>
        private void HandleStaffStrike(int staffId)
        {
            WebGLOptimizer.LogWarning($"[StaffManager] スタッフ (ID:{staffId}) がストライキに入りました！"
                             + $" 現在のストライキ人数: {StrikingStaffCount}");

            // ストライキ発生を通知
            if (NotificationSystem.Instance != null)
                NotificationSystem.Instance.Notify("スタッフがストライキ！休息場所を確保してください", NotifLevel.Warning);
        }

        /// <summary>疲労警告チェック用タイマー</summary>
        private float _fatigueCheckTimer;

        private void Update()
        {
            if (!isInitialized || allStaff.Count == 0) return;

            _fatigueCheckTimer -= Time.deltaTime;
            if (_fatigueCheckTimer > 0f) return;
            _fatigueCheckTimer = 30f; // 30秒ごとにチェック

            int highFatigueCount = 0;
            foreach (var staff in allStaff.Values)
            {
                if (staff != null && staff.Fatigue > 75f && !staff.IsOnStrike)
                    highFatigueCount++;
            }

            if (highFatigueCount >= 2 && NotificationSystem.Instance != null)
            {
                NotificationSystem.Instance.Notify(
                    $"スタッフ{highFatigueCount}名が疲労困憊！スタッフルームを増設しましょう",
                    NotifLevel.Warning);
            }
        }

        // ============================================================
        // 雇用
        // ============================================================

        /// <summary>
        /// 新しいスタッフを雇用する。
        ///
        /// 【ゲームデザイン】
        /// ・雇用コストが即座に差し引かれる
        /// ・以降、毎月の月給が固定費として発生する
        /// ・種別ごとの上限数に達している場合は雇用できない
        /// ・資金不足の場合も雇用できない
        /// </summary>
        /// <param name="type">雇用するスタッフの種別</param>
        /// <param name="spawnPosition">スタッフの出現位置</param>
        /// <param name="staffName">スタッフ名（nullの場合は自動生成）</param>
        /// <returns>雇用に成功した場合はStaffMember、失敗した場合はnull</returns>
        public StaffMember HireStaff(StaffType type, Vector3 spawnPosition, string staffName = null)
        {
            if (!isInitialized)
            {
                WebGLOptimizer.LogError("[StaffManager] 未初期化です。Initialize()を先に呼んでください");
                return null;
            }

            // 上限チェック
            if (GetStaffCountByType(type) >= maxStaffPerType)
            {
                WebGLOptimizer.LogWarning($"[StaffManager] {type} の雇用上限 ({maxStaffPerType}) に達しています");
                return null;
            }

            // 雇用コスト確認・支払い処理
            float hiringCost = GetHiringCost(type);
            if (GameManager.Instance != null && GameManager.Instance.EconomyManager != null)
            {
                if (!GameManager.Instance.EconomyManager.CanAfford(hiringCost))
                {
                    WebGLOptimizer.LogWarning($"[StaffManager] 資金不足。雇用コスト: {hiringCost:F0}");
                    return null;
                }
                GameManager.Instance.EconomyManager.PayExpense(hiringCost, Economy.ExpenseCategory.Other);
            }

            // プレハブからスタッフを生成
            GameObject prefab = GetPrefabForType(type);
            if (prefab == null)
            {
                WebGLOptimizer.LogError($"[StaffManager] {type} のプレハブが設定されていません");
                return null;
            }

            GameObject staffObj = Instantiate(prefab, spawnPosition, Quaternion.identity);
            StaffMember staff = staffObj.GetComponent<StaffMember>();
            if (staff == null)
            {
                WebGLOptimizer.LogError($"[StaffManager] プレハブにStaffMemberコンポーネントがありません: {type}");
                Destroy(staffObj);
                return null;
            }

            // アクティベート（プレハブが非アクティブの場合があるため）
            staffObj.SetActive(true);

            // 初期化
            int staffId = nextStaffId++;
            string name = staffName ?? GenerateStaffName(type, staffId);
            staff.Initialize(staffId, name, type);
            staff.Salary = BaseMonthlySalaries[type];

            // スタッフルーム設定
            Transform nearestRoom = FindNearestStaffRoom(spawnPosition);
            if (nearestRoom != null)
            {
                staff.SetStaffRoom(nearestRoom);
            }

            // 登録
            allStaff[staffId] = staff;
            staffByType[type].Add(staff);

            // 費用通知
            GameEvents.FireExpensePaid(hiringCost);
            GameEvents.FireStaffHired(staffId, type);

            WebGLOptimizer.LogVerbose($"[StaffManager] {type} を雇用しました: {name} (ID:{staffId})"
                      + $" 雇用コスト: {hiringCost}, 月給: {staff.Salary}");

            return staff;
        }

        // ============================================================
        // 解雇
        // ============================================================

        /// <summary>
        /// スタッフを解雇する。
        ///
        /// 【ゲームデザイン】
        /// ・解雇は即座に行われ、以降の月給は発生しなくなる
        /// ・解雇されたスタッフはパークから退場する
        /// ・ストライキ中のスタッフも解雇可能
        /// </summary>
        /// <param name="staffId">解雇するスタッフのID</param>
        /// <returns>解雇に成功した場合true</returns>
        public bool FireStaff(int staffId)
        {
            if (!allStaff.TryGetValue(staffId, out StaffMember staff))
            {
                WebGLOptimizer.LogWarning($"[StaffManager] スタッフ (ID:{staffId}) が見つかりません");
                return false;
            }

            StaffType type = staff.StaffType;
            string name = staff.Name;

            // サイエンティストの場合はラボから撤去
            if (staff is ScientistStaff scientist)
            {
                scientist.RemoveFromLab();
            }

            // リストから除去
            allStaff.Remove(staffId);
            staffByType[type].Remove(staff);

            // GameObjectを破棄
            if (staff.gameObject != null)
            {
                Destroy(staff.gameObject);
            }

            GameEvents.FireStaffFired(staffId, type);

            WebGLOptimizer.LogVerbose($"[StaffManager] {type} を解雇しました: {name} (ID:{staffId})");
            return true;
        }

        // ============================================================
        // 給与管理
        // ============================================================

        /// <summary>
        /// 月次給与を全スタッフに支払う。
        /// TimeManagerの月サイクル終了時に呼ばれる想定。
        ///
        /// 【ゲームデザイン】
        /// ・全スタッフの月給合計がパークの固定費として計上される
        /// ・資金不足の場合でも支払いは行われるが、赤字警告が発生する
        /// </summary>
        /// <returns>支払った総額</returns>
        public float PayMonthlySalaries()
        {
            float totalPaid = 0f;

            foreach (var staff in allStaff.Values)
            {
                // シフトに応じた給与倍率を適用（夜勤25%割増、終日50%割増）
                totalPaid += staff.Salary * staff.ShiftSalaryMultiplier;
            }

            if (totalPaid > 0f)
            {
                // EconomyManagerを通じて実際に資金を差し引く
                if (GameManager.Instance != null && GameManager.Instance.EconomyManager != null)
                {
                    GameManager.Instance.EconomyManager.PayExpense(totalPaid, Economy.ExpenseCategory.StaffSalary);
                }

                WebGLOptimizer.LogVerbose($"[StaffManager] 月次給与支払い: {totalPaid:F0}"
                          + $"（{allStaff.Count}名）");
            }

            return totalPaid;
        }

        /// <summary>
        /// 特定スタッフの給与を変更する。
        ///
        /// 【ゲームデザイン】
        /// ・給与の増額はスタッフの満足度を上げ、ストライキリスクを低下させる
        /// ・給与の減額は不満を招く可能性がある
        /// </summary>
        public void SetStaffSalary(int staffId, float newSalary)
        {
            if (allStaff.TryGetValue(staffId, out StaffMember staff))
            {
                float oldSalary = staff.Salary;
                staff.Salary = newSalary;
                WebGLOptimizer.LogVerbose($"[StaffManager] {staff.Name} の給与変更: {oldSalary:F0} → {newSalary:F0}");
            }
        }

        // ============================================================
        // 訓練
        // ============================================================

        /// <summary>
        /// スタッフの訓練を実施する。
        ///
        /// 【ゲームデザイン】
        /// ・訓練コストはスキルレベルに応じて増加する
        /// ・訓練投資は長期的に大きなリターンをもたらす
        ///   - メカニック: 修理速度向上 → ダウンタイム短縮
        ///   - クリーナー: 清掃速度向上 → 衛生向上
        ///   - エンターテイナー: 効果範囲拡大 → 幸福度向上
        ///   - ガードマン: 検知・追跡速度向上 → 治安向上
        ///   - サイエンティスト: 研究速度向上 → 早期アンロック
        /// </summary>
        /// <param name="staffId">訓練するスタッフのID</param>
        /// <returns>訓練が実行できた場合true</returns>
        public bool TrainStaff(int staffId)
        {
            if (!allStaff.TryGetValue(staffId, out StaffMember staff))
            {
                WebGLOptimizer.LogWarning($"[StaffManager] スタッフ (ID:{staffId}) が見つかりません");
                return false;
            }

            float cost = staff.GetTrainingCost();
            if (cost <= 0f)
            {
                WebGLOptimizer.LogVerbose($"[StaffManager] {staff.Name} は既に最高スキルレベルです");
                return false;
            }

            // 訓練費用の確認・支払い
            if (GameManager.Instance != null && GameManager.Instance.EconomyManager != null)
            {
                if (!GameManager.Instance.EconomyManager.CanAfford(cost))
                {
                    WebGLOptimizer.LogWarning($"[StaffManager] 訓練資金不足。コスト: {cost:F0}");
                    return false;
                }
                GameManager.Instance.EconomyManager.PayExpense(cost, Economy.ExpenseCategory.Other);
            }

            if (staff.Train())
            {
                GameEvents.FireExpensePaid(cost);
                WebGLOptimizer.LogVerbose($"[StaffManager] {staff.Name} を訓練しました"
                          + $"（コスト: {cost:F0}, 新スキルレベル: {staff.SkillLevel}）");
                return true;
            }

            return false;
        }

        // ============================================================
        // ストライキ管理
        // ============================================================

        /// <summary>
        /// ストライキ中の全スタッフのストライキを解除する。
        ///
        /// 【ゲームデザイン】
        /// ・ストライキ解除時に給与ボーナスを支払う必要がある
        /// ・ボーナス支払い後、スタッフは休息状態に移行する
        /// ・根本的な解決（スタッフルーム増設、人員増加）をしないと再発する
        /// </summary>
        /// <returns>解除したスタッフの数</returns>
        public int ResolveAllStrikes()
        {
            int resolvedCount = 0;
            float totalBonus = 0f;

            foreach (var staff in allStaff.Values)
            {
                if (staff.IsOnStrike)
                {
                    staff.Salary += strikeResolutionSalaryBonus;
                    staff.ResolveStrike();
                    totalBonus += strikeResolutionSalaryBonus;
                    resolvedCount++;
                }
            }

            if (resolvedCount > 0)
            {
                GameEvents.FireExpensePaid(totalBonus);
                WebGLOptimizer.LogVerbose($"[StaffManager] {resolvedCount}名のストライキを解除しました"
                          + $"（ボーナス総額: {totalBonus:F0}）");
            }

            return resolvedCount;
        }

        /// <summary>
        /// 特定のスタッフのストライキを解除する。
        /// </summary>
        public bool ResolveStrike(int staffId)
        {
            if (!allStaff.TryGetValue(staffId, out StaffMember staff))
            {
                return false;
            }

            if (!staff.IsOnStrike) return false;

            staff.Salary += strikeResolutionSalaryBonus;
            staff.ResolveStrike();
            GameEvents.FireExpensePaid(strikeResolutionSalaryBonus);

            WebGLOptimizer.LogVerbose($"[StaffManager] {staff.Name} のストライキを解除しました"
                      + $"（ボーナス: {strikeResolutionSalaryBonus:F0}）");
            return true;
        }

        // ============================================================
        // スタッフ配置
        // ============================================================

        /// <summary>
        /// スタッフにパトロールエリアを割り当てる。
        /// </summary>
        public void AssignPatrolArea(int staffId, Bounds area)
        {
            if (allStaff.TryGetValue(staffId, out StaffMember staff))
            {
                staff.SetPatrolArea(area);
                WebGLOptimizer.LogVerbose($"[StaffManager] {staff.Name} にパトロールエリアを割り当てました");
            }
        }

        /// <summary>
        /// スタッフにパトロール地点リストを割り当てる。
        /// </summary>
        public void AssignPatrolPoints(int staffId, List<Transform> points)
        {
            if (allStaff.TryGetValue(staffId, out StaffMember staff))
            {
                staff.SetPatrolPoints(points);
                WebGLOptimizer.LogVerbose($"[StaffManager] {staff.Name} にパトロール地点を割り当てました"
                          + $"（{points.Count}地点）");
            }
        }

        /// <summary>
        /// 指定エリアにスタッフを自動配置する。
        ///
        /// 【ゲームデザイン】
        /// ・プレイヤーがエリアを指定すると、空いているスタッフが自動的に配置される
        /// ・既にパトロールエリアが割り当てられているスタッフは対象外
        /// ・配置可能なスタッフがいない場合はfalseを返す
        /// </summary>
        public bool AutoAssignStaffToArea(StaffType type, Bounds area)
        {
            if (!staffByType.ContainsKey(type)) return false;

            var available = staffByType[type]
                .Where(s => !s.HasPatrolArea && !s.IsOnStrike)
                .FirstOrDefault();

            if (available == null)
            {
                WebGLOptimizer.LogVerbose($"[StaffManager] 配置可能な {type} がいません");
                return false;
            }

            available.SetPatrolArea(area);
            WebGLOptimizer.LogVerbose($"[StaffManager] {available.Name} をエリアに自動配置しました");
            return true;
        }

        /// <summary>
        /// サイエンティストをラボに配置する。
        /// </summary>
        public bool AssignScientistToLab(int staffId, Transform lab, int labId, int slotIndex)
        {
            if (!allStaff.TryGetValue(staffId, out StaffMember staff))
            {
                return false;
            }

            if (staff is ScientistStaff scientist)
            {
                scientist.AssignToLab(lab, labId, slotIndex);
                return true;
            }

            WebGLOptimizer.LogWarning($"[StaffManager] スタッフ (ID:{staffId}) はサイエンティストではありません");
            return false;
        }

        // ============================================================
        // シフト・ゾーン管理
        // ============================================================

        /// <summary>
        /// スタッフの勤務シフトを設定する。
        /// </summary>
        public void SetStaffShift(int staffId, ShiftType shift)
        {
            if (allStaff.TryGetValue(staffId, out StaffMember staff))
            {
                staff.CurrentShift = shift;
                WebGLOptimizer.LogVerbose($"[StaffManager] {staff.Name} のシフトを {shift} に設定しました");
            }
        }

        /// <summary>
        /// スタッフの担当ゾーンを設定する。
        /// </summary>
        public void SetStaffZone(int staffId, ThemeZone? zone)
        {
            if (allStaff.TryGetValue(staffId, out StaffMember staff))
            {
                staff.AssignedZone = zone;
                string zoneName = zone.HasValue ? zone.Value.ToString() : "全エリア";
                WebGLOptimizer.LogVerbose($"[StaffManager] {staff.Name} の担当ゾーンを {zoneName} に設定しました");
            }
        }

        /// <summary>
        /// 全スタッフの連続勤務日数を1日加算する。日次処理で呼ばれる。
        /// シフトがAllDayのスタッフは毎日加算される。
        /// </summary>
        public void AdvanceShiftDay()
        {
            foreach (var staff in allStaff.Values)
            {
                if (staff.CurrentShift == ShiftType.AllDay)
                {
                    staff.ConsecutiveShiftDays++;
                }
                else
                {
                    // シフト制のスタッフは勤務外時間に休めるため連続勤務はリセット
                    staff.ConsecutiveShiftDays = 0;
                }
            }
        }

        /// <summary>
        /// 現在勤務時間内のスタッフ数を種別ごとに取得する。
        /// 人員不足の判定に使用。
        /// </summary>
        public int GetOnDutyStaffCount(StaffType type)
        {
            if (!staffByType.ContainsKey(type)) return 0;
            int count = 0;
            foreach (var staff in staffByType[type])
            {
                if (staff.IsOnDuty && !staff.IsOnStrike) count++;
            }
            return count;
        }

        /// <summary>
        /// 指定ゾーンに配置されているスタッフ数を取得する。
        /// </summary>
        public int GetStaffCountInZone(StaffType type, ThemeZone zone)
        {
            if (!staffByType.ContainsKey(type)) return 0;
            int count = 0;
            foreach (var staff in staffByType[type])
            {
                if (staff.AssignedZone == zone && !staff.IsOnStrike) count++;
            }
            return count;
        }

        /// <summary>
        /// シフト配置を自動最適化する。
        /// 各シフトに均等に配置し、夜間は最低限の人数を確保する。
        /// </summary>
        public void AutoOptimizeShifts(StaffType type)
        {
            if (!staffByType.ContainsKey(type)) return;

            var staffList = staffByType[type];
            if (staffList.Count == 0) return;

            int total = staffList.Count;
            // 3人以下は全員AllDayのまま
            if (total <= 3)
            {
                foreach (var s in staffList)
                    s.CurrentShift = ShiftType.AllDay;
                return;
            }

            // 4人以上: Morning/Day/Night を均等に、余りはDayに
            int perShift = total / 3;
            int remainder = total % 3;
            int idx = 0;

            for (int i = 0; i < perShift; i++)
                staffList[idx++].CurrentShift = ShiftType.Morning;
            for (int i = 0; i < perShift + (remainder > 0 ? 1 : 0); i++)
                staffList[idx++].CurrentShift = ShiftType.Day;
            remainder = remainder > 1 ? remainder - 1 : 0;
            for (int i = 0; i < perShift + (remainder > 0 ? 1 : 0); i++)
            {
                if (idx >= total) break;
                staffList[idx++].CurrentShift = ShiftType.Night;
            }

            WebGLOptimizer.LogVerbose($"[StaffManager] {type} のシフトを自動最適化しました（{total}名）");
        }

        // ============================================================
        // 検索・取得
        // ============================================================

        /// <summary>IDでスタッフを取得する</summary>
        public StaffMember GetStaffById(int staffId)
        {
            allStaff.TryGetValue(staffId, out StaffMember staff);
            return staff;
        }

        /// <summary>種別ごとのスタッフ数を取得する</summary>
        public int GetStaffCountByType(StaffType type)
        {
            return staffByType.ContainsKey(type) ? staffByType[type].Count : 0;
        }

        /// <summary>種別ごとのスタッフリストを取得する</summary>
        public IReadOnlyList<StaffMember> GetStaffByType(StaffType type)
        {
            return staffByType.ContainsKey(type) ? staffByType[type] : new List<StaffMember>();
        }

        /// <summary>全スタッフのリストを取得する</summary>
        public IReadOnlyCollection<StaffMember> GetAllStaff()
        {
            return allStaff.Values;
        }

        /// <summary>指定タイプのスタッフが1人以上いるか</summary>
        public bool HasStaffOfType(StaffType type)
        {
            return staffByType.ContainsKey(type) && staffByType[type].Count > 0;
        }

        /// <summary>
        /// 指定位置から最も近い空きスタッフを検索する。
        /// 緊急タスク（故障など）の割り当てに使用する。
        /// </summary>
        public StaffMember FindNearestAvailableStaff(StaffType type, Vector3 position)
        {
            if (!staffByType.ContainsKey(type)) return null;

            StaffMember nearest = null;
            float nearestDist = float.MaxValue;

            foreach (var staff in staffByType[type])
            {
                if (!staff.IsAvailable) continue;

                float dist = Vector3.Distance(staff.transform.position, position);
                if (dist < nearestDist)
                {
                    nearestDist = dist;
                    nearest = staff;
                }
            }

            return nearest;
        }

        // ============================================================
        // UI向けデータ取得
        // ============================================================

        /// <summary>種別ごとのスタッフ数を取得する（GetStaffCountByTypeのエイリアス）</summary>
        public int GetStaffCount(StaffType type)
        {
            return GetStaffCountByType(type);
        }

        /// <summary>
        /// 指定スタッフのUI表示用データを取得する。
        /// </summary>
        /// <param name="staffId">スタッフID</param>
        /// <returns>スタッフ表示データ。見つからない場合はnull</returns>
        public StaffDisplayData GetStaffData(int staffId)
        {
            if (!allStaff.TryGetValue(staffId, out StaffMember staff))
            {
                return null;
            }

            return ConvertToDisplayData(staff);
        }

        /// <summary>
        /// 指定種別のスタッフ一覧をUI表示用データのリストとして取得する。
        /// </summary>
        /// <param name="type">スタッフ種別</param>
        /// <returns>スタッフ表示データのリスト</returns>
        public List<StaffDisplayData> GetStaffListByType(StaffType type)
        {
            if (!staffByType.ContainsKey(type))
            {
                return new List<StaffDisplayData>();
            }

            var result = new List<StaffDisplayData>();
            foreach (var staff in staffByType[type])
            {
                result.Add(ConvertToDisplayData(staff));
            }
            return result;
        }

        /// <summary>
        /// StaffMemberからStaffDisplayDataへ変換するヘルパー。
        /// </summary>
        private StaffDisplayData ConvertToDisplayData(StaffMember staff)
        {
            return new StaffDisplayData
            {
                StaffId = staff.Id,
                Name = staff.Name,
                Type = staff.StaffType,
                Portrait = null, // ポートレートはプレハブ側で管理
                SkillLevel = (float)(staff.SkillLevel - StaffMember.MinSkillLevel)
                             / (StaffMember.MaxSkillLevel - StaffMember.MinSkillLevel),
                Fatigue = staff.Fatigue / StaffMember.MaxFatigue,
                BehaviorState = staff.CurrentState
            };
        }

        // ============================================================
        // UI操作向けコマンド
        // ============================================================

        /// <summary>
        /// パトロールエリア指定モードを開始する。
        /// UIからパトロールエリア割り当て操作が開始された際に呼ばれる。
        /// 実際のエリア選択はInputManager等で処理し、完了時にAssignPatrolAreaが呼ばれる想定。
        /// </summary>
        /// <param name="staffId">パトロールを指定するスタッフのID</param>
        public void StartPatrolAreaAssignment(int staffId)
        {
            if (!allStaff.TryGetValue(staffId, out StaffMember staff))
            {
                WebGLOptimizer.LogWarning($"[StaffManager] スタッフ (ID:{staffId}) が見つかりません");
                return;
            }

            WebGLOptimizer.LogVerbose($"[StaffManager] パトロールエリア指定モード開始: {staff.Name} (ID:{staffId})");

            // スタッフの現在位置を中心にパトロールエリアを自動設定
            Vector3 pos = staff.transform.position;
            float patrolRadius = 15f;
            staff.SetPatrolArea(new Bounds(pos, new Vector3(patrolRadius * 2f, 5f, patrolRadius * 2f)));
            GameManager.Instance?.ShowNotification(
                $"{staff.Name} のパトロールエリアを現在地付近に設定しました", NotifLevel.Info);
        }

        /// <summary>
        /// スタッフをスタッフルームへ送って休憩させる。
        /// </summary>
        /// <param name="staffId">休憩させるスタッフのID</param>
        public void SendToStaffRoom(int staffId)
        {
            if (!allStaff.TryGetValue(staffId, out StaffMember staff))
            {
                WebGLOptimizer.LogWarning($"[StaffManager] スタッフ (ID:{staffId}) が見つかりません");
                return;
            }

            if (staff.IsOnStrike)
            {
                WebGLOptimizer.LogWarning($"[StaffManager] {staff.Name} はストライキ中のため休憩指示できません");
                return;
            }

            // 最寄りのスタッフルームを設定して休息を開始
            Transform nearestRoom = FindNearestStaffRoom(staff.transform.position);
            if (nearestRoom != null)
            {
                staff.SetStaffRoom(nearestRoom);
            }

            staff.SendToRest();
            WebGLOptimizer.LogVerbose($"[StaffManager] {staff.Name} をスタッフルームへ送りました");
        }

        // ============================================================
        // 統計
        // ============================================================

        /// <summary>
        /// スタッフ全体の満足度スコアを計算する (0-100)。
        ///
        /// 【ゲームデザイン】
        /// ・スタッフ満足度はパーク全体の運営品質に影響する
        /// ・満足度が低いとストライキリスクが上昇する
        /// ・疲労の平均値が主要な指標となる
        /// </summary>
        public float CalculateStaffSatisfaction()
        {
            if (allStaff.Count == 0) return 100f;

            float totalSatisfaction = 0f;
            foreach (var staff in allStaff.Values)
            {
                // 満足度 = 100 - 疲労値（ストライキ中は0）
                float satisfaction = staff.IsOnStrike ? 0f : 100f - staff.Fatigue;
                totalSatisfaction += satisfaction;
            }

            return totalSatisfaction / allStaff.Count;
        }

        /// <summary>
        /// 種別ごとのスタッフ統計サマリーを取得する。
        /// UI表示やデバッグ用。
        /// </summary>
        public StaffTypeSummary GetTypeSummary(StaffType type)
        {
            var list = staffByType.ContainsKey(type) ? staffByType[type] : new List<StaffMember>();

            return new StaffTypeSummary
            {
                Type = type,
                Count = list.Count,
                AverageSkillLevel = list.Count > 0 ? (float)list.Average(s => s.SkillLevel) : 0f,
                AverageFatigue = list.Count > 0 ? (float)list.Average(s => s.Fatigue) : 0f,
                StrikingCount = list.Count(s => s.IsOnStrike),
                TotalMonthlySalary = list.Sum(s => s.Salary * s.ShiftSalaryMultiplier),
                OnDutyCount = list.Count(s => s.IsOnDuty && !s.IsOnStrike)
            };
        }

        // ============================================================
        // コスト計算
        // ============================================================

        /// <summary>スタッフ種別の雇用コストを取得する</summary>
        public float GetHiringCost(StaffType type)
        {
            return BaseHiringCosts.TryGetValue(type, out float cost) ? cost : 500f;
        }

        /// <summary>スタッフ種別の基本月給を取得する</summary>
        public float GetBaseSalary(StaffType type)
        {
            return BaseMonthlySalaries.TryGetValue(type, out float salary) ? salary : 300f;
        }

        /// <summary>メカニックの平均スキルレベルを取得する（1.0～5.0）</summary>
        public float GetAverageMechanicSkill()
        {
            if (!staffByType.ContainsKey(StaffType.Mechanic) ||
                staffByType[StaffType.Mechanic].Count == 0)
                return 1f;

            float totalSkill = 0f;
            int count = 0;

            foreach (var staff in staffByType[StaffType.Mechanic])
            {
                totalSkill += staff.SkillLevel;
                count++;
            }

            return count > 0 ? totalSkill / count : 1f;
        }

        // ============================================================
        // ユーティリティ
        // ============================================================

        /// <summary>
        /// ランタイムでスタッフプレハブを設定する。
        /// RuntimeGameSetupから呼ばれる（エディタ外でのプレハブ注入用）。
        /// </summary>
        public void ConfigureRuntimePrefabs(
            GameObject mechanic, GameObject cleaner,
            GameObject entertainer, GameObject guard, GameObject scientist,
            GameObject doctor = null, GameObject vendor = null, GameObject gardener = null)
        {
            mechanicPrefab = mechanic;
            cleanerPrefab = cleaner;
            entertainerPrefab = entertainer;
            guardPrefab = guard;
            scientistPrefab = scientist;
            doctorPrefab = doctor;
            vendorPrefab = vendor;
            gardenerPrefab = gardener;
        }

        /// <summary>スタッフ種別に対応するプレハブを取得する</summary>
        private GameObject GetPrefabForType(StaffType type)
        {
            return type switch
            {
                StaffType.Mechanic => mechanicPrefab,
                StaffType.Cleaner => cleanerPrefab,
                StaffType.Entertainer => entertainerPrefab,
                StaffType.Guard => guardPrefab,
                StaffType.Scientist => scientistPrefab,
                StaffType.Doctor => doctorPrefab,
                StaffType.Vendor => vendorPrefab,
                StaffType.Gardener => gardenerPrefab,
                _ => null
            };
        }

        /// <summary>最寄りのスタッフルームを検索する</summary>
        private Transform FindNearestStaffRoom(Vector3 position)
        {
            if (staffRooms == null || staffRooms.Count == 0) return null;

            Transform nearest = null;
            float nearestDist = float.MaxValue;

            foreach (var room in staffRooms)
            {
                if (room == null) continue;
                float dist = Vector3.Distance(position, room.position);
                if (dist < nearestDist)
                {
                    nearestDist = dist;
                    nearest = room;
                }
            }

            return nearest;
        }

        /// <summary>スタッフ名を自動生成する</summary>
        private string GenerateStaffName(StaffType type, int id)
        {
            string prefix = type switch
            {
                StaffType.Mechanic => "Mechanic",
                StaffType.Cleaner => "Cleaner",
                StaffType.Entertainer => "Entertainer",
                StaffType.Guard => "Guard",
                StaffType.Scientist => "Scientist",
                StaffType.Doctor => "Doctor",
                StaffType.Vendor => "Vendor",
                StaffType.Gardener => "Gardener",
                _ => "Staff"
            };

            return $"{prefix}_{id}";
        }

        /// <summary>スタッフルームを登録する</summary>
        public void RegisterStaffRoom(Transform room)
        {
            if (room != null && !staffRooms.Contains(room))
            {
                staffRooms.Add(room);

                // 既存スタッフに最寄りのスタッフルームを再割り当て
                foreach (var staff in allStaff.Values)
                {
                    Transform nearest = FindNearestStaffRoom(staff.transform.position);
                    if (nearest != null)
                    {
                        staff.SetStaffRoom(nearest);
                    }
                }

                WebGLOptimizer.LogVerbose($"[StaffManager] スタッフルームを登録しました（合計: {staffRooms.Count}）");
            }
        }

        /// <summary>スタッフルームの登録を解除する</summary>
        public void UnregisterStaffRoom(Transform room)
        {
            staffRooms.Remove(room);
        }

        // ============================================================
        // クリーンアップ
        // ============================================================

        /// <summary>
        /// 全スタッフを削除してシステムをリセットする。
        /// ゲーム終了時やシナリオ変更時に呼ばれる。
        /// </summary>
        public void ClearAllStaff()
        {
            foreach (var staff in allStaff.Values)
            {
                if (staff != null && staff.gameObject != null)
                {
                    Destroy(staff.gameObject);
                }
            }

            allStaff.Clear();
            foreach (var list in staffByType.Values)
            {
                list.Clear();
            }

            MechanicStaff.ClearRepairQueue();

            WebGLOptimizer.LogVerbose("[StaffManager] 全スタッフを削除しました");
        }
    }

    // ============================================================
    // スタッフ種別サマリー構造体
    // ============================================================

    /// <summary>
    /// スタッフ種別ごとの統計サマリー。
    /// UI表示やバランス調整のデバッグに使用する。
    /// </summary>
    [Serializable]
    public struct StaffTypeSummary
    {
        public StaffType Type;
        public int Count;
        public float AverageSkillLevel;
        public float AverageFatigue;
        public int StrikingCount;
        public float TotalMonthlySalary;
        public int OnDutyCount;

        public override string ToString()
        {
            return $"[{Type}] 人数:{Count}, 勤務中:{OnDutyCount}, 平均Lv:{AverageSkillLevel:F1}"
                   + $", 平均疲労:{AverageFatigue:F0}, ストライキ:{StrikingCount}"
                   + $", 月給合計:{TotalMonthlySalary:F0}";
        }
    }
}
