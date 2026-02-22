// ============================================================
// ThemeParkGame - ScientistStaff
// サイエンティスト（研究開発スタッフ）
// ============================================================

using UnityEngine;
using ThemeParkGame.Core;

namespace ThemeParkGame.Staff
{
    /// <summary>
    /// サイエンティストは研究ラボで新アトラクションやアップグレードの研究開発を担当するスタッフ。
    ///
    /// 【ゲームデザイン】
    /// ・研究ラボに配置することで研究進捗が発生する（ラボなしでは働けない）
    /// ・同一ラボに複数のサイエンティストを配置すると研究速度が加算される
    ///   （ただし3人目以降は効率低下あり - 収穫逓減）
    /// ・スキルレベルが高いほど研究速度が速い
    /// ・研究完了で新アトラクション・ショップ・アップグレードがアンロックされる
    /// ・原作では研究は必須要素で、サイエンティストの確保と訓練がゲーム進行の鍵だった
    /// ・複数の研究を同時進行させるには複数のラボとサイエンティストが必要
    /// </summary>
    public class ScientistStaff : StaffMember
    {
        // ============================================================
        // 定数
        // ============================================================

        /// <summary>基本研究速度（ポイント/秒）</summary>
        private const float BaseResearchSpeed = 1f;

        /// <summary>
        /// 同一ラボ内の追加サイエンティストによる効率低下率。
        /// 2人目: 100%, 3人目: 80%, 4人目: 60% ...
        /// </summary>
        private const float DiminishingReturnFactor = 0.2f;

        /// <summary>1ラボあたりの最大配置可能人数</summary>
        private const int MaxScientistsPerLab = 5;

        /// <summary>研究中に進捗を報告する間隔（秒）</summary>
        private const float ProgressReportInterval = 5f;

        // ============================================================
        // フィールド
        // ============================================================

        [Header("Scientist Settings")]
        [SerializeField] private Transform assignedLab;

        /// <summary>配置先ラボのID</summary>
        private int assignedLabId = -1;

        /// <summary>進捗報告タイマー</summary>
        private float progressReportTimer;

        /// <summary>このサイエンティストがラボ内で何番目のスロットに入っているか（0始まり）</summary>
        private int labSlotIndex;

        /// <summary>累計研究貢献ポイント（統計用）</summary>
        public float TotalResearchContribution { get; private set; }

        /// <summary>現在研究中のリサーチID</summary>
        public string CurrentResearchId { get; private set; }

        // ============================================================
        // プロパティ
        // ============================================================

        /// <summary>配置先のラボTransform</summary>
        public Transform AssignedLab
        {
            get => assignedLab;
            private set => assignedLab = value;
        }

        /// <summary>ラボに配置されているか</summary>
        public bool IsAssignedToLab => assignedLab != null;

        /// <summary>
        /// 現在の研究速度（ポイント/秒）。
        /// スキルレベルと収穫逓減を考慮した実効値。
        /// </summary>
        public float EffectiveResearchSpeed
        {
            get
            {
                float skillMultiplier = WorkEfficiencyMultiplier;
                float diminishingMultiplier = CalculateDiminishingReturn();
                return BaseResearchSpeed * skillMultiplier * diminishingMultiplier;
            }
        }

        // ============================================================
        // 初期化
        // ============================================================

        protected override void Awake()
        {
            base.Awake();
            StaffType = StaffType.Scientist;
        }

        // ============================================================
        // イベント購読
        // ============================================================

        protected override void SubscribeToEvents()
        {
            GameEvents.OnResearchCompleted += HandleResearchCompleted;
        }

        protected override void UnsubscribeFromEvents()
        {
            GameEvents.OnResearchCompleted -= HandleResearchCompleted;
        }

