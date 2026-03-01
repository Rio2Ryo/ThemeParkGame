// ============================================================
// ThemeParkGame - RepeaterSystem
// リピーター（再来場者）記憶・管理システム
// 満足度の高い来場者が後日再訪する仕組みを実現する。
// ロイヤリティスコアに応じて再来場確率・ボーナスが上昇。
// ============================================================

using System;
using System.Collections.Generic;
using UnityEngine;
using ThemeParkGame.Core;

namespace ThemeParkGame.Visitor
{
    // ================================================================
    // 来場記録データ
    // ================================================================

    /// <summary>
    /// 来場者の1回の訪問記録。退園時にスナップショットとして保存される。
    /// 【ゲームデザイン】
    /// ・満足度60以上の来場者のみリピーター候補に登録される。
    /// ・お気に入りアトラクション上位3つと最悪アトラクションを記憶し、
    ///   再来園時のAI行動選択に活用する（将来拡張用）。
    /// </summary>
    [Serializable]
    public class VisitorVisitRecord
    {
        /// <summary>来場者名（リピーターDBのキー）</summary>
        public string VisitorName;

        /// <summary>来場者タイプ（Kids/Young/Family等）</summary>
        public VisitorType Type;

        /// <summary>退園時の最終満足度（0-100）</summary>
        public float FinalSatisfaction;

        /// <summary>退園時の最終幸福度（0-100）</summary>
        public float FinalHappiness;

        /// <summary>体験したアトラクション数</summary>
        public int RidesExperienced;

        /// <summary>お気に入りアトラクション上位3つ</summary>
        public List<string> FavoriteAttractionNames;

        /// <summary>最も評価の低かったアトラクション名</summary>
        public string WorstAttractionName;

        /// <summary>来園したゲーム内日数</summary>
        public int VisitDay;

        /// <summary>来園したゲーム内月</summary>
        public int VisitMonth;

        /// <summary>累計来園回数（初回=1）</summary>
        public int RepeatCount;

        /// <summary>総支出額</summary>
        public float SpentAmount;
    }

    // ================================================================
    // リピーターデータ
    // ================================================================

    /// <summary>
    /// リピーター1人分の統合データ。来園履歴・ロイヤリティ・次回来園資格を管理する。
    /// 【ゲームデザイン】
    /// ・ロイヤリティスコアは来園回数と満足度に比例して上昇（最大100）。
    /// ・ロイヤリティが高いほど再来園確率が上がり、支出倍率も向上する。
    /// ・次回来園可能日は満足度が高いほど短縮される（リピートを早めたい設計）。
    /// </summary>
    [Serializable]
    public class RepeaterData
    {
        /// <summary>直近の来園記録</summary>
        public VisitorVisitRecord LastVisit;

        /// <summary>累計来園回数</summary>
        public int TotalVisits;

        /// <summary>全来園の平均満足度</summary>
        public float AverageSatisfaction;

        /// <summary>ロイヤリティスコア（0-100）。来園と満足度で上昇する</summary>
        public float LoyaltyScore;

        /// <summary>次回来園可能となる最早ゲーム日</summary>
        public int NextEligibleDay;
    }

    // ================================================================
    // リピーター管理システム本体
    // ================================================================

    /// <summary>
    /// リピーター（再来場者）のスポーン管理・記憶保持を担うシングルトン。
    /// 【ゲームデザイン】
    /// ・パーク運営が軌道に乗ると満足した来場者がリピーターとして再訪する。
    /// ・リピーターは初回来場者より多くの金額を使い、SNS拡散力も高い。
    /// ・1日あたりのリピーター上限を設けてバランスを維持する。
    /// ・30～60日の再訪間隔により長期運営のモチベーションを支える。
    /// </summary>
    public class RepeaterSystem : MonoBehaviour
    {
        public static RepeaterSystem Instance { get; private set; }

        // ---- リピーターデータベース（名前をキーとする辞書） ----
        private readonly Dictionary<string, RepeaterData> repeaterDatabase =
            new Dictionary<string, RepeaterData>();

        // ================================================================
        // 定数パラメータ
        // ================================================================

        /// <summary>リピーター登録に必要な最低満足度</summary>
        private const float MinSatisfactionForRepeat = 60f;

