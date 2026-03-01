// ============================================================
// ThemeParkGame - MechanicStaff
// メカニック（修理・点検スタッフ）
// ============================================================

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using ThemeParkGame.Core;

namespace ThemeParkGame.Staff
{
    /// <summary>
    /// メカニックはアトラクションの定期点検・故障修理を担当するスタッフ。
    ///
    /// 【ゲームデザイン】
    /// ・アトラクションは稼働時間に応じて劣化し、定期的な点検が必要
    /// ・メカニックが定期巡回することで故障を未然に防ぐ（事故防止）
    /// ・故障イベント発生時、最も近い空きメカニックが駆けつけて修理する
    /// ・スキルレベルが高いほど修理が速く、点検精度も高い
    /// ・メカニック不足 → 点検漏れ → 故障頻発 → 事故発生 → パーク評価低下
    ///   というリスクチェーンが存在する
    /// </summary>
    public class MechanicStaff : StaffMember
    {
        // ============================================================
        // 定数
        // ============================================================

        /// <summary>基本修理時間（秒）。スキルレベルで短縮される</summary>
        private const float BaseRepairDuration = 10f;

        /// <summary>基本点検時間（秒）</summary>
        private const float BaseInspectionDuration = 5f;

        /// <summary>基本オーバーホール時間（秒）。修理の3倍。</summary>
        private const float BaseOverhaulDuration = 30f;

        /// <summary>点検によって蓄積される安全ポイント</summary>
        private const float InspectionSafetyBonus = 15f;

        /// <summary>修理完了時に回復する耐久値</summary>
        private const float RepairDurabilityRestore = 100f;

        /// <summary>オーバーホールが必要なコンディション閾値</summary>
        private const float OverhaulConditionThreshold = 30f;

        // ============================================================
        // フィールド
        // ============================================================

        [Header("Mechanic Settings")]
        [SerializeField] private float detectionRadius = 50f;

        /// <summary>現在対応中のアトラクションID（-1なら未対応）</summary>
        private int targetAttractionId = -1;

        /// <summary>現在の作業が修理か点検か</summary>
        private bool isRepairing;

        /// <summary>現在の作業がオーバーホールか</summary>
        private bool isOverhauling;

        /// <summary>作業の経過時間</summary>
        private float workTimer;

        /// <summary>作業の所要時間</summary>
        private float workDuration;

        /// <summary>修理待ちキュー（故障したアトラクションID）</summary>
        private static readonly Queue<int> repairQueue = new Queue<int>();

        /// <summary>対応中のアトラクションIDセット（重複割り当て防止）</summary>
        private static readonly HashSet<int> claimedAttractions = new HashSet<int>();

        // ============================================================
        // 初期化
        // ============================================================

        protected override void Awake()
        {
            base.Awake();
            StaffType = StaffType.Mechanic;
        }

        // ============================================================
        // イベント購読
        // ============================================================

        protected override void SubscribeToEvents()
        {
            GameEvents.OnAttractionBrokenDown += HandleAttractionBrokenDown;
        }

        protected override void UnsubscribeFromEvents()
        {
            GameEvents.OnAttractionBrokenDown -= HandleAttractionBrokenDown;
        }

        /// <summary>
        /// アトラクション故障イベントハンドラ。
        /// 修理キューに追加し、空いているメカニックが対応する。
        /// </summary>
        private void HandleAttractionBrokenDown(int attractionId)
        {
            if (!claimedAttractions.Contains(attractionId))
            {
                repairQueue.Enqueue(attractionId);
            }
        }

        // ============================================================
        // タスク検索
        // ============================================================

        /// <summary>
        /// タスクを検索する優先順位:
        /// 1. 修理キューから故障アトラクションを取得
        /// 2. コンディション低下でオーバーホールが必要なアトラクションを探す
        /// 3. パトロールエリア内で点検が必要なアトラクションを探す
        /// </summary>
        protected override bool FindAndAssignTask()
        {
            // 優先: 修理キューからの故障対応
            if (TryClaimRepairTask())
            {
                return true;
            }

            // 次点: オーバーホールが必要な対象
            if (TryFindOverhaulTarget())
            {
                return true;
            }

            // 巡回中に発見した点検対象
            if (TryFindInspectionTarget())
            {
                return true;
            }

            return false;
        }

        /// <summary>修理キューからタスクを取得して担当を宣言する</summary>
        private bool TryClaimRepairTask()
        {
            while (repairQueue.Count > 0)
            {
                int attractionId = repairQueue.Dequeue();

                if (claimedAttractions.Contains(attractionId))
                {
                    continue;
                }

                // アトラクションの位置を取得してパトロールエリア内か確認
                // （実際の実装ではAttractionManagerから位置を取得する）
                var attractionObj = FindAttractionById(attractionId);
                if (attractionObj == null) continue;

                Vector3 attractionPos = attractionObj.transform.position;
                if (!IsWithinPatrolArea(attractionPos)) continue;

                claimedAttractions.Add(attractionId);
                targetAttractionId = attractionId;
                isRepairing = true;
                isOverhauling = false;
                workDuration = CalculateRepairDuration();
                workTimer = 0f;

                NavigateTo(attractionPos);
                CurrentState = StaffBehaviorState.MovingToTask;
                WebGLOptimizer.LogVerbose($"[Mechanic] {Name} がアトラクション {attractionId} の修理に向かいます");
                return true;
            }

            return false;
        }

