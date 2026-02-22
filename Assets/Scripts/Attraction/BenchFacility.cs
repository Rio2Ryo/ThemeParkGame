// ============================================================
// ThemeParkGame - BenchFacility
// ベンチ施設の具象クラス（FacilityBase継承）
// ============================================================

using UnityEngine;
using ThemeParkGame.Core;

namespace ThemeParkGame.Attraction
{
    public class BenchFacility : FacilityBase
    {
        [Header("Bench Settings")]
        [SerializeField] private int maxSeats = 3;
        [SerializeField] private float restRecoveryRate = 10f;

        private int currentUsers;

        public override FacilityType FacilityType => FacilityType.Bench;
        public override string DisplayName => "ベンチ";

        public override bool CanAcceptVisitor(int visitorId)
        {
            return IsActive && currentUsers < maxSeats;
        }

        public override bool OnVisitorArrive(int visitorId)
        {
            if (!CanAcceptVisitor(visitorId)) return false;
            currentUsers++;
            return true;
        }

        public override void OnVisitorLeave(int visitorId)
        {
            currentUsers = Mathf.Max(0, currentUsers - 1);
        }

        public override float CalculateAppeal(int visitorId)
        {
            if (!IsActive) return 0f;
            float occupancyRatio = (float)currentUsers / maxSeats;
            return Mathf.Clamp01(0.6f - occupancyRatio * 0.3f);
        }

        /// <summary>休憩回復速度を返す</summary>
        public float GetRestRecoveryRate() => restRecoveryRate;
    }
}
