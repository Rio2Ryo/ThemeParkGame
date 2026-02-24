// ============================================================
// ThemeParkGame - CleanerStaff
// スイーパー（清掃スタッフ）
// ============================================================

using System.Collections.Generic;
using UnityEngine;
using ThemeParkGame.Core;
using ThemeParkGame.Attraction;

namespace ThemeParkGame.Staff
{
    /// <summary>
    /// クリーナー（スイーパー）はパーク内のゴミ拾い・嘔吐物清掃・トイレ清掃を担当するスタッフ。
    ///
    /// 【ゲームデザイン】
    /// ・パークの衛生スコアに直結する最も重要なスタッフの一つ
    /// ・来場者は汚い場所に強い不満を持ち、「TooDirty」バブルを出す
    /// ・清掃対象の優先度: 嘔吐物 > ゴミ > トイレ（嘔吐物は放置すると連鎖嘔吐を誘発）
    /// ・スキルレベルが高いほど清掃速度が速くなり、巡回効率が上がる
    /// ・担当エリアを適切に分割して配置しないと、清掃が追いつかなくなる
    /// ・原作ではスイーパー不足が最も即座にパーク評価を下げる要因だった
    /// </summary>
    public class CleanerStaff : StaffMember
    {
        // ============================================================
        // 清掃対象の種別
        // ============================================================

        /// <summary>清掃対象の種別</summary>
        public enum MessType
        {
            /// <summary>来場者が捨てたゴミ</summary>
            Litter,

            /// <summary>
            /// 嘔吐物 - 最優先で清掃すべき。放置すると他の来場者も嘔吐する連鎖反応が起きる
            /// </summary>
            Vomit,

            /// <summary>トイレの汚れ - 定期的な清掃が必要</summary>
            Toilet
        }

        // ============================================================
        // 定数
        // ============================================================

        /// <summary>ゴミ1個あたりの基本清掃時間（秒）</summary>
        private const float BaseLitterCleanDuration = 2f;

        /// <summary>嘔吐物の基本清掃時間（秒）</summary>
        private const float BaseVomitCleanDuration = 4f;

        /// <summary>トイレの基本清掃時間（秒）</summary>
        private const float BaseToiletCleanDuration = 6f;

        /// <summary>清掃対象を検知する半径（メートル）</summary>
        private const float DetectionRadius = 30f;

        /// <summary>ゴミ1個あたりのパーク衛生スコア低下量</summary>
        private const float HygieneScorePerLitter = -0.5f;

        /// <summary>嘔吐物1個あたりのパーク衛生スコア低下量</summary>
        private const float HygieneScorePerVomit = -2f;

        /// <summary>清掃完了時のパーク衛生スコア回復量</summary>
        private const float HygieneScorePerClean = 1f;

        // ============================================================
        // フィールド
        // ============================================================

        [Header("Cleaner Settings")]
        #pragma warning disable CS0414
        [SerializeField] private float cleaningRange = 1.5f;
        #pragma warning restore CS0414

        /// <summary>現在の清掃対象</summary>
        private GameObject currentTarget;

        /// <summary>現在の清掃対象種別</summary>
        private MessType currentMessType;

        /// <summary>清掃作業の経過時間</summary>
        private float cleanTimer;

        /// <summary>清掃作業の所要時間</summary>
        private float cleanDuration;

        /// <summary>
        /// このクリーナーが貢献した清掃回数。
        /// StaffManagerで統計として利用する。
        /// </summary>
        public int TotalCleanCount { get; private set; }

        // ============================================================
        // 初期化
        // ============================================================

        protected override void Awake()
        {
            base.Awake();
            StaffType = StaffType.Cleaner;
        }

        // ============================================================
        // イベント購読
        // ============================================================

        protected override void SubscribeToEvents()
        {
            GameEvents.OnVisitorVomited += HandleVisitorVomited;
        }

        protected override void UnsubscribeFromEvents()
        {
            GameEvents.OnVisitorVomited -= HandleVisitorVomited;
        }

