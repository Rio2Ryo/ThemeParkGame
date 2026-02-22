// ============================================================
// ThemeParkGame - Shop (Runtime Instance)
// ショップ施設のランタイムインスタンス
// フード・ドリンク・お土産の販売を管理する
// ============================================================

using System;
using System.Collections.Generic;
using UnityEngine;
using ThemeParkGame.Core;
using ThemeParkGame.Economy;

namespace ThemeParkGame.Attraction
{
    /// <summary>
    /// ショップ施設のランタイムインスタンス。
    ///
    /// 価格設定の仕組み:
    /// - 各商品には仕入れ値（WholesalePrice）が設定される
    /// - 適正販売価格は仕入れ値の1.6倍未満（仕入れ値の1.6倍以上は「高すぎる」と判定される）
    /// - 仕入れ値以下で売ると赤字になる
    /// - 適正価格内であれば来場者の購買満足度が高く、リピート率が上がる
    ///
    /// 在庫の仕組み:
    /// - 各ショップには最大在庫数があり、一定時間ごとに自動補充される
    /// - 在庫切れ時は来場者にサービス提供不可
    /// - 仕入れコストは在庫補充時に発生する
    /// </summary>
    public class Shop : FacilityBase
    {
        // ---- ショップタイプ ----

        [Header("Shop Configuration")]
        [SerializeField] private ShopType shopType = ShopType.FoodShop;
        public ShopType ShopType => shopType;

        public override FacilityType FacilityType
        {
            get
            {
                switch (shopType)
                {
                    case ShopType.FoodShop: return FacilityType.FoodShop;
                    case ShopType.DrinkShop: return FacilityType.DrinkShop;
                    case ShopType.SouvenirShop: return FacilityType.SouvenirShop;
                    default: return FacilityType.FoodShop;
                }
            }
        }

        public override string DisplayName => shopName;

        [SerializeField] private string shopName = "ショップ";

        // ---- 商品・価格設定 ----

        /// <summary>
        /// 仕入れ値。商品1個あたりのコスト。
        /// 在庫補充時にこの金額が経費として計上される。
        /// </summary>
        [Header("Pricing")]
        [Tooltip("商品1個あたりの仕入れ値")]
        [SerializeField] private int wholesalePrice = 100;
        public int WholesalePrice => wholesalePrice;

        /// <summary>
        /// プレイヤーが設定する販売価格。
        /// 適正価格は仕入れ値の1.6倍未満。
        /// </summary>
        [Tooltip("販売価格（プレイヤー設定）")]
        [SerializeField] private int sellingPrice = 150;
        public int SellingPrice
        {
            get => sellingPrice;
            set
            {
                sellingPrice = Mathf.Max(0, value);
                GameEvents.FirePriceChanged(FacilityId, sellingPrice);
            }
        }

        /// <summary>
        /// 適正価格の上限倍率。
        /// 仕入れ値にこの倍率を掛けた値が適正価格の上限。
        /// 原作準拠: 仕入れ値の1.6倍未満が適正。
        /// </summary>
        private const float FairPriceMultiplier = 1.6f;

        /// <summary>適正価格の上限値</summary>
        public int FairPriceLimit => Mathf.FloorToInt(wholesalePrice * FairPriceMultiplier);

        /// <summary>
        /// 現在の価格が適正範囲内かどうか。
        /// 適正: 仕入れ値 < 販売価格 < 仕入れ値 * 1.6
        /// </summary>
        public bool IsPriceFair => sellingPrice > wholesalePrice && sellingPrice < FairPriceLimit;

        /// <summary>現在の価格が赤字かどうか</summary>
        public bool IsPriceDeficit => sellingPrice <= wholesalePrice;

        // ---- 在庫管理 ----

        [Header("Stock")]
        [Tooltip("最大在庫数")]
        [SerializeField] private int maxStock = 50;
        public int MaxStock => maxStock;

