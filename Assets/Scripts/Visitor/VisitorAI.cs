// ============================================================
// ThemeParkGame - VisitorAI
// 来場者のAI行動制御（状態機械 + 意思決定 + パス探索）
// ============================================================

using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using ThemeParkGame.Core;
using ThemeParkGame.Park;
using ThemeParkGame.Staff;

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
        [SerializeField] private float maxQueueWaitTime = 90f;
        [SerializeField] private float wanderRadius = 15f;
        [SerializeField] private float facilitySearchRadius = 50f;
        [SerializeField] private float idleWanderInterval = 8f;

        [Header("感情バブル表示間隔")]
        [SerializeField] private float emotionBubbleInterval = 10f;

        // ---- コンポーネント参照 ----

        private NavMeshAgent navAgent;
        private EmotionBubble emotionBubble;
        private VisitorVisualController visualController;
        private VisitorStateMachine stateMachine;

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

        // NavMeshフォールバック移動
        private Vector3 fallbackDestination;
        private bool useFallbackMovement;
        private const float FallbackMoveSpeed = 3.5f;

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

        /// <summary>アクティブかどうか（初期化済み。OnLeftParkでfalseになる）</summary>
        public bool IsActive => isInitialized;

        /// <summary>現在の感情バブルタイプ</summary>
        public EmotionBubbleType CurrentEmotionType =>
            emotionBubble != null ? emotionBubble.CurrentType : EmotionBubbleType.Resting;

        /// <summary>ライフサイクルFSMへの読み取り専用アクセス</summary>
        public VisitorStateMachine StateMachine => stateMachine;

        // ---- Unity ライフサイクル ----

        private void Awake()
        {
            navAgent = GetComponent<NavMeshAgent>();
            emotionBubble = GetComponentInChildren<EmotionBubble>();
            visualController = GetComponent<VisitorVisualController>();
            stateMachine = GetComponent<VisitorStateMachine>();

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

            // 満足度0で即退場（意思決定サイクルを待たず即座に退園）
            if (parameters.Happiness <= 0f && currentState != VisitorBehaviorState.LeavingPark)
            {
                if (emotionBubble != null)
                    emotionBubble.ShowBubble(EmotionBubbleType.NotExcitingEnough, EmotionBubbleColor.Gray);
                WebGLOptimizer.LogVerbose($"[VisitorAI] Visitor {visitorId} leaving immediately: satisfaction dropped to 0.");
                InterruptCurrentAction();
                TransitionTo(VisitorBehaviorState.LeavingPark);
                return;
            }

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

            // エンターテイナー近接チェック
            CheckEntertainerProximity(dt);

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
            useFallbackMovement = false;
            if (navAgent != null)
            {
                navAgent.speed = GetWalkSpeed();
                navAgent.enabled = true;

                // NavMesh上に配置を試みる
                NavMeshHit hit;
                if (NavMesh.SamplePosition(spawnPosition, out hit, 10f, NavMesh.AllAreas))
                {
                    navAgent.Warp(hit.position);
                }
                else
                {
                    // NavMeshが無い場合はフォールバック移動モード
                    navAgent.enabled = false;
                    useFallbackMovement = true;
                }
            }

            isInitialized = true;

            // ライフサイクルFSM初期化
            if (stateMachine != null)
                stateMachine.Initialize(visitorId);

            GameEvents.FireVisitorEnterPark(visitorId);
            GameEvents.FireVisitorHappinessChanged(visitorId, parameters.Happiness);

            // 入場料を徴収
            if (GameManager.Instance?.EconomyManager != null)
            {
                GameManager.Instance.EconomyManager.ChargeEntranceFee();
            }

            WebGLOptimizer.LogVerbose($"[VisitorAI] Visitor {visitorId} ({visitorType}) entered the park. {parameters}");
        }

        /// <summary>来場者をリセットする（オブジェクトプール再利用時）</summary>
        public void ResetVisitor()
        {
            isInitialized = false;
            currentState = VisitorBehaviorState.Idle;
            currentTarget = null;
            currentTargetFacilityId = -1;
            useFallbackMovement = false;

            if (navAgent != null)
            {
                if (navAgent.isOnNavMesh)
                    navAgent.ResetPath();
                navAgent.enabled = false;
            }

            if (emotionBubble != null)
            {
                emotionBubble.ForceHide();
            }

            if (visualController != null)
            {
                visualController.ResetVisual();
            }

            if (stateMachine != null)
            {
                stateMachine.ResetStateMachine();
            }
        }

        // ---- 状態機械: 意思決定 ----

        /// <summary>
        /// 現在のパラメータに基づいて次の行動を決定する。
        /// 【ゲームデザイン】欲求駆動型意思決定。最も切迫した欲求を優先する。
        ///
        /// 優先順位:
        /// 1. トイレ（生理的に最も緊急）
        /// 2. 空腹 → フードショップ（渇きより不快度が高い場合優先）
        /// 3. 渇き → ドリンクショップ
        /// 4. 興奮低下 → アトラクション（残高+価格許容度を考慮）
        /// 5. 疲労/退屈 → ベンチで休憩
        /// 6. お土産欲求 → スーベニアショップ（一定確率で発生）
        /// 7. Idle → 散策/マップ確認
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

            // 優先度1: トイレ欲求（最も緊急）
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
                    if (emotionBubble != null)
                        emotionBubble.ShowBubble(EmotionBubbleType.LookingForToilet, EmotionBubbleColor.Green);
                }
                return;
            }

            // 優先度2: 空腹（渇きより不快度が高い場合優先）
            if (parameters.IsHungry && parameters.Hunger >= parameters.Thirst)
            {
                if (currentState != VisitorBehaviorState.WalkingToShop &&
                    currentState != VisitorBehaviorState.Eating)
                {
                    if (CanAffordShop(FacilityType.FoodShop) &&
                        TryFindAffordableShop(FacilityType.FoodShop))
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
                    if (CanAffordShop(FacilityType.DrinkShop) &&
                        TryFindAffordableShop(FacilityType.DrinkShop))
                    {
                        targetFacilityType = FacilityType.DrinkShop;
                        TransitionTo(VisitorBehaviorState.WalkingToShop);
                        return;
                    }
                }
                return;
            }

            // 優先度4: 興奮を求める → アトラクションに行く（所持金がある場合のみ）
            if (parameters.Excitement < 85f && parameters.HasMoney)
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

            // 優先度5: 疲労時の休憩（幸福度低下 AND 興奮度低下）
            if (parameters.Happiness < 60f && parameters.Excitement < 30f)
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

            // 優先度5.5: 悪天候時のシェルター探し
            if (IsWeatherBad() && currentState == VisitorBehaviorState.Idle)
            {
                if (TryFindAndNavigateTo(FacilityType.Bench))
                {
                    TransitionTo(VisitorBehaviorState.Resting);
                    if (emotionBubble != null)
                        emotionBubble.ShowBubble(EmotionBubbleType.Resting, EmotionBubbleColor.Blue);
                    return;
                }
            }

            // 優先度6: お土産欲求（一定条件で発動）
            // 幸福度が高い時にお土産を買いたくなる。VIPとCoupleは確率が高い。
            if (ShouldBuySouvenir())
            {
                if (currentState != VisitorBehaviorState.WalkingToShop)
                {
                    if (CanAffordShop(FacilityType.SouvenirShop) &&
                        TryFindAffordableShop(FacilityType.SouvenirShop))
                    {
                        targetFacilityType = FacilityType.SouvenirShop;
                        TransitionTo(VisitorBehaviorState.WalkingToShop);
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

        // ---- 価格許容度 & ショップ選択 ----

        /// <summary>
        /// VisitorType別の価格許容度倍率を返す。
        /// 【ゲームデザイン】VIPは高い価格を許容し、Kidsは低価格を好む。
        /// 返値は適正価格の何倍まで許容するかを示す。
        /// </summary>
        private float GetPriceToleranceMultiplier()
        {
            switch (visitorType)
            {
                case VisitorType.Kids:   return 1.0f;  // 安さ重視
                case VisitorType.Young:  return 1.2f;
                case VisitorType.Family: return 1.3f;
                case VisitorType.Couple: return 1.5f;  // デートなので多少高くてもOK
                case VisitorType.Senior: return 1.1f;
                case VisitorType.VIP:    return 2.0f;  // 金額は気にしない
                default: return 1.2f;
            }
        }

        /// <summary>指定タイプのショップに支払える最低限の所持金があるかチェック</summary>
        private bool CanAffordShop(FacilityType shopType)
        {
            // 最低購入額の見積もり（タイプ別）
            float minExpectedPrice;
            switch (shopType)
            {
                case FacilityType.FoodShop:     minExpectedPrice = 3f;  break;
                case FacilityType.DrinkShop:    minExpectedPrice = 2f;  break;
                case FacilityType.SouvenirShop: minExpectedPrice = 5f;  break;
                default: minExpectedPrice = 1f; break;
            }
            return parameters.Cash >= minExpectedPrice;
        }

        /// <summary>
        /// 価格許容範囲内で最寄りのショップを探してナビゲーションを設定する。
        /// Shopコンポーネントの実際の販売価格を参照し、
        /// VisitorType別の価格許容度を超えるショップを除外する。
        /// </summary>
        private bool TryFindAffordableShop(FacilityType shopType)
        {
            string tag = GetFacilityTag(shopType);
            GameObject[] tagged = GetCachedFacilitiesByTag(tag);

            if (tagged.Length == 0)
            {
                // キャッシュに無い場合はフォールバック
                return TryFindAndNavigateTo(shopType);
            }

            Transform bestShop = null;
            float bestScore = float.MinValue;
            float priceTolerance = GetPriceToleranceMultiplier();

            for (int i = 0; i < tagged.Length; i++)
            {
                if (tagged[i] == null) continue;

                float dist = Vector3.Distance(transform.position, tagged[i].transform.position);
                if (dist > facilitySearchRadius) continue;

                // Shopコンポーネントから実際の価格を取得
                var shopComp = tagged[i].GetComponent<ThemeParkGame.Attraction.Shop>();
                if (shopComp == null) continue;

                // 在庫切れチェック
                if (shopComp.IsOutOfStock) continue;

                // 価格チェック: 販売価格が所持金を超えていたらスキップ
                if (shopComp.SellingPrice > parameters.Cash) continue;

                // 価格許容度チェック: 適正価格の許容倍率を超えていたらスコアペナルティ
                float priceRatio = (float)shopComp.SellingPrice / Mathf.Max(1, shopComp.WholesalePrice);
                float priceScore = 0f;
                if (priceRatio <= priceTolerance * 2f)
                {
                    priceScore = 10f; // 適正価格内
                }
                else
                {
                    priceScore = -20f; // 高すぎる
                }

                // 総合スコア: 近いほど高い + 価格適正度
                float score = priceScore - dist * 0.3f;

                if (score > bestScore)
                {
                    bestScore = score;
                    bestShop = tagged[i].transform;
                }
            }

            if (bestShop == null) return false;

            currentTarget = bestShop;
            targetFacilityType = shopType;
            NavigateTo(bestShop.position);
            return true;
        }

        /// <summary>
        /// お土産を買うべきかどうかを判定する。
        /// 【ゲームデザイン】幸福度が高い来場者はお土産を買いやすい。
        /// VIPとCoupleは特にお土産を買いやすい。
        /// 既にお土産を買っている場合は確率が下がる。
        /// </summary>
        private bool ShouldBuySouvenir()
        {
            // 幸福度が低い時はお土産に興味がない
            if (parameters.Happiness < 50f) return false;

            // 所持金チェック
            if (parameters.Cash < 10f) return false;

            float baseChance = 0.02f; // 意思決定ごとの基本確率

            // 来場者タイプ補正
            switch (visitorType)
            {
                case VisitorType.VIP:    baseChance *= 3.0f; break;
                case VisitorType.Couple: baseChance *= 2.0f; break;
                case VisitorType.Family: baseChance *= 1.5f; break;
                case VisitorType.Kids:   baseChance *= 1.2f; break;
                case VisitorType.Young:  baseChance *= 0.8f; break;
                case VisitorType.Senior: baseChance *= 1.0f; break;
            }

            // 幸福度ボーナス（高いほど買いたくなる）
            baseChance *= (parameters.Happiness / 100f);

            // 既にお土産を買った回数による確率低下
            int souvenirCount = profile.GetSouvenirPurchaseCount();
            baseChance /= (1f + souvenirCount * 0.5f);

            return UnityEngine.Random.value < baseChance;
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
            // StopNavigationはOnStateEntered(Idle)で一度だけ呼ばれる。
            // ここで毎フレーム呼ぶとWanderRandomlyのパスがキャンセルされるため削除。
        }

        private void ExecuteWalking(float deltaTime)
        {
            // 通路混雑度による歩行速度調整
            UpdateWalkSpeedByCongestion();

            // フォールバック移動
            if (useFallbackMovement)
            {
                MoveFallback(deltaTime);
            }

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

                WebGLOptimizer.LogVerbose($"[VisitorAI] Visitor {visitorId} left queue after {queueWaitTimer:F0}s (patience exceeded)");

                // アトラクションのキューから自分を除去する
                LeaveAttractionQueue();

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
            // フォールバック移動
            if (useFallbackMovement)
            {
                MoveFallback(deltaTime);
            }

            // 出口に到着したか
            bool reached = HasReachedDestination();
            bool noPath = navAgent == null || !navAgent.enabled || !navAgent.isOnNavMesh || !navAgent.hasPath;

            if (reached || (noPath && !useFallbackMovement))
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
            WebGLOptimizer.LogVerbose($"[VisitorAI] Visitor {visitorId} finished eating. {parameters}");
            TransitionTo(VisitorBehaviorState.Idle);
        }

        private void OnFinishDrinking()
        {
            // 飲料完了: 渇き減少、トイレ欲求増加
            parameters.ApplyDrinkEffect(
                thirstReduction: UnityEngine.Random.Range(50f, 70f),
                qualityBonus: UnityEngine.Random.Range(2f, 5f)
            );
            WebGLOptimizer.LogVerbose($"[VisitorAI] Visitor {visitorId} finished drinking. {parameters}");
            TransitionTo(VisitorBehaviorState.Idle);
        }

        private void OnFinishToilet()
        {
            // トイレ完了: トイレ欲求リセット
            parameters.ApplyToiletEffect(
                cleanlinessBonus: UnityEngine.Random.Range(1f, 5f)
            );
            WebGLOptimizer.LogVerbose($"[VisitorAI] Visitor {visitorId} finished using toilet. {parameters}");
            TransitionTo(VisitorBehaviorState.Idle);
        }

        private void OnFinishResting()
        {
            // 休憩完了: 幸福度少し回復
            parameters.ModifyHappiness(3f);
            WebGLOptimizer.LogVerbose($"[VisitorAI] Visitor {visitorId} finished resting. {parameters}");
            TransitionTo(VisitorBehaviorState.Idle);
        }

        private void OnFinishWatching()
        {
            // ショー鑑賞完了: 興奮度UP、幸福度UP
            parameters.ModifyExcitement(UnityEngine.Random.Range(10f, 25f));
            parameters.ModifyHappiness(UnityEngine.Random.Range(5f, 15f));
            WebGLOptimizer.LogVerbose($"[VisitorAI] Visitor {visitorId} finished watching show. {parameters}");
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
            WebGLOptimizer.LogVerbose($"[VisitorAI] Visitor {visitorId} vomited! {parameters}");
            TransitionTo(VisitorBehaviorState.Idle);
        }

        /// <summary>
        /// ショップ到着時の統合処理。
        /// Shopコンポーネントを取得し、実際の販売価格で購入→アクション開始。
        /// </summary>
        private void OnArrivedAtShop()
        {
            // Shopコンポーネント取得
            ThemeParkGame.Attraction.Shop shopComp = null;
            if (currentTarget != null)
            {
                shopComp = currentTarget.GetComponent<ThemeParkGame.Attraction.Shop>();
            }

            float actualPrice;
            if (shopComp != null)
            {
                // Shopコンポーネントの実際の販売価格を使用
                actualPrice = shopComp.SellingPrice;

                // 在庫切れチェック
                if (shopComp.IsOutOfStock)
                {
                    if (emotionBubble != null)
                        emotionBubble.ShowBubble(EmotionBubbleType.FoodTastesBad, EmotionBubbleColor.Gray);
                    parameters.ModifyHappiness(-3f);
                    TransitionTo(VisitorBehaviorState.Idle);
                    return;
                }

                // ShopのOnVisitorArriveで行列/サービスを開始
                shopComp.OnVisitorArrive(visitorId);
            }
            else
            {
                // Shopコンポーネントが無い場合のフォールバック価格
                actualPrice = targetFacilityType switch
                {
                    FacilityType.FoodShop => UnityEngine.Random.Range(5f, 15f),
                    FacilityType.DrinkShop => UnityEngine.Random.Range(3f, 8f),
                    FacilityType.SouvenirShop => UnityEngine.Random.Range(8f, 30f),
                    _ => 5f
                };
            }

            // 支払い
            if (!parameters.SpendCash(actualPrice))
            {
                if (emotionBubble != null)
                    emotionBubble.ShowBubble(EmotionBubbleType.NoMoney, EmotionBubbleColor.LightBlue);
                TransitionTo(VisitorBehaviorState.Idle);
                return;
            }

            // 収益計上（Shopコンポーネントが無い場合のみ直接計上）
            if (shopComp == null)
            {
                if (GameManager.Instance != null && GameManager.Instance.EconomyManager != null)
                {
                    GameManager.Instance.EconomyManager.AddRevenue(
                        actualPrice,
                        ThemeParkGame.Economy.RevenueCategory.ShopSale,
                        currentTargetFacilityId);
                }
                else
                {
                    GameEvents.FireRevenueEarned(actualPrice);
                }
            }

            // アクション開始
            actionTimer = 0f;
            switch (targetFacilityType)
            {
                case FacilityType.FoodShop:
                    TransitionTo(VisitorBehaviorState.Eating);
                    break;
                case FacilityType.DrinkShop:
                    TransitionTo(VisitorBehaviorState.Drinking);
                    break;
                case FacilityType.SouvenirShop:
                    // お土産購入: 幸福度UP、購入記録
                    float happyBonus = UnityEngine.Random.Range(5f, 15f);
                    parameters.ModifyHappiness(happyBonus);
                    profile.RecordSouvenirPurchase();
                    if (emotionBubble != null)
                        emotionBubble.ShowBubble(EmotionBubbleType.LovingIt, EmotionBubbleColor.Blue);
                    WebGLOptimizer.LogVerbose($"[VisitorAI] Visitor {visitorId} bought souvenir for {actualPrice}. {parameters}");
                    TransitionTo(VisitorBehaviorState.Idle);
                    break;
                default:
                    TransitionTo(VisitorBehaviorState.Idle);
                    break;
            }
        }

        // ---- 目的地到着処理 ----

        private void OnReachedDestination()
        {
            StopNavigation();

            switch (currentState)
            {
                case VisitorBehaviorState.WalkingToAttraction:
                    // アトラクション前に到着 → チケット購入 → キューに登録
                    if (currentTarget != null)
                    {
                        var attraction = currentTarget.GetComponent<ThemeParkGame.Attraction.Attraction>();
                        if (attraction != null && attraction.CanAcceptVisitor(visitorId))
                        {
                            // チケット料金を支払う
                            float ticketCost = attraction.TicketPrice;
                            if (!parameters.SpendCash(ticketCost))
                            {
                                // お金が足りない → 別行動
                                WebGLOptimizer.LogVerbose($"[VisitorAI] Visitor {visitorId} can't afford {attraction.DisplayName} (${ticketCost})");
                                TransitionTo(VisitorBehaviorState.Idle);
                            }
                            else if (attraction.OnVisitorArrive(visitorId))
                            {
                                queueWaitTimer = 0f;
                                TransitionTo(VisitorBehaviorState.WaitingInQueue);
                            }
                            else
                            {
                                // キューが満員 → 返金して別行動
                                parameters.AddCash(ticketCost);
                                WebGLOptimizer.LogVerbose($"[VisitorAI] Visitor {visitorId} couldn't join queue at {attraction.DisplayName}");
                                TransitionTo(VisitorBehaviorState.Idle);
                            }
                        }
                        else
                        {
                            WebGLOptimizer.LogVerbose($"[VisitorAI] Visitor {visitorId} arrived but attraction unavailable");
                            TransitionTo(VisitorBehaviorState.Idle);
                        }
                    }
                    else
                    {
                        TransitionTo(VisitorBehaviorState.Idle);
                    }
                    break;

                case VisitorBehaviorState.WalkingToShop:
                    OnArrivedAtShop();
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
                WebGLOptimizer.LogVerbose($"[VisitorAI] Visitor {visitorId} had a toilet accident! {parameters}");

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

            // お金がなくなった場合:
            // 幸福度が高い(>=50)なら散策を楽しめるのでまだ滞在
            // 幸福度が低い場合は退園
            if (!parameters.HasMoney)
            {
                if (parameters.Happiness >= 50f)
                {
                    // 散策モードに切り替え（アトラクション/ショップには行かない）
                    return false;
                }
                return true;
            }

            // 幸福度が低すぎる
            float leaveThreshold = visitorType == VisitorType.VIP
                ? 30f
                : VisitorParameters.HappinessLeaveThreshold;

            if (parameters.Happiness < leaveThreshold)
            {
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
            WebGLOptimizer.LogVerbose($"[VisitorAI] Visitor {visitorId} left the park. Final: {parameters}");

            isInitialized = false;
        }

        // ---- 通路混雑による速度調整 ----

        private static PathwaySystem s_pathwaySystem;
        private static float s_pathwayCacheTime = -1f;
        private float _congestionSpeedTimer;

        /// <summary>
        /// 通路の混雑度に応じてNavMeshAgentの速度を調整する。
        /// 混雑しているほど遅くなる（最大40%減速）。
        /// </summary>
        private void UpdateWalkSpeedByCongestion()
        {
            _congestionSpeedTimer -= Time.deltaTime;
            if (_congestionSpeedTimer > 0f) return;
            _congestionSpeedTimer = 1f; // 1秒間隔

            // PathwaySystemのキャッシュ
            if (Time.time - s_pathwayCacheTime > 3f || s_pathwaySystem == null)
            {
                s_pathwaySystem = UnityEngine.Object.FindObjectOfType<PathwaySystem>();
                s_pathwayCacheTime = Time.time;
            }

            float baseSpeed = GetWalkSpeed();

            if (s_pathwaySystem != null)
            {
                float congestion = s_pathwaySystem.GetCongestionAt(transform.position);
                // 混雑度0→100%速度、混雑度1→60%速度
                float speedMul = Mathf.Lerp(1f, 0.6f, congestion);
                float targetSpeed = baseSpeed * speedMul;

                if (navAgent != null && navAgent.enabled && navAgent.isOnNavMesh)
                {
                    navAgent.speed = targetSpeed;
                }
            }
        }

        // ---- 施設探索 ----

        /// <summary>
        /// 指定タイプの最寄り施設を探してナビゲーションを設定する。
        /// 実装はParkManagerの施設検索APIに委譲する想定。
        /// </summary>
        /// <returns>施設が見つかりナビゲーション設定できたらtrue</returns>
        private bool TryFindAndNavigateTo(FacilityType facilityType)
        {
            // タグベースで最寄り施設を検索（ParkManager不要）
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
        // ---- 施設キャッシュ（FindGameObjectsWithTag最適化） ----

        /// <summary>タグ別施設GameObjectキャッシュ。全VisitorAIインスタンスで共有する。</summary>
        private static readonly Dictionary<string, GameObject[]> s_facilityCache =
            new Dictionary<string, GameObject[]>();

        /// <summary>キャッシュの最終更新時刻</summary>
        private static float s_facilityCacheTime = -1f;

        /// <summary>キャッシュの有効期間（秒）</summary>
        private const float FacilityCacheLifetime = 2f;

        /// <summary>キャッシュを更新する（有効期限切れの場合のみ）</summary>
        private static GameObject[] GetCachedFacilitiesByTag(string tag)
        {
            if (string.IsNullOrEmpty(tag)) return System.Array.Empty<GameObject>();

            float now = Time.time;
            if (now - s_facilityCacheTime > FacilityCacheLifetime)
            {
                s_facilityCache.Clear();
                s_facilityCacheTime = now;
            }

            if (!s_facilityCache.TryGetValue(tag, out GameObject[] cached))
            {
                cached = GameObject.FindGameObjectsWithTag(tag);
                s_facilityCache[tag] = cached;
            }

            return cached;
        }

        /// <summary>施設キャッシュを強制的にクリアする（施設の建設・撤去時に呼ぶ）</summary>
        public static void InvalidateFacilityCache()
        {
            s_facilityCache.Clear();
            s_facilityCacheTime = -1f;
        }

        private Transform FindNearestFacility(FacilityType facilityType)
        {
            string tag = GetFacilityTag(facilityType);

            // キャッシュされた施設リストから最寄りを検索する
            GameObject[] tagged = GetCachedFacilitiesByTag(tag);
            if (tagged.Length > 0)
            {
                Transform nearest = null;
                float nearestDist = float.MaxValue;

                for (int i = 0; i < tagged.Length; i++)
                {
                    if (tagged[i] == null) continue;
                    float dist = Vector3.Distance(transform.position, tagged[i].transform.position);
                    if (dist < nearestDist && dist <= facilitySearchRadius)
                    {
                        nearestDist = dist;
                        nearest = tagged[i].transform;
                    }
                }

                if (nearest != null) return nearest;
            }

            // フォールバック: Physics.OverlapSphereで近傍検索
            Collider[] colliders = Physics.OverlapSphere(transform.position, facilitySearchRadius);
            Transform nearestFallback = null;
            float nearestDistFallback = float.MaxValue;

            for (int i = 0; i < colliders.Length; i++)
            {
                if (!string.IsNullOrEmpty(tag) && colliders[i].CompareTag(tag))
                {
                    float dist = Vector3.Distance(transform.position, colliders[i].transform.position);
                    if (dist < nearestDistFallback)
                    {
                        nearestDistFallback = dist;
                        nearestFallback = colliders[i].transform;
                    }
                }
            }

            return nearestFallback;
        }

        /// <summary>
        /// 好みに合うアトラクションを検索する。
        /// 【ゲームデザイン】好みのカテゴリを優先しつつ、未体験のアトラクションを高く評価する。
        /// </summary>
        private Transform FindPreferredAttraction()
        {
            // キャッシュからアトラクションを取得
            GameObject[] tagged = GetCachedFacilitiesByTag("Attraction");
            Transform bestAttraction = null;
            float bestScore = float.MinValue;

            for (int i = 0; i < tagged.Length; i++)
            {
                if (tagged[i] == null) continue;

                float distance = Vector3.Distance(transform.position, tagged[i].transform.position);
                if (distance > facilitySearchRadius) continue;

                // アトラクションコンポーネント取得
                var attractionComp = tagged[i].GetComponent<ThemeParkGame.Attraction.Attraction>();
                if (attractionComp == null) continue;

                // 稼働中でないアトラクションはスキップ
                if (!attractionComp.IsOperating) continue;

                // キューが満杯ならスキップ
                if (!attractionComp.CanAcceptVisitor(visitorId)) continue;

                // チケット価格チェック: 所持金で払えるか
                if (attractionComp.TicketPrice > parameters.Cash) continue;

                float score = 0f;

                // 距離ペナルティ（近いほど高得点）
                score -= distance * 0.5f;

                // 未体験ボーナス
                int attractionId = tagged[i].GetInstanceID();
                if (!profile.HasVisitedAttraction(attractionId))
                {
                    score += 50f * profile.Personality.Curiosity;
                }
                else
                {
                    // 再訪ペナルティ（好奇心が高いほど再訪を避ける）
                    score -= 20f * profile.Personality.Curiosity;
                }

                // アトラクションカテゴリの好み一致ボーナス
                if (attractionComp.Data != null)
                {
                    switch (visitorType)
                    {
                        case VisitorType.Kids:
                            // キッズ: 低嘔吐率を好む
                            if (attractionComp.Data.NauseaFactor < 0.3f) score += 30f;
                            if (attractionComp.Data.NauseaFactor > 0.6f) score -= 40f;
                            break;
                        case VisitorType.Young:
                            // ヤング: 高興奮度を好む
                            if (attractionComp.EffectiveExcitement > 6f) score += 30f;
                            if (attractionComp.EffectiveExcitement > 8f) score += 20f;
                            break;
                        case VisitorType.Family:
                            // ファミリー: 中程度の刺激、低嘔吐率
                            if (attractionComp.Data.NauseaFactor < 0.5f) score += 20f;
                            if (attractionComp.EffectiveExcitement >= 3f &&
                                attractionComp.EffectiveExcitement <= 7f) score += 15f;
                            break;
                        case VisitorType.Couple:
                            // カップル: 観覧車/ショー系を好む
                            if (attractionComp.EffectiveExcitement <= 5f) score += 15f;
                            break;
                        case VisitorType.Senior:
                            // シニア: 低嘔吐率・低興奮度を好む
                            if (attractionComp.Data.NauseaFactor < 0.2f) score += 25f;
                            if (attractionComp.Data.NauseaFactor > 0.5f) score -= 50f;
                            break;
                        case VisitorType.VIP:
                            // VIP: 高評価アトラクションを好む
                            score += attractionComp.SatisfactionRating * 30f;
                            break;
                    }
                }

                // 待ち行列の短さボーナス
                float queueRatio = (float)attractionComp.QueueLength / attractionComp.MaxQueueLength;
                score -= queueRatio * 20f;

                // 価格妥当性ボーナス
                float priceTolerance = GetPriceToleranceMultiplier();
                if (attractionComp.Data != null)
                {
                    float priceRatio = (float)attractionComp.TicketPrice /
                                       Mathf.Max(1, attractionComp.Data.SuggestedTicketPrice);
                    if (priceRatio <= priceTolerance)
                        score += 10f;
                    else
                        score -= (priceRatio - priceTolerance) * 15f;
                }

                // 満足度評判ボーナス
                score += attractionComp.SatisfactionRating * 15f;

                if (score > bestScore)
                {
                    bestScore = score;
                    bestAttraction = tagged[i].transform;
                }
            }

            return bestAttraction;
        }

        // ---- ナビゲーション ----

        /// <summary>指定位置へのナビゲーションを開始する</summary>
        private void NavigateTo(Vector3 destination)
        {
            useFallbackMovement = false;
            fallbackDestination = destination;

            if (navAgent != null && navAgent.enabled && navAgent.isOnNavMesh)
            {
                navAgent.isStopped = false;
                if (!navAgent.SetDestination(destination))
                {
                    // NavMeshパス設定失敗 → フォールバック
                    useFallbackMovement = true;
                }
            }
            else
            {
                // NavMeshAgentが使えない → フォールバック移動
                useFallbackMovement = true;
            }
        }

        /// <summary>ナビゲーションを停止する</summary>
        private void StopNavigation()
        {
            useFallbackMovement = false;

            if (navAgent != null && navAgent.enabled && navAgent.isOnNavMesh)
            {
                if (navAgent.hasPath)
                {
                    navAgent.isStopped = true;
                    navAgent.ResetPath();
                }
            }
        }

        /// <summary>目的地に到達したかどうか</summary>
        private bool HasReachedDestination()
        {
            if (useFallbackMovement)
            {
                float dist = Vector3.Distance(transform.position, fallbackDestination);
                return dist <= 2.0f;
            }

            if (navAgent == null || !navAgent.enabled || !navAgent.isOnNavMesh)
                return true;

            return !navAgent.pathPending
                && navAgent.remainingDistance <= navAgent.stoppingDistance + 0.1f
                && (!navAgent.hasPath || navAgent.velocity.sqrMagnitude < 0.01f);
        }

        /// <summary>ランダムな方向に散策する</summary>
        private void WanderRandomly()
        {
            Vector3 randomDirection = UnityEngine.Random.insideUnitSphere * wanderRadius;
            randomDirection.y = 0f;
            randomDirection += transform.position;

            NavMeshHit hit;
            if (NavMesh.SamplePosition(randomDirection, out hit, wanderRadius, NavMesh.AllAreas))
            {
                NavigateTo(hit.position);
            }
            else
            {
                // NavMeshなし → フォールバック直接移動
                NavigateTo(randomDirection);
            }
        }

        /// <summary>NavMesh不使用時のフォールバック移動（Transform直接操作）</summary>
        private void MoveFallback(float deltaTime)
        {
            if (!useFallbackMovement) return;

            Vector3 direction = fallbackDestination - transform.position;
            direction.y = 0f;

            if (direction.sqrMagnitude < 0.1f) return;

            float speed = (navAgent != null) ? navAgent.speed : FallbackMoveSpeed;
            Vector3 move = direction.normalized * speed * deltaTime;

            // 目的地を超えないようにクランプ
            if (move.sqrMagnitude > direction.sqrMagnitude)
            {
                transform.position = new Vector3(fallbackDestination.x, transform.position.y, fallbackDestination.z);
            }
            else
            {
                transform.position += move;
            }

            // 進行方向を向く
            if (direction.sqrMagnitude > 0.01f)
            {
                transform.rotation = Quaternion.LookRotation(direction.normalized);
            }
        }

        // ---- 状態遷移 ----

        /// <summary>指定した状態に遷移する</summary>
        private void TransitionTo(VisitorBehaviorState newState)
        {
            if (currentState == newState) return;

            // WaitingInQueue から RidingAttraction 以外への遷移時はキューから自分を除去
            if (currentState == VisitorBehaviorState.WaitingInQueue &&
                newState != VisitorBehaviorState.RidingAttraction)
            {
                LeaveAttractionQueue();
            }

            previousState = currentState;
            currentState = newState;
            actionTimer = 0f;

            OnStateEntered(newState);

            // ビジュアルコントローラーに状態変更を通知
            if (visualController != null)
                visualController.OnStateChanged(newState);

            // ライフサイクルFSMに状態変更を通知
            if (stateMachine != null)
                stateMachine.OnBehaviorStateChanged(newState);

            WebGLOptimizer.LogVerbose($"[VisitorAI] Visitor {visitorId}: {previousState} -> {newState}");
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
            // パーク出口位置を取得（ParkExitタグで検索、フォールバックは原点）
            Vector3 exitPosition = Vector3.zero;

            // VisitorManagerの出口位置を優先
            if (GameManager.Instance != null && GameManager.Instance.VisitorManager != null)
            {
                exitPosition = GameManager.Instance.VisitorManager.ExitPosition;
            }

            // ParkExitタグでも検索
            if (exitPosition == Vector3.zero)
            {
                GameObject exitObj = GameObject.FindGameObjectWithTag("ParkExit");
                if (exitObj != null)
                {
                    exitPosition = exitObj.transform.position;
                }
            }

            NavMeshHit hit;
            if (NavMesh.SamplePosition(exitPosition, out hit, 50f, NavMesh.AllAreas))
            {
                NavigateTo(hit.position);
            }
            else
            {
                // NavMeshなし → フォールバック直接移動
                NavigateTo(exitPosition);
            }
        }

        /// <summary>現在のアクションを中断する</summary>
        private void InterruptCurrentAction()
        {
            StopNavigation();
            actionTimer = 0f;
        }

        /// <summary>アトラクションの行列から自分を除去する（忍耐切れ等）</summary>
        private void LeaveAttractionQueue()
        {
            if (currentTarget == null) return;
            var attraction = currentTarget.GetComponent<ThemeParkGame.Attraction.Attraction>();
            if (attraction != null)
            {
                attraction.RemoveFromQueue(visitorId);
            }
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
            float oldSat = parameters.Satisfaction;

            // 搭乗効果を適用（ApplyRideEffect内でSatisfactionも更新される）
            parameters.ApplyRideEffect(excitementGain, nauseaGain, satisfactionGain, visitorType);

            // 来場者の好みカテゴリに応じた満足度ボーナス
            ApplyPreferenceBonus(attractionId);

            float satDelta = parameters.Satisfaction - oldSat;

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

            GameEvents.FireVisitorHappinessChanged(visitorId, parameters.Happiness);
            GameEvents.FireVisitorSatisfactionChanged(visitorId, parameters.Satisfaction, satDelta);
            WebGLOptimizer.LogVerbose($"[VisitorAI] Visitor {visitorId} finished riding '{attractionName}'. Sat:{parameters.Satisfaction:F0}(Δ{satDelta:+0.0;-0.0}) {parameters}");

            TransitionTo(VisitorBehaviorState.Idle);
        }

        /// <summary>来場者の好みに応じた満足度ボーナスを適用する</summary>
        private void ApplyPreferenceBonus(int attractionId)
        {
            // AttractionManagerから該当アトラクションを探す
            var gm = GameManager.Instance;
            if (gm == null || gm.AttractionManager == null) return;

            var attraction = gm.AttractionManager.GetAttractionById(attractionId);
            if (attraction == null || attraction.Data == null) return;

            var category = attraction.Data.Category;
            if (profile.LikesCategory(category))
            {
                // 好みのカテゴリ → 追加ボーナス
                parameters.ModifySatisfaction(3f);
            }
            else if (profile.DislikesCategory(category))
            {
                // 苦手なカテゴリ → ペナルティ
                parameters.ModifySatisfaction(-2f);
            }
        }

        /// <summary>
        /// アトラクション故障/事故により行列から追い出された時に呼ばれる。
        /// WaitingInQueue → Idle に遷移し、幸福度にペナルティを適用する。
        /// </summary>
        public void OnQueueAbandoned()
        {
            if (currentState == VisitorBehaviorState.WaitingInQueue)
            {
                parameters.ModifyHappiness(-15f);
                if (emotionBubble != null)
                    emotionBubble.ShowBubble(EmotionBubbleType.LongWait, EmotionBubbleColor.Gray);
                WebGLOptimizer.LogVerbose($"[VisitorAI] Visitor {visitorId} forced out of queue (attraction breakdown).");
                TransitionTo(VisitorBehaviorState.Idle);
            }
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
                return GameManager.Instance.WeatherSystem.CurrentWeather;
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

        /// <summary>悪天候かどうかを判定する（雨・雪）</summary>
        private bool IsWeatherBad()
        {
            Weather w = GetCurrentWeather();
            return w == Weather.Rainy || w == Weather.Snowy;
        }

        /// <summary>
        /// エンターテイナーとの近接判定。
        /// 近くにエンターテイナーがいると幸福度と興奮度が少し回復する。
        /// </summary>
        private float _entertainerCheckTimer;
        private void CheckEntertainerProximity(float dt)
        {
            _entertainerCheckTimer -= dt;
            if (_entertainerCheckTimer > 0f) return;
            _entertainerCheckTimer = 5f; // 5秒ごとにチェック

            var sm = GameManager.Instance?.StaffManager;
            if (sm == null) return;

            foreach (var staff in sm.GetAllStaff())
            {
                if (staff.StaffType != StaffType.Entertainer) continue;
                if (staff == null || !staff.gameObject.activeInHierarchy) continue;
                float dist = Vector3.Distance(transform.position, staff.transform.position);
                if (dist < 8f)
                {
                    // エンターテイナーの近くにいると幸福度+3、興奮+5
                    parameters.ModifyHappiness(3f);
                    parameters.ModifyExcitement(5f);
                    if (emotionBubble != null && UnityEngine.Random.value < 0.3f)
                        emotionBubble.ShowBubble(EmotionBubbleType.Happy, EmotionBubbleColor.Pink);
                    break; // 1回のチェックで1人のエンターテイナーのみ
                }
            }
        }
    }
}
