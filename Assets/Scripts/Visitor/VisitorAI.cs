// ============================================================
// ThemeParkGame - VisitorAI
// 来場者のAI行動制御（状態機械 + 意思決定 + パス探索）
// ============================================================

using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using ThemeParkGame.Core;

namespace ThemeParkGame.Visitor
{
    /// <summary>
    /// 来場者の行動AIを制御するメインコンポーネント。
    /// 有限状態機械（FSM）による行動制御と、パラメータベースの意思決定を統合する。
    ///
    /// 【ゲームデザイン】
    /// 来場者AIは「欲求駆動型」で動作する。
    /// 各欲求（トイレ・空腹・渇き・興奮・幸福）に優先度が設定されており、
    /// 最も緊急性の高い欲求を満たす行動を選択する。
    ///
    /// 優先順位:
    /// 1. トイレ（生理的に最も緊急）
    /// 2. 嘔吐（制御不能）
    /// 3. 空腹/渇き（不快度が高い方を優先）
    /// 4. 興奮を求める（アトラクションに乗りたい）
    /// 5. 休憩/散策（特に緊急な欲求がない時）
    ///
    /// VisitorTypeごとに好みのアトラクションカテゴリが異なり、
    /// 同じパークでも来場者ごとに異なる行動パターンが生まれる。
    /// </summary>
    [RequireComponent(typeof(NavMeshAgent))]
    public class VisitorAI : MonoBehaviour
    {
        // ---- シリアライズフィールド ----

        [Header("来場者設定")]
        [SerializeField] private VisitorType visitorType = VisitorType.Family;

        [Header("行動パラメータ")]
        [SerializeField] private float decisionInterval = 2f;
        [SerializeField] private float maxQueueWaitTime = 120f;
        [SerializeField] private float wanderRadius = 15f;
        [SerializeField] private float facilitySearchRadius = 50f;
        [SerializeField] private float idleWanderInterval = 8f;

        [Header("感情バブル表示間隔")]
        [SerializeField] private float emotionBubbleInterval = 10f;

        // ---- コンポーネント参照 ----

        private NavMeshAgent navAgent;
        private EmotionBubble emotionBubble;

        // ---- 内部データ ----

        private VisitorParameters parameters;
        private VisitorProfile profile;

        // 状態機械
        private VisitorBehaviorState currentState = VisitorBehaviorState.Idle;
        private VisitorBehaviorState previousState;

        // タイマー類
        private float decisionTimer;
        private float queueWaitTimer;
        private float idleTimer;
        private float emotionBubbleTimer;
        private float vomitTimer;
        private float actionTimer; // 食事・トイレ等のアクション継続時間

        // ターゲット
        private Transform currentTarget;
        private int currentTargetFacilityId = -1;
        private FacilityType targetFacilityType;

        // 一意ID（VisitorManagerから割り当て）
        private int visitorId;
        private bool isInitialized;
        private bool isParkClosing;

        // ---- 行動定数 ----

        /// <summary>食事にかかる時間（秒）</summary>
        private const float EatingDuration = 5f;

        /// <summary>飲料にかかる時間（秒）</summary>
        private const float DrinkingDuration = 3f;

        /// <summary>トイレにかかる時間（秒）</summary>
        private const float ToiletDuration = 4f;

        /// <summary>休憩にかかる時間（秒）</summary>
        private const float RestingDuration = 8f;

        /// <summary>嘔吐にかかる時間（秒）</summary>
        private const float VomitDuration = 3f;

        /// <summary>マップ確認にかかる時間（秒）</summary>
        private const float MapLookDuration = 4f;

        /// <summary>ショー鑑賞にかかる時間（秒）</summary>
        private const float ShowWatchDuration = 15f;

        // ---- プロパティ ----

        /// <summary>来場者の一意ID</summary>
        public int VisitorId => visitorId;

        /// <summary>来場者タイプ</summary>
        public VisitorType Type => visitorType;

        /// <summary>現在の行動状態</summary>
        public VisitorBehaviorState CurrentState => currentState;

        /// <summary>パラメータへの読み取り専用アクセス</summary>
        public VisitorParameters Parameters => parameters;

        /// <summary>プロファイルへの読み取り専用アクセス</summary>
        public VisitorProfile Profile => profile;

        /// <summary>現在の幸福度（外部参照用ショートカット）</summary>
        public float Happiness => parameters.Happiness;

        /// <summary>アクティブかどうか（初期化済みかつ退園していない）</summary>
        public bool IsActive => isInitialized && currentState != VisitorBehaviorState.LeavingPark;

        // ---- Unity ライフサイクル ----

        private void Awake()
        {
            navAgent = GetComponent<NavMeshAgent>();
            emotionBubble = GetComponentInChildren<EmotionBubble>();

            parameters = new VisitorParameters();
            profile = new VisitorProfile();
        }

        private void OnEnable()
        {
            SubscribeEvents();
        }

        private void OnDisable()
        {
            UnsubscribeEvents();
        }