        /// <summary>再来園までの最短日数</summary>
        private const int MinDaysBeforeRepeat = 30;

        /// <summary>再来園までの最長日数</summary>
        private const int MaxDaysBeforeRepeat = 60;

        /// <summary>スポーンサイクルあたりの基本再来園確率</summary>
        private const float BaseRepeatChance = 0.15f;

        /// <summary>ロイヤリティ1ポイントあたりの追加確率</summary>
        private const float LoyaltyBonusMultiplier = 0.005f;

        /// <summary>リピーター抽選チェック間隔（リアル秒）</summary>
        private const float RepeaterCheckInterval = 10f;

        // ================================================================
        // ランタイム状態
        // ================================================================

        /// <summary>リピーター抽選タイマー</summary>
        private float repeaterCheckTimer;

        /// <summary>本日スポーン済みリピーター数</summary>
        private int repeatersSpawnedToday;

        /// <summary>1日あたりのリピーター上限数</summary>
        [Header("Repeater Limits")]
        [Tooltip("1日にスポーンできるリピーターの最大数")]
        [SerializeField] private int maxRepeatersPerDay = 5;

        /// <summary>累計リピーター来園回数（ライフタイム統計）</summary>
        private int totalRepeatVisits;

        // ================================================================
        // プロパティ
        // ================================================================

        /// <summary>登録済みリピーター総数</summary>
        public int RepeaterCount => repeaterDatabase.Count;

        /// <summary>本日スポーン済みリピーター数</summary>
        public int RepeatersSpawnedToday => repeatersSpawnedToday;

        /// <summary>
        /// 全リピーターの平均ロイヤリティスコア。
        /// リピーターが未登録の場合は0を返す。
        /// </summary>
        public float AverageLoyaltyScore
        {
            get
            {
                if (repeaterDatabase.Count == 0) return 0f;

                float sum = 0f;
                foreach (var kvp in repeaterDatabase)
                {
                    sum += kvp.Value.LoyaltyScore;
                }
                return sum / repeaterDatabase.Count;
            }
        }

        // ================================================================
        // イベント
        // ================================================================

        /// <summary>
        /// リピーターのスポーンが要求されたときに発火する。
        /// VisitorManagerがこのイベントを購読してリピーターを生成する。
        /// </summary>
        public event Action<RepeaterData> OnRepeaterSpawnRequested;

        /// <summary>
        /// 新規リピーターが登録されたときに発火する。
        /// 引数は来場者名。
        /// </summary>
        public event Action<string> OnNewRepeaterRegistered;

        // ================================================================
        // ライフサイクル
        // ================================================================

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;

            // TimeManagerのイベント購読を解除
            UnsubscribeFromTimeManager();
        }

        private void OnEnable()
        {
            SubscribeToTimeManager();
        }

        private void OnDisable()
        {
            UnsubscribeFromTimeManager();
        }

        private void Update()
        {
            if (GameManager.Instance != null && GameManager.Instance.IsPaused) return;
            if (repeaterDatabase.Count == 0) return;

            repeaterCheckTimer -= Time.deltaTime;
            if (repeaterCheckTimer <= 0f)
            {
                repeaterCheckTimer = RepeaterCheckInterval;
                ProcessRepeaterSpawns();
            }
        }

        // ================================================================
        // TimeManager連携
        // ================================================================

        /// <summary>TimeManagerの日替わりイベントに購読する</summary>
        private void SubscribeToTimeManager()
        {
            if (GameManager.Instance?.TimeManager != null)
            {
                GameManager.Instance.TimeManager.OnDayChanged += OnDayChanged;
            }
        }

        /// <summary>TimeManagerの日替わりイベントの購読を解除する</summary>
        private void UnsubscribeFromTimeManager()
        {
            if (GameManager.Instance?.TimeManager != null)
            {
                GameManager.Instance.TimeManager.OnDayChanged -= OnDayChanged;
            }
        }

        /// <summary>
        /// 日付変更時に呼ばれる。本日のリピータースポーンカウンタをリセットする。
        /// 【ゲームデザイン】毎日上限をリセットすることで、長期間放置しても
        /// リピーターが一気に押し寄せることを防ぐ。
        /// </summary>
        private void OnDayChanged()
        {
            repeatersSpawnedToday = 0;
            WebGLOptimizer.LogVerbose("[RepeaterSystem] 日替わりリセット: repeatersSpawnedToday = 0");
        }

