// ============================================================
// ThemeParkGame - PricingSystem
// 価格管理と最適化システム
// ============================================================

using System;
using System.Collections.Generic;
using UnityEngine;
using ThemeParkGame.Core;

namespace ThemeParkGame.Economy
{
    /// <summary>
    /// 施設ごとの価格情報を保持する。
    /// </summary>
    [Serializable]
    public class FacilityPriceEntry
    {
        public int FacilityId;
        public FacilityType FacilityType;

        /// <summary>現在設定中の価格</summary>
        public float CurrentPrice;

        /// <summary>
        /// 仕入れ原価（ショップ）またはメンテナンス費用（アトラクション）。
        /// 価格アドバイザーの基準値となる。
        /// </summary>
        public float BaseCost;

        /// <summary>施設名（表示用）</summary>
        public string FacilityName;
    }

    /// <summary>
    /// 価格管理と最適化を担当するシステム。
    /// 入場料・アトラクション料金・ショップ価格の一元管理を行う。
    ///
    /// 【ゲームデザイン: 価格設定の哲学】
    /// Theme Park Worldの価格バランスを再現:
    /// - アトラクション料金: メンテナンスコストの4%未満が「最高評価」の目安
    ///   → 高すぎると来場者が「TooExpensive」バブルを出す
    /// - ショップ価格: 仕入れ原価の1.6倍未満が最適
    ///   → 高すぎると売上数が激減、安すぎると利益率が低下
    /// - 入場料: パーク評価と連動。評価が高いほど高額でも受け入れられる
    /// - 価格弾力性: 値上げは客単価上昇だが来場者減少を招く（トレードオフ）
    /// </summary>
    public class PricingSystem
    {
        // ---- 価格バランス定数 ----

        /// <summary>
        /// アトラクション最高評価の料金上限倍率。
        /// メンテナンスコストに対してこの割合未満なら最高評価。
        /// </summary>
        private const float ATTRACTION_BEST_PRICE_RATIO = 0.04f;

        /// <summary>
        /// アトラクション許容範囲の料金上限倍率。
        /// これを超えると来場者の不満が急増する。
        /// </summary>
        private const float ATTRACTION_MAX_ACCEPTABLE_RATIO = 0.08f;

        /// <summary>
        /// ショップ最適価格倍率。仕入れ原価に対するこの倍率未満が最適。
        /// </summary>
        private const float SHOP_OPTIMAL_PRICE_MULTIPLIER = 1.6f;

        /// <summary>
        /// ショップ最大許容倍率。これを超えると売上が激減する。
        /// </summary>
        private const float SHOP_MAX_ACCEPTABLE_MULTIPLIER = 2.5f;

        /// <summary>入場料の基本価格（パーク評価50の場合）</summary>
        private const float BASE_ENTRANCE_FEE = 10f;

        /// <summary>入場料の最大倍率（パーク評価100の場合）</summary>
        private const float MAX_ENTRANCE_FEE_MULTIPLIER = 5f;

        // ---- 価格弾力性定数 ----

        /// <summary>
        /// 入場料の価格弾力性係数。
        /// 1.0で、価格が推奨値の2倍なら来場者が半減する線形モデル。
        /// </summary>
        private const float ENTRANCE_FEE_ELASTICITY = 1.0f;

        /// <summary>
        /// ショップの価格弾力性係数。
        /// ショップは代替手段が少ないため弾力性がやや低い。
        /// </summary>
        private const float SHOP_PRICE_ELASTICITY = 0.7f;

        /// <summary>
        /// アトラクションの価格弾力性係数。
        /// 人気アトラクションは弾力性が低い（値上げしても乗りたい）。
        /// </summary>
        private const float ATTRACTION_PRICE_ELASTICITY = 0.8f;

        // ---- データ ----

        /// <summary>現在の入場料</summary>
        public float EntranceFee { get; private set; }