        private void Update()
        {
            if (!isInitialized) return;
            if (GameManager.Instance != null && GameManager.Instance.IsPaused) return;

            float dt = Time.deltaTime;

            // 天候取得
            Weather currentWeather = GetCurrentWeather();

            // パラメータの自動増減
            parameters.Tick(dt, currentWeather, visitorType);

            // 緊急状態チェック（状態に関係なく割り込む）
            CheckEmergencyConditions();

            // 意思決定タイマー
            decisionTimer -= dt;
            if (decisionTimer <= 0f)
            {
                decisionTimer = decisionInterval;
                MakeDecision();
            }

            // 現在の状態を実行
            ExecuteCurrentState(dt);

            // 感情バブル更新
            emotionBubbleTimer -= dt;
            if (emotionBubbleTimer <= 0f)
            {
                emotionBubbleTimer = emotionBubbleInterval;
                UpdateEmotionBubble();
            }
        }

        // ---- 初期化 ----

        /// <summary>
        /// 来場者を初期化する。VisitorManagerから呼ばれる。
        /// </summary>
        /// <param name="id">一意の来場者ID</param>
        /// <param name="type">来場者タイプ</param>
        /// <param name="spawnPosition">スポーン位置</param>
        public void Initialize(int id, VisitorType type, Vector3 spawnPosition)
        {
            visitorId = id;
            visitorType = type;

            parameters.Initialize(type);
            profile.Initialize(type);

            transform.position = spawnPosition;
            currentState = VisitorBehaviorState.Idle;
            previousState = VisitorBehaviorState.Idle;
            isParkClosing = false;

            // タイマー初期化（ばらつきを持たせて一斉行動を防ぐ）
            decisionTimer = UnityEngine.Random.Range(0f, decisionInterval);
            emotionBubbleTimer = UnityEngine.Random.Range(0f, emotionBubbleInterval);
            idleTimer = 0f;
            queueWaitTimer = 0f;

            // NavMeshAgent設定
            if (navAgent != null)
            {
                navAgent.speed = GetWalkSpeed();
                navAgent.enabled = true;
            }

            isInitialized = true;

            GameEvents.FireVisitorEnterPark(visitorId);
            GameEvents.FireVisitorHappinessChanged(visitorId, parameters.Happiness);

            Debug.Log($"[VisitorAI] Visitor {visitorId} ({visitorType}) entered the park. {parameters}");
        }

        /// <summary>来場者をリセットする（オブジェクトプール再利用時）</summary>
        public void ResetVisitor()
        {
            isInitialized = false;
            currentState = VisitorBehaviorState.Idle;
            currentTarget = null;
            currentTargetFacilityId = -1;

            if (navAgent != null)
            {
                navAgent.ResetPath();
                navAgent.enabled = false;
            }

            if (emotionBubble != null)
            {
                emotionBubble.ForceHide();
            }
        }

        // ---- 状態機械: 意思決定 ----

        /// <summary>
        /// 現在のパラメータに基づいて次の行動を決定する。
        /// 【ゲームデザイン】欲求駆動型意思決定。最も切迫した欲求を優先する。
        /// </summary>
        private void MakeDecision()
        {
            // 現在アクション実行中の場合は割り込まない（嘔吐以外）
            if (IsInAction() && currentState != VisitorBehaviorState.Idle)
                return;

            // パーク閉園チェック
            if (isParkClosing)
            {
                TransitionTo(VisitorBehaviorState.LeavingPark);
                return;
            }

            // 退園条件チェック
            if (ShouldLeavePark())
            {
                TransitionTo(VisitorBehaviorState.LeavingPark);
                return;
            }

            // 優先度1: トイレ欲求
            if (parameters.NeedsToilet)
            {
                if (currentState != VisitorBehaviorState.WalkingToToilet &&
                    currentState != VisitorBehaviorState.UsingToilet)
                {
                    if (TryFindAndNavigateTo(FacilityType.Toilet))
                    {
                        TransitionTo(VisitorBehaviorState.WalkingToToilet);
                        return;
                    }
                    // トイレが見つからない場合、感情バブルで表示
                    if (emotionBubble != null)
                        emotionBubble.ShowBubble(EmotionBubbleType.LookingForToilet, EmotionBubbleColor.Green);
                }
                return;
            }

            // 優先度2: 空腹（渇きより優先度が高い場合）
            if (parameters.IsHungry && parameters.Hunger >= parameters.Thirst)
            {
                if (currentState != VisitorBehaviorState.WalkingToShop &&
                    currentState != VisitorBehaviorState.Eating)
                {
                    if (parameters.HasMoney && TryFindAndNavigateTo(FacilityType.FoodShop))
                    {
                        targetFacilityType = FacilityType.FoodShop;
                        TransitionTo(VisitorBehaviorState.WalkingToShop);
                        return;
                    }
                    if (emotionBubble != null)
                        emotionBubble.ShowBubble(EmotionBubbleType.LookingForFood, EmotionBubbleColor.Green);
                }
                return;
            }

            // 優先度3: 喉の渇き
            if (parameters.IsThirsty)
            {
                if (currentState != VisitorBehaviorState.WalkingToShop &&
                    currentState != VisitorBehaviorState.Drinking)
                {
                    if (parameters.HasMoney && TryFindAndNavigateTo(FacilityType.DrinkShop))
                    {
                        targetFacilityType = FacilityType.DrinkShop;
                        TransitionTo(VisitorBehaviorState.WalkingToShop);
                        return;
                    }
                }
                return;
            }

            // 優先度4: 興奮を求める → アトラクションに行く
            if (parameters.Excitement < 50f && parameters.HasMoney)
            {
                if (currentState != VisitorBehaviorState.WalkingToAttraction &&
                    currentState != VisitorBehaviorState.WaitingInQueue &&
                    currentState != VisitorBehaviorState.RidingAttraction)
                {
                    if (TryFindAttraction())
                    {
                        TransitionTo(VisitorBehaviorState.WalkingToAttraction);
                        return;
                    }
                }
            }

            // 優先度5: 疲労時の休憩
            if (parameters.IsBored && parameters.Happiness < 50f)
            {
                if (currentState != VisitorBehaviorState.Resting)
                {
                    if (TryFindAndNavigateTo(FacilityType.Bench))
                    {
                        TransitionTo(VisitorBehaviorState.Resting);
                        return;
                    }
                }
            }

            // デフォルト: Idle状態で散策
            if (currentState == VisitorBehaviorState.Idle)
            {
                idleTimer += decisionInterval;
                if (idleTimer >= idleWanderInterval)
                {
                    idleTimer = 0f;
                    WanderRandomly();

                    // 時々マップを見る
                    if (UnityEngine.Random.value < 0.15f)
                    {
                        TransitionTo(VisitorBehaviorState.LookingAtMap);
                    }
                }
            }
        }