        // ================================================================
        // 来場者退園記録
        // ================================================================

        /// <summary>
        /// 来場者がパークを退園する際に呼び出される。
        /// 満足度が閾値以上であればリピーターDBに登録/更新する。
        /// 【ゲームデザイン】
        /// ・満足度60未満の来場者はリピーター候補にならない。
        /// ・満足度が高いほど再訪間隔が短くなる（最短30日、最長60日）。
        /// ・ロイヤリティは来園ごとに+10、さらに満足度×0.2のボーナスが加算される。
        /// </summary>
        /// <param name="visitorName">来場者名（リピーターDBのキー）</param>
        /// <param name="type">来場者タイプ</param>
        /// <param name="satisfaction">退園時満足度（0-100）</param>
        /// <param name="happiness">退園時幸福度（0-100）</param>
        /// <param name="ridesCount">体験アトラクション数</param>
        /// <param name="favoriteAttractions">お気に入りアトラクション名リスト（上位3つ）</param>
        /// <param name="worstAttraction">最低評価アトラクション名</param>
        /// <param name="spent">総支出額</param>
        public void RecordVisitorDeparture(
            string visitorName,
            VisitorType type,
            float satisfaction,
            float happiness,
            int ridesCount,
            List<string> favoriteAttractions,
            string worstAttraction,
            float spent)
        {
            // 満足度が閾値未満の来場者はリピーター候補から除外
            if (satisfaction < MinSatisfactionForRepeat)
            {
                WebGLOptimizer.LogVerbose(
                    $"[RepeaterSystem] {visitorName} の満足度 {satisfaction:F1} が閾値 {MinSatisfactionForRepeat} 未満。リピーター登録スキップ");
                return;
            }

            // 現在のゲーム内日付を取得
            int currentDay = 0;
            int currentMonth = 0;
            if (GameManager.Instance?.TimeManager != null)
            {
                currentDay = GameManager.Instance.TimeManager.CurrentDay;
                currentMonth = GameManager.Instance.TimeManager.CurrentMonth;
            }

            // 来園記録を作成
            bool isNewRepeater = !repeaterDatabase.ContainsKey(visitorName);
            int repeatCount = isNewRepeater ? 1 : repeaterDatabase[visitorName].TotalVisits + 1;

            var record = new VisitorVisitRecord
            {
                VisitorName = visitorName,
                Type = type,
                FinalSatisfaction = satisfaction,
                FinalHappiness = happiness,
                RidesExperienced = ridesCount,
                FavoriteAttractionNames = favoriteAttractions ?? new List<string>(),
                WorstAttractionName = worstAttraction ?? "",
                VisitDay = currentDay,
                VisitMonth = currentMonth,
                RepeatCount = repeatCount,
                SpentAmount = spent
            };

            // 次回来園可能日を計算
            // 満足度が高いほど待機日数が短縮される（満足度100で最短、60で最長）
            float satisfactionRatio = Mathf.InverseLerp(MinSatisfactionForRepeat, 100f, satisfaction);
            int waitDays = Mathf.RoundToInt(
                Mathf.Lerp(MaxDaysBeforeRepeat, MinDaysBeforeRepeat, satisfactionRatio));
            // ランダム性を加味（±5日の揺らぎ）
            waitDays += UnityEngine.Random.Range(-5, 6);
            waitDays = Mathf.Clamp(waitDays, MinDaysBeforeRepeat, MaxDaysBeforeRepeat);
            int nextEligibleDay = currentDay + waitDays;

            if (isNewRepeater)
            {
                // 新規リピーター登録
                var data = new RepeaterData
                {
                    LastVisit = record,
                    TotalVisits = 1,
                    AverageSatisfaction = satisfaction,
                    LoyaltyScore = Mathf.Clamp(10f + 0.2f * satisfaction, 0f, 100f),
                    NextEligibleDay = nextEligibleDay
                };

                repeaterDatabase[visitorName] = data;

                WebGLOptimizer.LogVerbose(
                    $"[RepeaterSystem] 新規リピーター登録: {visitorName} (満足度={satisfaction:F1}, ロイヤリティ={data.LoyaltyScore:F1}, 次回来園日={nextEligibleDay})");

                OnNewRepeaterRegistered?.Invoke(visitorName);
            }
            else
            {
                // 既存リピーターの更新
                var data = repeaterDatabase[visitorName];
                data.LastVisit = record;
                data.TotalVisits++;
                totalRepeatVisits++;

                // 平均満足度を更新（移動平均）
                data.AverageSatisfaction =
                    ((data.AverageSatisfaction * (data.TotalVisits - 1)) + satisfaction) / data.TotalVisits;

                // ロイヤリティ更新: +10（来園ボーナス） + 満足度×0.2
                float loyaltyGain = 10f + 0.2f * satisfaction;
                data.LoyaltyScore = Mathf.Clamp(data.LoyaltyScore + loyaltyGain, 0f, 100f);

                data.NextEligibleDay = nextEligibleDay;

                WebGLOptimizer.LogVerbose(
                    $"[RepeaterSystem] リピーター更新: {visitorName} (来園{data.TotalVisits}回目, 満足度={satisfaction:F1}, ロイヤリティ={data.LoyaltyScore:F1}, 次回来園日={nextEligibleDay})");
            }
        }

