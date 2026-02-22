// ============================================================
// ThemeParkGame - GenericFacility
// 汎用施設の具象クラス（FacilityBase継承）
// TrashCan, InfoBoard, StaffRoom, ResearchLab, Pathway, Decoration 用
// ============================================================

using UnityEngine;
using ThemeParkGame.Core;

namespace ThemeParkGame.Attraction
{
    public class GenericFacility : FacilityBase
    {
        [Header("Generic Facility Settings")]
        [SerializeField] private FacilityType facilityType = FacilityType.TrashCan;
        [SerializeField] private string displayName = "施設";

        public override FacilityType FacilityType => facilityType;
        public override string DisplayName => displayName;

        /// <summary>施設タイプを外部から設定する（プレハブ生成時に使用）</summary>
        public void SetFacilityType(FacilityType type)
        {
            facilityType = type;
        }

        /// <summary>表示名を外部から設定する（プレハブ生成時に使用）</summary>
        public void SetDisplayName(string name)
        {
            displayName = name;
        }

        public override bool CanAcceptVisitor(int visitorId)
        {
            // 汎用施設は基本的に来場者を受け入れない
            // (InfoBoardは閲覧可能だが、キャパシティ制限なし)
            return IsActive && facilityType == FacilityType.InfoBoard;
        }

        public override bool OnVisitorArrive(int visitorId)
        {
            if (!CanAcceptVisitor(visitorId)) return false;
            return true;
        }

        public override void OnVisitorLeave(int visitorId)
        {
            // 汎用施設は特別な退出処理なし
        }

        public override float CalculateAppeal(int visitorId)
        {
            if (!IsActive) return 0f;

            switch (facilityType)
            {
                case FacilityType.InfoBoard:
                    return 0.3f;
                default:
                    return 0f;
            }
        }
    }
}
