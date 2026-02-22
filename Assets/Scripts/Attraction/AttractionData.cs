// ============================================================
// ThemeParkGame - AttractionData (ScriptableObject)
// アトラクション定義データ
// Unityエディタ上でアトラクションのマスターデータを設定する
// ============================================================

using System;
using System.Collections.Generic;
using UnityEngine;
using ThemeParkGame.Core;

namespace ThemeParkGame.Attraction
{
    /// <summary>
    /// アトラクションのアップグレード段階データ。
    /// 各レベルで性能・見た目がどう変化するかを定義する。
    /// 最大3段階のアップグレードが可能。
    /// </summary>
    [Serializable]
    public class AttractionUpgradeLevel
    {
        /// <summary>アップグレードレベル（1～3）</summary>
        [Tooltip("アップグレードレベル（1～3）")]
        public int Level;

        /// <summary>アップグレード名</summary>
        [Tooltip("アップグレード名（例: 「強化フレーム」）")]
        public string UpgradeName;

        /// <summary>アップグレードの説明</summary>
        [TextArea(2, 4)]
        public string Description;

        /// <summary>アップグレード費用</summary>
        [Tooltip("このレベルへのアップグレードに必要な費用")]
        public int UpgradeCost;

        // ---- 性能変化量（基本値からの乗算係数） ----

        /// <summary>興奮度への乗算係数</summary>
        [Tooltip("興奮度への乗算係数（例: 1.2 = 20%向上）")]
        [Range(1f, 2f)]
        public float ExcitementMultiplier = 1f;

        /// <summary>定員への乗算係数</summary>
        [Tooltip("定員への乗算係数")]
        [Range(1f, 2f)]
        public float CapacityMultiplier = 1f;

        /// <summary>嘔吐率への乗算係数（低いほど良い）</summary>
        [Tooltip("嘔吐率への乗算係数（0.8 = 20%軽減）")]
        [Range(0.5f, 1.5f)]
        public float NauseaMultiplier = 1f;

        /// <summary>故障率への乗算係数（低いほど良い）</summary>
        [Tooltip("故障率への乗算係数（0.8 = 20%軽減）")]
        [Range(0.5f, 1.5f)]
        public float BreakdownRateMultiplier = 1f;

        /// <summary>このレベルで解放されるアンロック研究ID（空なら不要）</summary>
        [Tooltip("このアップグレードに必要な研究ID（空欄なら研究不要）")]
        public string RequiredResearchId;

        /// <summary>アップグレード後の外観プレハブ（nullなら変更なし）</summary>
        [Tooltip("アップグレード後の外観プレハブ（nullなら基本外観を使用）")]
        public GameObject UpgradedVisualPrefab;
    }

    /// <summary>
    /// アトラクション定義データ（ScriptableObject）。
    /// Unityエディタ上でアトラクションのマスターデータを設定し、
    /// ランタイムではAttractionコンポーネントがこのデータを参照して動作する。
    ///
    /// 設計思想:
    /// - 同一アトラクションが複数台設置されてもデータは共有される
    /// - ランタイムの可変状態はAttractionコンポーネント側が保持する
    /// - アップグレードパスは最大3段階で、各段階の性能変化を定義する
    /// </summary>
    [CreateAssetMenu(fileName = "NewAttractionData", menuName = "ThemeParkGame/Attraction Data")]
    public class AttractionData : ScriptableObject
    {
        // ---- 識別情報 ----

        [Header("識別情報")]
        [Tooltip("アトラクションの一意な識別子（例: LK_COASTER_01）")]
        public string AttractionId;

        [Tooltip("日本語名")]
        public string NameJP;

        [Tooltip("英語名")]
        public string NameEN;

        [TextArea(2, 5)]
        [Tooltip("アトラクションの説明文")]
        public string Description;

        // ---- カテゴリ・所属 ----

        [Header("カテゴリ・所属")]
        [Tooltip("アトラクションカテゴリ（G系・縦回転・横回転・展望系・見せ物系・乗り物系）")]
        public AttractionCategory Category;

        [Tooltip("対応テーマゾーン（このゾーンに設置するとボーナスあり）")]
        public ThemeZone PrimaryThemeZone;

        [Tooltip("他のテーマゾーンにも設置可能か")]
        public bool AllowCrossZonePlacement = true;

        // ---- 基本性能 ----

        [Header("基本性能")]
        [Tooltip("興奮度（0.0～10.0）。高いほど来場者の満足度に寄与する")]
        [Range(0f, 10f)]
        public float ExcitementRating = 5f;

        [Tooltip("嘔吐率（0.0～1.0）。乗車後に来場者が嘔吐する確率")]
        [Range(0f, 1f)]
        public float NauseaFactor = 0.1f;

