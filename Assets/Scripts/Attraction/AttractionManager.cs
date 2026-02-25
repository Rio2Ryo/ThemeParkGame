// ============================================================
// ThemeParkGame - AttractionManager
// 全アトラクションの集中管理 + State Patternによる状態遷移
// 建設→稼働→故障→修理のライフサイクルを管理する
// ============================================================

using System;
using System.Collections.Generic;
using UnityEngine;
using ThemeParkGame.Core;
using ThemeParkGame.Visitor;

namespace ThemeParkGame.Attraction
{
    // ================================================================
    // アトラクションライフサイクル状態
    // ================================================================

    /// <summary>
    /// アトラクションのライフサイクル状態。
    /// RideCycleState（WaitingForRiders/Loading/Running/Unloading）は
    /// Operational状態の内部サブステートとして動作する。
    /// </summary>
    public enum AttractionLifecycleState
    {
        /// <summary>建設中（プレイヤーが配置して建設が進行中）</summary>
        Construction,
        /// <summary>稼働中（通常のRideCycleStateで運転）</summary>
        Operational,
        /// <summary>故障中（メカニック到着待ち）</summary>
        BrokenDown,
        /// <summary>修理中（メカニックが修理作業中）</summary>
        Repairing,
        /// <summary>事故発生（閉鎖状態、特別な対応が必要）</summary>
        Accident,
        /// <summary>休止中（プレイヤーが手動で停止）</summary>
        Suspended
    }

    // ================================================================
    // IAttractionState インターフェース（State Pattern）
    // ================================================================

    /// <summary>
    /// アトラクション状態の共通インターフェース（State Pattern）。
    /// 各状態クラスがこのインターフェースを実装し、
    /// AttractionManagerのコンテキストに対して動作する。
    /// </summary>
    public interface IAttractionState
    {
        /// <summary>状態に入った時の初期化処理</summary>
        void Enter(AttractionContext context);

        /// <summary>毎フレーム更新（deltaTime秒）</summary>
        void Update(AttractionContext context, float deltaTime);

        /// <summary>状態から出る時のクリーンアップ処理</summary>
        void Exit(AttractionContext context);

        /// <summary>この状態のライフサイクル状態列挙値</summary>
        AttractionLifecycleState StateType { get; }
    }

    /// <summary>
    /// 状態遷移のコンテキスト。各IAttractionStateが操作するデータ。
    /// </summary>
    public class AttractionContext
    {
        public Attraction Attraction { get; }
        public AttractionManager Manager { get; }
        public float StateTimer { get; set; }
        public float ConstructionProgress { get; set; }

        public AttractionContext(Attraction attraction, AttractionManager manager)
        {
            Attraction = attraction;
            Manager = manager;
        }
    }

    // ================================================================
    // 具象State実装
    // ================================================================

    /// <summary>建設中状態: 建設時間の経過を管理する</summary>
    public class ConstructionState : IAttractionState
    {
        public AttractionLifecycleState StateType => AttractionLifecycleState.Construction;

        public void Enter(AttractionContext context)
        {
            context.StateTimer = 0f;
            context.ConstructionProgress = 0f;
            context.Attraction.SetActive(false);
            WebGLOptimizer.LogVerbose($"[AttractionState] 建設開始: {context.Attraction.DisplayName}");
        }

        public void Update(AttractionContext context, float deltaTime)
        {
            float buildTime = context.Manager.GetBuildTime(context.Attraction);
            context.StateTimer += deltaTime;
            context.ConstructionProgress = Mathf.Clamp01(context.StateTimer / buildTime);

            if (context.ConstructionProgress >= 1f)
            {
                context.Manager.TransitionState(
                    context.Attraction.FacilityId, AttractionLifecycleState.Operational);
            }
        }

        public void Exit(AttractionContext context)
        {
            context.ConstructionProgress = 1f;
            WebGLOptimizer.LogVerbose($"[AttractionState] 建設完了: {context.Attraction.DisplayName}");
        }
    }