        // ---- 状態機械: 状態実行 ----

        /// <summary>現在の状態に応じた処理を実行する</summary>
        private void ExecuteCurrentState(float deltaTime)
        {
            switch (currentState)
            {
                case VisitorBehaviorState.Idle:
                    ExecuteIdle(deltaTime);
                    break;

                case VisitorBehaviorState.WalkingToAttraction:
                case VisitorBehaviorState.WalkingToShop:
                case VisitorBehaviorState.WalkingToToilet:
                    ExecuteWalking(deltaTime);
                    break;

                case VisitorBehaviorState.WaitingInQueue:
                    ExecuteQueueWaiting(deltaTime);
                    break;

                case VisitorBehaviorState.RidingAttraction:
                    // アトラクション搭乗中は外部（アトラクションシステム）から制御
                    break;

                case VisitorBehaviorState.Eating:
                    ExecuteTimedAction(deltaTime, EatingDuration, OnFinishEating);
                    break;

                case VisitorBehaviorState.Drinking:
                    ExecuteTimedAction(deltaTime, DrinkingDuration, OnFinishDrinking);
                    break;

                case VisitorBehaviorState.UsingToilet:
                    ExecuteTimedAction(deltaTime, ToiletDuration, OnFinishToilet);
                    break;

                case VisitorBehaviorState.Resting:
                    ExecuteTimedAction(deltaTime, RestingDuration, OnFinishResting);
                    break;

                case VisitorBehaviorState.WatchingEntertainment:
                    ExecuteTimedAction(deltaTime, ShowWatchDuration, OnFinishWatching);
                    break;

                case VisitorBehaviorState.LookingAtMap:
                    ExecuteTimedAction(deltaTime, MapLookDuration, OnFinishLookingAtMap);
                    break;

                case VisitorBehaviorState.Vomiting:
                    ExecuteTimedAction(deltaTime, VomitDuration, OnFinishVomiting);
                    break;

                case VisitorBehaviorState.LeavingPark:
                    ExecuteLeavingPark(deltaTime);
                    break;

                case VisitorBehaviorState.TalkingToPlayer:
                    // AI会話中は行動停止。AIConversationManagerから制御される。
                    break;
            }
        }

        // ---- 状態別実行メソッド ----

        private void ExecuteIdle(float deltaTime)
        {
            // Idle中は特に何もしない。MakeDecisionで次の行動が決まる。
            StopNavigation();
        }

        private void ExecuteWalking(float deltaTime)
        {
            if (navAgent == null || !navAgent.enabled) return;

            // 目的地に到着したか
            if (HasReachedDestination())
            {
                OnReachedDestination();
            }
        }

