// ============================================================
// ThemeParkGame - ParkRating
// パーク評価システム（5カテゴリの評価・認定証管理）
// ============================================================

using System;
using System.Collections.Generic;
using UnityEngine;
using ThemeParkGame.Core;

namespace ThemeParkGame.Park
{
    /// <summary>
    /// 個別カテゴリの評価データ。
    /// </summary>
    [Serializable]
    public class CategoryRating
    {
        public CertificateCategory Category;

        /// <summary>現在の評価値（0～100）</summary>
        public float Score;

        /// <summary>認定証を取得済みか</summary>
        public bool CertificateAwarded;

        /// <summary>認定証取得に必要なスコア</summary>
        public float CertificateThreshold;

        /// <summary>総合評価への重み（全カテゴリの合計で1.0になる）</summary>
        public float Weight;
    }

    /// <summary>
    /// 認定証の特殊条件を管理する。
    /// スコア以外に必要な追加条件（例: 安全認定は事故ゼロ4年維持）。
    /// </summary>
    [Serializable]
    public class CertificateCondition
    {
        public CertificateCategory Category;
        public string ConditionDescription;
        public bool IsMet;
    }

    /// <summary>
    /// パーク総合評価システム。
    /// 5つのカテゴリ（Fame, Safety, Comfort, Excitement, Mood）で
    /// パークを0～100で評価し、条件を満たすと認定証を授与する。
    ///
    /// 【ゲームデザイン: 評価カテゴリの設計意図】
    /// - Fame (知名度): 累計来場者数とパーク年齢に依存。長期運営が有利。
    /// - Safety (安全性): 事故・故障の少なさ。メカニック配置が重要。
    ///   認定証は「4年間事故ゼロ」が必須条件（最も取得困難）。
    /// - Comfort (快適性): トイレ・ベンチ・清掃・飲食の充実度。
    ///   設備投資と清掃スタッフのバランスが鍵。
    /// - Excitement (興奮度): アトラクションの多様性と質。
    ///   同カテゴリ重複よりもバラエティが重要。
    /// - Mood (ムード): 来場者の平均幸福度。エンターテイナー配置が効果的。
    ///   価格設定・待ち時間・天候など複合要因。
    /// </summary>
    public class ParkRating
    {
        // ---- 認定証スコア閾値 ----
        private const float FAME_CERTIFICATE_THRESHOLD = 80f;
        private const float SAFETY_CERTIFICATE_THRESHOLD = 85f;
        private const float COMFORT_CERTIFICATE_THRESHOLD = 80f;
        private const float EXCITEMENT_CERTIFICATE_THRESHOLD = 75f;
        private const float MOOD_CERTIFICATE_THRESHOLD = 80f;

        // ---- 総合評価の加重 ----
        private const float FAME_WEIGHT = 0.15f;
        private const float SAFETY_WEIGHT = 0.25f;
        private const float COMFORT_WEIGHT = 0.20f;
        private const float EXCITEMENT_WEIGHT = 0.20f;
        private const float MOOD_WEIGHT = 0.20f;

        /// <summary>
        /// 安全認定証に必要な無事故年数。
        /// Theme Park Worldでは4年間無事故が要求される。
        /// </summary>
        private const int SAFETY_CERTIFICATE_ACCIDENT_FREE_YEARS = 4;

        /// <summary>カテゴリ別評価データ</summary>
        private readonly Dictionary<CertificateCategory, CategoryRating> _ratings;

        /// <summary>認定証の特殊条件</summary>
        private readonly Dictionary<CertificateCategory, CertificateCondition> _certificateConditions;

        /// <summary>事故履歴（年単位）。各年の事故発生数を記録する。</summary>
        private readonly Dictionary<int, int> _accidentHistory;

        /// <summary>現在のパーク年数</summary>
        private int _currentYear;

        /// <summary>総合評価（0～100）</summary>
        public float OverallRating { get; private set; }

        /// <summary>授与済み認定証の一覧</summary>
        public List<CertificateCategory> AwardedCertificates { get; private set; }