    /// <summary>稼働中状態: Attraction内部のRideCycleStateに委譲する</summary>
    public class OperationalState : IAttractionState
    {
        public AttractionLifecycleState StateType => AttractionLifecycleState.Operational;

        public void Enter(AttractionContext context)
        {
            context.Attraction.SetActive(true);
            GameEvents.FireAttractionBuilt(context.Attraction.FacilityId);
            WebGLOptimizer.LogVerbose($"[AttractionState] 稼働開始: {context.Attraction.DisplayName}");
        }

        public void Update(AttractionContext context, float deltaTime)
        {
            // Attraction.Update()内のRideCycleStateマシンが動作する
            // 故障を検知したらBrokenDown状態へ遷移
            if (context.Attraction.IsBrokenDown)
            {
                context.Manager.TransitionState(
                    context.Attraction.FacilityId, AttractionLifecycleState.BrokenDown);
            }
            else if (context.Attraction.HasAccident)
            {
                context.Manager.TransitionState(
                    context.Attraction.FacilityId, AttractionLifecycleState.Accident);
            }
        }

        public void Exit(AttractionContext context)
        {
        }
    }

    /// <summary>故障状態: メカニックの到着を待つ</summary>
    public class BrokenDownState : IAttractionState
    {
        public AttractionLifecycleState StateType => AttractionLifecycleState.BrokenDown;

        public void Enter(AttractionContext context)
        {
            context.StateTimer = 0f;
            GameEvents.FireAttractionBrokenDown(context.Attraction.FacilityId);
            WebGLOptimizer.LogVerbose($"[AttractionState] 故障発生: {context.Attraction.DisplayName}");

            // メカニックへの修理リクエストを発行
            context.Manager.RequestRepair(context.Attraction);
        }

        public void Update(AttractionContext context, float deltaTime)
        {
            context.StateTimer += deltaTime;

            // 事故チェック（Attraction内部のUpdateBrokenDownと連動）
            if (context.Attraction.HasAccident)
            {
                context.Manager.TransitionState(
                    context.Attraction.FacilityId, AttractionLifecycleState.Accident);
            }
        }

        public void Exit(AttractionContext context)
        {
        }
    }

    /// <summary>修理中状態: メカニックが修理作業中</summary>
    public class RepairingState : IAttractionState
    {
        public AttractionLifecycleState StateType => AttractionLifecycleState.Repairing;

        /// <summary>修理にかかる基本時間（秒）</summary>
        private const float BaseRepairDuration = 30f;

        public void Enter(AttractionContext context)
        {
            context.StateTimer = 0f;
            WebGLOptimizer.LogVerbose($"[AttractionState] 修理開始: {context.Attraction.DisplayName}");
        }

        public void Update(AttractionContext context, float deltaTime)
        {
            context.StateTimer += deltaTime;

            float repairDuration = BaseRepairDuration;

            // メカニックスキルによる修理時間短縮はStaffManagerから取得
            float skillModifier = context.Manager.GetRepairSkillModifier(context.Attraction);
            repairDuration *= skillModifier;

            if (context.StateTimer >= repairDuration)
            {
                context.Attraction.OnRepaired();
                context.Manager.TransitionState(
                    context.Attraction.FacilityId, AttractionLifecycleState.Operational);
            }
        }

        public void Exit(AttractionContext context)
        {
            WebGLOptimizer.LogVerbose($"[AttractionState] 修理完了: {context.Attraction.DisplayName}");
        }
    }

    /// <summary>事故状態: 閉鎖・調査・特別対応が必要</summary>
    public class AccidentState : IAttractionState
    {
        public AttractionLifecycleState StateType => AttractionLifecycleState.Accident;

        /// <summary>事故後の閉鎖調査時間（秒）</summary>
        private const float InvestigationDuration = 120f;

        public void Enter(AttractionContext context)
        {
            context.StateTimer = 0f;
            context.Attraction.SetActive(false);
            GameEvents.FireAttractionAccident(context.Attraction.FacilityId);
            Debug.LogWarning($"[AttractionState] 事故発生! {context.Attraction.DisplayName}");
        }

