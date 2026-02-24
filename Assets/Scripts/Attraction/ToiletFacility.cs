// ============================================================
// ThemeParkGame - ToiletFacility
// トイレ施設の具象クラス（FacilityBase継承）
// ============================================================

using UnityEngine;
using ThemeParkGame.Core;

namespace ThemeParkGame.Attraction
{
    public class ToiletFacility : FacilityBase
    {
        [Header("Toilet Settings")]
        [SerializeField] private int maxCapacity = 5;
        #pragma warning disable CS0414
        [SerializeField] private float useTime = 30f;
        #pragma warning restore CS0414

        private int currentUsers;

        public override FacilityType FacilityType => FacilityType.Toilet;
        public override string DisplayName => "トイレ";

        public override bool CanAcceptVisitor(int visitorId)
        {
            return IsActive && currentUsers < maxCapacity;
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
            float occupancyRatio = (float)currentUsers / maxCapacity;
            return Mathf.Clamp01(1f - occupancyRatio * 0.5f);
        }
    }
}
