// ============================================================
// ThemeParkGame - ScenarioData
// シナリオ個別データクラス（国ごとの設定・目標）
// ============================================================

using System;
using System.Collections.Generic;
using ThemeParkGame.Core;

namespace ThemeParkGame.Park
{
    /// <summary>
    /// シナリオ目標の種別。
    /// 各シナリオは複数の目標を持ち、全達成でクリアとなる。
    /// </summary>
    public enum ObjectiveType
    {
        /// <summary>累計来場者数が目標値に到達する</summary>
        VisitorTarget,

        /// <summary>月間利益が目標値を超える</summary>
        MonthlyProfitTarget,

        /// <summary>パーク総合評価が目標値に到達する</summary>
        ParkRatingTarget,

        /// <summary>指定カテゴリの認定証を取得する</summary>
        ObtainCertificate,

        /// <summary>アトラクション数が目標に到達する</summary>
        AttractionCountTarget,

        /// <summary>特定テーマゾーンをアンロックする</summary>
        UnlockZone,

        /// <summary>累計収入が目標値に到達する</summary>
        TotalRevenueTarget,

        /// <summary>来場者の平均幸福度が目標値以上を維持する</summary>
        HappinessTarget,

        /// <summary>ゴールデンチケットを指定枚数獲得する</summary>
        GoldenTicketTarget
    }

    /// <summary>
    /// シナリオ内の個別目標。達成条件と報酬を定義する。
    /// </summary>
    [Serializable]
    public class ScenarioObjective
    {
        /// <summary>目標の種別</summary>
        public ObjectiveType Type;

        /// <summary>目標値（来場者数、利益額、評価値など型に依存）</summary>
        public float TargetValue;

        /// <summary>
        /// 補助パラメータ。ObtainCertificateの場合はCertificateCategoryのint値、
        /// UnlockZoneの場合はThemeZoneのint値として使用する。
        /// </summary>
        public int SubParameter;

        /// <summary>目標の表示名（ローカライズキー）</summary>
        public string DescriptionKey;

        /// <summary>達成済みフラグ</summary>
        public bool IsCompleted;

        public ScenarioObjective() { }

        public ScenarioObjective(ObjectiveType type, float targetValue, string descriptionKey, int subParameter = 0)
        {
            Type = type;
            TargetValue = targetValue;
            DescriptionKey = descriptionKey;
            SubParameter = subParameter;
            IsCompleted = false;
        }
    }

    /// <summary>
    /// シナリオクリア時の報酬データ。
    /// </summary>
    [Serializable]
    public class ScenarioReward
    {
        /// <summary>報酬として獲得するゴールデンチケット数</summary>
        public int GoldenTickets;

        /// <summary>報酬金額</summary>
        public int BonusMoney;

        /// <summary>アンロックされるアトラクションID（あれば）</summary>
        public List<string> UnlockedAttractions;

        /// <summary>アンロックされるテーマゾーン（あれば）</summary>
        public ThemeZone? UnlockedZone;

        public ScenarioReward()
        {
            UnlockedAttractions = new List<string>();
        }
    }

    /// <summary>
    /// 初期配置施設の定義。シナリオ開始時に自動配置される施設。
    /// </summary>
    [Serializable]
    public class PrebuiltFacilityData
    {
        /// <summary>施設タイプ</summary>
        public FacilityType FacilityType;

        /// <summary>施設固有ID（アトラクション名など）</summary>
        public string FacilityId;

        /// <summary>配置グリッド座標X</summary>
        public int GridX;

        /// <summary>配置グリッド座標Y</summary>
        public int GridY;

        /// <summary>所属ゾーン</summary>
        public ThemeZone Zone;

        public PrebuiltFacilityData() { }

        public PrebuiltFacilityData(FacilityType type, string facilityId, int gridX, int gridY, ThemeZone zone)
        {
            FacilityType = type;
            FacilityId = facilityId;
            GridX = gridX;
            GridY = gridY;
            Zone = zone;
        }
    }

    /// <summary>
    /// シナリオの全設定データ。各国のシナリオ情報を包括する。
    /// ScenarioDatabaseから取得し、GameManager.StartScenario()で使用される。
    ///
    /// 【ゲームデザイン】
    /// シナリオは段階的な難易度で構成される:
    /// - Easy (4ヶ国): 初期資金潤沢、目標緩め、チュートリアル的要素あり
    /// - Normal (2ヶ国): 標準的な経営バランス
    /// - Hard (4ヶ国): 初期資金少、厳しい目標、特殊条件あり
    /// </summary>
    [Serializable]
    public class ScenarioData
    {
        /// <summary>シナリオ国</summary>
        public ScenarioCountry Country;

        /// <summary>難易度</summary>
        public ScenarioDifficulty Difficulty;

        /// <summary>初期所持金</summary>
        public int StartingMoney;

        /// <summary>開始時のテーマゾーン</summary>
        public ThemeZone InitialZone;

        /// <summary>初期配置施設リスト</summary>
        public List<PrebuiltFacilityData> PrebuiltFacilities;

        /// <summary>シナリオ目標リスト（全達成でクリア）</summary>
        public List<ScenarioObjective> Objectives;

        /// <summary>クリア報酬</summary>
        public ScenarioReward CompletionReward;

        /// <summary>シナリオ名表示キー</summary>
        public string NameKey;

        /// <summary>シナリオ説明表示キー</summary>
        public string DescriptionKey;

        /// <summary>クリア制限時間（ゲーム年数、0=制限なし）</summary>
        public int TimeLimitYears;

        public ScenarioData()
        {
            PrebuiltFacilities = new List<PrebuiltFacilityData>();
            Objectives = new List<ScenarioObjective>();
            CompletionReward = new ScenarioReward();
        }

        /// <summary>全目標が達成済みかどうか</summary>
        public bool AreAllObjectivesCompleted()
        {
            if (Objectives == null || Objectives.Count == 0) return false;

            for (int i = 0; i < Objectives.Count; i++)
            {
                if (!Objectives[i].IsCompleted) return false;
            }
            return true;
        }

        /// <summary>達成済み目標数 / 総目標数</summary>
        public string GetProgressString()
        {
            if (Objectives == null) return "0/0";
            int completed = 0;
            for (int i = 0; i < Objectives.Count; i++)
            {
                if (Objectives[i].IsCompleted) completed++;
            }
            return $"{completed}/{Objectives.Count}";
        }
    }
}