        public void Update(AttractionContext context, float deltaTime)
        {
            context.StateTimer += deltaTime;

            // 調査完了後、修理状態へ
            if (context.StateTimer >= InvestigationDuration)
            {
                context.Attraction.OnRepaired(); // 内部状態リセット
                context.Manager.TransitionState(
                    context.Attraction.FacilityId, AttractionLifecycleState.Repairing);
            }
        }

        public void Exit(AttractionContext context)
        {
        }
    }

    /// <summary>休止状態: プレイヤーが手動で停止</summary>
    public class SuspendedState : IAttractionState
    {
        public AttractionLifecycleState StateType => AttractionLifecycleState.Suspended;

        public void Enter(AttractionContext context)
        {
            context.Attraction.SetActive(false);
            WebGLOptimizer.LogVerbose($"[AttractionState] 休止: {context.Attraction.DisplayName}");
        }

        public void Update(AttractionContext context, float deltaTime)
        {
            // 休止中は何もしない。外部からResumeが呼ばれるのを待つ。
        }

        public void Exit(AttractionContext context)
        {
            context.Attraction.SetActive(true);
        }
    }

    // ================================================================
    // AttractionManager 本体
    // ================================================================

    /// <summary>
    /// 全アトラクションのライフサイクルを集中管理するマネージャー。
    ///
    /// State Patternにより各アトラクションは独立した状態オブジェクトを持ち、
    /// Construction → Operational → BrokenDown → Repairing → Operational
    /// の状態遷移を管理する。
    ///
    /// 主な責務:
    /// - アトラクションの登録/解除
    /// - 各アトラクションの状態遷移管理（IAttractionState）
    /// - 修理リクエストの発行（StaffManagerへの委譲）
    /// - 全アトラクション統計の集計
    /// </summary>
    public class AttractionManager : MonoBehaviour
    {
        // ---- 状態テンプレート（Flyweightパターン: 状態オブジェクトを共有） ----
        private static readonly ConstructionState s_constructionState = new ConstructionState();
        private static readonly OperationalState s_operationalState = new OperationalState();
        private static readonly BrokenDownState s_brokenDownState = new BrokenDownState();
        private static readonly RepairingState s_repairingState = new RepairingState();
        private static readonly AccidentState s_accidentState = new AccidentState();
        private static readonly SuspendedState s_suspendedState = new SuspendedState();

        // ---- アトラクション管理 ----

        /// <summary>登録済みアトラクション(FacilityId -> Context)</summary>
        private readonly Dictionary<int, AttractionContext> _contexts
            = new Dictionary<int, AttractionContext>();

        /// <summary>各アトラクションの現在状態</summary>
        private readonly Dictionary<int, IAttractionState> _currentStates
            = new Dictionary<int, IAttractionState>();

        // ---- 統計 ----

        /// <summary>現在稼働中のアトラクション数</summary>
        public int OperationalCount { get; private set; }

        /// <summary>現在故障中のアトラクション数</summary>
        public int BrokenDownCount { get; private set; }

        /// <summary>現在建設中のアトラクション数</summary>
        public int ConstructionCount { get; private set; }

        /// <summary>登録済みアトラクション総数</summary>
        public int TotalCount => _contexts.Count;

        // ---- 設定 ----

        [Header("建設設定")]
        [Tooltip("基本建設時間（秒）")]
        [SerializeField] private float baseBuildTime = 60f;

        [Tooltip("建設コスト1万あたりの追加建設時間（秒）")]
        [SerializeField] private float buildTimePerCostUnit = 3f;

        // ================================================================
        // 初期化
        // ================================================================

        public void Initialize()
        {
            _contexts.Clear();
            _currentStates.Clear();
            OperationalCount = 0;
            BrokenDownCount = 0;
            ConstructionCount = 0;

            SubscribeToEvents();
            WebGLOptimizer.LogVerbose("[AttractionManager] 初期化完了");
        }

