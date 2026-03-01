using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using ThemeParkGame.Core;

namespace ThemeParkGame.Economy
{
    /// <summary>
    /// マーケティングキャンペーンのデータを保持するクラス。
    /// 各キャンペーンのチャネル、期間、コスト、効果情報を管理します。
    /// </summary>
    [Serializable]
    public class CampaignData
    {
        /// <summary>
        /// マーケティングチャネルの種類。
        /// </summary>
        public MarketingChannel Channel;

        /// <summary>
        /// キャンペーンの実施期間（日数）。1日、3日、7日のいずれか。
        /// </summary>
        public int DurationDays;

        /// <summary>
        /// 1日あたりのコスト。
        /// </summary>
        public float DailyCost;

        /// <summary>
        /// キャンペーンの合計コスト（DurationDays × DailyCost）。
        /// </summary>
        public float TotalCost => DurationDays * DailyCost;

        /// <summary>
        /// キャンペーンの残り日数。
        /// </summary>
        public int RemainingDays;

        /// <summary>
        /// 来場者スポーン率の倍率。
        /// キャンペーンが来場者の発生頻度にどれだけ影響を与えるかを示します。
        /// </summary>
        public float SpawnRateMultiplier;

        /// <summary>
        /// 来場者タイプごとのスポーン倍率ブースト。
        /// 特定のタイプの来場者により強い効果を発揮するチャネルで使用されます。
        /// </summary>
        public Dictionary<VisitorType, float> TypeBoosts;

        /// <summary>
        /// キャンペーンが現在アクティブかどうか。
        /// </summary>
        public bool IsActive;

        /// <summary>
        /// 過度な広告による期待値の膨張ペナルティ。
        /// 同時に多くのキャンペーンを実施すると、来場者の期待値が上がり
        /// 満足度にマイナスの影響を与えます。
        /// </summary>
        public float ExpectationInflation;

        /// <summary>
        /// CampaignDataの新しいインスタンスを初期化します。
        /// </summary>
        /// <param name="channel">マーケティングチャネル</param>
        /// <param name="durationDays">実施期間（日数）</param>
        /// <param name="dailyCost">1日あたりのコスト</param>
        /// <param name="spawnRateMultiplier">スポーン率の倍率</param>
        /// <param name="typeBoosts">タイプ別ブースト辞書</param>
        public CampaignData(
            MarketingChannel channel,
            int durationDays,
            float dailyCost,
            float spawnRateMultiplier,
            Dictionary<VisitorType, float> typeBoosts)
        {
            Channel = channel;
            DurationDays = durationDays;
            DailyCost = dailyCost;
            RemainingDays = durationDays;
            SpawnRateMultiplier = spawnRateMultiplier;
            TypeBoosts = typeBoosts ?? new Dictionary<VisitorType, float>();
            IsActive = true;
            ExpectationInflation = 0f;
        }
    }

    /// <summary>
    /// テーマパークのマーケティング＆広告キャンペーンシステム。
    /// 各種マーケティングチャネルを通じたキャンペーンの作成・管理・効果計算を行います。
    ///
    /// <para>
    /// 主な機能:
    /// <list type="bullet">
    /// <item>TV CM、Web広告、チラシ配布、インフルエンサー招待の4チャネル対応</item>
    /// <item>キャンペーン期間に応じた来場者スポーン率ブースト</item>
    /// <item>来場者タイプ別のターゲティング効果</item>
    /// <item>過度な広告による期待値膨張（満足度ペナルティ）の管理</item>
    /// <item>パーク評価が高い場合のTV CM効果倍増</item>
    /// </list>
    /// </para>
    /// </summary>
    public class MarketingSystem : MonoBehaviour
    {
        // ========================================================================
        // シングルトン
        // ========================================================================

        /// <summary>
        /// MarketingSystemのシングルトンインスタンス。
        /// </summary>
        public static MarketingSystem Instance { get; private set; }

        // ========================================================================
        // 定数
        // ========================================================================

        /// <summary>
        /// 同時に実施できるキャンペーンの最大数。
        /// </summary>
        public const int MaxActiveCampaigns = 3;

        /// <summary>
        /// この数を超える同時キャンペーンは期待値膨張ペナルティを受けます。
        /// </summary>
        public const int OverAdvertisingThreshold = 2;

        /// <summary>
        /// 閾値を超えた1キャンペーンあたりの満足度ペナルティ。
        /// </summary>
        public const float ExpectationPenaltyPerExcess = 5f;

        /// <summary>
        /// パーク評価がこの値を超えるとTV CMの効果が倍増します。
        /// </summary>
        private const float HighParkRatingThreshold = 70f;

        /// <summary>
        /// 有効なキャンペーン期間の選択肢（日数）。
        /// </summary>
        private static readonly int[] ValidDurations = { 1, 3, 7 };

        // ========================================================================
        // チャネル別基本コスト定義
        // ========================================================================