        // ================================================================
        // リピータースポーン処理
        // ================================================================

        /// <summary>
        /// リピーターDBを走査し、来園資格のあるリピーターを確率でスポーンする。
        /// 【ゲームデザイン】
        /// ・基本確率15% + ロイヤリティ×0.5%のボーナス（最大ロイヤリティ100で+50%）。
        /// ・1日あたり最大5人まで。これにより急激な来場者増を抑制する。
        /// ・資格日に達していないリピーターはスキップされる。
        /// </summary>
        private void ProcessRepeaterSpawns()
        {
            if (repeatersSpawnedToday >= maxRepeatersPerDay) return;

            int currentDay = 0;
            if (GameManager.Instance?.TimeManager != null)
            {
                currentDay = GameManager.Instance.TimeManager.CurrentDay;
            }

            // 辞書のイテレーション中に変更しないようリストにコピー
            var candidates = new List<KeyValuePair<string, RepeaterData>>();
            foreach (var kvp in repeaterDatabase)
            {
                candidates.Add(kvp);
            }

            foreach (var kvp in candidates)
            {
                if (repeatersSpawnedToday >= maxRepeatersPerDay) break;

                var data = kvp.Value;

                // 来園資格日チェック
                if (currentDay < data.NextEligibleDay) continue;

                // 再来園確率を計算（ベース + ロイヤリティボーナス）
                float chance = BaseRepeatChance + (data.LoyaltyScore * LoyaltyBonusMultiplier);
                float roll = UnityEngine.Random.value;

                if (roll <= chance)
                {
                    SpawnRepeater(data);
                    repeatersSpawnedToday++;

                    WebGLOptimizer.LogVerbose(
                        $"[RepeaterSystem] リピータースポーン: {data.LastVisit.VisitorName} (確率={chance:P1}, 出目={roll:F3}, 本日{repeatersSpawnedToday}/{maxRepeatersPerDay}人目)");
                }
            }
        }

        /// <summary>
        /// リピーターのスポーンを要求する。
        /// OnRepeaterSpawnRequestedイベントを発火し、VisitorManagerに処理を委譲する。
        /// </summary>
        /// <param name="data">スポーン対象のリピーターデータ</param>
        private void SpawnRepeater(RepeaterData data)
        {
            // 次回来園資格日を無効化（重複スポーン防止）
            // RecordVisitorDepartureで再来園時に再設定される
            data.NextEligibleDay = int.MaxValue;

            OnRepeaterSpawnRequested?.Invoke(data);

            WebGLOptimizer.LogVerbose(
                $"[RepeaterSystem] スポーンリクエスト発火: {data.LastVisit.VisitorName} (ロイヤリティ={data.LoyaltyScore:F1}, 累計来園={data.TotalVisits}回)");
        }

        // ================================================================
        // クエリ系メソッド
        // ================================================================