        /// <summary>
        /// 行列待ち処理。
        /// 【ゲームデザイン】忍耐力に応じた待ち時間上限を持つ。
        /// 待ち時間が長すぎると幸福度が低下し、最終的に行列から離脱する。
        /// </summary>
        private void ExecuteQueueWaiting(float deltaTime)
        {
            queueWaitTimer += deltaTime;

            // 忍耐力に応じた最大待ち時間
            float maxWait = maxQueueWaitTime * GetPatienceMultiplier();

            if (queueWaitTimer >= maxWait)
            {
                // 我慢の限界 → 行列離脱
                parameters.ModifyHappiness(-10f);
                if (emotionBubble != null)
                    emotionBubble.ShowBubble(EmotionBubbleType.LongWait, EmotionBubbleColor.Gray);

                Debug.Log($"[VisitorAI] Visitor {visitorId} left queue after {queueWaitTimer:F0}s (patience exceeded)");
                TransitionTo(VisitorBehaviorState.Idle);
                return;
            }

            // 待ち時間に応じた幸福度減少（緩やかに）
            if (queueWaitTimer > maxWait * 0.5f)
            {
                parameters.ModifyHappiness(-0.5f * deltaTime);
            }
        }

        /// <summary>汎用タイマー付きアクション実行</summary>
        private void ExecuteTimedAction(float deltaTime, float duration, Action onComplete)
        {
            actionTimer += deltaTime;
            if (actionTimer >= duration)
            {
                actionTimer = 0f;
                onComplete?.Invoke();
            }
        }

        private void ExecuteLeavingPark(float deltaTime)
        {
            // 出口に向かう（出口位置はParkManagerから取得する想定）
            if (HasReachedDestination() || !navAgent.hasPath)
            {
                OnLeftPark();
            }
        }

        // ---- アクション完了コールバック ----

        private void OnFinishEating()
        {
            // 食事完了: 空腹減少、幸福度UP、トイレ欲求増加
            parameters.ApplyFoodEffect(
                hungerReduction: UnityEngine.Random.Range(40f, 60f),
                qualityBonus: UnityEngine.Random.Range(3f, 8f)
            );
            parameters.ModifyHappiness(5f);
            Debug.Log($"[VisitorAI] Visitor {visitorId} finished eating. {parameters}");
            TransitionTo(VisitorBehaviorState.Idle);
        }

        private void OnFinishDrinking()
        {
            // 飲料完了: 渇き減少、トイレ欲求増加
            parameters.ApplyDrinkEffect(
                thirstReduction: UnityEngine.Random.Range(50f, 70f),
                qualityBonus: UnityEngine.Random.Range(2f, 5f)
            );
            Debug.Log($"[VisitorAI] Visitor {visitorId} finished drinking. {parameters}");
            TransitionTo(VisitorBehaviorState.Idle);
        }

        private void OnFinishToilet()
        {
            // トイレ完了: トイレ欲求リセット
            parameters.ApplyToiletEffect(
                cleanlinessBonus: UnityEngine.Random.Range(1f, 5f)
            );
            Debug.Log($"[VisitorAI] Visitor {visitorId} finished using toilet. {parameters}");
            TransitionTo(VisitorBehaviorState.Idle);
        }

        private void OnFinishResting()
        {
            // 休憩完了: 幸福度少し回復
            parameters.ModifyHappiness(3f);
            Debug.Log($"[VisitorAI] Visitor {visitorId} finished resting. {parameters}");
            TransitionTo(VisitorBehaviorState.Idle);
        }

        private void OnFinishWatching()
        {
            // ショー鑑賞完了: 興奮度UP、幸福度UP
            parameters.ModifyExcitement(UnityEngine.Random.Range(10f, 25f));
            parameters.ModifyHappiness(UnityEngine.Random.Range(5f, 15f));
            Debug.Log($"[VisitorAI] Visitor {visitorId} finished watching show. {parameters}");
            TransitionTo(VisitorBehaviorState.Idle);
        }

        private void OnFinishLookingAtMap()
        {
            TransitionTo(VisitorBehaviorState.Idle);
        }

        private void OnFinishVomiting()
        {
            parameters.ApplyVomitEffect();
            GameEvents.FireVisitorVomited(visitorId);
            Debug.Log($"[VisitorAI] Visitor {visitorId} vomited! {parameters}");
            TransitionTo(VisitorBehaviorState.Idle);
        }

        // ---- 目的地到着処理 ----