        /// <summary>
        /// 研究完了イベントハンドラ。
        /// 自分が担当していた研究が完了した場合、次の研究への再割り当てを待つ。
        /// </summary>
        private void HandleResearchCompleted(string researchId)
        {
            if (CurrentResearchId == researchId)
            {
                Debug.Log($"[Scientist] {Name} が担当していた研究 '{researchId}' が完了しました");
                CurrentResearchId = null;
                CurrentState = StaffBehaviorState.Idle;
            }
        }

        // ============================================================
        // ラボ配置
        // ============================================================

        /// <summary>
        /// サイエンティストをラボに配置する。
        /// StaffManagerまたはUI経由で呼ばれる。
        /// </summary>
        /// <param name="lab">配置先のラボTransform</param>
        /// <param name="labId">ラボの施設ID</param>
        /// <param name="slotIndex">ラボ内のスロット番号</param>
        public void AssignToLab(Transform lab, int labId, int slotIndex)
        {
            assignedLab = lab;
            assignedLabId = labId;
            labSlotIndex = slotIndex;

            Debug.Log($"[Scientist] {Name} がラボ (ID:{labId}, スロット:{slotIndex}) に配置されました");

            // ラボへ移動を開始
            if (lab != null)
            {
                NavigateTo(lab.position);
                CurrentState = StaffBehaviorState.MovingToTask;
            }
        }

        /// <summary>ラボからサイエンティストを撤去する</summary>
        public void RemoveFromLab()
        {
            Debug.Log($"[Scientist] {Name} がラボ (ID:{assignedLabId}) から撤去されました");

            assignedLab = null;
            assignedLabId = -1;
            labSlotIndex = 0;
            CurrentResearchId = null;
            CurrentState = StaffBehaviorState.Idle;
        }

        /// <summary>
        /// 研究対象を設定する。ResearchManagerから呼ばれる。
        /// </summary>
        public void AssignResearch(string researchId)
        {
            CurrentResearchId = researchId;
            Debug.Log($"[Scientist] {Name} が研究 '{researchId}' に着手しました");
        }

        // ============================================================
        // タスク検索
        // ============================================================

        /// <summary>
        /// サイエンティストのタスク検索。
        /// ラボに配置されていればラボへ向かう。
        /// ラボ未配置の場合はタスクなし（StaffManagerからの配置を待つ）。
        /// </summary>
        protected override bool FindAndAssignTask()
        {
            if (!IsAssignedToLab)
            {
                // ラボ未配置 - StaffManagerからの指示を待つ
                return false;
            }

            // ラボが設定されているが到着していない場合、ラボへ向かう
            float distToLab = Vector3.Distance(transform.position, assignedLab.position);
            if (distToLab > 2f)
            {
                NavigateTo(assignedLab.position);
                CurrentState = StaffBehaviorState.MovingToTask;
                return true;
            }

            // ラボに到着済みで研究対象がある場合
            if (!string.IsNullOrEmpty(CurrentResearchId))
            {
                CurrentState = StaffBehaviorState.Working;
                return true;
            }

            return false;
        }

        // ============================================================
        // 研究作業
        // ============================================================

        protected override void OnTaskReached()
        {
            StopNavigation();

            if (!string.IsNullOrEmpty(CurrentResearchId))
            {
                CurrentState = StaffBehaviorState.Working;
                progressReportTimer = 0f;
                Debug.Log($"[Scientist] {Name} がラボで研究作業を開始しました"
                          + $"（研究: {CurrentResearchId}）");
            }
            else
            {
                Debug.Log($"[Scientist] {Name} がラボに到着しましたが、研究対象がありません");
                CurrentState = StaffBehaviorState.Idle;
            }
        }

