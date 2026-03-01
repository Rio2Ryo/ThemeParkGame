// ============================================================
// ThemeParkGame - Attraction (Runtime Instance)
// アトラクションのランタイムインスタンス
// 乗車サイクル・故障・アップグレード・収益を管理する
// ============================================================

using System;
using System.Collections.Generic;
using UnityEngine;
using ThemeParkGame.Core;
using ThemeParkGame.Economy;
using ThemeParkGame.Visitor;

namespace ThemeParkGame.Attraction
{
    /// <summary>
    /// アトラクション運転サイクルの状態。
    /// Loading → Running → Unloading → WaitingForRiders のループで動作する。
    /// </summary>
    public enum RideCycleState
    {
        /// <summary>乗客待ち（キューから乗客を受け付ける状態）</summary>
        WaitingForRiders,
        /// <summary>乗車中（キューからライドへ乗客を移動中）</summary>
        Loading,
        /// <summary>運転中（ライドが動作中）</summary>
        Running,
        /// <summary>降車中（乗客がライドから降りている最中）</summary>
        Unloading,
        /// <summary>故障中（メカニックの修理待ち）</summary>
        BrokenDown,
        /// <summary>事故発生中（故障を放置した結果の重大事態）</summary>
        Accident
    }

    /// <summary>
    /// アトラクションのランタイムインスタンス。
    /// AttractionDataから定義を読み込み、実際のゲームプレイ中の状態を管理する。
    ///
    /// 主な責務:
    /// - 乗車サイクル管理（乗車→運転→降車のループ）
    /// - 待ち行列管理（来場者のFIFOキュー）
    /// - 故障・事故システム（メンテナンス不足で故障率上昇、放置で事故）
    /// - チケット価格と収益トラッキング
    /// - アップグレードレベル管理
    /// - 最近の乗客からの満足度評価
    /// </summary>
    [RequireComponent(typeof(BoxCollider))]
    public class Attraction : FacilityBase
    {
        // ---- データ参照 ----

        [Header("Attraction Data")]
        [SerializeField] private AttractionData attractionData;
        public AttractionData Data => attractionData;

        public override FacilityType FacilityType => FacilityType.Attraction;
        public override string DisplayName => attractionData != null ? attractionData.NameJP : "Unknown";

        // ---- 運転状態 ----

        [Header("Runtime State (Read Only)")]
        [SerializeField] private RideCycleState currentCycleState = RideCycleState.WaitingForRiders;
        public RideCycleState CurrentCycleState => currentCycleState;

        /// <summary>稼働中か（故障・事故・コンディション強制停止でなく、アクティブ状態）</summary>
        public bool IsOperating => IsActive && currentCycleState != RideCycleState.BrokenDown
                                             && currentCycleState != RideCycleState.Accident
                                             && !_conditionForcedStop && !IsUnderOverhaul;

        /// <summary>故障中か</summary>
        public bool IsBrokenDown => currentCycleState == RideCycleState.BrokenDown;

        /// <summary>事故発生中か</summary>
        public bool HasAccident => currentCycleState == RideCycleState.Accident;

        // ---- 乗客管理 ----

        /// <summary>現在ライドに乗っている来場者IDリスト</summary>
        private readonly List<int> _currentRiders = new List<int>();
        public IReadOnlyList<int> CurrentRiders => _currentRiders;
        public int CurrentRiderCount => _currentRiders.Count;

        /// <summary>待ち行列（FIFO）</summary>
        private readonly Queue<int> _visitorQueue = new Queue<int>();

        /// <summary>ファストパス優先キュー（FIFO）。通常キューより先に搭乗</summary>
        private readonly Queue<int> _fastPassQueue = new Queue<int>();

        /// <summary>全体の待ち行列長（通常+ファストパス）</summary>
        public int QueueLength => _visitorQueue.Count + _fastPassQueue.Count;

        /// <summary>通常キューの長さ</summary>
        public int NormalQueueLength => _visitorQueue.Count;

        /// <summary>ファストパスキューの長さ</summary>
        public int FastPassQueueLength => _fastPassQueue.Count;

        /// <summary>待ち行列の最大長（これを超えると来場者は並ばない）</summary>
        [Header("Queue Settings")]
        [SerializeField] private int maxQueueLength = 20;
        public int MaxQueueLength
        {
            get => maxQueueLength;
            set => maxQueueLength = Mathf.Max(1, value);
        }

        /// <summary>
        /// キューテーマレベル。投資で待ち列体験を改善する。
        /// 0=なし, 1=基本テーマ化(ペナルティ30%軽減), 2=フル演出(ペナルティ70%軽減)
        /// </summary>
        [Header("Queue Entertainment")]
        [SerializeField] private int queueThemeLevel;
        public int QueueThemeLevel
        {
            get => queueThemeLevel;
            set => queueThemeLevel = Mathf.Clamp(value, 0, 2);
        }

        /// <summary>キューエンターテイナーが配置されているか</summary>
        public bool HasQueueEntertainer { get; set; }

