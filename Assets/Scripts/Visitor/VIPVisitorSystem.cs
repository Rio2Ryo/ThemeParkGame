// ============================================================
// ThemeParkGame - VIP Visitor System
// VIP来場者の特別対応・リクエスト管理・評価ブースト
// ============================================================

using System.Collections.Generic;
using UnityEngine;
using ThemeParkGame.Core;
using ThemeParkGame.Economy;

namespace ThemeParkGame.Visitor
{
    /// <summary>
    /// VIP来場者プログラムを管理する。
    /// VIPが来場すると特別リクエストが発生し、
    /// 満足させるとパーク評価が大幅に上昇する。
    /// 失望させると評価が下がる。
    /// </summary>
    public class VIPVisitorSystem : MonoBehaviour
    {
        public static VIPVisitorSystem Instance { get; private set; }

        // ---- 設定 ----
        private const float VIP_RATING_BONUS = 3.0f;        // VIP満足時の評価ボーナス
        private const float VIP_RATING_PENALTY = -1.5f;     // VIP不満時の評価ペナルティ
        private const float VIP_HAPPINESS_THRESHOLD = 65f;   // VIP満足判定閾値
        private const float VIP_CHECK_INTERVAL = 10f;        // VIPステータスチェック間隔
        private const float VIP_SPENDING_MULTIPLIER = 3.0f;  // VIPの消費倍率
        private const float VIP_FAME_BONUS = 2.0f;           // VIP満足時の知名度ボーナス

        // ---- 内部状態 ----
        private readonly Dictionary<int, VIPData> _activeVIPs = new Dictionary<int, VIPData>();
        private float _checkTimer;
        private int _totalVIPsServed;
        private int _satisfiedVIPs;

        // ---- 統計プロパティ ----
        public int TotalVIPsServed => _totalVIPsServed;
        public int SatisfiedVIPs => _satisfiedVIPs;
        public int ActiveVIPCount => _activeVIPs.Count;
        public float VIPSatisfactionRate => _totalVIPsServed > 0
            ? (float)_satisfiedVIPs / _totalVIPsServed * 100f : 0f;

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        private void OnEnable()
        {
            GameEvents.OnVIPArrived += HandleVIPArrived;
            GameEvents.OnVisitorLeavePark += HandleVisitorLeave;
        }

        private void OnDisable()
        {
            GameEvents.OnVIPArrived -= HandleVIPArrived;
            GameEvents.OnVisitorLeavePark -= HandleVisitorLeave;
        }

        private void Update()
        {
            if (_activeVIPs.Count == 0) return;
            if (GameManager.Instance != null && GameManager.Instance.IsPaused) return;

            _checkTimer -= Time.deltaTime;
            if (_checkTimer <= 0f)
            {
                _checkTimer = VIP_CHECK_INTERVAL;
                CheckVIPStatus();
            }
        }

        // ================================================================
        // VIPイベントハンドラ
        // ================================================================

        private void HandleVIPArrived(int visitorId)
        {
            if (_activeVIPs.ContainsKey(visitorId)) return;

            var vipData = new VIPData
            {
                VisitorId = visitorId,
                ArrivalTime = Time.time,
                RequestType = GenerateVIPRequest(),
                IsRequestCompleted = false,
                InitialHappiness = GetVisitorHappiness(visitorId)
            };

            _activeVIPs[visitorId] = vipData;

            // VIPの初期キャッシュブースト（VIPはお金持ち）
            BoostVIPCash(visitorId);

            // 通知
            string requestDesc = GetRequestDescription(vipData.RequestType);
            if (NotificationSystem.Instance != null)
            {
                NotificationSystem.Instance.Notify(
                    $"VIP来場！ リクエスト: {requestDesc}",
                    NotifLevel.Info);
            }

            WebGLOptimizer.LogVerbose($"[VIPSystem] VIP #{visitorId} arrived. Request: {vipData.RequestType}");
        }

        private void HandleVisitorLeave(int visitorId)
        {
            if (!_activeVIPs.TryGetValue(visitorId, out var vipData)) return;

            _totalVIPsServed++;
            float happiness = GetVisitorHappiness(visitorId);
            bool satisfied = happiness >= VIP_HAPPINESS_THRESHOLD;

            if (satisfied)
            {
                _satisfiedVIPs++;
                ApplyRatingBonus(VIP_RATING_BONUS);
                ApplyFameBonus(VIP_FAME_BONUS);

                if (NotificationSystem.Instance != null)
                {
                    NotificationSystem.Instance.Notify(
                        $"VIPが満足して帰りました！ (幸福度: {happiness:F0}) 評価UP!",
                        NotifLevel.Success);
                }

                // VIP満足時の収益ボーナス
                if (GameManager.Instance?.EconomyManager != null)
                {
                    float bonus = 500f + happiness * 10f;
                    GameManager.Instance.EconomyManager.AddRevenue(bonus, RevenueCategory.Other);
                    WebGLOptimizer.LogVerbose($"[VIPSystem] VIP #{visitorId} satisfied. Bonus: ${bonus:N0}");
                }
            }
            else
            {
                ApplyRatingBonus(VIP_RATING_PENALTY);

                if (NotificationSystem.Instance != null)
                {
                    NotificationSystem.Instance.Notify(
                        $"VIPが不満のまま帰りました... (幸福度: {happiness:F0}) 評価DOWN",
                        NotifLevel.Warning);
                }

                WebGLOptimizer.LogVerbose($"[VIPSystem] VIP #{visitorId} unsatisfied. Happiness: {happiness:F0}");
            }

            GameEvents.FireVIPRequestCompleted(visitorId, satisfied);
            _activeVIPs.Remove(visitorId);
        }