        /// <summary>
        /// チャネル別の1日あたりの基本コスト。
        /// </summary>
        private static readonly Dictionary<MarketingChannel, float> ChannelDailyCosts =
            new Dictionary<MarketingChannel, float>
            {
                { MarketingChannel.TvCommercial, 500f },
                { MarketingChannel.WebAdvertising, 300f },
                { MarketingChannel.FlyerDistribution, 150f },
                { MarketingChannel.InfluencerInvite, 400f }
            };

        /// <summary>
        /// チャネル別・期間別のスポーン率倍率。
        /// キーは (チャネル, 期間日数) のタプル。
        /// 長期キャンペーンほど1日あたりの効果は下がりますが、
        /// 持続期間が長いため合計効果は大きくなります。
        /// </summary>
        private static readonly Dictionary<(MarketingChannel, int), float> ChannelDurationMultipliers =
            new Dictionary<(MarketingChannel, int), float>
            {
                // TV CM: 高コスト・全タイプ均等効果
                { (MarketingChannel.TvCommercial, 1), 1.3f },
                { (MarketingChannel.TvCommercial, 3), 1.25f },
                { (MarketingChannel.TvCommercial, 7), 1.2f },

                // Web広告: 中コスト・若者とカップルに強い
                { (MarketingChannel.WebAdvertising, 1), 1.25f },
                { (MarketingChannel.WebAdvertising, 3), 1.2f },
                { (MarketingChannel.WebAdvertising, 7), 1.15f },

                // チラシ配布: 低コスト・ファミリーと子供に強い
                { (MarketingChannel.FlyerDistribution, 1), 1.2f },
                { (MarketingChannel.FlyerDistribution, 3), 1.15f },
                { (MarketingChannel.FlyerDistribution, 7), 1.1f },

                // インフルエンサー招待: 高コスト・若者に効果的
                { (MarketingChannel.InfluencerInvite, 1), 1.35f },
                { (MarketingChannel.InfluencerInvite, 3), 1.3f },
                { (MarketingChannel.InfluencerInvite, 7), 1.2f }
            };

        /// <summary>
        /// チャネル別の来場者タイプごとのブースト倍率。
        /// 各チャネルが特定の来場者タイプにどれだけ強い効果を発揮するかを定義します。
        /// </summary>
        private static readonly Dictionary<MarketingChannel, Dictionary<VisitorType, float>> ChannelTypeBoosts =
            new Dictionary<MarketingChannel, Dictionary<VisitorType, float>>
            {
                // TV CM: 全タイプ均等（特別なブーストなし）
                {
                    MarketingChannel.TvCommercial,
                    new Dictionary<VisitorType, float>()
                },

                // Web広告: 若者×2.0、カップル×1.8
                {
                    MarketingChannel.WebAdvertising,
                    new Dictionary<VisitorType, float>
                    {
                        { VisitorType.Young, 2.0f },
                        { VisitorType.Couple, 1.8f }
                    }
                },

                // チラシ配布: ファミリー×2.0、子供×1.8
                {
                    MarketingChannel.FlyerDistribution,
                    new Dictionary<VisitorType, float>
                    {
                        { VisitorType.Family, 2.0f },
                        { VisitorType.Kids, 1.8f }
                    }
                },

                // インフルエンサー招待: 若者×1.5、インフルエンサー来場
                {
                    MarketingChannel.InfluencerInvite,
                    new Dictionary<VisitorType, float>
                    {
                        { VisitorType.Young, 1.5f },
                        { VisitorType.Influencer, 1.0f }
                    }
                }
            };

        // ========================================================================
        // キャンペーン管理フィールド
        // ========================================================================

        /// <summary>
        /// 現在アクティブなキャンペーンのリスト。
        /// 最大 <see cref="MaxActiveCampaigns"/> 件まで同時に実施可能です。
        /// </summary>
        [SerializeField]
        private List<CampaignData> activeCampaigns = new List<CampaignData>();

        /// <summary>
        /// 完了または期限切れになったキャンペーンの履歴。
        /// 過去のマーケティング実績を追跡するために使用されます。
        /// </summary>
        [SerializeField]
        private List<CampaignData> campaignHistory = new List<CampaignData>();

        /// <summary>
        /// これまでのマーケティング支出の累計金額。
        /// </summary>
        [SerializeField]
        private float totalMarketingSpend;

        // ========================================================================
        // プロパティ
        // ========================================================================

        /// <summary>
        /// 現在アクティブなキャンペーンの読み取り専用リスト。
        /// </summary>
        public List<CampaignData> ActiveCampaigns => activeCampaigns;

        /// <summary>
        /// 完了したキャンペーンの履歴の読み取り専用リスト。
        /// </summary>
        public List<CampaignData> CampaignHistory => campaignHistory;

        /// <summary>
        /// これまでのマーケティング支出の累計金額。
        /// </summary>
        public float TotalMarketingSpend => totalMarketingSpend;

        // ========================================================================
        // イベント
        // ========================================================================

        /// <summary>
        /// 新しいキャンペーンが開始されたときに発火するイベント。
        /// 引数には開始されたキャンペーンのデータが渡されます。
        /// </summary>
        public event Action<CampaignData> OnCampaignStarted;