        /// <summary>待ち列のペナルティ軽減率（0.0～1.0）。テーマ化+エンターテイナー効果の合算。</summary>
        public float QueuePenaltyReduction
        {
            get
            {
                float reduction = queueThemeLevel switch
                {
                    1 => 0.3f,  // 基本テーマ化: 30%軽減
                    2 => 0.7f,  // フル演出: 70%軽減
                    _ => 0f
                };
                if (HasQueueEntertainer) reduction = Mathf.Min(1f, reduction + 0.25f);
                return reduction;
            }
        }

        /// <summary>推定待ち時間（秒）。来場者がキュー選択の参考にする。</summary>
        public float EstimatedWaitTime
        {
            get
            {
                if (attractionData == null || !IsOperating) return float.MaxValue;
                int capacity = EffectiveCapacity;
                if (capacity <= 0) return float.MaxValue;
                float cyclesNeeded = (float)QueueLength / capacity;
                return cyclesNeeded * (attractionData.RideDuration + LoadUnloadDuration * 2f);
            }
        }

        /// <summary>
        /// キューテーマをアップグレードする。
        /// </summary>
        /// <returns>成功ならtrue</returns>
        public bool TryUpgradeQueueTheme()
        {
            if (queueThemeLevel >= 2) return false;
            queueThemeLevel++;
            WebGLOptimizer.LogVerbose($"[Attraction] キューテーマ Lv.{queueThemeLevel}: {DisplayName}");
            return true;
        }

        /// <summary>キューテーマのアップグレードコスト</summary>
        public int GetQueueThemeUpgradeCost()
        {
            return queueThemeLevel switch
            {
                0 => 500,   // なし → 基本テーマ化
                1 => 1500,  // 基本 → フル演出
                _ => -1     // 最大レベル
            };
        }

        // ---- チケット・収益 ----

        /// <summary>
        /// プレイヤーが設定するチケット価格。
        /// 推奨価格より高すぎると来場者の不満が増す。
        /// </summary>
        [Header("Pricing")]
        [SerializeField] private int ticketPrice = 300;
        public int TicketPrice
        {
            get => ticketPrice;
            set
            {
                ticketPrice = Mathf.Max(0, value);
                GameEvents.FirePriceChanged(FacilityId, ticketPrice);
            }
        }

        /// <summary>本日の売上</summary>
        public float TodayRevenue { get; private set; }

        /// <summary>累計売上</summary>
        public float TotalRevenue { get; private set; }

        /// <summary>本日の乗車人数</summary>
        public int TodayRiderCount { get; private set; }

        /// <summary>累計乗車人数</summary>
        public int TotalRiderCount { get; private set; }

        // ---- アップグレード ----

        /// <summary>現在のアップグレードレベル（0=基本、1～3=アップグレード済み）</summary>
        [Header("Upgrade")]
        [SerializeField] private int upgradeLevel;
        public int UpgradeLevel => upgradeLevel;

        /// <summary>アップグレード適用後の実効興奮度</summary>
        public float EffectiveExcitement =>
            attractionData != null ? attractionData.GetEffectiveExcitement(upgradeLevel) : 0f;

        /// <summary>アップグレード適用後の実効定員</summary>
        public int EffectiveCapacity =>
            attractionData != null ? attractionData.GetEffectiveCapacity(upgradeLevel) : 0;

        // ---- 故障・メンテナンス ----

        /// <summary>
        /// 現在の故障確率（0.0～1.0）。
        /// メンテナンスなしで時間経過とともに上昇し、
        /// メカニックの点検で基本値にリセットされる。
        /// </summary>
        [Header("Maintenance")]
        [SerializeField] private float currentBreakdownProbability;
        public float CurrentBreakdownProbability => currentBreakdownProbability;

        /// <summary>前回メカニックが点検した時刻</summary>
        private float _lastMaintenanceTime;

        /// <summary>故障が発生した時刻（故障中のみ有効）</summary>
        private float _breakdownStartTime;

        /// <summary>累計故障回数</summary>
        public int TotalBreakdownCount { get; private set; }

        /// <summary>累計事故回数</summary>
        public int TotalAccidentCount { get; private set; }

        // ---- 経年劣化（コンディション）システム ----

        /// <summary>
        /// アトラクションのコンディション（0～100%）。
        /// 運行サイクルごとに低下し、メンテナンスで回復する。
        /// 50%以下で故障率上昇、20%以下で強制運行停止。
        /// </summary>
        [Header("Condition")]
        [SerializeField] private float condition = 100f;
        public float Condition => condition;

        /// <summary>コンディション20%以下で強制停止中か</summary>
        public bool IsConditionCritical => condition <= 20f;

        /// <summary>コンディション低下による強制停止中か</summary>
        private bool _conditionForcedStop;
        public bool IsConditionForcedStop => _conditionForcedStop;

        /// <summary>オーバーホール中か（メカニックが大規模修繕実施中）</summary>
        public bool IsUnderOverhaul { get; set; }