        /// <summary>施設別価格テーブル</summary>
        private readonly Dictionary<int, FacilityPriceEntry> _facilityPrices;

        public PricingSystem()
        {
            _facilityPrices = new Dictionary<int, FacilityPriceEntry>();
            EntranceFee = BASE_ENTRANCE_FEE;
        }

        // ================================================================
        // 入場料管理
        // ================================================================

        /// <summary>入場料を設定する</summary>
        public void SetEntranceFee(float fee)
        {
            EntranceFee = Mathf.Max(0f, fee);
            WebGLOptimizer.LogVerbose($"[PricingSystem] 入場料を {EntranceFee:F0} に設定");
        }

        /// <summary>
        /// パーク評価に基づく推奨入場料を算出する。
        /// 評価が高いほど高い入場料が許容される。
        /// </summary>
        public float GetRecommendedEntranceFee(float parkRating)
        {
            // パーク評価0～100に対して、線形に入場料を算出
            float ratingFactor = Mathf.Clamp01(parkRating / 100f);
            float multiplier = Mathf.Lerp(0.5f, MAX_ENTRANCE_FEE_MULTIPLIER, ratingFactor);
            return BASE_ENTRANCE_FEE * multiplier;
        }

        // ================================================================
        // 施設価格管理
        // ================================================================

        /// <summary>施設の価格情報を登録する</summary>
        public void RegisterFacility(int facilityId, FacilityType type, string name, float baseCost, float initialPrice)
        {
            var entry = new FacilityPriceEntry
            {
                FacilityId = facilityId,
                FacilityType = type,
                FacilityName = name,
                BaseCost = baseCost,
                CurrentPrice = initialPrice
            };
            _facilityPrices[facilityId] = entry;
        }

        /// <summary>施設の価格を解除する（施設撤去時）</summary>
        public void UnregisterFacility(int facilityId)
        {
            _facilityPrices.Remove(facilityId);
        }

        /// <summary>施設の価格を設定する</summary>
        public void SetFacilityPrice(int facilityId, float price)
        {
            if (_facilityPrices.TryGetValue(facilityId, out FacilityPriceEntry entry))
            {
                entry.CurrentPrice = Mathf.Max(0f, price);
                GameEvents.FirePriceChanged(facilityId, entry.CurrentPrice);
            }
        }

        /// <summary>施設の現在価格を取得する</summary>
        public float GetFacilityPrice(int facilityId)
        {
            if (_facilityPrices.TryGetValue(facilityId, out FacilityPriceEntry entry))
            {
                return entry.CurrentPrice;
            }
            return 0f;
        }

        /// <summary>施設の価格情報を取得する</summary>
        public FacilityPriceEntry GetFacilityPriceEntry(int facilityId)
        {
            _facilityPrices.TryGetValue(facilityId, out FacilityPriceEntry entry);
            return entry;
        }

        // ================================================================
        // 価格満足度計算
        // ================================================================

        /// <summary>
        /// 入場料に対する来場者の満足度を計算する（0～1）。
        /// 推奨価格以下なら1.0、超過分に応じて低下する。
        /// </summary>
        public float CalculateEntranceFeeSatisfaction(float parkRating, float visitorWealth)
        {
            float recommended = GetRecommendedEntranceFee(parkRating);
            // 来場者の富裕度で許容範囲を調整（富裕層は高くても気にしない）
            float adjustedRecommended = recommended * (0.8f + visitorWealth * 0.4f);

            if (EntranceFee <= adjustedRecommended)
            {
                return 1.0f;
            }

            // 推奨価格を超えた分だけ満足度が低下
            float overchargeRatio = (EntranceFee - adjustedRecommended) / adjustedRecommended;
            return Mathf.Clamp01(1.0f - overchargeRatio * ENTRANCE_FEE_ELASTICITY);
        }