        /// <summary>オーバーホールが必要なアトラクションを探す（コンディション30%以下）</summary>
        private bool TryFindOverhaulTarget()
        {
            Collider[] hits = Physics.OverlapSphere(transform.position, detectionRadius);
            Transform bestTarget = null;
            float worstCondition = OverhaulConditionThreshold;
            int bestAttrId = -1;

            foreach (var hit in hits)
            {
                if (!hit.CompareTag("Attraction")) continue;
                if (!IsWithinPatrolArea(hit.transform.position)) continue;

                var facility = hit.GetComponent<Attraction.FacilityBase>();
                if (facility == null) continue;

                int attrId = facility.FacilityId;
                if (claimedAttractions.Contains(attrId)) continue;

                var attraction = hit.GetComponent<Attraction.Attraction>();
                if (attraction == null) continue;

                // コンディション30%以下またはコンディション強制停止中のアトラクションを優先
                if (attraction.Condition <= OverhaulConditionThreshold || attraction.IsConditionForcedStop)
                {
                    if (attraction.IsUnderOverhaul) continue; // 既にオーバーホール中

                    if (attraction.Condition < worstCondition || attraction.IsConditionForcedStop)
                    {
                        worstCondition = attraction.Condition;
                        bestTarget = hit.transform;
                        bestAttrId = attrId;
                    }
                }
            }

            if (bestTarget != null)
            {
                claimedAttractions.Add(bestAttrId);
                targetAttractionId = bestAttrId;
                isRepairing = false;
                isOverhauling = true;
                workDuration = CalculateOverhaulDuration();
                workTimer = 0f;

                // オーバーホール開始をアトラクションに通知
                var attraction = bestTarget.GetComponent<Attraction.Attraction>();
                if (attraction != null)
                    attraction.IsUnderOverhaul = true;

                NavigateTo(bestTarget.position);
                CurrentState = StaffBehaviorState.MovingToTask;
                WebGLOptimizer.LogVerbose($"[Mechanic] {Name} がアトラクション {bestAttrId} のオーバーホールに向かいます");
                return true;
            }

            return false;
        }