        // ---- 満足度 ----

        /// <summary>
        /// 最近の乗客からの満足度評価（0.0～1.0）。
        /// 直近の乗車回数分の移動平均で算出する。
        /// 興奮度・待ち時間・価格設定・天候などが影響する。
        /// </summary>
        [Header("Satisfaction")]
        [SerializeField] private float satisfactionRating = 0.5f;
        public float SatisfactionRating => satisfactionRating;

        /// <summary>満足度計算用の直近評価バッファ</summary>
        private readonly Queue<float> _recentSatisfactionScores = new Queue<float>();

        /// <summary>満足度移動平均のウィンドウサイズ</summary>
        private const int SatisfactionWindowSize = 50;

        // ---- サイクルタイマー ----

        /// <summary>現在のサイクル状態の経過時間</summary>
        private float _cycleTimer;

        /// <summary>乗車・降車にかかる時間（秒）</summary>
        private const float LoadUnloadDuration = 2f;

        // ---- ランタイム初期化 ----

        /// <summary>
        /// ランタイムでAttractionDataを設定する。
        /// プレハブ/Inspector設定なしで動作させる場合に使用。
        /// </summary>
        public void SetAttractionData(AttractionData data)
        {
            attractionData = data;
        }

        // ---- 初期化 ----

        protected override void Awake()
        {
            base.Awake();
        }

        protected override void Start()
        {
            base.Start();
            if (attractionData != null)
            {
                GridSize = attractionData.Size;
                BuildCost = attractionData.BuildCost;
                currentBreakdownProbability = attractionData.BaseBreakdownRate;
                _lastMaintenanceTime = Time.time;
                condition = 100f;
                _conditionForcedStop = false;

                if (ticketPrice <= 0)
                    ticketPrice = attractionData.SuggestedTicketPrice;
            }
        }

        // ---- メインループ ----

        protected override void Update()
        {
            base.Update();
            if (!IsActive || attractionData == null) return;

            UpdateConditionCheck();
            UpdateBreakdownProbability();
            UpdateRideCycle();
        }

        // ---- 乗車サイクル管理 ----

        /// <summary>
        /// 乗車サイクルのメインステートマシン。
        /// WaitingForRiders → Loading → Running → Unloading → WaitingForRiders
        /// のループで動作する。
        /// </summary>
        private void UpdateRideCycle()
        {
            switch (currentCycleState)
            {
                case RideCycleState.WaitingForRiders:
                    UpdateWaitingForRiders();
                    break;

                case RideCycleState.Loading:
                    UpdateLoading();
                    break;

                case RideCycleState.Running:
                    UpdateRunning();
                    break;

                case RideCycleState.Unloading:
                    UpdateUnloading();
                    break;

                case RideCycleState.BrokenDown:
                    UpdateBrokenDown();
                    break;

                case RideCycleState.Accident:
                    // 事故状態は外部からの解決を待つ
                    break;
            }
        }

        /// <summary>
        /// 乗客待ち状態の更新。
        /// キューに乗客がいれば乗車フェーズに移行する。
        /// 最低1人でも乗客がいれば発車する（効率より回転率を優先）。
        /// </summary>
        private void UpdateWaitingForRiders()
        {
            if (_visitorQueue.Count > 0)
            {
                TransitionTo(RideCycleState.Loading);
            }
        }

        /// <summary>
        /// 乗車フェーズ。キューから定員まで乗客をライドに移す。
        /// 各来場者の VisitorAI に搭乗開始を通知する。
        /// </summary>
        private void UpdateLoading()
        {
            _cycleTimer += Time.deltaTime;

            // キューから定員分の乗客をライドに移す（ファストパスキュー優先）
            int capacity = EffectiveCapacity;

            // まずファストパスキューから乗車
            while (_currentRiders.Count < capacity && _fastPassQueue.Count > 0)
            {
                int vid = _fastPassQueue.Dequeue();
                _currentRiders.Add(vid);
                NotifyVisitorStartRiding(vid);
            }

            // 残り座席に通常キューから乗車
            while (_currentRiders.Count < capacity && _visitorQueue.Count > 0)
            {
                int vid = _visitorQueue.Dequeue();
                _currentRiders.Add(vid);
                NotifyVisitorStartRiding(vid);
            }

            // 乗車時間が経過したら運転開始
            if (_cycleTimer >= LoadUnloadDuration)
            {
                // アトラクション動作音を再生
                if (AudioManager.Instance != null)
                    AudioManager.Instance.PlayAttractionRideSE();

                TransitionTo(RideCycleState.Running);
            }
        }

