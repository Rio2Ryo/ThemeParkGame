// ============================================================
// ThemeParkGame - DoctorStaff
// ドクター（医務スタッフ） - 体調不良・事故負傷の来場者を診察・治療
// ============================================================

using System.Collections.Generic;
using UnityEngine;
using ThemeParkGame.Core;
using ThemeParkGame.Visitor;

namespace ThemeParkGame.Staff
{
    /// <summary>
    /// ドクターは体調不良や事故負傷の来場者を診察・治療するスタッフ。
    ///
    /// 【ゲームデザイン】
    /// ・吐き気の高い来場者を検知し、近づいて治療する（嘔吐イベントを未然に防ぐ）
    /// ・アトラクション事故で負傷した来場者を応急処置し、幸福度を回復させる
    /// ・治療優先度: 事故負傷 > 吐き気 の順で対応する
    /// ・スキルレベルが高いほど治療速度が速く、回復量も大きい
    /// ・パーク内の衛生・安全性と来場者満足度に間接的に貢献する
    /// ・ドクター不足 → 嘔吐放置 → 連鎖嘔吐 → 衛生低下のリスクチェーンが存在する
    /// </summary>
    public class DoctorStaff : StaffMember
    {
        // ============================================================
        // 診察対象の種別
        // ============================================================

        /// <summary>診察・治療の種別</summary>
        public enum TreatmentType
        {
            /// <summary>吐き気治療 - 嘔吐を未然に防ぐ</summary>
            Nausea,

            /// <summary>事故負傷の応急処置 - アトラクション事故後の来場者を治療</summary>
            AccidentInjury
        }

        // ============================================================
        // 定数
        // ============================================================

        /// <summary>基本吐き気治療時間（秒）。スキルレベルで短縮される</summary>
        private const float BaseNauseaTreatmentDuration = 3f;

        /// <summary>基本事故治療時間（秒）。吐き気より重いため長め</summary>
        private const float BaseAccidentTreatmentDuration = 6f;

        /// <summary>吐き気の基本減少量</summary>
        private const float BaseNauseaReduction = 50f;

        /// <summary>治療完了時の基本幸福度回復量（吐き気治療）</summary>
        private const float BaseHappinessRestoreNausea = 10f;

        /// <summary>治療完了時の基本幸福度回復量（事故治療）</summary>
        private const float BaseHappinessRestoreAccident = 20f;

        /// <summary>吐き気がこの値以上の来場者を治療対象とする</summary>
        private const float NauseaThreshold = 60f;

        /// <summary>事故発生からこの秒数以内の来場者を治療対象とする</summary>
        private const float AccidentTimeWindow = 120f;

        /// <summary>治療完了時にパークの快適性評価に加算するボーナス</summary>
        private const float ComfortBonusPerTreatment = 0.2f;

        // ============================================================
        // フィールド
        // ============================================================

        [Header("Doctor Settings")]
        [SerializeField] private float detectionRadius = 25f;

        /// <summary>現在治療中の来場者</summary>
        private VisitorAI _targetVisitor;

        /// <summary>治療タイマー</summary>
        private float _treatTimer;

        /// <summary>現在の治療の所要時間</summary>
        private float _treatDuration;

        /// <summary>現在の治療種別</summary>
        private TreatmentType _currentTreatmentType;

        /// <summary>治療済み合計回数</summary>
        public int TotalTreatments { get; private set; }

        /// <summary>吐き気治療の回数</summary>
        public int NauseaTreatments { get; private set; }

        /// <summary>事故治療の回数</summary>
        public int AccidentTreatments { get; private set; }

        /// <summary>対応中の来場者IDセット（重複割り当て防止）</summary>
        private static readonly HashSet<int> _claimedVisitors = new HashSet<int>();

        /// <summary>事故が報告された来場者IDと報告時刻</summary>
        private static readonly Dictionary<int, float> _accidentReports = new Dictionary<int, float>();