        /// <summary>パトロールエリア内で点検対象を探す</summary>
        private bool TryFindInspectionTarget()
        {
            // 周囲のアトラクションをスキャンして点検が必要なものを探す
            Collider[] hits = Physics.OverlapSphere(transform.position, detectionRadius);
            Transform bestTarget = null;
            float bestDistance = float.MaxValue;

            foreach (var hit in hits)
            {
                if (!hit.CompareTag("Attraction")) continue;
                if (!IsWithinPatrolArea(hit.transform.position)) continue;

                // 論理FacilityIdで管理する
                var facility = hit.GetComponent<Attraction.FacilityBase>();
                if (facility == null) continue;

                int attrId = facility.FacilityId;
                if (claimedAttractions.Contains(attrId)) continue;

                float dist = Vector3.Distance(transform.position, hit.transform.position);
                if (dist < bestDistance)
                {
                    bestDistance = dist;
                    bestTarget = hit.transform;
                    targetAttractionId = attrId;
                }
            }

            if (bestTarget != null)
            {
                claimedAttractions.Add(targetAttractionId);
                isRepairing = false;
                isOverhauling = false;
                workDuration = CalculateInspectionDuration();
                workTimer = 0f;

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
            workTimer = 0f;
            StopNavigation();

            if (isRepairing)
            {
                WebGLOptimizer.LogVerbose($"[Mechanic] {Name} がアトラクション {targetAttractionId} の修理を開始します");
            }
            else if (isOverhauling)
            {
                WebGLOptimizer.LogVerbose($"[Mechanic] {Name} がアトラクション {targetAttractionId} のオーバーホールを開始します");
            }
            else
            {
                WebGLOptimizer.LogVerbose($"[Mechanic] {Name} がアトラクション {targetAttractionId} の点検を開始します");
            }
        }

        /// <summary>
        /// 修理・点検作業を毎フレーム進行させる。
        ///
        /// 【ゲームデザイン】
        /// ・修理: 故障したアトラクションを復旧させる。完了後OnAttractionRepairedイベント発火
        /// ・点検: アトラクションの安全性を向上させ、故障確率を低下させる
        /// ・どちらもスキルレベルが高いほど所要時間が短くなる
        /// </summary>
        protected override void PerformWork()
        {
            workTimer += Time.deltaTime;

            if (workTimer >= workDuration)
            {
                if (isRepairing)
                {
                    CompleteRepair();
                }
                else if (isOverhauling)
                {
                    CompleteOverhaul();
                }
                else
                {
                    CompleteInspection();
                }
            }
        }

        /// <summary>修理を完了する</summary>
        private void CompleteRepair()
        {
            WebGLOptimizer.LogVerbose($"[Mechanic] {Name} がアトラクション {targetAttractionId} の修理を完了しました"
                      + $"（所要時間: {workTimer:F1}秒）");

            GameEvents.FireAttractionRepaired(targetAttractionId);

            ReleaseTarget();
            CompleteCurrentTask();
        }

        /// <summary>オーバーホールを完了する</summary>
        private void CompleteOverhaul()
        {
            WebGLOptimizer.LogVerbose($"[Mechanic] {Name} がアトラクション {targetAttractionId} のオーバーホールを完了しました"
                      + $"（所要時間: {workTimer:F1}秒）");

            var attractionObj = FindAttractionById(targetAttractionId);
            if (attractionObj != null)
            {
                var attraction = attractionObj.GetComponent<Attraction.Attraction>();
                if (attraction != null)
                {
                    attraction.OnOverhaulCompleted();
                    WebGLOptimizer.LogVerbose($"[Mechanic] {Name} のオーバーホールにより {attraction.DisplayName} のコンディションが100%に回復");
                }
            }

            if (NotificationSystem.Instance != null)
            {
                NotificationSystem.Instance.Notify(
                    $"オーバーホール完了！コンディション100%に回復",
                    NotifLevel.Success);
            }

            ReleaseTarget();
            CompleteCurrentTask();
        }

        /// <summary>点検を完了する</summary>
        private void CompleteInspection()
        {
            WebGLOptimizer.LogVerbose($"[Mechanic] {Name} がアトラクション {targetAttractionId} の点検を完了しました");

            // 点検完了により安全性を向上（故障確率リセット）
            var attractionObj = FindAttractionById(targetAttractionId);
            if (attractionObj != null)
            {
                var attraction = attractionObj.GetComponent<Attraction.Attraction>();
                if (attraction != null)
                {
                    attraction.OnMaintenancePerformed();
                    WebGLOptimizer.LogVerbose($"[Mechanic] {Name} の点検により {attraction.DisplayName} の安全性が向上しました");
                }
            }

            ReleaseTarget();
            CompleteCurrentTask();
        }

        /// <summary>担当アトラクションの占有を解放する</summary>
        private void ReleaseTarget()
        {
            if (targetAttractionId >= 0)
            {
                claimedAttractions.Remove(targetAttractionId);
                targetAttractionId = -1;
            }
        }

        // ============================================================
        // 所要時間算出
        // ============================================================

        /// <summary>
        /// 修理時間を算出する。
        /// スキルレベル1で10秒、スキルレベル5で5秒。
        /// </summary>
        private float CalculateRepairDuration()
        {
            return BaseRepairDuration / WorkEfficiencyMultiplier;
        }

        /// <summary>
        /// 点検時間を算出する。
        /// スキルレベル1で5秒、スキルレベル5で2.5秒。
        /// </summary>
        private float CalculateInspectionDuration()
        {
            return BaseInspectionDuration / WorkEfficiencyMultiplier;
        }

        /// <summary>
        /// オーバーホール時間を算出する。
        /// スキルレベル1で30秒、スキルレベル5で15秒。
        /// </summary>
        private float CalculateOverhaulDuration()
        {
            return BaseOverhaulDuration / WorkEfficiencyMultiplier;
        }

        // ============================================================
        // ユーティリティ
        // ============================================================

        /// <summary>
        /// 論理FacilityIdからアトラクションのGameObjectを検索する。
        /// FacilityBase.FacilityIdと一致するオブジェクトを返す。
        /// </summary>
        private GameObject FindAttractionById(int facilityId)
        {
            var attractions = GameObject.FindGameObjectsWithTag("Attraction");
            foreach (var attraction in attractions)
            {
                var facility = attraction.GetComponent<Attraction.FacilityBase>();
                if (facility != null && facility.FacilityId == facilityId)
                {
                    return attraction;
                }
            }
            return null;
        }

        /// <summary>修理キューをクリアする（ゲームリセット時等）</summary>
        public static void ClearRepairQueue()
        {
            repairQueue.Clear();
            claimedAttractions.Clear();
        }

        // ============================================================
        // デバッグ
        // ============================================================

#if UNITY_EDITOR
        protected override void OnDrawGizmosSelected()
        {
            base.OnDrawGizmosSelected();

            // 検知範囲
            Gizmos.color = new Color(1f, 0.5f, 0f, 0.15f);
            Gizmos.DrawSphere(transform.position, detectionRadius);

            // 修理対象への線
            if (currentTaskTarget.HasValue)
            {
                Gizmos.color = isRepairing ? Color.red : Color.yellow;
                Gizmos.DrawLine(transform.position, currentTaskTarget.Value);
            }
        }
#endif
    }
}