        public ParkRating()
        {
            _ratings = new Dictionary<CertificateCategory, CategoryRating>();
            _certificateConditions = new Dictionary<CertificateCategory, CertificateCondition>();
            _accidentHistory = new Dictionary<int, int>();
            AwardedCertificates = new List<CertificateCategory>();
            _currentYear = 1;

            InitializeRatings();
            InitializeCertificateConditions();
        }

        private void InitializeRatings()
        {
            _ratings[CertificateCategory.Fame] = new CategoryRating
            {
                Category = CertificateCategory.Fame,
                Score = 0f,
                CertificateAwarded = false,
                CertificateThreshold = FAME_CERTIFICATE_THRESHOLD,
                Weight = FAME_WEIGHT
            };

            _ratings[CertificateCategory.Safety] = new CategoryRating
            {
                Category = CertificateCategory.Safety,
                Score = 50f, // 開園直後は中間値（事故もないが実績もない）
                CertificateAwarded = false,
                CertificateThreshold = SAFETY_CERTIFICATE_THRESHOLD,
                Weight = SAFETY_WEIGHT
            };

            _ratings[CertificateCategory.Comfort] = new CategoryRating
            {
                Category = CertificateCategory.Comfort,
                Score = 0f,
                CertificateAwarded = false,
                CertificateThreshold = COMFORT_CERTIFICATE_THRESHOLD,
                Weight = COMFORT_WEIGHT
            };

            _ratings[CertificateCategory.Excitement] = new CategoryRating
            {
                Category = CertificateCategory.Excitement,
                Score = 0f,
                CertificateAwarded = false,
                CertificateThreshold = EXCITEMENT_CERTIFICATE_THRESHOLD,
                Weight = EXCITEMENT_WEIGHT
            };

            _ratings[CertificateCategory.Mood] = new CategoryRating
            {
                Category = CertificateCategory.Mood,
                Score = 50f, // 開園直後は中間値
                CertificateAwarded = false,
                CertificateThreshold = MOOD_CERTIFICATE_THRESHOLD,
                Weight = MOOD_WEIGHT
            };
        }

        private void InitializeCertificateConditions()
        {
            _certificateConditions[CertificateCategory.Fame] = new CertificateCondition
            {
                Category = CertificateCategory.Fame,
                ConditionDescription = "スコアが閾値以上",
                IsMet = false
            };

            _certificateConditions[CertificateCategory.Safety] = new CertificateCondition
            {
                Category = CertificateCategory.Safety,
                ConditionDescription = $"{SAFETY_CERTIFICATE_ACCIDENT_FREE_YEARS}年間事故ゼロ",
                IsMet = false
            };

            _certificateConditions[CertificateCategory.Comfort] = new CertificateCondition
            {
                Category = CertificateCategory.Comfort,
                ConditionDescription = "スコアが閾値以上",
                IsMet = false
            };

            _certificateConditions[CertificateCategory.Excitement] = new CertificateCondition
            {
                Category = CertificateCategory.Excitement,
                ConditionDescription = "スコアが閾値以上",
                IsMet = false
            };

            _certificateConditions[CertificateCategory.Mood] = new CertificateCondition
            {
                Category = CertificateCategory.Mood,
                ConditionDescription = "スコアが閾値以上",
                IsMet = false
            };
        }

        // ================================================================
        // 評価更新
        // ================================================================

        /// <summary>
        /// 知名度を更新する。
        /// 累計来場者数とパーク年齢から算出。
        /// </summary>
        /// <param name="totalVisitorsEver">累計来場者数</param>
        /// <param name="parkAge">パーク年齢（年）</param>
        public void UpdateFameRating(int totalVisitorsEver, int parkAge)
        {
            // 来場者数ベース（1万人で最大80ポイント）
            float visitorScore = Mathf.Clamp(totalVisitorsEver / 10000f * 80f, 0f, 80f);

            // パーク年齢ボーナス（最大20ポイント、10年で上限）
            float ageBonus = Mathf.Clamp(parkAge / 10f * 20f, 0f, 20f);

            _ratings[CertificateCategory.Fame].Score = Mathf.Clamp(visitorScore + ageBonus, 0f, 100f);
        }