        /// <summary>
        /// アトラクション料金に対する来場者の満足度を計算する（0～1）。
        /// メンテナンスコストの4%未満なら最高評価。
        /// </summary>
        public float CalculateAttractionPriceSatisfaction(int facilityId, float visitorWealth)
        {
            if (!_facilityPrices.TryGetValue(facilityId, out FacilityPriceEntry entry))
            {
                return 1.0f;
            }

            float bestPrice = entry.BaseCost * ATTRACTION_BEST_PRICE_RATIO;
            float maxPrice = entry.BaseCost * ATTRACTION_MAX_ACCEPTABLE_RATIO;

            // 富裕度で許容ラインを調整
            float wealthAdjustment = 0.8f + visitorWealth * 0.4f;
            bestPrice *= wealthAdjustment;
            maxPrice *= wealthAdjustment;

            if (entry.CurrentPrice <= bestPrice)
            {
                return 1.0f;
            }

            if (entry.CurrentPrice >= maxPrice)
            {
                return Mathf.Max(0f, 0.3f - (entry.CurrentPrice - maxPrice) / maxPrice * 0.3f);
            }

            // bestPriceとmaxPriceの間で線形に低下
            float t = (entry.CurrentPrice - bestPrice) / (maxPrice - bestPrice);
            return Mathf.Lerp(1.0f, 0.3f, t);
        }

        /// <summary>
        /// ショップ価格に対する来場者の満足度を計算する（0～1）。
        /// 仕入れ原価の1.6倍未満が最適。
        /// </summary>
        public float CalculateShopPriceSatisfaction(int facilityId, float visitorWealth)
        {
            if (!_facilityPrices.TryGetValue(facilityId, out FacilityPriceEntry entry))
            {
                return 1.0f;
            }

            float optimalPrice = entry.BaseCost * SHOP_OPTIMAL_PRICE_MULTIPLIER;
            float maxPrice = entry.BaseCost * SHOP_MAX_ACCEPTABLE_MULTIPLIER;

            float wealthAdjustment = 0.8f + visitorWealth * 0.4f;
            optimalPrice *= wealthAdjustment;
            maxPrice *= wealthAdjustment;

            if (entry.CurrentPrice <= optimalPrice)
            {
                return 1.0f;
            }

            if (entry.CurrentPrice >= maxPrice)
            {
                return Mathf.Max(0f, 0.2f - (entry.CurrentPrice - maxPrice) / maxPrice * 0.2f);
            }

            float t = (entry.CurrentPrice - optimalPrice) / (maxPrice - optimalPrice);
            return Mathf.Lerp(1.0f, 0.2f, t);
        }

        /// <summary>
        /// パーク全体の価格満足度を計算する（0～1の加重平均）。
        /// </summary>
        public float CalculateOverallPriceSatisfaction(float parkRating, float averageVisitorWealth)
        {
            float entranceSat = CalculateEntranceFeeSatisfaction(parkRating, averageVisitorWealth);

            float attractionSatSum = 0f;
            int attractionCount = 0;
            float shopSatSum = 0f;
            int shopCount = 0;

            foreach (var kvp in _facilityPrices)
            {
                FacilityPriceEntry entry = kvp.Value;
                if (entry.FacilityType == FacilityType.Attraction)
                {
                    attractionSatSum += CalculateAttractionPriceSatisfaction(kvp.Key, averageVisitorWealth);
                    attractionCount++;
                }
                else if (entry.FacilityType == FacilityType.FoodShop ||
                         entry.FacilityType == FacilityType.DrinkShop ||
                         entry.FacilityType == FacilityType.SouvenirShop)
                {
                    shopSatSum += CalculateShopPriceSatisfaction(kvp.Key, averageVisitorWealth);
                    shopCount++;
                }
            }

            float avgAttractionSat = attractionCount > 0 ? attractionSatSum / attractionCount : 1.0f;
            float avgShopSat = shopCount > 0 ? shopSatSum / shopCount : 1.0f;

            // 加重平均: 入場料30%, アトラクション40%, ショップ30%
            return entranceSat * 0.3f + avgAttractionSat * 0.4f + avgShopSat * 0.3f;
        }