        /// <summary>
        /// 来場者嘔吐イベントハンドラ。
        /// 嘔吐物は最優先で清掃すべきため、現在待機中であれば即座に対応する。
        /// </summary>
        private void HandleVisitorVomited(int visitorId)
        {
            // 待機中であれば即座にタスク検索をトリガーする
            if (CurrentState == StaffBehaviorState.Idle)
            {
                FindAndAssignTask();
            }
        }

        // ============================================================
        // タスク検索
        // ============================================================

        /// <summary>
        /// 清掃対象を優先度順に検索する。
        ///
        /// 【検索優先度】
        /// 1. 嘔吐物（Vomit） - 連鎖嘔吐を防ぐため最優先
        /// 2. ゴミ（Litter） - パーク衛生スコアに影響
        /// 3. トイレ（Toilet） - 定期清掃
        ///
        /// 同種の中では距離が近いものを優先する。
        /// </summary>
        protected override bool FindAndAssignTask()
        {
            // 嘔吐物を最優先で検索
            if (TryFindMessOfType("Vomit", MessType.Vomit))
            {
                return true;
            }

            // 次にゴミを検索
            if (TryFindMessOfType("Litter", MessType.Litter))
            {
                return true;
            }

            // 最後にトイレを検索
            if (TryFindDirtyToilet())
            {
                return true;
            }

            return false;
        }

        /// <summary>指定タグの清掃対象を検索する</summary>
        private bool TryFindMessOfType(string tag, MessType messType)
        {
            Collider[] hits = Physics.OverlapSphere(transform.position, DetectionRadius);
            Transform bestTarget = null;
            float bestDistance = float.MaxValue;

            foreach (var hit in hits)
            {
                if (!hit.CompareTag(tag)) continue;
                if (!IsWithinPatrolArea(hit.transform.position)) continue;

                float dist = Vector3.Distance(transform.position, hit.transform.position);
                if (dist < bestDistance)
                {
                    bestDistance = dist;
                    bestTarget = hit.transform;
                }
            }

            if (bestTarget != null)
            {
                currentTarget = bestTarget.gameObject;
                currentMessType = messType;
                cleanDuration = CalculateCleanDuration(messType);
                cleanTimer = 0f;

                NavigateTo(bestTarget.position);
                CurrentState = StaffBehaviorState.MovingToTask;
                return true;
            }

            return false;
        }

        /// <summary>清掃が必要なトイレを検索する</summary>
        private bool TryFindDirtyToilet()
        {
            Collider[] hits = Physics.OverlapSphere(transform.position, DetectionRadius);
            Transform bestTarget = null;
            float bestDistance = float.MaxValue;

            foreach (var hit in hits)
            {
                if (!hit.CompareTag("Toilet")) continue;
                if (!IsWithinPatrolArea(hit.transform.position)) continue;

                // トイレの汚れ度合いをチェック（FacilityDirtコンポーネントがあれば利用）
                var dirtComp = hit.GetComponent<FacilityDirt>();
                if (dirtComp != null && dirtComp.DirtLevel < 0.3f) continue;

                float dist = Vector3.Distance(transform.position, hit.transform.position);
                if (dist < bestDistance)
                {
                    bestDistance = dist;
                    bestTarget = hit.transform;
                }
            }

            if (bestTarget != null)
            {
                currentTarget = bestTarget.gameObject;
                currentMessType = MessType.Toilet;
                cleanDuration = CalculateCleanDuration(MessType.Toilet);
                cleanTimer = 0f;

                NavigateTo(bestTarget.position);
                CurrentState = StaffBehaviorState.MovingToTask;
                return true;
            }

            return false;
        }

        // ============================================================
        // 作業実行
        // ============================================================

        protected override void OnTaskReached()
        {
            cleanTimer = 0f;
            StopNavigation();

            string targetName = currentMessType switch
            {
                MessType.Vomit => "嘔吐物",
                MessType.Litter => "ゴミ",
                MessType.Toilet => "トイレ",
                _ => "不明"
            };

            Debug.Log($"[Cleaner] {Name} が{targetName}の清掃を開始します");
        }