        // ============================================================
        // 初期化
        // ============================================================

        protected override void Awake()
        {
            base.Awake();
            StaffType = StaffType.Doctor;
        }

        // ============================================================
        // イベント購読
        // ============================================================

        protected override void SubscribeToEvents()
        {
            GameEvents.OnVisitorVomited += HandleVisitorVomited;
            GameEvents.OnVisitorHadAccident += HandleVisitorHadAccident;
        }

        protected override void UnsubscribeFromEvents()
        {
            GameEvents.OnVisitorVomited -= HandleVisitorVomited;
            GameEvents.OnVisitorHadAccident -= HandleVisitorHadAccident;
        }

        /// <summary>
        /// 来場者嘔吐イベントハンドラ。
        /// 嘔吐は吐き気が高い来場者がいることの兆候なので、待機中なら即座に対応する。
        /// </summary>
        private void HandleVisitorVomited(int visitorId)
        {
            if (CurrentState == StaffBehaviorState.Idle)
            {
                FindAndAssignTask();
            }
        }

        /// <summary>
        /// 来場者事故イベントハンドラ。
        /// 事故負傷は最優先で対応すべきため、事故報告リストに追加し即座にタスク検索する。
        /// </summary>
        private void HandleVisitorHadAccident(int visitorId)
        {
            if (!_accidentReports.ContainsKey(visitorId))
            {
                _accidentReports[visitorId] = Time.time;
            }

            if (CurrentState == StaffBehaviorState.Idle)
            {
                FindAndAssignTask();
            }
        }

        // ============================================================
        // タスク検索
        // ============================================================

        /// <summary>
        /// 治療対象を優先度順に検索する。
        ///
        /// 【検索優先度】
        /// 1. 事故負傷者（AccidentInjury） - 最優先、放置するとパーク評価が大きく下がる
        /// 2. 吐き気が高い来場者（Nausea） - 嘔吐防止
        ///
        /// 同種の中では距離が近いものを優先する。
        /// </summary>
        protected override bool FindAndAssignTask()
        {
            if (GameManager.Instance?.VisitorManager == null) return false;

            // 期限切れの事故報告をクリーンアップ
            CleanupExpiredAccidentReports();

            // 優先: 事故負傷者の治療
            if (TryFindAccidentVictim())
            {
                return true;
            }

            // 次点: 吐き気の高い来場者
            if (TryFindNauseaPatient())
            {
                return true;
            }

            return false;
        }

        /// <summary>事故負傷者を検索する</summary>
        private bool TryFindAccidentVictim()
        {
            if (_accidentReports.Count == 0) return false;

            var visitors = GameManager.Instance.VisitorManager.GetAllActiveVisitors();
            VisitorAI bestTarget = null;
            float bestDist = detectionRadius;

            for (int i = 0; i < visitors.Count; i++)
            {
                var v = visitors[i];
                if (v == null || !v.IsActive) continue;
                if (!_accidentReports.ContainsKey(v.VisitorId)) continue;
                if (_claimedVisitors.Contains(v.VisitorId)) continue;
                if (!IsWithinPatrolArea(v.transform.position)) continue;

                float dist = Vector3.Distance(transform.position, v.transform.position);
                if (dist < bestDist)
                {
                    bestDist = dist;
                    bestTarget = v;
                }
            }

            if (bestTarget != null)
            {
                ClaimAndAssignTarget(bestTarget, TreatmentType.AccidentInjury);
                WebGLOptimizer.LogVerbose($"[Doctor] {Name} が事故負傷者#{bestTarget.VisitorId}の治療に向かいます");
                return true;
            }

            return false;
        }

