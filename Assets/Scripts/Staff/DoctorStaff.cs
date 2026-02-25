// ============================================================
// ThemeParkGame - DoctorStaff
// ドクター（医務スタッフ） - 体調不良の来場者を治療
// ============================================================

using UnityEngine;
using ThemeParkGame.Core;
using ThemeParkGame.Visitor;

namespace ThemeParkGame.Staff
{
    /// <summary>
    /// ドクターは体調不良の来場者（吐き気・疲労）を治療するスタッフ。
    ///
    /// 【ゲームデザイン】
    /// ・吐き気の高い来場者を検知し、近づいて治療する
    /// ・治療により吐き気を大幅に低下させ、嘔吐イベントを未然に防ぐ
    /// ・スキルレベルが高いほど治療速度が速く、回復量も大きい
    /// ・パーク内の衛生と来場者満足度に間接的に貢献する
    /// </summary>
    public class DoctorStaff : StaffMember
    {
        private const float DetectionRadius = 25f;
        private const float BaseTreatmentDuration = 3f;
        private const float BaseNauseaReduction = 50f;
        private const float BaseHappinessRestore = 10f;
        private const float NauseaThreshold = 60f;

        private VisitorAI _targetVisitor;
        private float _treatTimer;
        private float _treatDuration;

        public int TotalTreatments { get; private set; }

        protected override void Awake()
        {
            base.Awake();
            StaffType = StaffType.Doctor;
        }

        protected override void SubscribeToEvents()
        {
            GameEvents.OnVisitorVomited += HandleVisitorVomited;
        }

        protected override void UnsubscribeFromEvents()
        {
            GameEvents.OnVisitorVomited -= HandleVisitorVomited;
        }

        private void HandleVisitorVomited(int visitorId)
        {
            if (CurrentState == StaffBehaviorState.Idle)
            {
                FindAndAssignTask();
            }
        }

        protected override bool FindAndAssignTask()
        {
            if (GameManager.Instance?.VisitorManager == null) return false;

            var visitors = GameManager.Instance.VisitorManager.GetAllActiveVisitors();
            VisitorAI bestTarget = null;
            float bestDist = DetectionRadius;

            for (int i = 0; i < visitors.Count; i++)
            {
                var v = visitors[i];
                if (v == null || !v.IsActive) continue;
                if (v.Parameters.Nausea < NauseaThreshold) continue;
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
                _targetVisitor = bestTarget;
                _treatDuration = BaseTreatmentDuration / WorkEfficiencyMultiplier;
                _treatTimer = 0f;
                NavigateTo(bestTarget.transform.position);
                CurrentState = StaffBehaviorState.MovingToTask;
                return true;
            }

            return false;
        }

        protected override void OnTaskReached()
        {
            _treatTimer = 0f;
            StopNavigation();
            WebGLOptimizer.LogVerbose($"[Doctor] {Name} が来場者の治療を開始");
        }

        protected override void PerformWork()
        {
            if (_targetVisitor == null || !_targetVisitor.IsActive)
            {
                _targetVisitor = null;
                CompleteCurrentTask();
                return;
            }

            _treatTimer += Time.deltaTime;

            if (_treatTimer >= _treatDuration)
            {
                CompleteTreatment();
            }
        }

        private void CompleteTreatment()
        {
            TotalTreatments++;

            if (_targetVisitor != null && _targetVisitor.Parameters != null)
            {
                float nauseaReduction = BaseNauseaReduction * WorkEfficiencyMultiplier;
                float happinessBoost = BaseHappinessRestore * WorkEfficiencyMultiplier;

                _targetVisitor.Parameters.ModifyNausea(-nauseaReduction, _targetVisitor.Type);
                _targetVisitor.Parameters.ModifyHappiness(happinessBoost);

                WebGLOptimizer.LogVerbose($"[Doctor] {Name} が来場者#{_targetVisitor.VisitorId}を治療完了 " +
                    $"(吐き気-{nauseaReduction:F0}, 幸福+{happinessBoost:F0})");
            }

            _targetVisitor = null;
            CompleteCurrentTask();
        }
    }
}