        /// <summary>
        /// 運転フェーズ。ライドが動作中。
        /// 運転中に故障判定を行う。
        /// </summary>
        private void UpdateRunning()
        {
            _cycleTimer += Time.deltaTime;

            // 運転中の故障判定（毎フレームチェック）
            // currentBreakdownProbability は「1回の運行サイクルあたりの故障確率」なので、
            // RideDurationで割って「1秒あたりの故障率」に変換してからdeltaTimeを掛ける。
            // これにより BaseBreakdownRate=0.02 は「1回の運行で2%の故障確率」として正しく動作する。
            float rideDuration = attractionData.RideDuration;
            float perSecondRate = rideDuration > 0f
                ? currentBreakdownProbability / rideDuration
                : currentBreakdownProbability;
            if (UnityEngine.Random.value < perSecondRate * Time.deltaTime)
            {
                TriggerBreakdown();
                return;
            }

            // 運転時間が経過したら降車フェーズへ
            if (_cycleTimer >= rideDuration)
            {
                TransitionTo(RideCycleState.Unloading);
            }
        }

        /// <summary>
        /// 降車フェーズ。乗客を降ろし、満足度評価と収益計上を行う。
        /// </summary>
        private void UpdateUnloading()
        {
            _cycleTimer += Time.deltaTime;

            if (_cycleTimer >= LoadUnloadDuration)
            {
                // 乗客を全員降ろして評価・収益を処理する
                ProcessRideCompletion();
                TransitionTo(RideCycleState.WaitingForRiders);
            }
        }

        /// <summary>
        /// 故障状態の更新。
        /// 一定時間修理されないと事故に発展する。
        /// </summary>
        private void UpdateBrokenDown()
        {
            float elapsedSinceBreakdown = Time.time - _breakdownStartTime;

            // 故障放置が限界時間を超えたら事故発生
            if (elapsedSinceBreakdown >= attractionData.TimeToAccidentAfterBreakdown)
            {
                TriggerAccident();
            }
        }

        /// <summary>サイクル状態を遷移する</summary>
        private void TransitionTo(RideCycleState newState)
        {
            currentCycleState = newState;
            _cycleTimer = 0f;
        }

        // ---- 乗車完了処理 ----

        /// <summary>
        /// 1回の乗車サイクル完了時の処理。
        /// 収益計上・満足度評価・嘔吐判定を行い、乗客を解放する。
        /// </summary>
        private void ProcessRideCompletion()
        {
            int riderCount = _currentRiders.Count;
            if (riderCount == 0) return;

            // 収益計上（ファストパス/フリーパス保有者はチケット料金免除）
            float cycleRevenue = 0f;
            foreach (int vid in _currentRiders)
            {
                bool ticketExempt = false;
                if (Economy.FastPassSystem.Instance != null && attractionData != null)
                {
                    ticketExempt = Economy.FastPassSystem.Instance.TryConsumeRideTicket(
                        vid, FacilityId, attractionData.PrimaryThemeZone);
                }

                if (!ticketExempt)
                {
                    cycleRevenue += ticketPrice;
                }
            }

            TodayRevenue += cycleRevenue;
            TotalRevenue += cycleRevenue;
            TodayRiderCount += riderCount;
            TotalRiderCount += riderCount;

            if (cycleRevenue > 0f && GameManager.Instance != null && GameManager.Instance.EconomyManager != null)
            {
                GameManager.Instance.EconomyManager.AddRevenue(
                    cycleRevenue, RevenueCategory.AttractionFee, FacilityId);
            }
            else if (cycleRevenue > 0f)
            {
                // EconomyManager未初期化時のフォールバック
                GameEvents.FireRevenueEarned(cycleRevenue);
            }

            // 各乗客の満足度評価・搭乗効果適用・嘔吐判定
            float excitementGain = EffectiveExcitement;
            float nauseaGain = GetEffectiveNauseaFactor() * 30f; // 嘔吐因子を吐き気増加量に変換

            foreach (int vid in _currentRiders)
            {
                float satisfaction = CalculateRiderSatisfaction(vid);
                RecordSatisfaction(satisfaction);

                // VisitorAI に搭乗完了を通知（RidingAttraction → Idle + 効果適用 + 記憶記録）
                NotifyVisitorFinishRiding(vid, excitementGain, nauseaGain,
                    satisfaction * 100f, FacilityId, DisplayName);
            }

            // 搭乗完了時に歓声SE
            if (AudioManager.Instance != null && riderCount > 0)
                AudioManager.Instance.PlayCheerSE();

            _currentRiders.Clear();

            // 経年劣化: 1回の運行サイクルでコンディション低下
            ApplyConditionDegradation();
        }