        /// <summary>現在の在庫数</summary>
        [SerializeField] private int currentStock;
        public int CurrentStock => currentStock;

        /// <summary>在庫が空かどうか</summary>
        public bool IsOutOfStock => currentStock <= 0;

        /// <summary>在庫自動補充の間隔（秒）</summary>
        [Tooltip("在庫自動補充の間隔（秒）")]
        [SerializeField] private float restockInterval = 120f;

        /// <summary>1回の補充量</summary>
        [Tooltip("1回の在庫補充量")]
        [SerializeField] private int restockAmount = 10;

        /// <summary>最後の在庫補充からの経過時間</summary>
        private float _restockTimer;

        // ---- 接客 ----

        [Header("Service")]
        [Tooltip("1人の客にサービスするのにかかる時間（秒）")]
        [SerializeField] private float servingTime = 3f;
        public float ServingTime => servingTime;

        /// <summary>現在サービス中の来場者ID（-1ならサービス中でない）</summary>
        private int _currentServingVisitorId = -1;

        /// <summary>サービス中の経過時間</summary>
        private float _servingTimer;

        /// <summary>待ち行列</summary>
        private readonly Queue<int> _customerQueue = new Queue<int>();
        public int QueueLength => _customerQueue.Count;

        [Tooltip("待ち行列の最大長")]
        [SerializeField] private int maxQueueLength = 10;
        public int MaxQueueLength => maxQueueLength;

        // ---- 収益トラッキング ----

        [Header("Revenue (Read Only)")]
        /// <summary>本日の売上</summary>
        public float TodayRevenue { get; private set; }

        /// <summary>累計売上</summary>
        public float TotalRevenue { get; private set; }

        /// <summary>本日の仕入れコスト</summary>
        public float TodayExpense { get; private set; }

        /// <summary>累計仕入れコスト</summary>
        public float TotalExpense { get; private set; }

        /// <summary>本日の純利益</summary>
        public float TodayProfit => TodayRevenue - TodayExpense;

        /// <summary>本日の販売数</summary>
        public int TodaySalesCount { get; private set; }

        /// <summary>累計販売数</summary>
        public int TotalSalesCount { get; private set; }

        // ---- 品質評価 ----

        /// <summary>
        /// 品質レーティング（0.0～1.0）。
        /// 価格設定の適正さ、在庫切れ頻度、待ち時間で決まる。
        /// </summary>
        [Header("Quality")]
        [SerializeField] private float qualityRating = 0.5f;
        public float QualityRating => qualityRating;

        /// <summary>品質計算用の直近評価バッファ</summary>
        private readonly Queue<float> _recentQualityScores = new Queue<float>();
        private const int QualityWindowSize = 30;

        // ---- 初期化 ----

        protected override void Start()
        {
            base.Start();
            currentStock = maxStock;
            _restockTimer = 0f;
        }

        // ---- メインループ ----

        protected override void Update()
        {
            base.Update();
            if (!IsActive) return;

            UpdateServing();
            UpdateRestock();
        }

        // ---- 接客処理 ----

        /// <summary>
        /// 接客処理の更新。
        /// 1人ずつ順番にサービスし、完了したら次の客へ進む。
        /// </summary>
        private void UpdateServing()
        {
            // 現在サービス中の客がいる場合
            if (_currentServingVisitorId >= 0)
            {
                _servingTimer += Time.deltaTime;

                if (_servingTimer >= servingTime)
                {
                    CompleteService(_currentServingVisitorId);
                    _currentServingVisitorId = -1;
                    _servingTimer = 0f;
                }
            }

            // サービス中でなければ、キューから次の客を取り出す
            if (_currentServingVisitorId < 0 && _customerQueue.Count > 0)
            {
                _currentServingVisitorId = _customerQueue.Dequeue();
                _servingTimer = 0f;
            }
        }