        /// <summary>
        /// 指定名の来場者がリピーターとして登録されているか判定する。
        /// </summary>
        /// <param name="visitorName">来場者名</param>
        /// <returns>リピーターDBに存在すればtrue</returns>
        public bool IsRepeater(string visitorName)
        {
            return repeaterDatabase.ContainsKey(visitorName);
        }

        /// <summary>
        /// 指定名のリピーターデータを取得する。未登録の場合はnullを返す。
        /// </summary>
        /// <param name="visitorName">来場者名</param>
        /// <returns>RepeaterData、または未登録ならnull</returns>
        public RepeaterData GetRepeaterData(string visitorName)
        {
            repeaterDatabase.TryGetValue(visitorName, out var data);
            return data;
        }

        /// <summary>
        /// リピーターの来園回数に応じたボーナス倍率を取得する。
        /// 【ゲームデザイン】
        /// ・キャッシュ倍率: 1.0 + 0.1 × min(来園数, 5) → 最大1.5倍
        /// ・SNS倍率: 1.0 + 0.15 × min(来園数, 5) → 最大1.75倍
        /// ・5回以上はキャップされ、際限ないインフレを防ぐ。
        /// </summary>
        /// <param name="visitorName">来場者名</param>
        /// <returns>(cashMultiplier, snsMultiplier) のタプル。未登録者は(1.0, 1.0)</returns>
        public (float cashMultiplier, float snsMultiplier) GetRepeaterBonus(string visitorName)
        {
            if (!repeaterDatabase.TryGetValue(visitorName, out var data))
            {
                return (1.0f, 1.0f);
            }

            int cappedVisits = Mathf.Min(data.TotalVisits, 5);
            float cashMultiplier = 1.0f + 0.1f * cappedVisits;
            float snsMultiplier = 1.0f + 0.15f * cappedVisits;

            return (cashMultiplier, snsMultiplier);
        }

        /// <summary>
        /// リピーターシステム全体の統計情報を取得する。
        /// HUDやスコアボードの表示に使用する。
        /// </summary>
        /// <returns>(総リピーター数, 平均ロイヤリティ, 平均満足度, 累計リピーター来園数)</returns>
        public (int totalRepeaters, float avgLoyalty, float avgSatisfaction, int totalRepeaterVisits) GetRepeaterStats()
        {
            if (repeaterDatabase.Count == 0)
            {
                return (0, 0f, 0f, totalRepeatVisits);
            }

            float loyaltySum = 0f;
            float satisfactionSum = 0f;

            foreach (var kvp in repeaterDatabase)
            {
                loyaltySum += kvp.Value.LoyaltyScore;
                satisfactionSum += kvp.Value.AverageSatisfaction;
            }

            int count = repeaterDatabase.Count;
            return (
                count,
                loyaltySum / count,
                satisfactionSum / count,
                totalRepeatVisits
            );
        }

        /// <summary>
        /// 指定日数より古いリピーターレコードを削除し、メモリ肥大を防止する。
        /// 【ゲームデザイン】
        /// ・長期プレイ時にリピーターDBが際限なく膨張するのを防ぐ。
        /// ・削除対象は最終来園日が指定日数以上前のレコード。
        /// ・ロイヤリティが高くても長期不訪問なら忘却される。
        /// </summary>
        /// <param name="olderThanDays">この日数より古いレコードを削除する</param>
        public void ClearOldRecords(int olderThanDays)
        {
            int currentDay = 0;
            if (GameManager.Instance?.TimeManager != null)
            {
                currentDay = GameManager.Instance.TimeManager.CurrentDay;
            }

            var toRemove = new List<string>();
            foreach (var kvp in repeaterDatabase)
            {
                int lastVisitDay = kvp.Value.LastVisit.VisitDay;
                if (currentDay - lastVisitDay > olderThanDays)
                {
                    toRemove.Add(kvp.Key);
                }
            }

            foreach (string name in toRemove)
            {
                repeaterDatabase.Remove(name);
            }

            if (toRemove.Count > 0)
            {
                WebGLOptimizer.LogVerbose(
                    $"[RepeaterSystem] 古いレコードを {toRemove.Count} 件削除しました (閾値={olderThanDays}日)");
            }
        }
    }
}