        private void OnReachedDestination()
        {
            StopNavigation();

            switch (currentState)
            {
                case VisitorBehaviorState.WalkingToAttraction:
                    // アトラクション前に到着 → 行列に並ぶ
                    queueWaitTimer = 0f;
                    TransitionTo(VisitorBehaviorState.WaitingInQueue);
                    break;

                case VisitorBehaviorState.WalkingToShop:
                    // ショップに到着 → タイプに応じて食事/飲料開始
                    if (targetFacilityType == FacilityType.FoodShop)
                    {
                        // 仮の食事コスト
                        float foodCost = UnityEngine.Random.Range(5f, 15f);
                        if (parameters.SpendCash(foodCost))
                        {
                            GameEvents.FireRevenueEarned(foodCost);
                            actionTimer = 0f;
                            TransitionTo(VisitorBehaviorState.Eating);
                        }
                        else
                        {
                            if (emotionBubble != null)
                                emotionBubble.ShowBubble(EmotionBubbleType.NoMoney, EmotionBubbleColor.LightBlue);
                            TransitionTo(VisitorBehaviorState.Idle);
                        }
                    }
                    else if (targetFacilityType == FacilityType.DrinkShop)
                    {
                        float drinkCost = UnityEngine.Random.Range(3f, 8f);
                        if (parameters.SpendCash(drinkCost))
                        {
                            GameEvents.FireRevenueEarned(drinkCost);
                            actionTimer = 0f;
                            TransitionTo(VisitorBehaviorState.Drinking);
                        }
                        else
                        {
                            if (emotionBubble != null)
                                emotionBubble.ShowBubble(EmotionBubbleType.NoMoney, EmotionBubbleColor.LightBlue);
                            TransitionTo(VisitorBehaviorState.Idle);
                        }
                    }
                    else
                    {
                        TransitionTo(VisitorBehaviorState.Idle);
                    }
                    break;

                case VisitorBehaviorState.WalkingToToilet:
                    actionTimer = 0f;
                    TransitionTo(VisitorBehaviorState.UsingToilet);
                    break;

                default:
                    TransitionTo(VisitorBehaviorState.Idle);
                    break;
            }
        }

        // ---- 緊急状態チェック ----

        /// <summary>
        /// 状態に関係なく割り込む緊急チェック。
        /// 嘔吐とトイレ事故は他の行動を中断して発生する。
        /// </summary>
        private void CheckEmergencyConditions()
        {
            // 嘔吐チェック
            if (parameters.IsAboutToVomit && currentState != VisitorBehaviorState.Vomiting)
            {
                // 確率的に嘔吐発生（毎フレーム判定すると即嘔吐になるので確率制御）
                float vomitChance = (parameters.Nausea - VisitorParameters.NauseaVomitThreshold)
                                    / (VisitorParameters.MaxValue - VisitorParameters.NauseaVomitThreshold);
                if (UnityEngine.Random.value < vomitChance * Time.deltaTime * 0.5f)
                {
                    InterruptCurrentAction();
                    actionTimer = 0f;
                    TransitionTo(VisitorBehaviorState.Vomiting);
                }
            }

            // トイレ事故チェック
            if (parameters.IsAboutToHaveAccident &&
                currentState != VisitorBehaviorState.UsingToilet &&
                currentState != VisitorBehaviorState.Vomiting)
            {
                parameters.ApplyAccidentEffect();
                GameEvents.FireVisitorHadAccident(visitorId);
                Debug.Log($"[VisitorAI] Visitor {visitorId} had a toilet accident! {parameters}");

                if (emotionBubble != null)
                    emotionBubble.ShowBubble(EmotionBubbleType.LookingForToilet, EmotionBubbleColor.Green);
            }
        }

        // ---- 退園判定 ----

        /// <summary>
        /// 退園すべきかどうかを判定する。
        /// 【ゲームデザイン】退園条件は複数あり、いずれかを満たすと退園する。
        /// - 幸福度が20未満（パークに不満）
        /// - 所持金が0（これ以上遊べない）
        /// - パーク閉園（営業時間終了）
        /// VIPは幸福度条件がやや厳しい（30未満で退園）。
        /// </summary>
        private bool ShouldLeavePark()
        {
            // パーク閉園
            if (isParkClosing) return true;

            // お金がなく、空腹か渇いている
            if (!parameters.HasMoney && (parameters.IsHungry || parameters.IsThirsty))
            {
                Debug.Log($"[VisitorAI] Visitor {visitorId} leaving: no money and hungry/thirsty.");
                return true;
            }

            // 幸福度が低すぎる
            float leaveThreshold = visitorType == VisitorType.VIP
                ? 30f
                : VisitorParameters.HappinessLeaveThreshold;

            if (parameters.Happiness < leaveThreshold)
            {
                Debug.Log($"[VisitorAI] Visitor {visitorId} leaving: unhappy ({parameters.Happiness:F0}).");
                return true;
            }

            return false;
        }

        /// <summary>退園完了時の処理</summary>
        private void OnLeftPark()
        {
            // SNS投稿生成（幸福度に応じて）
            if (UnityEngine.Random.value < 0.3f)
            {
                string snsContent = profile.GenerateSNSPostContent(parameters);
                GameEvents.FireSNSPostGenerated(visitorId, snsContent);
            }

            GameEvents.FireVisitorLeavePark(visitorId);
            Debug.Log($"[VisitorAI] Visitor {visitorId} left the park. Final: {parameters}");

            isInitialized = false;
        }

        // ---- 施設探索 ----