        /// <summary>
        /// 乗客1人の満足度を算出する。
        /// 興奮度・価格妥当性・待ち時間が影響する。
        /// </summary>
        /// <param name="visitorId">来場者ID</param>
        /// <returns>0.0～1.0の満足度スコア</returns>
        private float CalculateRiderSatisfaction(int visitorId)
        {
            if (attractionData == null) return 0.5f;

            // 基本満足度: 興奮度を10段階スケールから0～1に正規化
            float baseSatisfaction = EffectiveExcitement / 10f;

            // 価格妥当性による補正
            // 推奨価格の1.5倍以上で不満、推奨以下で好印象
            float priceRatio = (float)ticketPrice / Mathf.Max(1, attractionData.SuggestedTicketPrice);
            float priceModifier = 1f;
            if (priceRatio > 1.5f)
                priceModifier = 0.5f; // 高すぎる
            else if (priceRatio > 1.2f)
                priceModifier = 0.8f; // やや高い
            else if (priceRatio <= 0.8f)
                priceModifier = 1.1f; // お得感

            // 待ち行列の長さによる補正（長い待ち時間は不満に繋がる）
            float queueModifier = 1f;
            if (QueueLength > maxQueueLength * 0.8f)
                queueModifier = 0.7f; // 長い待ち行列
            else if (QueueLength > maxQueueLength * 0.5f)
                queueModifier = 0.85f;

            float satisfaction = Mathf.Clamp01(baseSatisfaction * priceModifier * queueModifier);
            return satisfaction;
        }

        /// <summary>満足度を記録し、移動平均を更新する</summary>
        private void RecordSatisfaction(float score)
        {
            _recentSatisfactionScores.Enqueue(score);
            while (_recentSatisfactionScores.Count > SatisfactionWindowSize)
            {
                _recentSatisfactionScores.Dequeue();
            }

            // 移動平均を再計算
            float sum = 0f;
            foreach (float s in _recentSatisfactionScores)
            {
                sum += s;
            }
            satisfactionRating = sum / _recentSatisfactionScores.Count;
        }

        /// <summary>アップグレードレベルを考慮した実効嘔吐率を計算する</summary>
        private float GetEffectiveNauseaFactor()
        {
            float nausea = attractionData.NauseaFactor;
            for (int i = 1; i <= upgradeLevel; i++)
            {
                var upgrade = attractionData.GetUpgradeLevel(i);
                if (upgrade != null)
                    nausea *= upgrade.NauseaMultiplier;
            }
            return Mathf.Clamp01(nausea);
        }

        // ---- 故障・事故システム ----

        /// <summary>
        /// 故障確率を時間経過に応じて更新する。
        /// 前回のメカニック点検からの経過時間が長いほど故障確率が上昇する。
        /// アップグレードによる故障率軽減も反映する。
        ///
        /// 単位: 1回の運行サイクルあたりの故障確率（0.0～1.0）。
        /// UpdateRunning()でRideDurationで割って秒単位レートに変換して使用する。
        /// </summary>
        private void UpdateBreakdownProbability()
        {
            if (IsBrokenDown || HasAccident) return;

            float timeSinceMaintenance = Time.time - _lastMaintenanceTime;
            float progress = Mathf.Clamp01(timeSinceMaintenance / attractionData.TimeToMaxBreakdownRate);

            // 基本故障率から最大故障率（0.5）まで線形補間
            float baseRate = attractionData.BaseBreakdownRate;
            float maxRate = 0.5f;
            currentBreakdownProbability = Mathf.Lerp(baseRate, maxRate, progress);

            // アップグレードによる故障率軽減
            for (int i = 1; i <= upgradeLevel; i++)
            {
                var upgrade = attractionData.GetUpgradeLevel(i);
                if (upgrade != null)
                    currentBreakdownProbability *= upgrade.BreakdownRateMultiplier;
            }

            // コンディション低下による故障率増加
            // コンディション50%以下で故障率が急上昇する
            if (condition < 50f)
            {
                float conditionPenalty = 1f + (50f - condition) / 50f * 3f; // 50%で×1.0、0%で×4.0
                currentBreakdownProbability *= conditionPenalty;
            }

            // 難易度による故障率補正（Easy:0.6 Normal:1.0 Hard:1.5）
            currentBreakdownProbability *= GameManager.GetBreakdownRateMultiplier(
                GameManager.Instance != null ? GameManager.Instance.CurrentDifficulty : GameDifficulty.Normal);
        }

        /// <summary>
        /// 故障を発生させる。
        /// 運転中に故障した場合、乗客は強制的に降ろされる。
        /// </summary>
        private void TriggerBreakdown()
        {
            if (IsBrokenDown || HasAccident) return;

            // 乗客がいる場合は緊急降車（満足度ペナルティ付き）
            if (_currentRiders.Count > 0)
            {
                foreach (int vid in _currentRiders)
                {
                    RecordSatisfaction(0.1f);
                    // 故障時: 興奮0、吐き気大、満足度マイナスで強制降車
                    NotifyVisitorFinishRiding(vid, 0f, 15f, -30f, FacilityId, DisplayName);
                }
                _currentRiders.Clear();
            }

            // 待ち行列の来場者も解放する（WaitingInQueue → Idle）
            while (_fastPassQueue.Count > 0)
            {
                int vid = _fastPassQueue.Dequeue();
                NotifyVisitorQueueAbandoned(vid);
            }
            while (_visitorQueue.Count > 0)
            {
                int vid = _visitorQueue.Dequeue();
                NotifyVisitorQueueAbandoned(vid);
            }

            currentCycleState = RideCycleState.BrokenDown;
            _breakdownStartTime = Time.time;
            TotalBreakdownCount++;

            GameEvents.FireAttractionBrokenDown(FacilityId);
            WebGLOptimizer.LogVerbose($"[Attraction] 故障発生: {DisplayName} (ID: {FacilityId})");
        }