        private void OnDestroy()
        {
            UnsubscribeFromEvents();
        }

        // ================================================================
        // 毎フレーム更新
        // ================================================================

        private void Update()
        {
            if (GameManager.Instance != null && GameManager.Instance.IsPaused) return;

            float dt = Time.deltaTime;

            // 全アトラクションの状態を更新
            foreach (var kvp in _currentStates)
            {
                int facilityId = kvp.Key;
                IAttractionState state = kvp.Value;

                if (_contexts.TryGetValue(facilityId, out AttractionContext context))
                {
                    state.Update(context, dt);
                }
            }
        }

        // ================================================================
        // アトラクション登録
        // ================================================================

        /// <summary>
        /// 新しいアトラクションを建設として登録する。
        /// 建設完了後に自動的にOperational状態へ遷移する。
        /// </summary>
        /// <param name="attraction">登録するアトラクション</param>
        /// <param name="skipConstruction">建設をスキップして即座に稼働開始するか（初期配置用）</param>
        public void RegisterAttraction(Attraction attraction, bool skipConstruction = false)
        {
            int id = attraction.FacilityId;

            if (_contexts.ContainsKey(id))
            {
                Debug.LogWarning($"[AttractionManager] アトラクション ID={id} は既に登録済み");
                return;
            }

            var context = new AttractionContext(attraction, this);
            _contexts[id] = context;

            AttractionLifecycleState initialState = skipConstruction
                ? AttractionLifecycleState.Operational
                : AttractionLifecycleState.Construction;

            IAttractionState stateObj = GetStateObject(initialState);
            _currentStates[id] = stateObj;
            stateObj.Enter(context);

            UpdateStateCounts();
            WebGLOptimizer.LogVerbose($"[AttractionManager] 登録: {attraction.DisplayName} (ID={id}, " +
                      $"初期状態={initialState})");
        }

        /// <summary>アトラクションの登録を解除する（撤去時）</summary>
        public void UnregisterAttraction(int facilityId)
        {
            if (!_contexts.TryGetValue(facilityId, out AttractionContext context))
                return;

            if (_currentStates.TryGetValue(facilityId, out IAttractionState currentState))
            {
                currentState.Exit(context);
            }

            _contexts.Remove(facilityId);
            _currentStates.Remove(facilityId);
            UpdateStateCounts();

            // 施設キャッシュをクリア
            VisitorAI.InvalidateFacilityCache();

            WebGLOptimizer.LogVerbose($"[AttractionManager] 登録解除: ID={facilityId}");
        }

        // ================================================================
        // 状態遷移
        // ================================================================

        /// <summary>
        /// 指定アトラクションの状態を遷移させる。
        /// 現在の状態のExit→新状態のEnterが呼ばれる。
        /// </summary>
        public void TransitionState(int facilityId, AttractionLifecycleState newState)
        {
            if (!_contexts.TryGetValue(facilityId, out AttractionContext context))
            {
                Debug.LogWarning($"[AttractionManager] 遷移失敗: ID={facilityId} は未登録");
                return;
            }

            if (_currentStates.TryGetValue(facilityId, out IAttractionState oldState))
            {
                // 同じ状態への遷移は無視
                if (oldState.StateType == newState) return;

                oldState.Exit(context);
            }

            IAttractionState newStateObj = GetStateObject(newState);
            _currentStates[facilityId] = newStateObj;
            context.StateTimer = 0f;
            newStateObj.Enter(context);

            UpdateStateCounts();

            WebGLOptimizer.LogVerbose($"[AttractionManager] 状態遷移: {context.Attraction.DisplayName} " +
                      $"({oldState?.StateType} → {newState})");
        }

        // ================================================================
        // 外部API
        // ================================================================

        /// <summary>アトラクションの現在のライフサイクル状態を取得する</summary>
        public AttractionLifecycleState GetLifecycleState(int facilityId)
        {
            if (_currentStates.TryGetValue(facilityId, out IAttractionState state))
                return state.StateType;
            return AttractionLifecycleState.Suspended;
        }

