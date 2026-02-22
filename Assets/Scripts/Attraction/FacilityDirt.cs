// ============================================================
// ThemeParkGame - FacilityDirt
// 施設の汚れ度管理コンポーネント（トイレ等に付与）
// ============================================================

using UnityEngine;

namespace ThemeParkGame.Attraction
{
    /// <summary>
    /// 施設の汚れ度を管理するコンポーネント。
    /// トイレや飲食施設に付与して、CleanerStaffが清掃対象として検出する。
    /// 時間経過と利用者数に応じて汚れ度が上昇する。
    /// </summary>
    public class FacilityDirt : MonoBehaviour
    {
        [Header("Dirt Settings")]
        [SerializeField] private float dirtLevel;
        [SerializeField] private float dirtAccumulationRate = 0.01f;
        [SerializeField] private float maxDirtLevel = 1f;

        /// <summary>現在の汚れ度（0.0=清潔 ～ 1.0=最大汚染）</summary>
        public float DirtLevel
        {
            get => dirtLevel;
            set => dirtLevel = Mathf.Clamp(value, 0f, maxDirtLevel);
        }

        /// <summary>清掃が必要な汚れ閾値</summary>
        public float CleaningThreshold => 0.3f;

        /// <summary>施設が汚れているか</summary>
        public bool IsDirty => dirtLevel >= CleaningThreshold;

        private void Update()
        {
            // 時間経過で汚れが蓄積
            dirtLevel = Mathf.Min(maxDirtLevel, dirtLevel + dirtAccumulationRate * Time.deltaTime);
        }

        /// <summary>利用者が使用した際に汚れを追加する</summary>
        public void AddUsageDirt(float amount = 0.05f)
        {
            dirtLevel = Mathf.Min(maxDirtLevel, dirtLevel + amount);
        }

        /// <summary>清掃して汚れをリセットする</summary>
        public void Clean()
        {
            dirtLevel = 0f;
        }
    }
}