        /// <summary>
        /// 安全性を更新する。
        /// 事故履歴とメカニック配置率から算出。
        /// </summary>
        /// <param name="recentAccidentCount">直近1年の事故件数</param>
        /// <param name="mechanicCoverageRatio">メカニック充足率（0～1）</param>
        /// <param name="totalAttractions">アトラクション総数</param>
        public void UpdateSafetyRating(int recentAccidentCount, float mechanicCoverageRatio, int totalAttractions)
        {
            // 基礎スコア: 事故0件で100、事故が増えるほど低下
            float accidentPenalty = totalAttractions > 0
                ? (float)recentAccidentCount / totalAttractions * 100f
                : 0f;
            float baseScore = Mathf.Clamp(100f - accidentPenalty * 20f, 0f, 100f);

            // メカニック配置ボーナス: 充足率100%で+10ポイント、不足で-20ポイント
            float mechanicModifier = Mathf.Lerp(-20f, 10f, Mathf.Clamp01(mechanicCoverageRatio));

            _ratings[CertificateCategory.Safety].Score = Mathf.Clamp(baseScore + mechanicModifier, 0f, 100f);
        }

        /// <summary>
        /// 快適性を更新する。
        /// 清掃状態、トイレ・ベンチ・飲食施設の充足度から算出。
        /// </summary>
        /// <param name="cleanliness">清掃状態（0～1）</param>
        /// <param name="toiletCoverage">トイレ充足率（0～1）</param>
        /// <param name="benchCoverage">ベンチ充足率（0～1）</param>
        /// <param name="foodDrinkAvailability">飲食施設充足率（0～1）</param>
        public void UpdateComfortRating(float cleanliness, float toiletCoverage,
            float benchCoverage, float foodDrinkAvailability)
        {
            // 各要素の加重平均（清掃が最重要）
            // 清掃35%, トイレ25%, 飲食25%, ベンチ15%
            float score = cleanliness * 35f
                        + toiletCoverage * 25f
                        + foodDrinkAvailability * 25f
                        + benchCoverage * 15f;

            _ratings[CertificateCategory.Comfort].Score = Mathf.Clamp(score, 0f, 100f);
        }

        /// <summary>
        /// 興奮度を更新する。
        /// アトラクションの多様性とクオリティから算出。
        /// </summary>
        /// <param name="attractionCount">アトラクション総数</param>
        /// <param name="uniqueCategoryCount">異なるアトラクションカテゴリの数</param>
        /// <param name="averageAttractionQuality">アトラクション平均品質（0～1）</param>
        public void UpdateExcitementRating(int attractionCount, int uniqueCategoryCount,
            float averageAttractionQuality)
        {
            // アトラクション数スコア（10個で最大40ポイント）
            float countScore = Mathf.Clamp(attractionCount / 10f * 40f, 0f, 40f);

            // 多様性スコア（6カテゴリ全制覇で最大30ポイント）
            float varietyScore = Mathf.Clamp(uniqueCategoryCount / 6f * 30f, 0f, 30f);

            // 品質スコア（最大30ポイント）
            float qualityScore = averageAttractionQuality * 30f;

            _ratings[CertificateCategory.Excitement].Score = Mathf.Clamp(
                countScore + varietyScore + qualityScore, 0f, 100f);
        }

        /// <summary>
        /// ムードを更新する。
        /// 来場者の平均幸福度とエンターテイナー配置率から算出。
        /// </summary>
        /// <param name="averageHappiness">来場者平均幸福度（0～1）</param>
        /// <param name="entertainerCoverage">エンターテイナー充足率（0～1）</param>
        public void UpdateMoodRating(float averageHappiness, float entertainerCoverage)
        {
            // 幸福度ベース（最大80ポイント）
            float happinessScore = averageHappiness * 80f;

            // エンターテイナーボーナス（最大20ポイント）
            float entertainerBonus = entertainerCoverage * 20f;

            _ratings[CertificateCategory.Mood].Score = Mathf.Clamp(
                happinessScore + entertainerBonus, 0f, 100f);
        }