        /// <summary>
        /// 清掃作業を毎フレーム進行させる。
        /// 対象が消滅していた場合（他のクリーナーが先に清掃した等）はタスクを中断する。
        /// </summary>
        protected override void PerformWork()
        {
            // 対象が既に除去されていた場合
            if (currentTarget == null)
            {
                CompleteCurrentTask();
                return;
            }

            cleanTimer += Time.deltaTime;

            if (cleanTimer >= cleanDuration)
            {
                CompleteCleanup();
            }
        }

        /// <summary>清掃を完了する</summary>
        private void CompleteCleanup()
        {
            TotalCleanCount++;

            string targetName = currentMessType switch
            {
                MessType.Vomit => "嘔吐物",
                MessType.Litter => "ゴミ",
                MessType.Toilet => "トイレ",
                _ => "不明"
            };

            Debug.Log($"[Cleaner] {Name} が{targetName}の清掃を完了しました"
                      + $"（累計清掃数: {TotalCleanCount}）");

            // 清掃対象を除去（トイレの場合は汚れフラグをリセットするのみ）
            if (currentTarget != null && currentMessType != MessType.Toilet)
            {
                Object.Destroy(currentTarget);
            }

            currentTarget = null;
            CompleteCurrentTask();
        }

        // ============================================================
        // 所要時間算出
        // ============================================================

        /// <summary>
        /// 清掃時間を算出する。スキルレベルが高いほど高速に清掃できる。
        /// </summary>
        private float CalculateCleanDuration(MessType messType)
        {
            float baseDuration = messType switch
            {
                MessType.Litter => BaseLitterCleanDuration,
                MessType.Vomit => BaseVomitCleanDuration,
                MessType.Toilet => BaseToiletCleanDuration,
                _ => BaseLitterCleanDuration
            };

            return baseDuration / WorkEfficiencyMultiplier;
        }

        // ============================================================
        // パーク衛生スコア
        // ============================================================

        /// <summary>
        /// パトロールエリア内の衛生スコアを計算する。
        /// StaffManagerやParkManagerから呼ばれてパーク全体の衛生評価に使われる。
        ///
        /// 【ゲームデザイン】
        /// ・ゴミや嘔吐物の数に応じてスコアが低下する
        /// ・クリーナーの配置数と巡回エリアの設計がパーク衛生に直結する
        /// ・衛生スコアが低いと来場者満足度が大幅に低下する
        /// </summary>
        public float CalculateAreaHygieneScore()
        {
            float score = 100f;

            Collider[] hits = Physics.OverlapSphere(transform.position, DetectionRadius);
            foreach (var hit in hits)
            {
                if (!IsWithinPatrolArea(hit.transform.position)) continue;

                if (hit.CompareTag("Litter"))
                {
                    score += HygieneScorePerLitter;
                }
                else if (hit.CompareTag("Vomit"))
                {
                    score += HygieneScorePerVomit;
                }
            }

            return Mathf.Clamp(score, 0f, 100f);
        }

        // ============================================================
        // デバッグ
        // ============================================================

#if UNITY_EDITOR
        protected override void OnDrawGizmosSelected()
        {
            base.OnDrawGizmosSelected();

            // 検知範囲
            Gizmos.color = new Color(0f, 0.8f, 0.2f, 0.1f);
            Gizmos.DrawSphere(transform.position, DetectionRadius);

            // 清掃対象への線
            if (currentTarget != null)
            {
                Gizmos.color = currentMessType switch
                {
                    MessType.Vomit => Color.magenta,
                    MessType.Litter => Color.yellow,
                    MessType.Toilet => Color.cyan,
                    _ => Color.white
                };
                Gizmos.DrawLine(transform.position, currentTarget.transform.position);
            }
        }
#endif
    }
}