        /// <summary>
        /// 事故を発生させる（故障を長時間放置した結果）。
        /// 事故は来場者の安全に影響し、パーク評価が大きく下がる。
        /// </summary>
        private void TriggerAccident()
        {
            if (HasAccident) return;

            currentCycleState = RideCycleState.Accident;
            TotalAccidentCount++;

            // 待ち行列の来場者を全員追い出す
            while (_fastPassQueue.Count > 0)
            {
                int vid = _fastPassQueue.Dequeue();
                NotifyVisitorQueueAbandoned(vid);
            }
            while (_visitorQueue.Count > 0)
            {
                int vid = _visitorQueue.Dequeue();
                NotifyVisitorQueueAbandoned(vid);
            }

            GameEvents.FireAttractionAccident(FacilityId);
            WebGLOptimizer.LogWarning($"[Attraction] 事故発生: {DisplayName} (ID: {FacilityId})");
        }

        /// <summary>
        /// メカニックが修理を完了した時に呼ばれる。
        /// 内部的にRequestStateChangeへ委譲する。
        /// </summary>
        public void OnRepaired()
        {
            RequestStateChange(RideCycleState.WaitingForRiders);
        }

        /// <summary>
        /// 外部システム（AttractionManager等）からの状態変更リクエスト。
        /// Attractionの内部FSMが遷移の妥当性を検証し、実行する。
        /// AttractionManagerが直接状態を操作する代わりに、このメソッドを経由することで
        /// 二重ステートマシンの競合を防止する。
        /// </summary>
        /// <returns>遷移が実行された場合true</returns>
        public bool RequestStateChange(RideCycleState requestedState)
        {
            switch (requestedState)
            {
                case RideCycleState.WaitingForRiders:
                    // 修理完了による復旧
                    if (!IsBrokenDown && !HasAccident) return false;
                    currentCycleState = RideCycleState.WaitingForRiders;
                    _cycleTimer = 0f;
                    _lastMaintenanceTime = Time.time;
                    if (attractionData != null)
                        currentBreakdownProbability = attractionData.BaseBreakdownRate;
                    GameEvents.FireAttractionRepaired(FacilityId);
                    WebGLOptimizer.LogVerbose($"[Attraction] 修理完了: {DisplayName} (ID: {FacilityId})");
                    return true;

                case RideCycleState.BrokenDown:
                    // 外部からの故障トリガー
                    if (IsBrokenDown || HasAccident) return false;
                    TriggerBreakdown();
                    return true;

                default:
                    WebGLOptimizer.LogVerbose(
                        $"[Attraction] 未対応の状態変更リクエスト: {requestedState} (ID: {FacilityId})");
                    return false;
            }
        }

        /// <summary>
        /// メカニックが定期点検を行った時に呼ばれる。
        /// 故障確率を基本値にリセットし、コンディションを部分回復する。
        /// </summary>
        public void OnMaintenancePerformed()
        {
            _lastMaintenanceTime = Time.time;
            currentBreakdownProbability = attractionData.BaseBreakdownRate;

            // 点検でコンディション+20回復（上限100）
            condition = Mathf.Min(100f, condition + 20f);
            WebGLOptimizer.LogVerbose($"[Attraction] 定期点検完了: {DisplayName} (ID: {FacilityId}, コンディション: {condition:F0}%)");
        }

        /// <summary>
        /// オーバーホール完了時に呼ばれる（メカニックの大規模修繕）。
        /// コンディションを100%に完全回復し、故障確率もリセットする。
        /// </summary>
        public void OnOverhaulCompleted()
        {
            condition = 100f;
            _conditionForcedStop = false;
            IsUnderOverhaul = false;
            _lastMaintenanceTime = Time.time;
            currentBreakdownProbability = attractionData.BaseBreakdownRate;
            WebGLOptimizer.LogVerbose($"[Attraction] オーバーホール完了: {DisplayName} (ID: {FacilityId}, コンディション: 100%)");
        }

        // ---- 経年劣化システム ----

        /// <summary>
        /// コンディション強制停止チェック。
        /// コンディション20%以下でオーバーホールが必要。
        /// </summary>
        private void UpdateConditionCheck()
        {
            if (_conditionForcedStop || IsUnderOverhaul) return;

            if (condition <= 20f && !IsBrokenDown && !HasAccident)
            {
                _conditionForcedStop = true;

                // キューの来場者を解放
                while (_fastPassQueue.Count > 0)
                {
                    int vid = _fastPassQueue.Dequeue();
                    NotifyVisitorQueueAbandoned(vid);
                }
                while (_visitorQueue.Count > 0)
                {
                    int vid = _visitorQueue.Dequeue();
                    NotifyVisitorQueueAbandoned(vid);
                }

                GameManager.Instance?.ShowNotification(
                    $"{DisplayName} のコンディションが危険水準！オーバーホールが必要です", NotifLevel.Warning);
                WebGLOptimizer.LogWarning($"[Attraction] コンディション強制停止: {DisplayName} (ID: {FacilityId}, コンディション: {condition:F0}%)");
            }
        }