        /// <summary>
        /// キャンペーンが終了（完了またはキャンセル）されたときに発火するイベント。
        /// 引数には終了したキャンペーンのデータが渡されます。
        /// </summary>
        public event Action<CampaignData> OnCampaignEnded;

        // ========================================================================
        // Unity ライフサイクル
        // ========================================================================

        /// <summary>
        /// シングルトンインスタンスの初期化と重複チェックを行います。
        /// </summary>
        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                WebGLOptimizer.LogVerbose("[MarketingSystem] 重複インスタンスを検出、破棄します。");
                Destroy(gameObject);
                return;
            }

            Instance = this;
            WebGLOptimizer.LogVerbose("[MarketingSystem] シングルトンインスタンスを初期化しました。");
        }

        /// <summary>
        /// イベント購読の開始を行います。
        /// TimeManagerのOnDayChangedイベントに日次更新処理を登録します。
        /// </summary>
        private void Start()
        {
            SubscribeToDayChanged();
            WebGLOptimizer.LogVerbose("[MarketingSystem] マーケティングシステムを起動しました。");
        }

        /// <summary>
        /// シングルトンの破棄とイベント購読の解除を行います。
        /// </summary>
        private void OnDestroy()
        {
            UnsubscribeFromDayChanged();

            if (Instance == this)
            {
                Instance = null;
                WebGLOptimizer.LogVerbose("[MarketingSystem] シングルトンインスタンスを破棄しました。");
            }
        }

        // ========================================================================
        // イベント購読管理
        // ========================================================================

        /// <summary>
        /// TimeManagerのOnDayChangedイベントに日次更新処理を購読します。
        /// </summary>
        private void SubscribeToDayChanged()
        {
            if (GameManager.Instance != null && GameManager.Instance.TimeManager != null)
            {
                GameManager.Instance.TimeManager.OnDayChanged += ProcessDailyUpdate;
                WebGLOptimizer.LogVerbose("[MarketingSystem] OnDayChangedイベントに購読しました。");
            }
            else
            {
                WebGLOptimizer.LogVerbose("[MarketingSystem] TimeManagerが未初期化のため、購読を保留します。");
            }
        }

        /// <summary>
        /// TimeManagerのOnDayChangedイベントから日次更新処理の購読を解除します。
        /// </summary>
        private void UnsubscribeFromDayChanged()
        {
            if (GameManager.Instance != null && GameManager.Instance.TimeManager != null)
            {
                GameManager.Instance.TimeManager.OnDayChanged -= ProcessDailyUpdate;
                WebGLOptimizer.LogVerbose("[MarketingSystem] OnDayChangedイベントの購読を解除しました。");
            }
        }

        // ========================================================================
        // キャンペーン開始
        // ========================================================================

        /// <summary>
        /// 指定されたチャネルと期間で新しいマーケティングキャンペーンを開始します。
        ///
        /// <para>
        /// 以下の条件をすべて満たす場合にキャンペーンが開始されます:
        /// <list type="bullet">
        /// <item>アクティブキャンペーン数が <see cref="MaxActiveCampaigns"/> 未満</item>
        /// <item>指定された期間が有効（1日、3日、7日のいずれか）</item>
        /// <item>初日分のコストを支払える十分な資金がある</item>
        /// </list>
        /// </para>
        /// </summary>
        /// <param name="channel">使用するマーケティングチャネル</param>
        /// <param name="durationDays">キャンペーン期間（1, 3, 7日のいずれか）</param>
        /// <returns>キャンペーンの開始に成功した場合はtrue、それ以外はfalse</returns>
        public bool StartCampaign(MarketingChannel channel, int durationDays)
        {
            // アクティブキャンペーン数の上限チェック
            if (activeCampaigns.Count >= MaxActiveCampaigns)
            {
                WebGLOptimizer.LogVerbose(
                    $"[MarketingSystem] キャンペーン開始失敗: アクティブキャンペーン数が上限({MaxActiveCampaigns})に達しています。");
                NotificationSystem.Instance?.Notify(
                    "キャンペーン数が上限に達しています。既存のキャンペーンが終了するまでお待ちください。",
                    NotifLevel.Warning);
                return false;
            }

            // 期間の有効性チェック
            if (!IsValidDuration(durationDays))
            {
                WebGLOptimizer.LogVerbose(
                    $"[MarketingSystem] キャンペーン開始失敗: 無効な期間({durationDays}日)が指定されました。");
                NotificationSystem.Instance?.Notify(
                    "無効なキャンペーン期間です。1日、3日、7日のいずれかを選択してください。",
                    NotifLevel.Warning);
                return false;
            }

            // 日額コストの取得
            float dailyCost = GetDailyCost(channel);

            // 初日分の資金チェック
            var economyManager = GameManager.Instance?.EconomyManager;
            if (economyManager == null)
            {
                WebGLOptimizer.LogVerbose("[MarketingSystem] キャンペーン開始失敗: EconomyManagerが利用できません。");
                return false;
            }

            if (!economyManager.CanAfford(dailyCost))
            {
                WebGLOptimizer.LogVerbose(
                    $"[MarketingSystem] キャンペーン開始失敗: 資金不足（必要額: ¥{dailyCost:N0}）。");
                NotificationSystem.Instance?.Notify(
                    $"資金が不足しています。{GetChannelDisplayName(channel)}の開始には¥{dailyCost:N0}が必要です。",
                    NotifLevel.Warning);
                return false;
            }

            // スポーン率倍率の決定
            float spawnMultiplier = GetSpawnMultiplierForDuration(channel, durationDays);

            // パーク評価が高い場合のTV CMボーナス
            if (channel == MarketingChannel.TvCommercial && IsHighParkRating())
            {
                spawnMultiplier = 1f + (spawnMultiplier - 1f) * 2f;
                WebGLOptimizer.LogVerbose(
                    "[MarketingSystem] パーク評価が高いため、TV CMの効果が倍増しました。" +
                    $"倍率: {spawnMultiplier:F2}x");
            }

            // タイプ別ブースト情報のコピー
            Dictionary<VisitorType, float> typeBoosts = GetTypeBoostsForChannel(channel);

            // キャンペーンデータの作成
            CampaignData campaign = new CampaignData(
                channel,
                durationDays,
                dailyCost,
                spawnMultiplier,
                typeBoosts
            );

            // 過度な広告による期待値膨張の計算
            int campaignsAfterStart = activeCampaigns.Count + 1;
            if (campaignsAfterStart > OverAdvertisingThreshold)
            {
                int excessCount = campaignsAfterStart - OverAdvertisingThreshold;
                campaign.ExpectationInflation = excessCount * ExpectationPenaltyPerExcess;
                WebGLOptimizer.LogVerbose(
                    $"[MarketingSystem] 過度な広告を検出。期待値膨張ペナルティ: {campaign.ExpectationInflation:F1}");
            }

            // 初日分のコストを支払い
            economyManager.PayExpense(dailyCost, ExpenseCategory.Other);
            totalMarketingSpend += dailyCost;

            // キャンペーンをアクティブリストに追加
            activeCampaigns.Add(campaign);

            // 既存キャンペーンの期待値膨張を再計算
            RecalculateExpectationInflation();

            // イベント発火
            OnCampaignStarted?.Invoke(campaign);

            // 通知
            string channelName = GetChannelDisplayName(channel);
            string durationText = durationDays == 1 ? "1日間" : $"{durationDays}日間";
            NotificationSystem.Instance?.Notify(
                $"{channelName}キャンペーンを開始しました！（{durationText}、日額¥{dailyCost:N0}）",
                NotifLevel.Info);

            WebGLOptimizer.LogVerbose(
                $"[MarketingSystem] キャンペーン開始: チャネル={channel}, 期間={durationDays}日, " +
                $"日額=¥{dailyCost:N0}, 倍率={spawnMultiplier:F2}x");

            return true;
        }

        // ========================================================================
        // キャンペーンキャンセル
        // ========================================================================

        /// <summary>
        /// 指定されたインデックスのアクティブキャンペーンをキャンセルします。
        /// キャンセルされたキャンペーンは履歴に移動され、即座に効果が失われます。
        /// </summary>
        /// <param name="index">キャンセルするキャンペーンのアクティブリスト内インデックス</param>
        public void CancelCampaign(int index)
        {
            if (index < 0 || index >= activeCampaigns.Count)
            {
                WebGLOptimizer.LogVerbose(
                    $"[MarketingSystem] キャンペーンキャンセル失敗: 無効なインデックス({index})。" +
                    $"アクティブ数: {activeCampaigns.Count}");
                return;
            }

            CampaignData campaign = activeCampaigns[index];
            campaign.IsActive = false;

            // アクティブリストから削除し、履歴に追加
            activeCampaigns.RemoveAt(index);
            campaignHistory.Add(campaign);

            // 期待値膨張の再計算
            RecalculateExpectationInflation();

            // イベント発火
            OnCampaignEnded?.Invoke(campaign);

            // 通知
            string channelName = GetChannelDisplayName(campaign.Channel);
            NotificationSystem.Instance?.Notify(
                $"{channelName}キャンペーンをキャンセルしました。（残り{campaign.RemainingDays}日）",
                NotifLevel.Info);

            WebGLOptimizer.LogVerbose(
                $"[MarketingSystem] キャンペーンキャンセル: チャネル={campaign.Channel}, " +
                $"残り日数={campaign.RemainingDays}");
        }

        // ========================================================================
        // 効果計算
        // ========================================================================

        /// <summary>
        /// 全アクティブキャンペーンの来場者スポーン率倍率を集計して返します。
        /// 複数キャンペーンの効果は乗算で合算されます。
        ///
        /// <example>
        /// キャンペーンAが1.3x、キャンペーンBが1.2xの場合、
        /// 結果は 1.3 × 1.2 = 1.56x となります。
        /// </example>
        /// </summary>
        /// <returns>全アクティブキャンペーンの合算スポーン率倍率。キャンペーンがない場合は1.0</returns>
        public float GetCurrentSpawnMultiplier()
        {
            if (activeCampaigns.Count == 0)
            {
                return 1.0f;
            }

            float combinedMultiplier = 1.0f;

            foreach (CampaignData campaign in activeCampaigns)
            {
                if (campaign.IsActive)
                {
                    combinedMultiplier *= campaign.SpawnRateMultiplier;
                }
            }

            WebGLOptimizer.LogVerbose(
                $"[MarketingSystem] 現在のスポーン率倍率: {combinedMultiplier:F3}x " +
                $"（アクティブキャンペーン数: {activeCampaigns.Count}）");

            return combinedMultiplier;
        }

        /// <summary>
        /// 指定された来場者タイプに対する全アクティブキャンペーンの
        /// タイプ別スポーン率倍率を集計して返します。
        /// 基本スポーン倍率にタイプ別ブーストが乗算されます。
        /// </summary>
        /// <param name="type">対象の来場者タイプ</param>
        /// <returns>指定タイプの合算スポーン率倍率。キャンペーンがない場合は1.0</returns>
        public float GetTypeSpawnMultiplier(VisitorType type)
        {
            if (activeCampaigns.Count == 0)
            {
                return 1.0f;
            }

            float combinedMultiplier = 1.0f;

            foreach (CampaignData campaign in activeCampaigns)
            {
                if (!campaign.IsActive) continue;

                // 基本スポーン倍率を適用
                float campaignEffect = campaign.SpawnRateMultiplier;

                // タイプ別ブーストがあれば追加で適用
                if (campaign.TypeBoosts != null &&
                    campaign.TypeBoosts.TryGetValue(type, out float typeBoost))
                {
                    campaignEffect *= typeBoost;
                }

                combinedMultiplier *= campaignEffect;
            }

            WebGLOptimizer.LogVerbose(
                $"[MarketingSystem] タイプ別スポーン率倍率: {type} = {combinedMultiplier:F3}x");

            return combinedMultiplier;
        }

        /// <summary>
        /// 過度な広告による期待値膨張ペナルティを計算します。
        /// アクティブキャンペーン数が <see cref="OverAdvertisingThreshold"/> を超えた場合、
        /// 超過分に <see cref="ExpectationPenaltyPerExcess"/> を乗じたペナルティが発生します。
        ///
        /// <example>
        /// 閾値が2で3つのキャンペーンがアクティブな場合:
        /// ペナルティ = (3 - 2) × 5.0 = 5.0
        /// </example>
        /// </summary>
        /// <returns>期待値膨張ペナルティの合計値。閾値以下の場合は0</returns>
        public float GetExpectationPenalty()
        {
            int activeCount = activeCampaigns.Count;

            if (activeCount <= OverAdvertisingThreshold)
            {
                return 0f;
            }

            int excessCount = activeCount - OverAdvertisingThreshold;
            float penalty = excessCount * ExpectationPenaltyPerExcess;

            WebGLOptimizer.LogVerbose(
                $"[MarketingSystem] 期待値膨張ペナルティ: {penalty:F1} " +
                $"（超過キャンペーン数: {excessCount}）");

            return penalty;
        }

        // ========================================================================
        // 日次更新処理
        // ========================================================================

        /// <summary>
        /// 日が変わった際に呼び出される日次更新処理。
        /// TimeManager.OnDayChangedイベントから自動的に呼び出されます。
        ///
        /// <para>
        /// 処理内容:
        /// <list type="number">
        /// <item>各アクティブキャンペーンの残り日数をデクリメント</item>
        /// <item>日額コストの支払い処理</item>
        /// <item>期限切れキャンペーンの終了と履歴移動</item>
        /// <item>期待値膨張の再計算</item>
        /// </list>
        /// </para>
        /// </summary>
        public void ProcessDailyUpdate()
        {
            WebGLOptimizer.LogVerbose(
                $"[MarketingSystem] 日次更新処理を開始します。アクティブキャンペーン数: {activeCampaigns.Count}");

            if (activeCampaigns.Count == 0)
            {
                return;
            }

            var economyManager = GameManager.Instance?.EconomyManager;
            List<CampaignData> expiredCampaigns = new List<CampaignData>();

            // 各キャンペーンの日次処理
            foreach (CampaignData campaign in activeCampaigns)
            {
                if (!campaign.IsActive) continue;

                // 残り日数をデクリメント
                campaign.RemainingDays--;

                WebGLOptimizer.LogVerbose(
                    $"[MarketingSystem] {campaign.Channel}: 残り{campaign.RemainingDays}日");

                // 期限切れチェック
                if (campaign.RemainingDays <= 0)
                {
                    campaign.IsActive = false;
                    expiredCampaigns.Add(campaign);

                    WebGLOptimizer.LogVerbose(
                        $"[MarketingSystem] {campaign.Channel}キャンペーンが期限切れになりました。");
                    continue;
                }

                // 日額コストの支払い
                if (economyManager != null)
                {
                    if (economyManager.CanAfford(campaign.DailyCost))
                    {
                        economyManager.PayExpense(campaign.DailyCost, ExpenseCategory.Other);
                        totalMarketingSpend += campaign.DailyCost;

                        WebGLOptimizer.LogVerbose(
                            $"[MarketingSystem] {campaign.Channel}: 日額¥{campaign.DailyCost:N0}を支払いました。" +
                            $"累計支出: ¥{totalMarketingSpend:N0}");
                    }
                    else
                    {
                        // 資金不足の場合はキャンペーンを強制終了
                        campaign.IsActive = false;
                        expiredCampaigns.Add(campaign);

                        NotificationSystem.Instance?.Notify(
                            $"資金不足のため{GetChannelDisplayName(campaign.Channel)}キャンペーンが中断されました。",
                            NotifLevel.Warning);

                        WebGLOptimizer.LogVerbose(
                            $"[MarketingSystem] {campaign.Channel}: 資金不足のためキャンペーンを強制終了しました。");
                    }
                }
            }

            // 期限切れキャンペーンの処理
            foreach (CampaignData expired in expiredCampaigns)
            {
                activeCampaigns.Remove(expired);
                campaignHistory.Add(expired);

                // イベント発火
                OnCampaignEnded?.Invoke(expired);

                // 通知
                string channelName = GetChannelDisplayName(expired.Channel);
                NotificationSystem.Instance?.Notify(
                    $"{channelName}キャンペーンが終了しました。",
                    NotifLevel.Info);
            }

            // 期待値膨張の再計算
            if (expiredCampaigns.Count > 0)
            {
                RecalculateExpectationInflation();
            }

            WebGLOptimizer.LogVerbose(
                $"[MarketingSystem] 日次更新処理完了。残りアクティブ: {activeCampaigns.Count}, " +
                $"終了: {expiredCampaigns.Count}, 累計支出: ¥{totalMarketingSpend:N0}");
        }

        // ========================================================================
        // コスト計算
        // ========================================================================

        /// <summary>
        /// 指定されたマーケティングチャネルの1日あたりの基本コストを取得します。
        /// </summary>
        /// <param name="channel">マーケティングチャネル</param>
        /// <returns>1日あたりの基本コスト</returns>
        public float GetDailyCost(MarketingChannel channel)
        {
            if (ChannelDailyCosts.TryGetValue(channel, out float cost))
            {
                return cost;
            }

            WebGLOptimizer.LogVerbose(
                $"[MarketingSystem] 不明なチャネル({channel})のコスト要求。デフォルト値を返します。");
            return 0f;
        }

        /// <summary>
        /// 指定されたチャネルと期間に基づくキャンペーンの合計予想コストを計算します。
        /// </summary>
        /// <param name="channel">マーケティングチャネル</param>
        /// <param name="days">キャンペーン期間（日数）</param>
        /// <returns>合計予想コスト（日額コスト × 期間日数）</returns>
        public float EstimateTotalCost(MarketingChannel channel, int days)
        {
            float dailyCost = GetDailyCost(channel);
            float totalCost = dailyCost * days;

            WebGLOptimizer.LogVerbose(
                $"[MarketingSystem] コスト見積り: {channel} × {days}日 = ¥{totalCost:N0}" +
                $"（日額¥{dailyCost:N0}）");

            return totalCost;
        }

        // ========================================================================
        // 表示名・説明文（日本語）
        // ========================================================================

        /// <summary>
        /// マーケティングチャネルの日本語表示名を取得します。
        /// </summary>
        /// <param name="channel">マーケティングチャネル</param>
        /// <returns>チャネルの日本語表示名</returns>
        public string GetChannelDisplayName(MarketingChannel channel)
        {
            switch (channel)
            {
                case MarketingChannel.TvCommercial:
                    return "TV CM";
                case MarketingChannel.WebAdvertising:
                    return "Web広告";
                case MarketingChannel.FlyerDistribution:
                    return "チラシ配布";
                case MarketingChannel.InfluencerInvite:
                    return "インフルエンサー招待";
                default:
                    return "不明なチャネル";
            }
        }

        /// <summary>
        /// マーケティングチャネルの日本語説明文を取得します。
        /// 各チャネルの特徴、効果対象、コスト感を含む説明を返します。
        /// </summary>
        /// <param name="channel">マーケティングチャネル</param>
        /// <returns>チャネルの日本語説明文</returns>
        public string GetChannelDescription(MarketingChannel channel)
        {
            switch (channel)
            {
                case MarketingChannel.TvCommercial:
                    return "テレビCMを放送し、幅広い層の来場者を集めます。" +
                           "パーク評価が高い場合は効果が倍増します。" +
                           "全タイプの来場者に均等に効果があります。" +
                           $"（日額¥{GetDailyCost(channel):N0}）";

                case MarketingChannel.WebAdvertising:
                    return "インターネット広告を配信し、若者やカップルを中心に集客します。" +
                           "SNSやウェブサイトを通じて効率的にリーチできます。" +
                           $"（日額¥{GetDailyCost(channel):N0}）";

                case MarketingChannel.FlyerDistribution:
                    return "チラシを配布し、ファミリー層や子供連れの来場者を集めます。" +
                           "低コストで地域密着型の集客が可能です。" +
                           $"（日額¥{GetDailyCost(channel):N0}）";

                case MarketingChannel.InfluencerInvite:
                    return "人気インフルエンサーを招待し、SNSでの拡散効果を狙います。" +
                           "若者への訴求力が高く、インフルエンサー自身も来場します。" +
                           $"（日額¥{GetDailyCost(channel):N0}）";

                default:
                    return "不明なマーケティングチャネルです。";
            }
        }

        // ========================================================================
        // 内部ヘルパーメソッド
        // ========================================================================

        /// <summary>
        /// 指定された期間が有効かどうかを検証します。
        /// 有効な期間は1日、3日、7日のいずれかです。
        /// </summary>
        /// <param name="durationDays">検証する期間（日数）</param>
        /// <returns>有効な期間の場合はtrue</returns>
        private bool IsValidDuration(int durationDays)
        {
            for (int i = 0; i < ValidDurations.Length; i++)
            {
                if (ValidDurations[i] == durationDays)
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// 指定されたチャネルと期間に対応するスポーン率倍率を取得します。
        /// 定義が見つからない場合はデフォルト値1.0を返します。
        /// </summary>
        /// <param name="channel">マーケティングチャネル</param>
        /// <param name="durationDays">キャンペーン期間</param>
        /// <returns>スポーン率倍率</returns>
        private float GetSpawnMultiplierForDuration(MarketingChannel channel, int durationDays)
        {
            var key = (channel, durationDays);

            if (ChannelDurationMultipliers.TryGetValue(key, out float multiplier))
            {
                return multiplier;
            }

            WebGLOptimizer.LogVerbose(
                $"[MarketingSystem] チャネル{channel}の{durationDays}日間倍率が未定義です。" +
                "デフォルト値1.0を使用します。");
            return 1.0f;
        }

        /// <summary>
        /// 指定されたチャネルの来場者タイプ別ブースト辞書のコピーを取得します。
        /// </summary>
        /// <param name="channel">マーケティングチャネル</param>
        /// <returns>タイプ別ブースト辞書のコピー</returns>
        private Dictionary<VisitorType, float> GetTypeBoostsForChannel(MarketingChannel channel)
        {
            if (ChannelTypeBoosts.TryGetValue(channel, out var boosts))
            {
                return new Dictionary<VisitorType, float>(boosts);
            }

            return new Dictionary<VisitorType, float>();
        }

        /// <summary>
        /// 現在のパーク評価が高評価閾値を超えているかどうかを判定します。
        /// ParkManagerが利用できない場合はfalseを返します。
        /// </summary>
        /// <returns>パーク評価が閾値を超えている場合はtrue</returns>
        private bool IsHighParkRating()
        {
            try
            {
                var parkManager = GameManager.Instance?.ParkManager;
                if (parkManager == null)
                {
                    return false;
                }

                float rating = parkManager.ParkRating;
                bool isHigh = rating > HighParkRatingThreshold;

                WebGLOptimizer.LogVerbose(
                    $"[MarketingSystem] パーク評価: {rating:F1} " +
                    $"(高評価: {(isHigh ? "はい" : "いいえ")})");

                return isHigh;
            }
            catch (Exception ex)
            {
                WebGLOptimizer.LogVerbose(
                    $"[MarketingSystem] パーク評価の取得に失敗しました: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 全アクティブキャンペーンの期待値膨張ペナルティを再計算します。
        /// アクティブキャンペーン数が <see cref="OverAdvertisingThreshold"/> を超えている場合、
        /// 各キャンペーンに超過分のペナルティが設定されます。
        /// </summary>
        private void RecalculateExpectationInflation()
        {
            int activeCount = activeCampaigns.Count;

            if (activeCount <= OverAdvertisingThreshold)
            {
                // 閾値以下の場合、全キャンペーンのペナルティをリセット
                foreach (CampaignData campaign in activeCampaigns)
                {
                    campaign.ExpectationInflation = 0f;
                }
            }
            else
            {
                // 閾値超過の場合、超過分のペナルティを設定
                int excessCount = activeCount - OverAdvertisingThreshold;
                float penalty = excessCount * ExpectationPenaltyPerExcess;

                foreach (CampaignData campaign in activeCampaigns)
                {
                    campaign.ExpectationInflation = penalty;
                }

                WebGLOptimizer.LogVerbose(
                    $"[MarketingSystem] 期待値膨張を再計算: アクティブ={activeCount}, " +
                    $"超過={excessCount}, ペナルティ={penalty:F1}");
            }
        }

        // ========================================================================
        // ユーティリティ / デバッグ
        // ========================================================================

        /// <summary>
        /// 現在のマーケティングシステムの状態を文字列として取得します。
        /// デバッグやUI表示用に使用できます。
        /// </summary>
        /// <returns>システム状態のサマリー文字列（日本語）</returns>
        public string GetStatusSummary()
        {
            string summary = $"--- マーケティングシステム状態 ---\n" +
                             $"アクティブキャンペーン: {activeCampaigns.Count}/{MaxActiveCampaigns}\n" +
                             $"累計マーケティング支出: ¥{totalMarketingSpend:N0}\n" +
                             $"現在のスポーン率倍率: {GetCurrentSpawnMultiplier():F3}x\n" +
                             $"期待値膨張ペナルティ: {GetExpectationPenalty():F1}\n" +
                             $"キャンペーン履歴数: {campaignHistory.Count}\n";

            if (activeCampaigns.Count > 0)
            {
                summary += "\n--- アクティブキャンペーン ---\n";
                for (int i = 0; i < activeCampaigns.Count; i++)
                {
                    CampaignData c = activeCampaigns[i];
                    summary += $"  [{i}] {GetChannelDisplayName(c.Channel)}: " +
                               $"残り{c.RemainingDays}日, " +
                               $"倍率={c.SpawnRateMultiplier:F2}x, " +
                               $"日額¥{c.DailyCost:N0}\n";
                }
            }

            return summary;
        }

        /// <summary>
        /// 指定されたチャネルに現在アクティブなキャンペーンがあるかどうかを確認します。
        /// </summary>
        /// <param name="channel">確認するマーケティングチャネル</param>
        /// <returns>指定チャネルでアクティブなキャンペーンがある場合はtrue</returns>
        public bool HasActiveCampaignForChannel(MarketingChannel channel)
        {
            foreach (CampaignData campaign in activeCampaigns)
            {
                if (campaign.Channel == channel && campaign.IsActive)
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// 全アクティブキャンペーンの1日あたりの合計コストを取得します。
        /// </summary>
        /// <returns>1日あたりの合計マーケティングコスト</returns>
        public float GetTotalDailyCost()
        {
            float total = 0f;
            foreach (CampaignData campaign in activeCampaigns)
            {
                if (campaign.IsActive)
                {
                    total += campaign.DailyCost;
                }
            }
            return total;
        }

        /// <summary>
        /// 利用可能な全マーケティングチャネルの情報を取得します。
        /// UI表示やキャンペーン選択画面で使用されます。
        /// </summary>
        /// <returns>チャネル情報のリスト（チャネル、表示名、説明、日額コスト）</returns>
        public List<(MarketingChannel channel, string displayName, string description, float dailyCost)>
            GetAvailableChannels()
        {
            var channels =
                new List<(MarketingChannel channel, string displayName, string description, float dailyCost)>();

            foreach (MarketingChannel channel in Enum.GetValues(typeof(MarketingChannel)))
            {
                channels.Add((
                    channel,
                    GetChannelDisplayName(channel),
                    GetChannelDescription(channel),
                    GetDailyCost(channel)
                ));
            }

            return channels;
        }

        /// <summary>
        /// 指定されたチャネルと期間のキャンペーン効果のプレビュー情報を取得します。
        /// キャンペーン開始前の確認画面で使用されます。
        /// </summary>
        /// <param name="channel">マーケティングチャネル</param>
        /// <param name="durationDays">キャンペーン期間</param>
        /// <returns>効果プレビュー文字列（日本語）</returns>
        public string GetCampaignPreview(MarketingChannel channel, int durationDays)
        {
            float dailyCost = GetDailyCost(channel);
            float totalCost = EstimateTotalCost(channel, durationDays);
            float multiplier = GetSpawnMultiplierForDuration(channel, durationDays);

            // パーク評価によるTV CMボーナスの確認
            bool hasParkRatingBonus = channel == MarketingChannel.TvCommercial && IsHighParkRating();
            if (hasParkRatingBonus)
            {
                multiplier = 1f + (multiplier - 1f) * 2f;
            }

            string durationText = durationDays == 1 ? "1日間" : $"{durationDays}日間";
            string preview = $"【{GetChannelDisplayName(channel)}キャンペーン】\n" +
                             $"期間: {durationText}\n" +
                             $"日額コスト: ¥{dailyCost:N0}\n" +
                             $"合計コスト: ¥{totalCost:N0}\n" +
                             $"来場者増加倍率: {multiplier:F2}x\n";

            if (hasParkRatingBonus)
            {
                preview += "※ パーク高評価ボーナス適用中（効果2倍）\n";
            }

            // タイプ別ブースト情報
            var typeBoosts = GetTypeBoostsForChannel(channel);
            if (typeBoosts.Count > 0)
            {
                preview += "ターゲット層: ";
                var boostTexts = new List<string>();
                foreach (var kvp in typeBoosts)
                {
                    boostTexts.Add($"{kvp.Key}(×{kvp.Value:F1})");
                }
                preview += string.Join(", ", boostTexts) + "\n";
            }
            else
            {
                preview += "ターゲット層: 全タイプ均等\n";
            }

            // 過度な広告の警告
            int campaignsAfterStart = activeCampaigns.Count + 1;
            if (campaignsAfterStart > OverAdvertisingThreshold)
            {
                int excess = campaignsAfterStart - OverAdvertisingThreshold;
                float penalty = excess * ExpectationPenaltyPerExcess;
                preview += $"⚠ 過度な広告警告: 満足度ペナルティ -{penalty:F0} が発生します\n";
            }

            return preview;
        }
    }
}