        // ================================================================
        // 自動価格提案
        // ================================================================

        /// <summary>
        /// アトラクションの推奨価格を算出する。
        /// メンテナンスコストの3%（最高評価ラインの少し下）を推奨。
        /// </summary>
        public float GetRecommendedAttractionPrice(int facilityId, float parkRating)
        {
            if (!_facilityPrices.TryGetValue(facilityId, out FacilityPriceEntry entry))
            {
                return 0f;
            }

            // 基本推奨: メンテナンスコストの3%（最高評価4%の少し下）
            float baseRecommended = entry.BaseCost * (ATTRACTION_BEST_PRICE_RATIO * 0.75f);

            // パーク評価が高いほど少し上乗せ可能
            float ratingBonus = Mathf.Lerp(0.8f, 1.2f, Mathf.Clamp01(parkRating / 100f));

            return Mathf.Round(baseRecommended * ratingBonus);
        }

        /// <summary>
        /// ショップの推奨価格を算出する。
        /// 仕入れ原価の1.4倍（最適1.6倍の少し下）を推奨。
        /// </summary>
        public float GetRecommendedShopPrice(int facilityId, float parkRating)
        {
            if (!_facilityPrices.TryGetValue(facilityId, out FacilityPriceEntry entry))
            {
                return 0f;
            }

            // 基本推奨: 仕入れ原価の1.4倍（最適1.6倍の少し下）
            float baseRecommended = entry.BaseCost * (SHOP_OPTIMAL_PRICE_MULTIPLIER * 0.875f);

            // パーク評価が高いほど上乗せ可能
            float ratingBonus = Mathf.Lerp(0.9f, 1.15f, Mathf.Clamp01(parkRating / 100f));

            return Mathf.Round(baseRecommended * ratingBonus);
        }

        /// <summary>
        /// 全施設に推奨価格を一括適用する。
        /// </summary>
        public void ApplyRecommendedPricesAll(float parkRating)
        {
            SetEntranceFee(Mathf.Round(GetRecommendedEntranceFee(parkRating)));

            var facilityIds = new List<int>(_facilityPrices.Keys);
            foreach (int id in facilityIds)
            {
                FacilityPriceEntry entry = _facilityPrices[id];
                if (entry.FacilityType == FacilityType.Attraction)
                {
                    SetFacilityPrice(id, GetRecommendedAttractionPrice(id, parkRating));
                }
                else if (entry.FacilityType == FacilityType.FoodShop ||
                         entry.FacilityType == FacilityType.DrinkShop ||
                         entry.FacilityType == FacilityType.SouvenirShop)
                {
                    SetFacilityPrice(id, GetRecommendedShopPrice(id, parkRating));
                }
            }

            WebGLOptimizer.LogVerbose("[PricingSystem] 全施設に推奨価格を適用しました");
        }

        // ================================================================
        // 価格弾力性モデル
        // ================================================================

