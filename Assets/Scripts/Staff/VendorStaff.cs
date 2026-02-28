// ============================================================
// ThemeParkGame - VendorStaff
// 移動販売員 - 園内を巡回して飲食物を来場者に販売
// ============================================================

using UnityEngine;
using ThemeParkGame.Core;
using ThemeParkGame.Economy;
using ThemeParkGame.Visitor;

namespace ThemeParkGame.Staff
{
    /// <summary>
    /// 移動販売員は園内を巡回し、空腹/喉が渇いた来場者に直接販売するスタッフ。
    ///
    /// 【ゲームデザイン】
    /// ・ショップに行かなくても飲食を購入できる便利なサービス
    /// ・販売価格はショップより少し割高だが、来場者の待ち時間を削減
    /// ・スキルレベルが高いほど販売速度が速く、利益率も向上
    /// ・ショップが少ないエリアに配置すると特に効果的
    /// </summary>
    public class VendorStaff : StaffMember
    {
        private const float DetectionRadius = 20f;
        private const float BaseSellDuration = 2f;
        private const float BasePrice = 15f;
        private const float HungerThreshold = 55f;
        private const float ThirstThreshold = 55f;
        private const float SellCooldown = 3f;

        private VisitorAI _targetVisitor;
        private float _sellTimer;
        private float _sellDuration;
        private float _cooldownTimer;
        private bool _sellingFood; // true=food, false=drink

        public int TotalSales { get; private set; }
        public float TotalRevenue { get; private set; }

        /// <summary>販売売上の静的集計（全VendorStaff合算）</summary>
        private static int _globalTotalSales;
        private static float _globalTotalRevenue;

        /// <summary>全Vendorの累計販売件数</summary>
        public static int GlobalTotalSales => _globalTotalSales;

        /// <summary>全Vendorの累計売上</summary>
        public static float GlobalTotalRevenue => _globalTotalRevenue;

        protected override void Awake()
        {
            base.Awake();
            StaffType = StaffType.Vendor;
        }

        // ============================================================
        // イベント購読
        // ============================================================

        protected override void SubscribeToEvents()
        {
            GameEvents.OnVisitorEnterPark += HandleVisitorEntered;
            GameEvents.OnParkEventStarted += HandleParkEventStarted;
        }

        protected override void UnsubscribeFromEvents()
        {
            GameEvents.OnVisitorEnterPark -= HandleVisitorEntered;
            GameEvents.OnParkEventStarted -= HandleParkEventStarted;
        }

        /// <summary>
        /// 来場者入園イベントハンドラ。
        /// 新しい来場者が入園した時、Idle状態なら即座に販売対象を検索する。
        /// </summary>
        private void HandleVisitorEntered(int visitorId)
        {
            if (CurrentState == StaffBehaviorState.Idle)
            {
                FindAndAssignTask();
            }
        }

        /// <summary>
        /// パークイベント開始ハンドラ。
        /// イベント開催中は来場者の飲食需要が高まるため、積極的に販売する。
        /// </summary>
        private void HandleParkEventStarted(string eventId, string displayName)
        {
            if (CurrentState == StaffBehaviorState.Idle)
            {
                _cooldownTimer = 0f; // イベント開始時はクールダウンをリセット
                FindAndAssignTask();
            }
        }

        protected override bool FindAndAssignTask()
        {
            if (_cooldownTimer > 0f)
            {
                _cooldownTimer -= Time.deltaTime;
                return false;
            }

            if (GameManager.Instance?.VisitorManager == null) return false;

            var visitors = GameManager.Instance.VisitorManager.GetAllActiveVisitors();
            VisitorAI bestTarget = null;
            float bestDist = DetectionRadius;
            bool targetFood = false;

            for (int i = 0; i < visitors.Count; i++)
            {
                var v = visitors[i];
                if (v == null || !v.IsActive) continue;
                if (v.Parameters.Cash < BasePrice) continue;
                if (!IsWithinPatrolArea(v.transform.position)) continue;

                // 空腹/渇き状態の来場者を優先
                bool hungry = v.Parameters.Hunger >= HungerThreshold;
                bool thirsty = v.Parameters.Thirst >= ThirstThreshold;
                if (!hungry && !thirsty) continue;

                float dist = Vector3.Distance(transform.position, v.transform.position);
                if (dist < bestDist)
                {
                    bestDist = dist;
                    bestTarget = v;
                    targetFood = hungry && (!thirsty || v.Parameters.Hunger > v.Parameters.Thirst);
                }
            }

            if (bestTarget != null)
            {
                _targetVisitor = bestTarget;
                _sellingFood = targetFood;
                _sellDuration = BaseSellDuration / WorkEfficiencyMultiplier;
                _sellTimer = 0f;
                NavigateTo(bestTarget.transform.position);
                CurrentState = StaffBehaviorState.MovingToTask;
                return true;
            }

            return false;
        }

        protected override void OnTaskReached()
        {
            _sellTimer = 0f;
            StopNavigation();
        }

        protected override void PerformWork()
        {
            if (_targetVisitor == null || !_targetVisitor.IsActive)
            {
                _targetVisitor = null;
                CompleteCurrentTask();
                return;
            }

            _sellTimer += Time.deltaTime;

            if (_sellTimer >= _sellDuration)
            {
                CompleteSale();
            }
        }

        private void CompleteSale()
        {
            if (_targetVisitor != null && _targetVisitor.Parameters != null)
            {
                float price = BasePrice * (1f + (WorkEfficiencyMultiplier - 1f) * 0.5f);

                if (_targetVisitor.Parameters.Cash >= price)
                {
                    TotalSales++;
                    TotalRevenue += price;
                    _globalTotalSales++;
                    _globalTotalRevenue += price;

                    // 来場者から代金を受け取り
                    _targetVisitor.Parameters.SpendCash(price);

                    // 飲食効果を適用
                    if (_sellingFood)
                    {
                        _targetVisitor.Parameters.ModifyHunger(-40f);
                        _targetVisitor.Parameters.ModifyHappiness(5f);
                    }
                    else
                    {
                        _targetVisitor.Parameters.ModifyThirst(-40f);
                        _targetVisitor.Parameters.ModifyHappiness(5f);
                    }

                    // パークの売上に計上
                    if (GameManager.Instance?.EconomyManager != null)
                    {
                        GameManager.Instance.EconomyManager.AddRevenue(price, RevenueCategory.ShopSale);
                    }

                    WebGLOptimizer.LogVerbose($"[Vendor] {Name} が来場者#{_targetVisitor.VisitorId}に" +
                        $"{(_sellingFood ? "食事" : "飲料")}を販売 (${price:F0})");
                }
            }

            _targetVisitor = null;
            _cooldownTimer = SellCooldown;
            CompleteCurrentTask();
        }

        /// <summary>グローバル販売統計をリセットする（ゲームリセット時等）</summary>
        public static void ClearGlobalStats()
        {
            _globalTotalSales = 0;
            _globalTotalRevenue = 0f;
        }
    }
}