        /// <summary>
        /// 1運行サイクルごとのコンディション低下処理。
        /// 耐久度(Durability)が高いほど劣化が遅い。
        /// アップグレードによる故障率軽減もコンディション維持に寄与する。
        /// </summary>
        private void ApplyConditionDegradation()
        {
            if (attractionData == null) return;

            float loss = attractionData.ConditionLossPerCycle;

            // 耐久度で劣化を軽減（Durability 1.0=標準、2.0=半減）
            loss /= Mathf.Max(0.1f, attractionData.Durability);

            // アップグレードによる劣化軽減
            for (int i = 1; i <= upgradeLevel; i++)
            {
                var upgrade = attractionData.GetUpgradeLevel(i);
                if (upgrade != null)
                    loss *= upgrade.BreakdownRateMultiplier; // 故障率軽減はコンディション維持にも寄与
            }

            condition = Mathf.Max(0f, condition - loss);

            if (condition <= 50f && condition + loss > 50f)
            {
                GameManager.Instance?.ShowNotification(
                    $"{DisplayName} のコンディションが50%を下回りました", NotifLevel.Info);
            }
        }

        // ---- アップグレード ----

        /// <summary>
        /// アトラクションをアップグレードする。
        /// </summary>
        /// <returns>アップグレード成功ならtrue</returns>
        public bool TryUpgrade()
        {
            if (attractionData == null) return false;

            int nextLevel = upgradeLevel + 1;
            var upgradeData = attractionData.GetUpgradeLevel(nextLevel);
            if (upgradeData == null)
            {
                WebGLOptimizer.LogWarning($"[Attraction] これ以上アップグレードできません: {DisplayName} (Lv.{upgradeLevel})");
                return false;
            }

            // アップグレード費用はEconomyManager経由で支払われることを想定
            upgradeLevel = nextLevel;

            // アップグレードでコンディション+10回復（ボーナス）
            condition = Mathf.Min(100f, condition + 10f);

            // アップグレード後の外観に切り替え
            if (upgradeData.UpgradedVisualPrefab != null)
            {
                ApplyUpgradedVisual(upgradeData.UpgradedVisualPrefab);
            }

            GameEvents.FireAttractionUpgraded(FacilityId);
            WebGLOptimizer.LogVerbose($"[Attraction] アップグレード完了: {DisplayName} → Lv.{upgradeLevel}");
            return true;
        }

        /// <summary>次のアップグレード費用を取得する（アップグレード不可の場合は-1）</summary>
        public int GetNextUpgradeCost()
        {
            if (attractionData == null) return -1;
            var upgradeData = attractionData.GetUpgradeLevel(upgradeLevel + 1);
            return upgradeData?.UpgradeCost ?? -1;
        }

        /// <summary>アップグレード後のビジュアルを適用する</summary>
        private void ApplyUpgradedVisual(GameObject prefab)
        {
            // 既存のビジュアル子オブジェクトを削除
            for (int i = transform.childCount - 1; i >= 0; i--)
            {
                Destroy(transform.GetChild(i).gameObject);
            }

            // 新しいビジュアルをインスタンス化
            if (prefab != null)
            {
                Instantiate(prefab, transform.position, transform.rotation, transform);
            }
        }

        // ---- 来場者インタラクション（FacilityBase実装） ----

        /// <summary>
        /// 来場者がこのアトラクションを利用可能かを判定する。
        /// 稼働中で、キューに空きがあり、故障・事故中でないことが条件。
        /// </summary>
        public override bool CanAcceptVisitor(int visitorId)
        {
            if (!IsOperating) return false;
            if (QueueLength >= maxQueueLength) return false;
            return true;
        }

        /// <summary>
        /// 来場者を待ち行列に追加する。
        /// ファストパス/フリーパス/ゾーンパス保有者は優先キューに入る。
        /// </summary>
        public override bool OnVisitorArrive(int visitorId)
        {
            if (!CanAcceptVisitor(visitorId)) return false;

            // ファストパス/フリーパス/ゾーンパスで優先キューに入るか判定
            bool hasPriority = false;
            if (Economy.FastPassSystem.Instance != null && attractionData != null)
            {
                hasPriority = Economy.FastPassSystem.Instance.HasPriorityAccess(
                    visitorId, FacilityId, attractionData.PrimaryThemeZone);
            }

            if (hasPriority)
            {
                _fastPassQueue.Enqueue(visitorId);
            }
            else
            {
                _visitorQueue.Enqueue(visitorId);
            }
            return true;
        }