        /// <summary>
        /// 研究作業を毎フレーム進行させる。
        ///
        /// 【ゲームデザイン】
        /// ・研究速度 = 基本速度 * スキル倍率 * 収穫逓減倍率
        /// ・ResearchManagerに進捗を通知し、ResearchManager側で完了判定を行う
        /// ・研究対象がなくなった場合は待機状態に戻る
        /// </summary>
        protected override void PerformWork()
        {
            if (string.IsNullOrEmpty(CurrentResearchId))
            {
                CurrentState = StaffBehaviorState.Idle;
                return;
            }

            if (!IsAssignedToLab)
            {
                CurrentState = StaffBehaviorState.Idle;
                return;
            }

            float deltaTime = Time.deltaTime;
            float researchPoints = EffectiveResearchSpeed * deltaTime;

            // 累計貢献ポイントを加算
            TotalResearchContribution += researchPoints;

            // ResearchManagerに進捗を通知する
            // 実際にはResearchManager.Instance.AddProgress(CurrentResearchId, researchPoints) を呼ぶ
            NotifyResearchProgress(researchPoints);

            // 定期的にログ出力
            progressReportTimer += deltaTime;
            if (progressReportTimer >= ProgressReportInterval)
            {
                progressReportTimer = 0f;
                Debug.Log($"[Scientist] {Name} 研究中: {CurrentResearchId}"
                          + $"（速度: {EffectiveResearchSpeed:F2} pts/s"
                          + $", 累計: {TotalResearchContribution:F0} pts）");
            }
        }

        /// <summary>
        /// ResearchManagerに研究進捗を通知する。
        /// </summary>
        private void NotifyResearchProgress(float points)
        {
            if (GameManager.Instance != null && GameManager.Instance.ResearchManager != null)
            {
                var research = GameManager.Instance.ResearchManager.CurrentResearch;
                if (research != null && research.ResearchId == CurrentResearchId)
                {
                    research.AddProgress(points);
                }
            }
        }

        // ============================================================
        // 収穫逓減計算
        // ============================================================

        /// <summary>
        /// 同一ラボ内の配置スロットに基づく収穫逓減倍率を計算する。
        ///
        /// 【ゲームデザイン】
        /// ・1人目: 100%  2人目: 100%  3人目: 80%  4人目: 60%  5人目: 40%
        /// ・最初の2人は完全な効率で研究できる
        /// ・3人目以降は1人追加ごとに20%ずつ効率が低下する
        /// ・これによりラボを増設する動機が生まれる
        /// </summary>
        private float CalculateDiminishingReturn()
        {
            if (labSlotIndex <= 1)
            {
                return 1f;
            }

            float reduction = (labSlotIndex - 1) * DiminishingReturnFactor;
            return Mathf.Max(0.2f, 1f - reduction);
        }

        /// <summary>
        /// 指定スロット数でのラボ全体の合計研究速度を算出する（計画用の静的ユーティリティ）。
        /// UIで「サイエンティストを追加した場合の効果」を表示する際に使用する。
        /// </summary>
        /// <param name="scientistCount">ラボ内のサイエンティスト数</param>
        /// <param name="averageSkillLevel">平均スキルレベル</param>
        /// <returns>合計研究速度（ポイント/秒）</returns>
        public static float CalculateLabTotalSpeed(int scientistCount, int averageSkillLevel)
        {
            float total = 0f;
            float skillMult = 1f + (averageSkillLevel - 1) * 0.25f;

            for (int i = 0; i < Mathf.Min(scientistCount, MaxScientistsPerLab); i++)
            {
                float diminishing = i <= 1 ? 1f : Mathf.Max(0.2f, 1f - (i - 1) * DiminishingReturnFactor);
                total += BaseResearchSpeed * skillMult * diminishing;
            }

            return total;
        }

        // ============================================================
        // デバッグ
        // ============================================================

#if UNITY_EDITOR
        protected override void OnDrawGizmosSelected()
        {
            base.OnDrawGizmosSelected();

            // ラボへの接続線
            if (assignedLab != null)
            {
                Gizmos.color = Color.blue;
                Gizmos.DrawLine(transform.position, assignedLab.position);
                Gizmos.DrawSphere(assignedLab.position, 0.5f);
            }
        }
#endif
    }
}