        /// <summary>
        /// 総合評価を再計算する。各カテゴリの加重平均を算出。
        /// </summary>
        public void RecalculateOverallRating()
        {
            float total = 0f;
            foreach (var kvp in _ratings)
            {
                total += kvp.Value.Score * kvp.Value.Weight;
            }
            OverallRating = Mathf.Clamp(total, 0f, 100f);
        }

        // ================================================================
        // 認定証システム
        // ================================================================

        /// <summary>
        /// 事故の発生を記録する。安全認定証の判定に使用。
        /// </summary>
        public void RecordAccident(int year)
        {
            if (_accidentHistory.ContainsKey(year))
            {
                _accidentHistory[year]++;
            }
            else
            {
                _accidentHistory[year] = 1;
            }
        }

        /// <summary>
        /// 年度更新を通知する。認定証の条件チェックに使用。
        /// </summary>
        public void OnYearAdvanced(int newYear)
        {
            _currentYear = newYear;

            // 新しい年の事故記録を初期化（まだ事故が記録されていない場合）
            if (!_accidentHistory.ContainsKey(newYear))
            {
                _accidentHistory[newYear] = 0;
            }
        }

        /// <summary>
        /// 全カテゴリの認定証条件をチェックし、条件を満たせば授与する。
        /// 毎月末にParkManagerから呼び出される。
        /// </summary>
        public List<CertificateCategory> CheckAndAwardCertificates()
        {
            var newlyAwarded = new List<CertificateCategory>();

            foreach (var kvp in _ratings)
            {
                CertificateCategory category = kvp.Key;
                CategoryRating rating = kvp.Value;

                if (rating.CertificateAwarded) continue;

                bool scoreMet = rating.Score >= rating.CertificateThreshold;
                bool specialConditionMet = CheckSpecialCondition(category);

                if (scoreMet && specialConditionMet)
                {
                    rating.CertificateAwarded = true;
                    AwardedCertificates.Add(category);
                    newlyAwarded.Add(category);
                    GameEvents.FireCertificateAwarded(category);

                    // 認定証取得を通知
                    string certName = category switch
                    {
                        CertificateCategory.Fame => "知名度",
                        CertificateCategory.Safety => "安全性",
                        CertificateCategory.Comfort => "快適性",
                        CertificateCategory.Excitement => "興奮度",
                        CertificateCategory.Mood => "ムード",
                        _ => category.ToString()
                    };
                    if (NotificationSystem.Instance != null)
                        NotificationSystem.Instance.Notify(
                            $"認定証を獲得！「{certName}」認定おめでとうございます！", NotifLevel.Info);

                    WebGLOptimizer.LogVerbose($"[ParkRating] 認定証授与: {category} (スコア: {rating.Score:F1})");
                }
            }

            return newlyAwarded;
        }

        /// <summary>
        /// カテゴリ固有の特殊条件を確認する。
        /// </summary>
        private bool CheckSpecialCondition(CertificateCategory category)
        {
            switch (category)
            {
                case CertificateCategory.Safety:
                    return CheckSafetySpecialCondition();

                // 他のカテゴリはスコア条件のみ
                case CertificateCategory.Fame:
                case CertificateCategory.Comfort:
                case CertificateCategory.Excitement:
                case CertificateCategory.Mood:
                    _certificateConditions[category].IsMet = true;
                    return true;

                default:
                    return true;
            }
        }

        /// <summary>
        /// 安全認定証の特殊条件: 直近4年間の事故件数がゼロであること。
        /// </summary>
        private bool CheckSafetySpecialCondition()
        {
            // パーク開園から4年未満の場合は取得不可
            if (_currentYear < SAFETY_CERTIFICATE_ACCIDENT_FREE_YEARS)
            {
                _certificateConditions[CertificateCategory.Safety].IsMet = false;
                return false;
            }

            // 直近4年分の事故件数を確認
            for (int y = _currentYear; y > _currentYear - SAFETY_CERTIFICATE_ACCIDENT_FREE_YEARS; y--)
            {
                if (_accidentHistory.TryGetValue(y, out int accidents) && accidents > 0)
                {
                    _certificateConditions[CertificateCategory.Safety].IsMet = false;
                    return false;
                }
            }

            _certificateConditions[CertificateCategory.Safety].IsMet = true;
            return true;
        }