        /// <summary>
        /// 来場者がアトラクションから退出する。
        /// （乗車完了時や故障時の強制退出で呼ばれる）
        /// </summary>
        public override void OnVisitorLeave(int visitorId)
        {
            // 現在のライダーリストから除去
            _currentRiders.Remove(visitorId);
        }

        /// <summary>
        /// 来場者が待ち行列から自主離脱する（忍耐切れ等）。
        /// Queue は先頭以外の要素を直接削除できないため、再構築する。
        /// 通常キューとファストパスキューの両方から検索する。
        /// </summary>
        public void RemoveFromQueue(int visitorId)
        {
            // ファストパスキューから検索・削除
            if (_fastPassQueue.Count > 0)
            {
                int count = _fastPassQueue.Count;
                for (int i = 0; i < count; i++)
                {
                    int id = _fastPassQueue.Dequeue();
                    if (id != visitorId)
                        _fastPassQueue.Enqueue(id);
                }
            }

            // 通常キューから検索・削除
            if (_visitorQueue.Count > 0)
            {
                int count = _visitorQueue.Count;
                for (int i = 0; i < count; i++)
                {
                    int id = _visitorQueue.Dequeue();
                    if (id != visitorId)
                        _visitorQueue.Enqueue(id);
                }
            }
        }

        // ---- VisitorAI 通知ヘルパー ----

        /// <summary>VisitorAI を ID から解決する</summary>
        private VisitorAI ResolveVisitorAI(int visitorId)
        {
            if (GameManager.Instance == null || GameManager.Instance.VisitorManager == null)
                return null;
            return GameManager.Instance.VisitorManager.FindVisitorById(visitorId);
        }

        /// <summary>来場者に搭乗開始を通知する（WaitingInQueue → RidingAttraction）</summary>
        private void NotifyVisitorStartRiding(int visitorId)
        {
            var ai = ResolveVisitorAI(visitorId);
            if (ai != null)
            {
                ai.StartRiding();
            }
        }

        /// <summary>来場者に搭乗完了を通知する（RidingAttraction → Idle + 効果適用）</summary>
        private void NotifyVisitorFinishRiding(int visitorId, float excitementGain,
            float nauseaGain, float satisfactionGain, int attractionId, string attractionName)
        {
            var ai = ResolveVisitorAI(visitorId);
            if (ai != null)
            {
                ai.FinishRiding(excitementGain, nauseaGain, satisfactionGain,
                    attractionId, attractionName);
            }
        }

        /// <summary>故障/事故により行列から追い出された来場者を通知する</summary>
        private void NotifyVisitorQueueAbandoned(int visitorId)
        {
            var ai = ResolveVisitorAI(visitorId);
            if (ai != null)
            {
                ai.OnQueueAbandoned();
            }
        }

        // ---- 魅力度計算 ----

        /// <summary>
        /// 来場者にとってのこのアトラクションの魅力度を計算する。
        /// 興奮度・価格・待ち時間・テーマゾーン一致が影響する。
        /// </summary>
        public override float CalculateAppeal(int visitorId)
        {
            if (!IsOperating || attractionData == null) return 0f;

            // 基本魅力度: 興奮度ベース
            float appeal = EffectiveExcitement / 10f;

            // 価格による抑制（高すぎると魅力低下）
            float priceRatio = (float)ticketPrice / Mathf.Max(1, attractionData.SuggestedTicketPrice);
            if (priceRatio > 1.5f)
                appeal *= 0.3f;
            else if (priceRatio > 1.2f)
                appeal *= 0.7f;

            // 待ち行列が長いと魅力低下
            float queueFillRatio = (float)QueueLength / maxQueueLength;
            appeal *= (1f - queueFillRatio * 0.5f);

            // 満足度の評判が魅力度に影響
            appeal *= (0.5f + satisfactionRating * 0.5f);

            // コンディション低下で魅力度減少（見た目の劣化が来場者に伝わる）
            if (condition < 70f)
            {
                appeal *= (0.5f + condition / 70f * 0.5f); // 70%未満で魅力度低下
            }

            return Mathf.Clamp01(appeal);
        }

        // ---- 日次リセット ----

        /// <summary>日次データをリセットする（TimeManagerから呼ばれる）</summary>
        public void ResetDailyStats()
        {
            TodayRevenue = 0f;
            TodayRiderCount = 0;
        }

        // ---- 配置コールバック ----

        protected override void OnPlaced()
        {
            GameEvents.FireAttractionBuilt(FacilityId);
            WebGLOptimizer.LogVerbose($"[Attraction] アトラクション建設完了: {DisplayName} (ID: {FacilityId})");
        }

        protected override void OnDemolished()
        {
            // 待ち行列と乗客を全員解放
            _fastPassQueue.Clear();
            _visitorQueue.Clear();
            _currentRiders.Clear();
        }

        // ---- デバッグ ----

#if UNITY_EDITOR
        private void OnGUI()
        {
            // エディタ上でのデバッグ表示は必要に応じて有効化
        }
#endif
    }
}