        /// <summary>
        /// 1人のサービスが完了した時の処理。
        /// 売上計上・在庫消費・満足度記録を行う。
        /// </summary>
        /// <param name="visitorId">サービスを受けた来場者のID</param>
        private void CompleteService(int visitorId)
        {
            if (IsOutOfStock)
            {
                // 在庫切れ: サービス不可、不満を記録
                RecordQuality(0.1f);
                GameEvents.FireVisitorEmotionChanged(visitorId, EmotionBubbleType.FoodTastesBad);
                OnVisitorLeave(visitorId);
                return;
            }

            // 在庫を消費
            currentStock--;

            // 売上計上（EconomyManager経由で正式に計上する）
            TodayRevenue += sellingPrice;
            TotalRevenue += sellingPrice;
            TodaySalesCount++;
            TotalSalesCount++;

            if (GameManager.Instance != null && GameManager.Instance.EconomyManager != null)
            {
                GameManager.Instance.EconomyManager.AddRevenue(
                    sellingPrice, RevenueCategory.ShopSale, FacilityId);
            }
            else
            {
                GameEvents.FireRevenueEarned(sellingPrice);
            }

            // 満足度評価
            float satisfaction = CalculateCustomerSatisfaction(visitorId);
            RecordQuality(satisfaction);

            // 価格が高すぎる場合の感情バブル
            if (!IsPriceFair && !IsPriceDeficit)
            {
                GameEvents.FireVisitorEmotionChanged(visitorId, EmotionBubbleType.TooExpensive);
            }

            OnVisitorLeave(visitorId);
        }

        /// <summary>
        /// 顧客満足度を計算する。
        /// 価格設定の適正さが最も大きく影響する。
        /// </summary>
        private float CalculateCustomerSatisfaction(int visitorId)
        {
            float satisfaction = 0.5f;

            // 価格適正性による評価
            if (IsPriceFair)
            {
                // 適正価格: 仕入れ値に近いほど高評価
                float priceRatio = (float)sellingPrice / wholesalePrice;
                // 1.0～1.6の範囲で、1.0に近いほど満足度が高い
                satisfaction = Mathf.Lerp(0.9f, 0.6f, (priceRatio - 1f) / (FairPriceMultiplier - 1f));
            }
            else if (IsPriceDeficit)
            {
                // 赤字価格: 顧客は大満足だが経営的には問題
                satisfaction = 1.0f;
            }
            else
            {
                // 適正価格を超えている: 不満
                float overPriceRatio = (float)sellingPrice / FairPriceLimit;
                satisfaction = Mathf.Lerp(0.4f, 0.0f, Mathf.Clamp01(overPriceRatio - 1f));
            }

            // 待ち行列の長さによる補正
            float queuePenalty = (float)_customerQueue.Count / maxQueueLength;
            satisfaction *= (1f - queuePenalty * 0.3f);

            return Mathf.Clamp01(satisfaction);
        }

        /// <summary>品質評価を記録し移動平均を更新する</summary>
        private void RecordQuality(float score)
        {
            _recentQualityScores.Enqueue(score);
            while (_recentQualityScores.Count > QualityWindowSize)
            {
                _recentQualityScores.Dequeue();
            }

            float sum = 0f;
            foreach (float s in _recentQualityScores)
            {
                sum += s;
            }
            qualityRating = sum / _recentQualityScores.Count;
        }

        // ---- 在庫補充 ----

        /// <summary>
        /// 在庫の自動補充処理。
        /// 一定間隔で在庫を補充し、仕入れコストを計上する。
        /// </summary>
        private void UpdateRestock()
        {
            if (currentStock >= maxStock) return;

            _restockTimer += Time.deltaTime;
            if (_restockTimer >= restockInterval)
            {
                _restockTimer = 0f;
                PerformRestock();
            }
        }

