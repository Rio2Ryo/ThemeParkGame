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

        /// <summary>稼働中か（故障・事故でなく、アクティブ状態）</summary>
        public bool IsOperating => IsActive && currentCycleState != RideCycleState.BrokenDown
                                             && currentCycleState != RideCycleState.Accident;

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
        public int QueueLength => _visitorQueue.Count;

        /// <summary>待ち行列の最大長（これを超えると来場者は並ばない）</summary>
        [Header("Queue Settings")]
        [SerializeField] private int maxQueueLength = 20;
        public int MaxQueueLength
        {
            get => maxQueueLength;
            set => maxQueueLength = Mathf.Max(1, value);
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

                if (ticketPrice <= 0)
                    ticketPrice = attractionData.SuggestedTicketPrice;
            }
        }

        // ---- メインループ ----

        protected override void Update()
        {
            base.Update();
            if (!IsActive || attractionData == null) return;

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

            // キューから定員分の乗客をライドに移す
            int capacity = EffectiveCapacity;
            while (_currentRiders.Count < capacity && _visitorQueue.Count > 0)
            {
                int vid = _visitorQueue.Dequeue();
                _currentRiders.Add(vid);

                // VisitorAI に搭乗開始を通知（WaitingInQueue → RidingAttraction）
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

            // 運転中の故障判定（毎秒チェック）
            if (UnityEngine.Random.value < currentBreakdownProbability * Time.deltaTime)
            {
                TriggerBreakdown();
                return;
            }

            // 運転時間が経過したら降車フェーズへ
            if (_cycleTimer >= attractionData.RideDuration)
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

            // 収益計上（EconomyManager経由で正式に計上する）
            float cycleRevenue = riderCount * ticketPrice;
            TodayRevenue += cycleRevenue;
            TotalRevenue += cycleRevenue;
            TodayRiderCount += riderCount;
            TotalRiderCount += riderCount;

            if (GameManager.Instance != null && GameManager.Instance.EconomyManager != null)
            {
                GameManager.Instance.EconomyManager.AddRevenue(
                    cycleRevenue, RevenueCategory.AttractionFee, FacilityId);
            }
            else
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
            while (_visitorQueue.Count > 0)
            {
                int vid = _visitorQueue.Dequeue();
                NotifyVisitorQueueAbandoned(vid);
            }

            currentCycleState = RideCycleState.BrokenDown;
            _breakdownStartTime = Time.time;
            TotalBreakdownCount++;

            GameEvents.FireAttractionBrokenDown(FacilityId);
            Debug.Log($"[Attraction] 故障発生: {DisplayName} (ID: {FacilityId})");
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
            while (_visitorQueue.Count > 0)
            {
                int vid = _visitorQueue.Dequeue();
                NotifyVisitorQueueAbandoned(vid);
            }

            GameEvents.FireAttractionAccident(FacilityId);
            Debug.LogWarning($"[Attraction] 事故発生: {DisplayName} (ID: {FacilityId})");
        }

        /// <summary>
        /// メカニックが修理を完了した時に呼ばれる。
        /// 故障状態を解除し、故障確率をリセットする。
        /// </summary>
        public void OnRepaired()
        {
            if (!IsBrokenDown && !HasAccident) return;

            currentCycleState = RideCycleState.WaitingForRiders;
            _lastMaintenanceTime = Time.time;
            currentBreakdownProbability = attractionData.BaseBreakdownRate;

            GameEvents.FireAttractionRepaired(FacilityId);
            Debug.Log($"[Attraction] 修理完了: {DisplayName} (ID: {FacilityId})");
        }

        /// <summary>
        /// メカニックが定期点検を行った時に呼ばれる。
        /// 故障確率を基本値にリセットする。
        /// </summary>
        public void OnMaintenancePerformed()
        {
            _lastMaintenanceTime = Time.time;
            currentBreakdownProbability = attractionData.BaseBreakdownRate;
            Debug.Log($"[Attraction] 定期点検完了: {DisplayName} (ID: {FacilityId})");
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
                Debug.LogWarning($"[Attraction] これ以上アップグレードできません: {DisplayName} (Lv.{upgradeLevel})");
                return false;
            }

            // アップグレード費用はEconomyManager経由で支払われることを想定
            upgradeLevel = nextLevel;

            // アップグレード後の外観に切り替え
            if (upgradeData.UpgradedVisualPrefab != null)
            {
                ApplyUpgradedVisual(upgradeData.UpgradedVisualPrefab);
            }

            GameEvents.FireAttractionUpgraded(FacilityId);
            Debug.Log($"[Attraction] アップグレード完了: {DisplayName} → Lv.{upgradeLevel}");
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
        /// </summary>
        public override bool OnVisitorArrive(int visitorId)
        {
            if (!CanAcceptVisitor(visitorId)) return false;

            _visitorQueue.Enqueue(visitorId);
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
        /// </summary>
        public void RemoveFromQueue(int visitorId)
        {
            if (_visitorQueue.Count == 0) return;

            int count = _visitorQueue.Count;
            for (int i = 0; i < count; i++)
            {
                int id = _visitorQueue.Dequeue();
                if (id != visitorId)
                {
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
            Debug.Log($"[Attraction] アトラクション建設完了: {DisplayName} (ID: {FacilityId})");
        }

        protected override void OnDemolished()
        {
            // 待ち行列と乗客を全員解放
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