        /// <summary>
        /// 指定タイプの最寄り施設を探してナビゲーションを設定する。
        /// 実装はParkManagerの施設検索APIに委譲する想定。
        /// </summary>
        /// <returns>施設が見つかりナビゲーション設定できたらtrue</returns>
        private bool TryFindAndNavigateTo(FacilityType facilityType)
        {
            // ParkManagerから最寄り施設を検索
            // （ParkManagerの実装は別途。ここではインターフェースのみ定義）
            if (GameManager.Instance == null || GameManager.Instance.ParkManager == null)
                return false;

            Transform facility = FindNearestFacility(facilityType);
            if (facility == null) return false;

            currentTarget = facility;
            targetFacilityType = facilityType;
            NavigateTo(facility.position);
            return true;
        }

        /// <summary>
        /// 好みに合うアトラクションを探す。
        /// 【ゲームデザイン】VisitorTypeの好みカテゴリと未体験を優先。
        /// 既に乗ったアトラクションは確率的に避ける（好奇心が高いほど再訪しにくい）。
        /// </summary>
        private bool TryFindAttraction()
        {
            Transform attraction = FindPreferredAttraction();
            if (attraction == null) return false;

            currentTarget = attraction;
            targetFacilityType = FacilityType.Attraction;
            NavigateTo(attraction.position);
            return true;
        }

        /// <summary>
        /// 最寄りの指定タイプ施設を検索する。
        /// ParkManagerのAPIが実装されるまでのスタブ実装として、
        /// 物理範囲検索で代用する。
        /// </summary>
        private Transform FindNearestFacility(FacilityType facilityType)
        {
            // TODO: ParkManager.FindNearestFacility(transform.position, facilityType, facilitySearchRadius)
            //       に置き換える。現在はCollider検索でのフォールバック実装。
            Collider[] colliders = Physics.OverlapSphere(transform.position, facilitySearchRadius);
            Transform nearest = null;
            float nearestDist = float.MaxValue;

            for (int i = 0; i < colliders.Length; i++)
            {
                // 施設コンポーネントの存在チェック（将来実装）
                // var facility = colliders[i].GetComponent<FacilityBase>();
                // if (facility != null && facility.Type == facilityType)
                // {
                //     float dist = Vector3.Distance(transform.position, colliders[i].transform.position);
                //     if (dist < nearestDist)
                //     {
                //         nearestDist = dist;
                //         nearest = colliders[i].transform;
                //     }
                // }

                // タグベースの仮実装
                string expectedTag = GetFacilityTag(facilityType);
                if (!string.IsNullOrEmpty(expectedTag) && colliders[i].CompareTag(expectedTag))
                {
                    float dist = Vector3.Distance(transform.position, colliders[i].transform.position);
                    if (dist < nearestDist)
                    {
                        nearestDist = dist;
                        nearest = colliders[i].transform;
                    }
                }
            }

            return nearest;
        }

        /// <summary>
        /// 好みに合うアトラクションを検索する。
        /// 【ゲームデザイン】好みのカテゴリを優先しつつ、未体験のアトラクションを高く評価する。
        /// </summary>
        private Transform FindPreferredAttraction()
        {
            // TODO: ParkManager.GetAllAttractions() に置き換える
            Collider[] colliders = Physics.OverlapSphere(transform.position, facilitySearchRadius);
            Transform bestAttraction = null;
            float bestScore = float.MinValue;

            for (int i = 0; i < colliders.Length; i++)
            {
                if (!colliders[i].CompareTag("Attraction")) continue;

                float score = 0f;
                float distance = Vector3.Distance(transform.position, colliders[i].transform.position);

                // 距離ペナルティ（近いほど高得点）
                score -= distance * 0.5f;

                // 未体験ボーナス
                int attractionId = colliders[i].GetInstanceID();
                if (!profile.HasVisitedAttraction(attractionId))
                {
                    score += 50f * profile.Personality.Curiosity;
                }
                else
                {
                    // 再訪ペナルティ（好奇心が高いほど再訪を避ける）
                    score -= 20f * profile.Personality.Curiosity;
                }

                // TODO: アトラクションカテゴリの好み一致ボーナス
                // var attractionComp = colliders[i].GetComponent<AttractionBase>();
                // if (attractionComp != null && profile.LikesCategory(attractionComp.Category))
                //     score += 30f;
                // if (attractionComp != null && profile.DislikesCategory(attractionComp.Category))
                //     score -= 40f;

                if (score > bestScore)
                {
                    bestScore = score;
                    bestAttraction = colliders[i].transform;
                }
            }

            return bestAttraction;
        }

        // ---- ナビゲーション ----

        /// <summary>指定位置へのナビゲーションを開始する</summary>
        private void NavigateTo(Vector3 destination)
        {
            if (navAgent == null || !navAgent.enabled) return;

            navAgent.isStopped = false;
            navAgent.SetDestination(destination);
        }

        /// <summary>ナビゲーションを停止する</summary>
        private void StopNavigation()
        {
            if (navAgent == null || !navAgent.enabled) return;

            if (navAgent.hasPath)
            {
                navAgent.isStopped = true;
                navAgent.ResetPath();
            }
        }