        /// <summary>在庫を補充する</summary>
        private void PerformRestock()
        {
            int amountToRestock = Mathf.Min(restockAmount, maxStock - currentStock);
            if (amountToRestock <= 0) return;

            int restockCost = amountToRestock * wholesalePrice;
            TodayExpense += restockCost;
            TotalExpense += restockCost;
            PayRestockExpense(restockCost);

            currentStock += amountToRestock;
            Debug.Log($"[Shop] 在庫補充: {shopName} +{amountToRestock}個 (在庫: {currentStock}/{maxStock}, コスト: {restockCost})");
        }

        /// <summary>在庫を手動で全補充する</summary>
        public void ForceFullRestock()
        {
            int amountToRestock = maxStock - currentStock;
            if (amountToRestock <= 0) return;

            int restockCost = amountToRestock * wholesalePrice;
            TodayExpense += restockCost;
            TotalExpense += restockCost;
            PayRestockExpense(restockCost);

            currentStock = maxStock;
        }

        /// <summary>仕入れコストをEconomyManager経由で支払う</summary>
        private void PayRestockExpense(float cost)
        {
            if (GameManager.Instance != null && GameManager.Instance.EconomyManager != null)
            {
                GameManager.Instance.EconomyManager.PayExpense(
                    cost, ExpenseCategory.Other, FacilityId);
            }
            else
            {
                GameEvents.FireExpensePaid(cost);
            }
        }

        // ---- 来場者インタラクション（FacilityBase実装） ----

        public override bool CanAcceptVisitor(int visitorId)
        {
            if (!IsActive) return false;
            if (_customerQueue.Count >= maxQueueLength) return false;
            return true;
        }

        public override bool OnVisitorArrive(int visitorId)
        {
            if (!CanAcceptVisitor(visitorId)) return false;

            _customerQueue.Enqueue(visitorId);
            return true;
        }

        public override void OnVisitorLeave(int visitorId)
        {
            // 来場者の退出を通知（VisitorManagerが処理する）
        }

        /// <summary>
        /// 来場者にとってのこのショップの魅力度を計算する。
        /// ショップタイプ・価格・在庫・待ち行列が影響する。
        /// </summary>
        public override float CalculateAppeal(int visitorId)
        {
            if (!IsActive) return 0f;

            float appeal = 0.5f;

            // 価格適正性
            if (IsPriceFair)
                appeal += 0.2f;
            else if (!IsPriceDeficit)
                appeal -= 0.3f;

            // 在庫状況
            if (IsOutOfStock)
                appeal = 0f;
            else if (currentStock < maxStock * 0.2f)
                appeal *= 0.5f;

            // 品質レーティング
            appeal *= (0.5f + qualityRating * 0.5f);

            // 待ち行列
            float queueFill = (float)_customerQueue.Count / maxQueueLength;
            appeal *= (1f - queueFill * 0.4f);

            return Mathf.Clamp01(appeal);
        }

        // ---- 日次リセット ----

        /// <summary>日次データをリセットする</summary>
        public void ResetDailyStats()
        {
            TodayRevenue = 0f;
            TodayExpense = 0f;
            TodaySalesCount = 0;
        }

        // ---- 配置コールバック ----

        protected override void OnPlaced()
        {
            Debug.Log($"[Shop] ショップ建設完了: {shopName} (ID: {FacilityId}, Type: {shopType})");
        }

        protected override void OnDemolished()
        {
            _customerQueue.Clear();
            _currentServingVisitorId = -1;
        }
    }

    /// <summary>
    /// ショップ種別。
    /// GameEnums.csのFacilityType(FoodShop/DrinkShop/SouvenirShop)と対応する。
    /// </summary>
    public enum ShopType
    {
        /// <summary>フードショップ - 食事を提供。空腹の来場者が訪れる</summary>
        FoodShop,
        /// <summary>ドリンクショップ - 飲料を提供。喉が渇いた来場者が訪れる</summary>
        DrinkShop,
        /// <summary>お土産ショップ - 記念品を販売。満足度が高い来場者が購入しやすい</summary>
        SouvenirShop
    }
}