        // ================================================================
        // データ参照
        // ================================================================

        /// <summary>指定カテゴリの評価スコアを取得する</summary>
        public float GetCategoryScore(CertificateCategory category)
        {
            if (_ratings.TryGetValue(category, out CategoryRating rating))
            {
                return rating.Score;
            }
            return 0f;
        }

        /// <summary>セーブデータからスコアと認定証を復元する</summary>
        public void RestoreFromSave(
            float fame, float safety, float comfort, float excitement, float mood,
            List<string> awardedCerts)
        {
            if (_ratings.ContainsKey(CertificateCategory.Fame))
                _ratings[CertificateCategory.Fame].Score = fame;
            if (_ratings.ContainsKey(CertificateCategory.Safety))
                _ratings[CertificateCategory.Safety].Score = safety;
            if (_ratings.ContainsKey(CertificateCategory.Comfort))
                _ratings[CertificateCategory.Comfort].Score = comfort;
            if (_ratings.ContainsKey(CertificateCategory.Excitement))
                _ratings[CertificateCategory.Excitement].Score = excitement;
            if (_ratings.ContainsKey(CertificateCategory.Mood))
                _ratings[CertificateCategory.Mood].Score = mood;

            RecalculateOverallRating();

            if (awardedCerts != null)
            {
                foreach (var certStr in awardedCerts)
                {
                    if (Enum.TryParse<CertificateCategory>(certStr, out var cat))
                    {
                        if (!AwardedCertificates.Contains(cat))
                            AwardedCertificates.Add(cat);
                        if (_ratings.ContainsKey(cat))
                            _ratings[cat].CertificateAwarded = true;
                    }
                }
            }
        }

        /// <summary>指定カテゴリの評価データを取得する</summary>
        public CategoryRating GetCategoryRating(CertificateCategory category)
        {
            _ratings.TryGetValue(category, out CategoryRating rating);
            return rating;
        }

        /// <summary>全カテゴリの評価データを取得する</summary>
        public IReadOnlyDictionary<CertificateCategory, CategoryRating> GetAllRatings()
        {
            return _ratings;
        }

        /// <summary>指定カテゴリの認定証が授与済みか確認する</summary>
        public bool IsCertificateAwarded(CertificateCategory category)
        {
            if (_ratings.TryGetValue(category, out CategoryRating rating))
            {
                return rating.CertificateAwarded;
            }
            return false;
        }

        /// <summary>認定証の取得進捗文字列を返す</summary>
        public string GetCertificateProgressString(CertificateCategory category)
        {
            if (!_ratings.TryGetValue(category, out CategoryRating rating))
            {
                return "---";
            }

            if (rating.CertificateAwarded)
            {
                return "取得済み";
            }

            string scoreProgress = $"スコア: {rating.Score:F0}/{rating.CertificateThreshold:F0}";

            if (_certificateConditions.TryGetValue(category, out CertificateCondition condition))
            {
                string conditionStatus = condition.IsMet ? "達成" : "未達成";
                return $"{scoreProgress} | 条件: {condition.ConditionDescription} [{conditionStatus}]";
            }

            return scoreProgress;
        }

        /// <summary>
        /// 指定年の事故件数を取得する。
        /// </summary>
        public int GetAccidentCount(int year)
        {
            return _accidentHistory.TryGetValue(year, out int count) ? count : 0;
        }

        /// <summary>
        /// 連続無事故年数を取得する。
        /// </summary>
        public int GetConsecutiveAccidentFreeYears()
        {
            int consecutiveYears = 0;
            for (int y = _currentYear; y >= 1; y--)
            {
                if (_accidentHistory.TryGetValue(y, out int accidents) && accidents > 0)
                {
                    break;
                }
                consecutiveYears++;
            }
            return consecutiveYears;
        }
    }
}
