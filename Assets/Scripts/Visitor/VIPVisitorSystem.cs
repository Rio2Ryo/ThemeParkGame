// ============================================================
// ThemeParkGame - VIP Visitor System
// VIP来場者の特別対応・リクエスト管理・評価ブースト
// VIPランク（Silver/Gold/Platinum）による差別化報酬
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
    /// ランクに応じて報酬が変動する。
    /// </summary>
    public class VIPVisitorSystem : MonoBehaviour
    {
        public static VIPVisitorSystem Instance { get; private set; }

        // ---- VIPランク定義 ----
        public enum VIPRank
        {
            Silver,    // 所持金2倍、報酬=$500+知名度2.0+ゴールデンチケット×1
            Gold,      // 所持金4倍、報酬=$1000+知名度3.0+ゴールデンチケット×1、幸福度閾値55
            Platinum   // 所持金6倍、報酬=$2000+知名度5.0+ゴールデンチケット×2、幸福度閾値45
        }

        // ---- 設定 ----
        private const float VIP_RATING_BONUS = 3.0f;        // VIP満足時の評価ボーナス
        private const float VIP_RATING_PENALTY = -1.5f;     // VIP不満時の評価ペナルティ
        private const float VIP_CHECK_INTERVAL = 10f;        // VIPステータスチェック間隔

        // ---- ランク別パラメータ ----
        private static float GetHappinessThreshold(VIPRank rank)
        {
            switch (rank)
            {
                case VIPRank.Silver:   return 65f;
                case VIPRank.Gold:     return 55f;
                case VIPRank.Platinum: return 45f;
                default: return 65f;
            }
        }

        private static float GetSpendingMultiplier(VIPRank rank)
        {
            switch (rank)
            {
                case VIPRank.Silver:   return 2.0f;
                case VIPRank.Gold:     return 4.0f;
                case VIPRank.Platinum: return 6.0f;
                default: return 2.0f;
            }
        }

        private static float GetRevenueBonus(VIPRank rank)
        {
            switch (rank)
            {
                case VIPRank.Silver:   return 500f;
                case VIPRank.Gold:     return 1000f;
                case VIPRank.Platinum: return 2000f;
                default: return 500f;
            }
        }

        private static float GetFameBonus(VIPRank rank)
        {
            switch (rank)
            {
                case VIPRank.Silver:   return 2.0f;
                case VIPRank.Gold:     return 3.0f;
                case VIPRank.Platinum: return 5.0f;
                default: return 2.0f;
            }
        }

        private static int GetGoldenTicketReward(VIPRank rank)
        {
            switch (rank)
            {
                case VIPRank.Silver:   return 1;
                case VIPRank.Gold:     return 1;
                case VIPRank.Platinum: return 2;
                default: return 1;
            }
        }

        /// <summary>VIPランクの日本語表示名を取得する</summary>
        public static string GetRankDisplayName(VIPRank rank)
        {
            switch (rank)
            {
                case VIPRank.Silver:   return "シルバー";
                case VIPRank.Gold:     return "ゴールド";
                case VIPRank.Platinum: return "プラチナ";
                default: return "不明";
            }
        }

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
        // VIPランク抽選
        // ================================================================

        /// <summary>VIPランクを確率で決定する（Silver60%, Gold30%, Platinum10%）</summary>
        private VIPRank RollVIPRank()
        {
            float roll = Random.value;
            if (roll < 0.60f) return VIPRank.Silver;
            if (roll < 0.90f) return VIPRank.Gold;
            return VIPRank.Platinum;
        }

        // ================================================================
        // VIPイベントハンドラ
        // ================================================================

        private void HandleVIPArrived(int visitorId)
        {
            if (_activeVIPs.ContainsKey(visitorId)) return;

            VIPRank rank = RollVIPRank();

            var vipData = new VIPData
            {
                VisitorId = visitorId,
                ArrivalTime = Time.time,
                RequestType = GenerateVIPRequest(),
                IsRequestCompleted = false,
                InitialHappiness = GetVisitorHappiness(visitorId),
                Rank = rank
            };

            _activeVIPs[visitorId] = vipData;

            // VIPの初期キャッシュブースト（ランクに応じた倍率）
            BoostVIPCash(visitorId, rank);

            // 通知
            string requestDesc = GetRequestDescription(vipData.RequestType);
            string rankName = GetRankDisplayName(rank);
            if (NotificationSystem.Instance != null)
            {
                NotificationSystem.Instance.Notify(
                    $"VIP[{rankName}]来場！ リクエスト: {requestDesc}",
                    NotifLevel.Info);
            }

            WebGLOptimizer.LogVerbose($"[VIPSystem] VIP #{visitorId} [{rank}] arrived. Request: {vipData.RequestType}");
        }

        private void HandleVisitorLeave(int visitorId)
        {
            if (!_activeVIPs.TryGetValue(visitorId, out var vipData)) return;

            _totalVIPsServed++;
            float happiness = GetVisitorHappiness(visitorId);
            float threshold = GetHappinessThreshold(vipData.Rank);
            bool satisfied = happiness >= threshold;

            if (satisfied)
            {
                _satisfiedVIPs++;

                float fameBonus = GetFameBonus(vipData.Rank);
                ApplyRatingBonus(VIP_RATING_BONUS);
                ApplyFameBonus(fameBonus);

                string rankName = GetRankDisplayName(vipData.Rank);
                if (NotificationSystem.Instance != null)
                {
                    NotificationSystem.Instance.Notify(
                        $"VIP[{rankName}]が満足して帰りました！ (幸福度: {happiness:F0}) 評価UP!",
                        NotifLevel.Success);
                }

                // VIP満足時の収益ボーナス（ランク別）
                if (GameManager.Instance?.EconomyManager != null)
                {
                    float bonus = GetRevenueBonus(vipData.Rank) + happiness * 10f;
                    GameManager.Instance.EconomyManager.AddRevenue(bonus, RevenueCategory.Other);
                    WebGLOptimizer.LogVerbose($"[VIPSystem] VIP #{visitorId} [{vipData.Rank}] satisfied. Bonus: ${bonus:N0}");
                }
            }
            else
            {
                ApplyRatingBonus(VIP_RATING_PENALTY);

                string rankName = GetRankDisplayName(vipData.Rank);
                if (NotificationSystem.Instance != null)
                {
                    NotificationSystem.Instance.Notify(
                        $"VIP[{rankName}]が不満のまま帰りました... (幸福度: {happiness:F0}) 評価DOWN",
                        NotifLevel.Warning);
                }

                WebGLOptimizer.LogVerbose($"[VIPSystem] VIP #{visitorId} [{vipData.Rank}] unsatisfied. Happiness: {happiness:F0}");
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
                var vipData = kvp.Value;
                var visitor = FindVisitor(vipId);

                if (visitor == null || !visitor.IsActive)
                {
                    toRemove.Add(vipId);
                    continue;
                }

                // 状態遷移を追跡してショップ利用・エンタメ鑑賞・行列待ちを記録
                TrackVIPActivity(visitor, vipData);

                // VIPリクエスト進捗チェック
                if (!vipData.IsRequestCompleted)
                {
                    if (CheckRequestProgress(visitor, vipData))
                    {
                        vipData.IsRequestCompleted = true;

                        // リクエスト達成で幸福度ブースト
                        if (visitor.Parameters != null)
                        {
                            visitor.Parameters.ModifyHappiness(20f);
                        }

                        // ゴールデンチケット報酬（ランク別）
                        int ticketCount = GetGoldenTicketReward(vipData.Rank);
                        for (int i = 0; i < ticketCount; i++)
                        {
                            GameManager.Instance?.AwardGoldenTicket();
                        }

                        string rankName = GetRankDisplayName(vipData.Rank);
                        if (NotificationSystem.Instance != null)
                        {
                            string ticketMsg = ticketCount > 1
                                ? $"ゴールデンチケット{ticketCount}枚獲得！"
                                : "ゴールデンチケット獲得！";
                            NotificationSystem.Instance.Notify(
                                $"VIP[{rankName}]のリクエストが達成されました！ {ticketMsg}",
                                NotifLevel.Success);
                        }

                        WebGLOptimizer.LogVerbose(
                            $"[VIPSystem] VIP #{vipId} [{vipData.Rank}] request fulfilled: {vipData.RequestType}, tickets: {ticketCount}");
                    }
                    else
                    {
                        // リクエスト未達の場合、時間経過で幸福度にペナルティ
                        // （VIPは待たされることを嫌う）
                        float elapsed = Time.time - vipData.ArrivalTime;
                        if (elapsed > 60f && vipData.PenaltyCount < 5)
                        {
                            // 60秒以降、チェックごとにペナルティ（最大5回）
                            vipData.PenaltyCount++;
                            if (visitor.Parameters != null)
                            {
                                visitor.Parameters.ModifyHappiness(-5f);
                            }
                            WebGLOptimizer.LogVerbose(
                                $"[VIPSystem] VIP #{vipId} growing impatient (penalty #{vipData.PenaltyCount})");
                        }
                    }
                }
            }

            foreach (int id in toRemove)
            {
                _activeVIPs.Remove(id);
            }
        }

        /// <summary>
        /// VIPの行動を追跡して、ショップ利用・エンタメ鑑賞・行列待ちを記録する。
        /// 状態遷移を検出して累積データを更新する。
        /// </summary>
        private void TrackVIPActivity(VisitorAI visitor, VIPData data)
        {
            var currentState = visitor.CurrentState;
            var prevState = data.LastState;

            // 食事完了を検出（Eating → 他の状態）
            if (prevState == VisitorBehaviorState.Eating && currentState != VisitorBehaviorState.Eating)
            {
                data.HasPurchasedFood = true;
                data.ShopPurchaseCount++;
            }

            // 飲料完了を検出（Drinking → 他の状態）
            if (prevState == VisitorBehaviorState.Drinking && currentState != VisitorBehaviorState.Drinking)
            {
                data.HasPurchasedDrink = true;
                data.ShopPurchaseCount++;
            }

            // お土産購入を検出（WalkingToShop → Idle/他でショップが完了）
            // ShopPurchaseCountが食事・飲料以外にも増えた場合お土産とみなす
            if (prevState == VisitorBehaviorState.WalkingToShop
                && currentState != VisitorBehaviorState.WalkingToShop
                && currentState != VisitorBehaviorState.Eating
                && currentState != VisitorBehaviorState.Drinking)
            {
                data.HasPurchasedSouvenir = true;
                data.ShopPurchaseCount++;
            }

            // エンタメ鑑賞完了を検出
            if (prevState == VisitorBehaviorState.WatchingEntertainment
                && currentState != VisitorBehaviorState.WatchingEntertainment)
            {
                data.HasWatchedEntertainment = true;
            }

            // アトラクション搭乗完了を検出（搭乗回数追跡）
            if (prevState == VisitorBehaviorState.RidingAttraction
                && currentState != VisitorBehaviorState.RidingAttraction)
            {
                data.AttractionRideCount++;
            }

            // 行列待ち時間の追跡
            if (currentState == VisitorBehaviorState.WaitingInQueue)
            {
                if (prevState != VisitorBehaviorState.WaitingInQueue)
                {
                    // 行列に入った
                    data.QueueStartTime = Time.time;
                }
            }
            else if (prevState == VisitorBehaviorState.WaitingInQueue)
            {
                // 行列から出た
                data.TotalWaitTime += Time.time - data.QueueStartTime;
            }

            data.LastState = currentState;
        }

        // ================================================================
        // VIPリクエスト生成
        // ================================================================

        private VIPRequestType GenerateVIPRequest()
        {
            float roll = Random.value;
            // 既存5種 + 新規3種 = 8種
            if (roll < 0.18f) return VIPRequestType.RideTopAttraction;
            if (roll < 0.33f) return VIPRequestType.TryAllShops;
            if (roll < 0.46f) return VIPRequestType.HighSatisfaction;
            if (roll < 0.56f) return VIPRequestType.ShortWaitTimes;
            if (roll < 0.66f) return VIPRequestType.MeetEntertainer;
            if (roll < 0.78f) return VIPRequestType.ExclusiveRide;
            if (roll < 0.89f) return VIPRequestType.PhotoWithMascot;
            return VIPRequestType.GourmetExperience;
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
                case VIPRequestType.ExclusiveRide:
                    return "アトラクションを堪能したい";
                case VIPRequestType.PhotoWithMascot:
                    return "マスコットと記念撮影したい";
                case VIPRequestType.GourmetExperience:
                    return "パークグルメを完全制覇したい";
                default:
                    return "特別な体験";
            }
        }

        private bool CheckRequestProgress(VisitorAI visitor, VIPData data)
        {
            switch (data.RequestType)
            {
                case VIPRequestType.RideTopAttraction:
                    // 興奮度5以上のアトラクションに乗り、満足度が高い体験をしたか
                    if (visitor.Profile?.AttractionMemories == null) return false;
                    foreach (var memory in visitor.Profile.AttractionMemories)
                    {
                        if (memory.SatisfactionScore >= 60f) return true;
                    }
                    return false;

                case VIPRequestType.TryAllShops:
                    // 食事と飲料の両方を購入したか（状態遷移追跡で記録）
                    return data.HasPurchasedFood && data.HasPurchasedDrink;

                case VIPRequestType.HighSatisfaction:
                    // 幸福度85以上かつ満足度が高い
                    return visitor.Happiness >= 85f
                        && visitor.Parameters != null
                        && visitor.Parameters.Satisfaction >= 70f;

                case VIPRequestType.ShortWaitTimes:
                    // アトラクションに1回以上乗り、累計待ち時間が30秒以内
                    if (visitor.Profile?.AttractionMemories == null
                        || visitor.Profile.AttractionMemories.Count == 0)
                        return false;
                    return data.TotalWaitTime <= 30f;

                case VIPRequestType.MeetEntertainer:
                    // エンターテイナーのショーを実際に鑑賞した
                    return data.HasWatchedEntertainment;

                case VIPRequestType.ExclusiveRide:
                    // アトラクション2回以上搭乗 + 幸福度70以上
                    return data.AttractionRideCount >= 2 && visitor.Happiness >= 70f;

                case VIPRequestType.PhotoWithMascot:
                    // エンターテイナー鑑賞 + ショップ1回以上利用
                    return data.HasWatchedEntertainment && data.ShopPurchaseCount >= 1;

                case VIPRequestType.GourmetExperience:
                    // 食事 + 飲料 + お土産の3種ショップ利用
                    return data.HasPurchasedFood && data.HasPurchasedDrink && data.HasPurchasedSouvenir;

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

        private void BoostVIPCash(int visitorId, VIPRank rank)
        {
            var visitor = FindVisitor(visitorId);
            if (visitor?.Parameters != null)
            {
                float multiplier = GetSpendingMultiplier(rank);
                float currentCash = visitor.Parameters.Cash;
                float extraCash = currentCash * (multiplier - 1f);
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
            /// <summary>VIPランク</summary>
            public VIPRank Rank;

            /// <summary>ショップ購入回数追跡（TryAllShops用）</summary>
            public int ShopPurchaseCount;
            /// <summary>食事購入済みフラグ</summary>
            public bool HasPurchasedFood;
            /// <summary>飲料購入済みフラグ</summary>
            public bool HasPurchasedDrink;
            /// <summary>お土産購入済みフラグ（GourmetExperience用）</summary>
            public bool HasPurchasedSouvenir;
            /// <summary>エンターテイナー鑑賞済みフラグ</summary>
            public bool HasWatchedEntertainment;
            /// <summary>アトラクション搭乗回数（ExclusiveRide用）</summary>
            public int AttractionRideCount;
            /// <summary>累計行列待ち時間（秒）</summary>
            public float TotalWaitTime;
            /// <summary>行列待ち開始時刻</summary>
            public float QueueStartTime;
            /// <summary>前回の状態（状態遷移検出用）</summary>
            public VisitorBehaviorState LastState;
            /// <summary>未達ペナルティ適用回数（際限なく下がるのを防ぐ）</summary>
            public int PenaltyCount;
        }

        private enum VIPRequestType
        {
            RideTopAttraction,
            TryAllShops,
            HighSatisfaction,
            ShortWaitTimes,
            MeetEntertainer,
            ExclusiveRide,      // アトラクション2回以上搭乗+幸福度70以上
            PhotoWithMascot,    // エンターテイナー鑑賞+ショップ1回以上利用
            GourmetExperience   // 食事+飲料+お土産の3種ショップ利用
        }
    }
}