        /// <summary>目的地に到達したかどうか</summary>
        private bool HasReachedDestination()
        {
            if (navAgent == null || !navAgent.enabled) return true;

            return !navAgent.pathPending
                && navAgent.remainingDistance <= navAgent.stoppingDistance + 0.1f
                && (!navAgent.hasPath || navAgent.velocity.sqrMagnitude < 0.01f);
        }

        /// <summary>ランダムな方向に散策する</summary>
        private void WanderRandomly()
        {
            Vector3 randomDirection = UnityEngine.Random.insideUnitSphere * wanderRadius;
            randomDirection += transform.position;

            NavMeshHit hit;
            if (NavMesh.SamplePosition(randomDirection, out hit, wanderRadius, NavMesh.AllAreas))
            {
                NavigateTo(hit.position);
            }
        }

        // ---- 状態遷移 ----

        /// <summary>指定した状態に遷移する</summary>
        private void TransitionTo(VisitorBehaviorState newState)
        {
            if (currentState == newState) return;

            previousState = currentState;
            currentState = newState;
            actionTimer = 0f;

            OnStateEntered(newState);

            Debug.Log($"[VisitorAI] Visitor {visitorId}: {previousState} -> {newState}");
        }

        /// <summary>状態に入った時の初期化処理</summary>
        private void OnStateEntered(VisitorBehaviorState state)
        {
            switch (state)
            {
                case VisitorBehaviorState.Idle:
                    StopNavigation();
                    idleTimer = 0f;
                    break;

                case VisitorBehaviorState.WaitingInQueue:
                    StopNavigation();
                    queueWaitTimer = 0f;
                    break;

                case VisitorBehaviorState.Eating:
                case VisitorBehaviorState.Drinking:
                case VisitorBehaviorState.UsingToilet:
                case VisitorBehaviorState.Resting:
                case VisitorBehaviorState.Vomiting:
                case VisitorBehaviorState.LookingAtMap:
                case VisitorBehaviorState.WatchingEntertainment:
                    StopNavigation();
                    break;

                case VisitorBehaviorState.LeavingPark:
                    NavigateToExit();
                    break;
            }
        }

        /// <summary>出口へのナビゲーションを開始する</summary>
        private void NavigateToExit()
        {
            // TODO: ParkManager.GetExitPosition() に置き換え
            // 仮実装: 原点方向を出口とする
            Vector3 exitPosition = Vector3.zero;

            NavMeshHit hit;
            if (NavMesh.SamplePosition(exitPosition, out hit, 50f, NavMesh.AllAreas))
            {
                NavigateTo(hit.position);
            }
        }

        /// <summary>現在のアクションを中断する</summary>
        private void InterruptCurrentAction()
        {
            StopNavigation();
            actionTimer = 0f;
        }

        /// <summary>時間のかかるアクション実行中かどうか</summary>
        private bool IsInAction()
        {
            switch (currentState)
            {
                case VisitorBehaviorState.Eating:
                case VisitorBehaviorState.Drinking:
                case VisitorBehaviorState.UsingToilet:
                case VisitorBehaviorState.Resting:
                case VisitorBehaviorState.Vomiting:
                case VisitorBehaviorState.WatchingEntertainment:
                case VisitorBehaviorState.LookingAtMap:
                case VisitorBehaviorState.RidingAttraction:
                case VisitorBehaviorState.TalkingToPlayer:
                    return true;
                default:
                    return false;
            }
        }

        // ---- 外部からの状態変更API ----

        /// <summary>
        /// アトラクション搭乗を開始する（アトラクションシステムから呼ばれる）。
        /// 行列から搭乗状態に遷移し、搭乗中はAIの意思決定を一時停止する。
        /// </summary>
        public void StartRiding()
        {
            TransitionTo(VisitorBehaviorState.RidingAttraction);
        }

        /// <summary>
        /// アトラクション搭乗を完了する（アトラクションシステムから呼ばれる）。
        /// 搭乗効果を適用し、体験を記憶に記録する。
        /// </summary>
        /// <param name="excitementGain">興奮度増加量</param>
        /// <param name="nauseaGain">吐き気増加量</param>
        /// <param name="satisfactionGain">満足度</param>
        /// <param name="attractionId">アトラクションID</param>
        /// <param name="attractionName">アトラクション名</param>
        public void FinishRiding(float excitementGain, float nauseaGain, float satisfactionGain,
                                  int attractionId, string attractionName)
        {
            // 搭乗効果を適用
            parameters.ApplyRideEffect(excitementGain, nauseaGain, satisfactionGain, visitorType);

            // 体験を記憶に記録
            var memory = new AttractionMemory
            {
                AttractionId = attractionId,
                AttractionName = attractionName,
                SatisfactionScore = satisfactionGain,
                VisitTime = Time.time,
                VomitedAfter = parameters.IsAboutToVomit,
                WaitDuration = queueWaitTimer
            };
            profile.RecordAttractionVisit(memory);

            // 搭乗料金
            float rideCost = UnityEngine.Random.Range(5f, 25f); // TODO: アトラクション側から取得
            if (parameters.SpendCash(rideCost))
            {
                GameEvents.FireRevenueEarned(rideCost);
            }

            GameEvents.FireVisitorHappinessChanged(visitorId, parameters.Happiness);
            Debug.Log($"[VisitorAI] Visitor {visitorId} finished riding '{attractionName}'. {parameters}");

            TransitionTo(VisitorBehaviorState.Idle);
        }