        /// <summary>吐き気の高い来場者を検索する</summary>
        private bool TryFindNauseaPatient()
        {
            var visitors = GameManager.Instance.VisitorManager.GetAllActiveVisitors();
            VisitorAI bestTarget = null;
            float bestDist = detectionRadius;

            for (int i = 0; i < visitors.Count; i++)
            {
                var v = visitors[i];
                if (v == null || !v.IsActive) continue;
                if (v.Parameters.Nausea < NauseaThreshold) continue;
                if (_claimedVisitors.Contains(v.VisitorId)) continue;
                if (!IsWithinPatrolArea(v.transform.position)) continue;

                float dist = Vector3.Distance(transform.position, v.transform.position);
                if (dist < bestDist)
                {
                    bestDist = dist;
                    bestTarget = v;
                }
            }

            if (bestTarget != null)
            {
                ClaimAndAssignTarget(bestTarget, TreatmentType.Nausea);
                WebGLOptimizer.LogVerbose($"[Doctor] {Name} が吐き気患者#{bestTarget.VisitorId}の治療に向かいます" +
                    $" (吐き気: {bestTarget.Parameters.Nausea:F0})");
                return true;
            }

            return false;
        }

        /// <summary>治療対象を担当として確保し、移動を開始する</summary>
        private void ClaimAndAssignTarget(VisitorAI target, TreatmentType treatmentType)
        {
            _claimedVisitors.Add(target.VisitorId);
            _targetVisitor = target;
            _currentTreatmentType = treatmentType;
            _treatDuration = CalculateTreatmentDuration(treatmentType);
            _treatTimer = 0f;
            NavigateTo(target.transform.position);
            CurrentState = StaffBehaviorState.MovingToTask;
        }

        // ============================================================
        // 作業実行
        // ============================================================

        protected override void OnTaskReached()
        {
            _treatTimer = 0f;
            StopNavigation();

            string treatmentName = _currentTreatmentType switch
            {
                TreatmentType.AccidentInjury => "事故負傷の応急処置",
                TreatmentType.Nausea => "吐き気の治療",
                _ => "診察"
            };

            WebGLOptimizer.LogVerbose($"[Doctor] {Name} が来場者の{treatmentName}を開始");
        }

        /// <summary>
        /// 治療作業を毎フレーム進行させる。
        /// 対象が退園済み等の場合はタスクを中断する。
        ///
        /// 【ゲームデザイン】
        /// ・治療中はドクターがその場に停止し、一定時間後に治療完了
        /// ・スキルレベルが高いほど所要時間が短くなる
        /// ・治療完了時に吐き気低下・幸福度回復の効果を適用
        /// </summary>
        protected override void PerformWork()
        {
            if (_targetVisitor == null || !_targetVisitor.IsActive)
            {
                ReleaseTarget();
                CompleteCurrentTask();
                return;
            }

            _treatTimer += Time.deltaTime;

            if (_treatTimer >= _treatDuration)
            {
                CompleteTreatment();
            }
        }

        /// <summary>治療を完了し、来場者のパラメータを回復させる</summary>
        private void CompleteTreatment()
        {
            TotalTreatments++;

            if (_targetVisitor != null && _targetVisitor.Parameters != null)
            {
                switch (_currentTreatmentType)
                {
                    case TreatmentType.Nausea:
                        ApplyNauseaTreatment();
                        break;

                    case TreatmentType.AccidentInjury:
                        ApplyAccidentTreatment();
                        break;
                }

                // 治療完了によりパークの快適性評価に微量ボーナス
                var pm = GameManager.Instance?.ParkManager;
                if (pm?.Rating != null)
                {
                    pm.Rating.ApplyExternalBonus(CertificateCategory.Comfort,
                        ComfortBonusPerTreatment * WorkEfficiencyMultiplier);
                }
            }

            ReleaseTarget();
            CompleteCurrentTask();
        }