        /// <summary>建設進捗を取得する（0.0～1.0）</summary>
        public float GetConstructionProgress(int facilityId)
        {
            if (_contexts.TryGetValue(facilityId, out AttractionContext context))
                return context.ConstructionProgress;
            return 1f;
        }

        /// <summary>アトラクションを手動で休止する</summary>
        public void SuspendAttraction(int facilityId)
        {
            var currentState = GetLifecycleState(facilityId);
            if (currentState == AttractionLifecycleState.Operational)
            {
                TransitionState(facilityId, AttractionLifecycleState.Suspended);
            }
        }

        /// <summary>休止中のアトラクションを再開する</summary>
        public void ResumeAttraction(int facilityId)
        {
            var currentState = GetLifecycleState(facilityId);
            if (currentState == AttractionLifecycleState.Suspended)
            {
                TransitionState(facilityId, AttractionLifecycleState.Operational);
            }
        }

        /// <summary>メカニックが到着して修理を開始する</summary>
        public void StartRepair(int facilityId)
        {
            var currentState = GetLifecycleState(facilityId);
            if (currentState == AttractionLifecycleState.BrokenDown)
            {
                TransitionState(facilityId, AttractionLifecycleState.Repairing);
            }
        }

        /// <summary>全稼働中アトラクションの平均満足度を取得する</summary>
        public float GetAverageSatisfaction()
        {
            float sum = 0f;
            int count = 0;

            foreach (var kvp in _contexts)
            {
                if (_currentStates.TryGetValue(kvp.Key, out IAttractionState state) &&
                    state.StateType == AttractionLifecycleState.Operational)
                {
                    sum += kvp.Value.Attraction.SatisfactionRating;
                    count++;
                }
            }

            return count > 0 ? sum / count : 0f;
        }

        /// <summary>全アトラクションの本日売上合計を取得する</summary>
        public float GetTotalTodayRevenue()
        {
            float total = 0f;
            foreach (var kvp in _contexts)
            {
                total += kvp.Value.Attraction.TodayRevenue;
            }
            return total;
        }

        /// <summary>登録済みアトラクション一覧を取得する</summary>
        public IEnumerable<Attraction> GetAllAttractions()
        {
            foreach (var kvp in _contexts)
            {
                yield return kvp.Value.Attraction;
            }
        }

        /// <summary>IDでアトラクションを取得する（見つからない場合null）</summary>
        public Attraction GetAttractionById(int facilityId)
        {
            if (_contexts.TryGetValue(facilityId, out var ctx))
                return ctx.Attraction;
            return null;
        }

        // ================================================================
        // 評価システム用データ提供
        // ================================================================

        /// <summary>設置済みアトラクションのうち異なるカテゴリの数を返す</summary>
        public int GetUniqueCategories()
        {
            var categories = new HashSet<AttractionCategory>();
            foreach (var kvp in _contexts)
            {
                if (kvp.Value.Attraction != null && kvp.Value.Attraction.Data != null)
                    categories.Add(kvp.Value.Attraction.Data.Category);
            }
            return categories.Count;
        }

        /// <summary>稼働中アトラクションの平均品質（0-1）を返す</summary>
        public float GetAverageQuality()
        {
            if (_contexts.Count == 0) return 0f;

            float totalQuality = 0f;
            int count = 0;
            foreach (var kvp in _contexts)
            {
                var attr = kvp.Value.Attraction;
                if (attr == null || attr.Data == null) continue;

                // 品質 = (EffectiveExcitement/10 * 0.6 + NauseaInverse * 0.4)
                float excitementNorm = attr.EffectiveExcitement / 10f;
                float nauseaInv = 1f - attr.Data.NauseaFactor;
                float quality = excitementNorm * 0.6f + nauseaInv * 0.4f;
                totalQuality += Mathf.Clamp01(quality);
                count++;
            }

            return count > 0 ? totalQuality / count : 0f;
        }

        // ================================================================
        // 内部ヘルパー
        // ================================================================