        // ================================================================
        // VIPステータスチェック
        // ================================================================

        private void CheckVIPStatus()
        {
            var toRemove = new List<int>();

            foreach (var kvp in _activeVIPs)
            {
                int vipId = kvp.Key;
                var visitor = FindVisitor(vipId);

                if (visitor == null || !visitor.IsActive)
                {
                    toRemove.Add(vipId);
                    continue;
                }

                // VIPリクエスト進捗チェック
                if (!kvp.Value.IsRequestCompleted)
                {
                    if (CheckRequestProgress(visitor, kvp.Value))
                    {
                        kvp.Value.IsRequestCompleted = true;

                        // リクエスト達成で幸福度ブースト
                        if (visitor.Parameters != null)
                        {
                            visitor.Parameters.ModifyHappiness(20f);
                        }

                        if (NotificationSystem.Instance != null)
                        {
                            NotificationSystem.Instance.Notify(
                                "VIPのリクエストが達成されました！",
                                NotifLevel.Success);
                        }
                    }
                }
            }

            foreach (int id in toRemove)
            {
                _activeVIPs.Remove(id);
            }
        }

        // ================================================================
        // VIPリクエスト生成
        // ================================================================

        private VIPRequestType GenerateVIPRequest()
        {
            float roll = Random.value;
            if (roll < 0.30f) return VIPRequestType.RideTopAttraction;
            if (roll < 0.55f) return VIPRequestType.TryAllShops;
            if (roll < 0.75f) return VIPRequestType.HighSatisfaction;
            if (roll < 0.90f) return VIPRequestType.ShortWaitTimes;
            return VIPRequestType.MeetEntertainer;
        }

        private string GetRequestDescription(VIPRequestType type)
        {
            switch (type)
            {
                case VIPRequestType.RideTopAttraction:
                    return "人気アトラクションに乗りたい";
                case VIPRequestType.TryAllShops:
                    return "ショップで買い物したい";
                case VIPRequestType.HighSatisfaction:
                    return "最高の体験がしたい";
                case VIPRequestType.ShortWaitTimes:
                    return "待ち時間が短いのが好き";
                case VIPRequestType.MeetEntertainer:
                    return "エンターテイナーに会いたい";
                default:
                    return "特別な体験";
            }
        }

        private bool CheckRequestProgress(VisitorAI visitor, VIPData data)
        {
            switch (data.RequestType)
            {
                case VIPRequestType.RideTopAttraction:
                    // アトラクションに1回以上乗ったか
                    return visitor.Profile?.AttractionMemories != null
                        && visitor.Profile.AttractionMemories.Count >= 1;

                case VIPRequestType.TryAllShops:
                    // 食事または飲み物を購入したか
                    return visitor.CurrentState == VisitorBehaviorState.Eating
                        || visitor.CurrentState == VisitorBehaviorState.Drinking
                        || (visitor.Profile?.AttractionMemories != null
                            && visitor.Profile.AttractionMemories.Count >= 1);

                case VIPRequestType.HighSatisfaction:
                    // 幸福度が80以上か
                    return visitor.Happiness >= 80f;

                case VIPRequestType.ShortWaitTimes:
                    // 行列待ち状態でないか
                    return visitor.CurrentState != VisitorBehaviorState.WaitingInQueue;

                case VIPRequestType.MeetEntertainer:
                    // エンターテイメントを見たか
                    return visitor.CurrentState == VisitorBehaviorState.WatchingEntertainment
                        || (visitor.Profile?.AttractionMemories != null
                            && visitor.Profile.AttractionMemories.Count >= 1);

                default:
                    return false;
            }
        }

        // ================================================================
        // ユーティリティ
        // ================================================================

        private VisitorAI FindVisitor(int visitorId)
        {
            if (GameManager.Instance?.VisitorManager == null) return null;
            return GameManager.Instance.VisitorManager.FindVisitorById(visitorId);
        }

        private float GetVisitorHappiness(int visitorId)
        {
            var visitor = FindVisitor(visitorId);
            return visitor?.Happiness ?? 50f;
        }

        private void BoostVIPCash(int visitorId)
        {
            var visitor = FindVisitor(visitorId);
            if (visitor?.Parameters != null)
            {
                // VIPは通常の3倍の所持金を持つ（追加分を加算）
                float currentCash = visitor.Parameters.Cash;
                float extraCash = currentCash * (VIP_SPENDING_MULTIPLIER - 1f);
                visitor.Parameters.AddCash(extraCash);
            }
        }

        private void ApplyRatingBonus(float amount)
        {
            var pm = GameManager.Instance?.ParkManager;
            if (pm?.Rating != null)
            {
                // Mood評価にボーナスを加算
                float current = pm.Rating.OverallRating;
                float newRating = Mathf.Clamp(current + amount, 0f, 100f);

                // ParkRatingのUpdateMoodRatingを通じてムード評価を変動させる
                // 直接OverallRatingは設定できないため、ムードスコアで影響を与える
                pm.Rating.ApplyExternalBonus(CertificateCategory.Mood, amount);
            }
        }

        private void ApplyFameBonus(float amount)
        {
            var pm = GameManager.Instance?.ParkManager;
            if (pm?.Rating != null)
            {
                pm.Rating.ApplyExternalBonus(CertificateCategory.Fame, amount);
            }
        }

        // ================================================================
        // データ構造
        // ================================================================

        private class VIPData
        {
            public int VisitorId;
            public float ArrivalTime;
            public VIPRequestType RequestType;
            public bool IsRequestCompleted;
            public float InitialHappiness;
        }

        private enum VIPRequestType
        {
            RideTopAttraction,
            TryAllShops,
            HighSatisfaction,
            ShortWaitTimes,
            MeetEntertainer
        }
    }
}