        [Tooltip("1回の乗車定員")]
        [Min(1)]
        public int Capacity = 12;

        [Tooltip("1回の運転サイクル時間（秒）。乗車～降車まで")]
        [Min(10f)]
        public float RideDuration = 60f;

        // ---- 経済性能 ----

        [Header("経済性能")]
        [Tooltip("建設費用")]
        [Min(100)]
        public int BuildCost = 5000;

        [Tooltip("月間維持費（メンテナンス込み）")]
        [Min(0)]
        public int MaintenanceCost = 200;

        [Tooltip("推奨チケット価格。この値前後が来場者の満足度最大")]
        [Min(0)]
        public int SuggestedTicketPrice = 300;

        // ---- 配置情報 ----

        [Header("配置情報")]
        [Tooltip("グリッド上のサイズ（タイル数）")]
        public Vector2Int Size = new Vector2Int(3, 3);

        // ---- 研究・アンロック ----

        [Header("研究・アンロック")]
        [Tooltip("建設に必要な研究ID。空欄なら最初から利用可能")]
        public string RequiredResearchId;

        [Tooltip("この研究が完了していないと建設メニューに表示されない")]
        public bool HideUntilResearched = true;

        // ---- アップグレードパス ----

        [Header("アップグレードパス")]
        [Tooltip("最大3段階のアップグレード定義")]
        public List<AttractionUpgradeLevel> UpgradePath = new List<AttractionUpgradeLevel>();

        // ---- ビジュアル ----

        [Header("ビジュアル")]
        [Tooltip("基本外観プレハブ")]
        public GameObject VisualPrefab;

        [Tooltip("建設中の外観プレハブ")]
        public GameObject ConstructionPrefab;

        [Tooltip("UIアイコン")]
        public Sprite Icon;

        // ---- 故障パラメータ ----

        [Header("故障パラメータ")]
        [Tooltip("基本故障確率（1時間あたり、0.0～1.0）")]
        [Range(0f, 0.5f)]
        public float BaseBreakdownRate = 0.02f;

        [Tooltip("メンテナンスなしで故障率が最大になるまでの時間（秒）")]
        [Min(60f)]
        public float TimeToMaxBreakdownRate = 600f;

        [Tooltip("故障放置から事故発生までの時間（秒）")]
        [Min(30f)]
        public float TimeToAccidentAfterBreakdown = 180f;

        // ---- ヘルパーメソッド ----

        /// <summary>
        /// 指定レベルのアップグレードデータを取得する。
        /// </summary>
        /// <param name="level">アップグレードレベル（1～3）</param>
        /// <returns>アップグレードデータ。該当レベルが存在しない場合はnull</returns>
        public AttractionUpgradeLevel GetUpgradeLevel(int level)
        {
            if (UpgradePath == null) return null;
            return UpgradePath.Find(u => u.Level == level);
        }

        /// <summary>最大アップグレードレベルを返す</summary>
        public int MaxUpgradeLevel => UpgradePath != null ? UpgradePath.Count : 0;

        /// <summary>
        /// 指定テーマゾーンに設置可能かどうかを判定する。
        /// プライマリゾーンなら常にtrue。他ゾーンはAllowCrossZonePlacementに依存。
        /// </summary>
        public bool IsCompatibleWithZone(ThemeZone zone)
        {
            if (zone == PrimaryThemeZone) return true;
            return AllowCrossZonePlacement;
        }

        /// <summary>
        /// 指定アップグレードレベルでの実効興奮度を計算する。
        /// </summary>
        public float GetEffectiveExcitement(int upgradeLevel)
        {
            float excitement = ExcitementRating;
            for (int i = 1; i <= upgradeLevel; i++)
            {
                var upgrade = GetUpgradeLevel(i);
                if (upgrade != null)
                    excitement *= upgrade.ExcitementMultiplier;
            }
            return excitement;
        }

        /// <summary>
        /// 指定アップグレードレベルでの実効定員を計算する。
        /// </summary>
        public int GetEffectiveCapacity(int upgradeLevel)
        {
            float capacity = Capacity;
            for (int i = 1; i <= upgradeLevel; i++)
            {
                var upgrade = GetUpgradeLevel(i);
                if (upgrade != null)
                    capacity *= upgrade.CapacityMultiplier;
            }
            return Mathf.RoundToInt(capacity);
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (string.IsNullOrEmpty(AttractionId))
            {
                Debug.LogWarning($"[AttractionData] AttractionIdが未設定です: {name}");
            }

            if (UpgradePath != null && UpgradePath.Count > 3)
            {
                Debug.LogWarning($"[AttractionData] アップグレードは最大3段階です: {name}");
                UpgradePath.RemoveRange(3, UpgradePath.Count - 3);
            }
        }
#endif
    }
}