        /// <summary>建設時間を計算する（コストに応じて変動）</summary>
        public float GetBuildTime(Attraction attraction)
        {
            float costFactor = attraction.BuildCost / 10000f;
            return baseBuildTime + costFactor * buildTimePerCostUnit;
        }

        /// <summary>修理スキル倍率を取得する（1.0=通常、低いほど高速修理）</summary>
        public float GetRepairSkillModifier(Attraction attraction)
        {
            // StaffManagerからメカニックのスキルレベルを取得
            if (GameManager.Instance != null && GameManager.Instance.StaffManager != null)
            {
                // スタッフのスキルレベル（1-5）に応じた修理速度倍率
                // スキルレベル1=1.0倍、スキルレベル5=0.4倍（2.5倍速）
                float avgSkill = GameManager.Instance.StaffManager.GetAverageMechanicSkill();
                return Mathf.Lerp(1.0f, 0.4f, (avgSkill - 1f) / 4f);
            }
            return 1.0f;
        }

        /// <summary>メカニックへの修理リクエストを発行する</summary>
        public void RequestRepair(Attraction attraction)
        {
            // StaffManagerのメカニック派遣システムに修理リクエストを送信
            // （StaffManagerは既存のFindAndAssignTask機構で処理する想定）
            WebGLOptimizer.LogVerbose($"[AttractionManager] 修理リクエスト発行: {attraction.DisplayName}");
        }

        /// <summary>状態列挙値から状態オブジェクトを取得する</summary>
        private static IAttractionState GetStateObject(AttractionLifecycleState state)
        {
            switch (state)
            {
                case AttractionLifecycleState.Construction: return s_constructionState;
                case AttractionLifecycleState.Operational:  return s_operationalState;
                case AttractionLifecycleState.BrokenDown:   return s_brokenDownState;
                case AttractionLifecycleState.Repairing:    return s_repairingState;
                case AttractionLifecycleState.Accident:     return s_accidentState;
                case AttractionLifecycleState.Suspended:    return s_suspendedState;
                default: return s_suspendedState;
            }
        }

        /// <summary>状態別カウントを再計算する</summary>
        private void UpdateStateCounts()
        {
            OperationalCount = 0;
            BrokenDownCount = 0;
            ConstructionCount = 0;

            foreach (var kvp in _currentStates)
            {
                switch (kvp.Value.StateType)
                {
                    case AttractionLifecycleState.Operational:
                        OperationalCount++;
                        break;
                    case AttractionLifecycleState.BrokenDown:
                    case AttractionLifecycleState.Repairing:
                        BrokenDownCount++;
                        break;
                    case AttractionLifecycleState.Construction:
                        ConstructionCount++;
                        break;
                }
            }
        }

        // ================================================================
        // イベント購読
        // ================================================================

        private void SubscribeToEvents()
        {
            GameEvents.OnAttractionBrokenDown += OnAttractionBrokenDownHandler;
            GameEvents.OnAttractionRepaired += OnAttractionRepairedHandler;
        }

        private void UnsubscribeFromEvents()
        {
            GameEvents.OnAttractionBrokenDown -= OnAttractionBrokenDownHandler;
            GameEvents.OnAttractionRepaired -= OnAttractionRepairedHandler;
        }

        private void OnAttractionBrokenDownHandler(int facilityId)
        {
            // 既にBrokenDown状態でなければ遷移（Attraction内部から発火された場合）
            var currentState = GetLifecycleState(facilityId);
            if (currentState == AttractionLifecycleState.Operational)
            {
                TransitionState(facilityId, AttractionLifecycleState.BrokenDown);
            }
        }

        private void OnAttractionRepairedHandler(int facilityId)
        {
            // 修理完了イベント→Operational状態へ
            var currentState = GetLifecycleState(facilityId);
            if (currentState == AttractionLifecycleState.Repairing ||
                currentState == AttractionLifecycleState.BrokenDown)
            {
                TransitionState(facilityId, AttractionLifecycleState.Operational);
            }
        }
    }
}