        /// <summary>
        /// AI会話を開始する（AIConversationManagerから呼ばれる）。
        /// 住人視点モードでプレイヤーに話しかけられた時に遷移する。
        /// </summary>
        public void StartConversation()
        {
            InterruptCurrentAction();
            TransitionTo(VisitorBehaviorState.TalkingToPlayer);
            StopNavigation();

            GameEvents.FireNPCConversationStarted(visitorId, "player_initiated");
        }

        /// <summary>AI会話を終了する</summary>
        public void EndConversation()
        {
            GameEvents.FireNPCConversationEnded(visitorId, "conversation_ended");
            TransitionTo(VisitorBehaviorState.Idle);
        }

        /// <summary>LLM用のプロンプトコンテキストを取得する</summary>
        public string GetConversationContext()
        {
            return profile.GenerateLLMPromptContext(parameters);
        }

        // ---- 感情バブル更新 ----

        private void UpdateEmotionBubble()
        {
            if (emotionBubble == null) return;
            emotionBubble.EvaluateAndShow(currentState, parameters);
        }

        // ---- イベント購読 ----

        private void SubscribeEvents()
        {
            GameEvents.OnParkClosed += HandleParkClosed;
            GameEvents.OnWeatherChanged += HandleWeatherChanged;
        }

        private void UnsubscribeEvents()
        {
            GameEvents.OnParkClosed -= HandleParkClosed;
            GameEvents.OnWeatherChanged -= HandleWeatherChanged;
        }

        private void HandleParkClosed()
        {
            isParkClosing = true;
        }

        private void HandleWeatherChanged(Weather newWeather)
        {
            // 雨天時の幸福度ペナルティ
            if (newWeather == Weather.Rainy)
            {
                parameters.ModifyHappiness(-5f);
            }
        }

        // ---- ユーティリティ ----

        /// <summary>
        /// VisitorType別の歩行速度を取得する。
        /// 【ゲームデザイン】Kidsは速く走り回り、Seniorはゆっくり歩く。
        /// </summary>
        private float GetWalkSpeed()
        {
            switch (visitorType)
            {
                case VisitorType.Kids:   return 4.5f;
                case VisitorType.Young:  return 4.0f;
                case VisitorType.Family: return 3.0f;
                case VisitorType.Couple: return 3.2f;
                case VisitorType.Senior: return 2.2f;
                case VisitorType.VIP:    return 3.5f;
                default: return 3.5f;
            }
        }

        /// <summary>
        /// 忍耐力に基づく行列待ち倍率。
        /// 【ゲームデザイン】Kidsは忍耐力が低く、Seniorは忍耐力が高い。
        /// 性格特性のPatience値も加味する。
        /// </summary>
        private float GetPatienceMultiplier()
        {
            float basePatience;
            switch (visitorType)
            {
                case VisitorType.Kids:   basePatience = 0.5f; break;
                case VisitorType.Young:  basePatience = 0.7f; break;
                case VisitorType.Family: basePatience = 1.0f; break;
                case VisitorType.Couple: basePatience = 0.8f; break;
                case VisitorType.Senior: basePatience = 1.2f; break;
                case VisitorType.VIP:    basePatience = 0.4f; break; // VIPは待つのが嫌い
                default: basePatience = 1.0f; break;
            }

            // 性格特性で補正（Patience: 0-1）
            float personalityBonus = profile.Personality.Patience * 0.5f;

            // 幸福度が高いほど忍耐力UP
            float happinessBonus = (parameters.Happiness / 100f) * 0.3f;

            return basePatience + personalityBonus + happinessBonus;
        }

        /// <summary>現在の天候を取得する</summary>
        private Weather GetCurrentWeather()
        {
            if (GameManager.Instance != null && GameManager.Instance.WeatherSystem != null)
            {
                // TODO: WeatherSystem.CurrentWeather プロパティから取得
                // return GameManager.Instance.WeatherSystem.CurrentWeather;
            }
            return Weather.Sunny;
        }

        /// <summary>FacilityTypeに対応するタグ名を返す（仮実装用）</summary>
        private string GetFacilityTag(FacilityType type)
        {
            switch (type)
            {
                case FacilityType.Attraction:   return "Attraction";
                case FacilityType.FoodShop:     return "FoodShop";
                case FacilityType.DrinkShop:    return "DrinkShop";
                case FacilityType.SouvenirShop: return "SouvenirShop";
                case FacilityType.Toilet:       return "Toilet";
                case FacilityType.Bench:        return "Bench";
                case FacilityType.InfoBoard:    return "InfoBoard";
                default: return null;
            }
        }
    }
}