        /// <summary>
        /// 入場料の価格弾力性を計算する。
        /// 返り値は来場者数の補正係数（1.0 = 変化なし、0.5 = 半減）。
        ///
        /// 【ゲームデザイン: 弾力性の意図】
        /// 高入場料 → 客単価UP + 来場者数DOWN のトレードオフを生む。
        /// プレイヤーは「薄利多売」か「少数精鋭（高額路線）」を選択する。
        /// </summary>
        public float GetEntranceFeeElasticityMultiplier(float parkRating)
        {
            float recommended = GetRecommendedEntranceFee(parkRating);
            if (recommended <= 0f) return 1.0f;

            float priceRatio = EntranceFee / recommended;

            if (priceRatio <= 1.0f)
            {
                // 推奨以下: 安ければ安いほど来場者が増える（最大1.3倍）
                return Mathf.Lerp(1.3f, 1.0f, priceRatio);
            }
            else if (priceRatio <= 3.0f)
            {
                // 推奨超過〜3倍: 線形に低下（1.0 → 0.05）
                // priceRatio 1.0 → 1.0, priceRatio 3.0 → 0.05
                float t = (priceRatio - 1.0f) / 2.0f; // 0〜1
                float multiplier = Mathf.Lerp(1.0f, 0.05f, t);
                WebGLOptimizer.LogVerbose(
                    $"[PricingSystem] 入場料弾力性: priceRatio={priceRatio:F2}, multiplier={multiplier:F3}");
                return multiplier;
            }
            else
            {
                // 推奨の3倍超: 来場者ゼロ（入場料が高すぎて誰も来ない）
                WebGLOptimizer.LogVerbose(
                    $"[PricingSystem] 入場料が推奨の{priceRatio:F1}倍 — 来場者ゼロ");
                return 0f;
            }
        }

        /// <summary>
        /// アトラクションの価格弾力性を計算する。
        /// 返り値は利用者数の補正係数。
        /// </summary>
        public float GetAttractionElasticityMultiplier(int facilityId)
        {
            if (!_facilityPrices.TryGetValue(facilityId, out FacilityPriceEntry entry))
            {
                return 1.0f;
            }

            float bestPrice = entry.BaseCost * ATTRACTION_BEST_PRICE_RATIO;
            if (bestPrice <= 0f) return 1.0f;

            float priceRatio = entry.CurrentPrice / bestPrice;

            if (priceRatio <= 1.0f)
            {
                return Mathf.Lerp(1.2f, 1.0f, priceRatio);
            }
            else
            {
                float overcharge = priceRatio - 1.0f;
                return Mathf.Max(0.1f, 1.0f - overcharge * ATTRACTION_PRICE_ELASTICITY * 0.5f);
            }
        }

        /// <summary>
        /// ショップの価格弾力性を計算する。
        /// 返り値は購入率の補正係数。
        /// </summary>
        public float GetShopElasticityMultiplier(int facilityId)
        {
            if (!_facilityPrices.TryGetValue(facilityId, out FacilityPriceEntry entry))
            {
                return 1.0f;
            }

            float optimalPrice = entry.BaseCost * SHOP_OPTIMAL_PRICE_MULTIPLIER;
            if (optimalPrice <= 0f) return 1.0f;

            float priceRatio = entry.CurrentPrice / optimalPrice;

            if (priceRatio <= 1.0f)
            {
                return Mathf.Lerp(1.15f, 1.0f, priceRatio);
            }
            else
            {
                float overcharge = priceRatio - 1.0f;
                return Mathf.Max(0.1f, 1.0f - overcharge * SHOP_PRICE_ELASTICITY);
            }
        }

        /// <summary>
        /// 施設の価格評価を文字列で返す（UI表示用）。
        /// "最高" / "適正" / "やや高い" / "高すぎる"
        /// </summary>
        public string GetPriceRatingLabel(int facilityId)
        {
            if (!_facilityPrices.TryGetValue(facilityId, out FacilityPriceEntry entry))
            {
                return "---";
            }

            float satisfaction;
            if (entry.FacilityType == FacilityType.Attraction)
            {
                satisfaction = CalculateAttractionPriceSatisfaction(facilityId, 0.5f);
            }
            else if (entry.FacilityType == FacilityType.FoodShop ||
                     entry.FacilityType == FacilityType.DrinkShop ||
                     entry.FacilityType == FacilityType.SouvenirShop)
            {
                satisfaction = CalculateShopPriceSatisfaction(facilityId, 0.5f);
            }
            else
            {
                return "---";
            }

            if (satisfaction >= 0.9f) return "最高";
            if (satisfaction >= 0.6f) return "適正";
            if (satisfaction >= 0.3f) return "やや高い";
            return "高すぎる";
        }
    }
}