        /// <summary>吐き気治療の効果を適用する</summary>
        private void ApplyNauseaTreatment()
        {
            NauseaTreatments++;

            float nauseaReduction = BaseNauseaReduction * WorkEfficiencyMultiplier;
            float happinessBoost = BaseHappinessRestoreNausea * WorkEfficiencyMultiplier;

            _targetVisitor.Parameters.ModifyNausea(-nauseaReduction, _targetVisitor.Type);
            _targetVisitor.Parameters.ModifyHappiness(happinessBoost);

            WebGLOptimizer.LogVerbose($"[Doctor] {Name} が来場者#{_targetVisitor.VisitorId}の吐き気治療を完了 " +
                $"(吐き気-{nauseaReduction:F0}, 幸福+{happinessBoost:F0})");
        }

        /// <summary>
        /// 事故負傷治療の効果を適用する。
        /// 事故で失われた幸福度を大きく回復し、事故報告リストから除去する。
        /// </summary>
        private void ApplyAccidentTreatment()
        {
            AccidentTreatments++;

            float happinessBoost = BaseHappinessRestoreAccident * WorkEfficiencyMultiplier;
            _targetVisitor.Parameters.ModifyHappiness(happinessBoost);

            // 事故報告リストから除去
            _accidentReports.Remove(_targetVisitor.VisitorId);

            WebGLOptimizer.LogVerbose($"[Doctor] {Name} が来場者#{_targetVisitor.VisitorId}の事故負傷を治療完了 " +
                $"(幸福+{happinessBoost:F0})");
        }

        /// <summary>担当来場者の占有を解放する</summary>
        private void ReleaseTarget()
        {
            if (_targetVisitor != null)
            {
                _claimedVisitors.Remove(_targetVisitor.VisitorId);
                _targetVisitor = null;
            }
        }

        // ============================================================
        // 所要時間算出
        // ============================================================

        /// <summary>
        /// 治療時間を算出する。スキルレベルが高いほど高速に治療できる。
        /// 吐き気治療: スキル1で3秒、スキル5で1.5秒
        /// 事故治療:   スキル1で6秒、スキル5で3秒
        /// </summary>
        private float CalculateTreatmentDuration(TreatmentType type)
        {
            float baseDuration = type switch
            {
                TreatmentType.Nausea => BaseNauseaTreatmentDuration,
                TreatmentType.AccidentInjury => BaseAccidentTreatmentDuration,
                _ => BaseNauseaTreatmentDuration
            };

            return baseDuration / WorkEfficiencyMultiplier;
        }

        // ============================================================
        // ユーティリティ
        // ============================================================

        /// <summary>期限切れの事故報告エントリを削除する</summary>
        private void CleanupExpiredAccidentReports()
        {
            var expiredKeys = new List<int>();
            float currentTime = Time.time;

            foreach (var kvp in _accidentReports)
            {
                if (currentTime - kvp.Value >= AccidentTimeWindow)
                {
                    expiredKeys.Add(kvp.Key);
                }
            }

            foreach (var key in expiredKeys)
            {
                _accidentReports.Remove(key);
            }
        }

        /// <summary>治療記録をクリアする（ゲームリセット時等）</summary>
        public static void ClearTreatmentRecords()
        {
            _claimedVisitors.Clear();
            _accidentReports.Clear();
        }

        // ============================================================
        // デバッグ
        // ============================================================

#if UNITY_EDITOR
        protected override void OnDrawGizmosSelected()
        {
            base.OnDrawGizmosSelected();

            // 検知範囲
            Gizmos.color = new Color(0f, 0.8f, 1f, 0.1f);
            Gizmos.DrawSphere(transform.position, detectionRadius);
            Gizmos.color = new Color(0f, 0.8f, 1f, 0.4f);
            Gizmos.DrawWireSphere(transform.position, detectionRadius);

            // 治療対象への線
            if (_targetVisitor != null)
            {
                Gizmos.color = _currentTreatmentType switch
                {
                    TreatmentType.AccidentInjury => Color.red,
                    TreatmentType.Nausea => Color.yellow,
                    _ => Color.white
                };
                Gizmos.DrawLine(transform.position, _targetVisitor.transform.position);
            }
        }
#endif
    }
}
