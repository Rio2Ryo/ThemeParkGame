// ============================================================
// ThemeParkGame - GardenerStaff
// 園芸師 - パークの美観・ムード評価を向上
// ============================================================

using UnityEngine;
using ThemeParkGame.Core;
using ThemeParkGame.Park;

namespace ThemeParkGame.Staff
{
    /// <summary>
    /// 園芸師はパーク内の緑地・花壇の手入れを行い、ムード評価を向上させるスタッフ。
    ///
    /// 【ゲームデザイン】
    /// ・パトロールエリア内を巡回し、定期的に「手入れ」作業を行う
    /// ・手入れ完了ごとにパークのムード評価にボーナスが加算される
    /// ・スキルレベルが高いほど手入れ速度が速く、ボーナス量も増加
    /// ・来場者の幸福度にも微量だが持続的な効果がある
    /// ・装飾エリアに配置すると効果が高い
    /// </summary>
    public class GardenerStaff : StaffMember
    {
        private const float BaseGardenDuration = 5f;
        private const float BaseMoodBonus = 0.5f;
        private const float GardenInterval = 15f;
        private const float HappinessAuraRadius = 15f;
        private const float HappinessAuraAmount = 0.02f; // 毎秒近くの来場者に加算

        private float _gardenTimer;
        private float _intervalTimer;
        private float _auraTimer;
        private bool _isGardening;
        private Vector3 _gardenSpot;

        public int TotalGardeningSessions { get; private set; }

        protected override void Awake()
        {
            base.Awake();
            StaffType = StaffType.Gardener;
        }

        protected override void SubscribeToEvents() { }
        protected override void UnsubscribeFromEvents() { }

        protected override bool FindAndAssignTask()
        {
            _intervalTimer -= Time.deltaTime;
            if (_intervalTimer > 0f) return false;

            // パトロールエリア内のランダムな地点を「手入れ対象」として設定
            Vector3 center = transform.position;
            Vector3 randomOffset = Random.insideUnitSphere * 10f;
            randomOffset.y = 0f;
            _gardenSpot = center + randomOffset;

            // パトロールエリア内か確認
            if (!IsWithinPatrolArea(_gardenSpot))
            {
                _gardenSpot = center; // エリア外ならその場で
            }

            _gardenTimer = 0f;
            _isGardening = false;
            NavigateTo(_gardenSpot);
            CurrentState = StaffBehaviorState.MovingToTask;
            return true;
        }

        protected override void OnTaskReached()
        {
            _gardenTimer = 0f;
            _isGardening = true;
            StopNavigation();
            WebGLOptimizer.LogVerbose($"[Gardener] {Name} が園芸作業を開始");
        }

        protected override void PerformWork()
        {
            if (!_isGardening)
            {
                CompleteCurrentTask();
                return;
            }

            _gardenTimer += Time.deltaTime;
            float duration = BaseGardenDuration / WorkEfficiencyMultiplier;

            // 作業中は近くの来場者の幸福度を微量上昇（花の香り効果）
            _auraTimer += Time.deltaTime;
            if (_auraTimer >= 1f)
            {
                _auraTimer = 0f;
                ApplyHappinessAura();
            }

            if (_gardenTimer >= duration)
            {
                CompleteGardening();
            }
        }

        private void CompleteGardening()
        {
            TotalGardeningSessions++;
            _isGardening = false;
            _intervalTimer = GardenInterval / WorkEfficiencyMultiplier;

            // ムード評価にボーナスを加算
            float bonus = BaseMoodBonus * WorkEfficiencyMultiplier;
            var pm = GameManager.Instance?.ParkManager;
            if (pm?.Rating != null)
            {
                pm.Rating.ApplyExternalBonus(CertificateCategory.Mood, bonus);
            }

            WebGLOptimizer.LogVerbose($"[Gardener] {Name} が園芸作業を完了 (ムード+{bonus:F1}, 累計{TotalGardeningSessions}回)");
            CompleteCurrentTask();
        }

        private void ApplyHappinessAura()
        {
            if (GameManager.Instance?.VisitorManager == null) return;

            var visitors = GameManager.Instance.VisitorManager.GetAllActiveVisitors();
            for (int i = 0; i < visitors.Count; i++)
            {
                var v = visitors[i];
                if (v == null || !v.IsActive) continue;

                float dist = Vector3.Distance(transform.position, v.transform.position);
                if (dist <= HappinessAuraRadius)
                {
                    v.Parameters.ModifyHappiness(HappinessAuraAmount);
                }
            }
        }
    }
}
