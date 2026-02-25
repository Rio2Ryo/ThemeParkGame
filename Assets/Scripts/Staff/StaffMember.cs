// ============================================================
// ThemeParkGame - StaffMember (Base Class)
// 全スタッフ共通の基底クラス
// テーマパークワールドのスタッフAI基盤
// ============================================================

using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using ThemeParkGame.Core;

namespace ThemeParkGame.Staff
{
    /// <summary>
    /// スタッフの基底クラス。移動・疲労・ストライキ・訓練などの共通ロジックを提供する。
    /// 各専門職（メカニック、クリーナー等）はこのクラスを継承して固有の業務ロジックを実装する。
    ///
    /// 【デザインメモ】
    /// ・疲労値が90を超え、一定時間休息できない場合にストライキが発生する
    /// ・スキルレベル(1-5)は訓練で上昇し、作業速度や品質に影響する
    /// ・パトロールエリアを割り当てることで、担当区域内を巡回させられる
    /// ・原作に忠実に、スタッフの不満→ストライキ→パーク運営に悪影響の流れを再現する
    /// </summary>
    [RequireComponent(typeof(NavMeshAgent))]
    public abstract class StaffMember : MonoBehaviour
    {
        // ============================================================
        // 定数定義
        // ============================================================

        /// <summary>スキルレベル下限</summary>
        public const int MinSkillLevel = 1;

        /// <summary>スキルレベル上限</summary>
        public const int MaxSkillLevel = 5;

        /// <summary>疲労値下限</summary>
        public const float MinFatigue = 0f;

        /// <summary>疲労値上限</summary>
        public const float MaxFatigue = 100f;

        /// <summary>ストライキ発生の疲労閾値</summary>
        private const float StrikeFatigueThreshold = 90f;

        /// <summary>
        /// 休息なしでストライキに至るまでの猶予時間（秒）
        /// 疲労が閾値を超えた状態でこの時間休息しなければストライキ発生
        /// </summary>
        private const float StrikeGracePeriod = 120f;

        /// <summary>疲労がこの値以下まで回復したら休息完了とみなす</summary>
        private const float RestCompleteThreshold = 20f;

        /// <summary>パトロール地点到達とみなす距離（メートル）</summary>
        private const float PatrolPointReachDistance = 1.5f;

        /// <summary>作業時に疲労が1秒あたりに増加する基本値</summary>
        private const float BaseFatigueIncreaseRate = 0.15f;

        /// <summary>休息時に疲労が1秒あたりに減少する基本値</summary>
        private const float BaseFatigueDecreaseRate = 0.5f;

        // ============================================================
        // シリアライズフィールド
        // ============================================================

        [Header("Staff Identity")]
        [SerializeField] private int id;
        [SerializeField] private string staffName;
        [SerializeField] private StaffType staffType;

        [Header("Stats")]
        [SerializeField, Range(MinSkillLevel, MaxSkillLevel)]
        private int skillLevel = MinSkillLevel;

        [SerializeField, Range(MinFatigue, MaxFatigue)]
        private float fatigue;

        [SerializeField] private float salary = 500f;

        [Header("Patrol")]
        [SerializeField] private List<Transform> patrolPoints = new List<Transform>();
        [SerializeField] private Bounds patrolArea;

        // ============================================================
        // プロパティ
        // ============================================================

        /// <summary>スタッフ固有ID</summary>
        public int Id
        {
            get => id;
            set => id = value;
        }

        /// <summary>スタッフ名</summary>
        public string Name
        {
            get => staffName;
            set => staffName = value;
        }

        /// <summary>スタッフ種別</summary>
        public StaffType StaffType
        {
            get => staffType;
            protected set => staffType = value;
        }

        /// <summary>月給</summary>
        public float Salary
        {
            get => salary;
            set => salary = Mathf.Max(0f, value);
        }

        /// <summary>
        /// スキルレベル (1-5)
        /// 訓練によって上昇し、作業効率・品質に影響する
        /// </summary>
        public int SkillLevel
        {
            get => skillLevel;
            private set => skillLevel = Mathf.Clamp(value, MinSkillLevel, MaxSkillLevel);
        }

        /// <summary>
        /// 疲労値 (0-100)
        /// 作業中に増加し、休息で減少する。90超でストライキリスク発生
        /// </summary>
        public float Fatigue
        {
            get => fatigue;
            private set => fatigue = Mathf.Clamp(value, MinFatigue, MaxFatigue);
        }

        /// <summary>現在の行動状態</summary>
        public StaffBehaviorState CurrentState { get; protected set; } = StaffBehaviorState.Idle;

        /// <summary>ストライキ中かどうか</summary>
        public bool IsOnStrike => CurrentState == StaffBehaviorState.OnStrike;

        /// <summary>作業可能かどうか</summary>
        public bool IsAvailable => CurrentState == StaffBehaviorState.Idle
                                   || CurrentState == StaffBehaviorState.Working;

        /// <summary>パトロールエリアが割り当てられているか</summary>
        public bool HasPatrolArea => patrolPoints.Count > 0 || patrolArea.size.sqrMagnitude > 0f;

        /// <summary>担当パトロールエリアのBounds</summary>
        public Bounds PatrolArea
        {
            get => patrolArea;
            set => patrolArea = value;
        }

        /// <summary>パトロール地点リスト</summary>
        public IReadOnlyList<Transform> PatrolPoints => patrolPoints;

        /// <summary>スキルレベルに応じた作業効率倍率 (1.0 ~ 2.0)</summary>
        public float WorkEfficiencyMultiplier => 1f + (SkillLevel - 1) * 0.25f;

        /// <summary>現在の作業経験値</summary>
        public float WorkExperience => _workExperience;

        /// <summary>次のレベルアップに必要な経験値</summary>
        public float ExperienceToNextLevel => SkillLevel >= MaxSkillLevel ? 0f : ExpPerLevelBase * SkillLevel;

        /// <summary>現在レベルの経験値進捗率 (0-1)</summary>
        public float ExperienceProgress => SkillLevel >= MaxSkillLevel ? 1f
            : Mathf.Clamp01(_workExperience / (ExpPerLevelBase * SkillLevel));

        // ============================================================
        // 内部状態
        // ============================================================

        protected NavMeshAgent navAgent;
        private int currentPatrolIndex;
        private float timeSinceLastRest;
        private float highFatigueAccumulator;
        private bool isInitialized;

        // 経験値（作業時間に応じて蓄積、閾値でスキルレベル自動上昇）
        private float _workExperience;
        private const float ExpPerLevelBase = 300f; // Lv1→2に必要な作業秒数

        /// <summary>現在のタスク目標地点</summary>
        protected Vector3? currentTaskTarget;

        /// <summary>スタッフルームの位置（休息先）</summary>
        protected Transform staffRoomTarget;

        // ============================================================
        // Unity ライフサイクル
        // ============================================================

        protected virtual void Awake()
        {
            navAgent = GetComponent<NavMeshAgent>();
        }

        protected virtual void Start()
        {
            if (!isInitialized)
            {
                Initialize(id, staffName, staffType);
            }
        }

        protected virtual void Update()
        {
            if (CurrentState == StaffBehaviorState.OnStrike)
            {
                return;
            }

            UpdateFatigue();
            CheckStrikeCondition();
            ExecuteBehaviorLoop();
        }

        protected virtual void OnEnable()
        {
            SubscribeToEvents();
        }

        protected virtual void OnDisable()
        {
            UnsubscribeFromEvents();
        }

        // ============================================================
        // 初期化
        // ============================================================

        /// <summary>
        /// スタッフを初期化する。StaffManagerから雇用時に呼ばれる。
        /// </summary>
        public virtual void Initialize(int staffId, string name, StaffType type)
        {
            Id = staffId;
            Name = name;
            StaffType = type;
            Fatigue = 0f;
            CurrentState = StaffBehaviorState.Idle;
            timeSinceLastRest = 0f;
            highFatigueAccumulator = 0f;
            currentPatrolIndex = 0;
            isInitialized = true;
        }

        // ============================================================
        // 疲労・ストライキシステム
        // ============================================================

        /// <summary>
        /// 疲労値を毎フレーム更新する。
        /// 【ゲームデザイン】
        /// ・作業中は疲労が徐々に蓄積される
        /// ・休息中は疲労が回復し、閾値以下になれば業務復帰する
        /// ・スキルが高いほど疲労の蓄積が緩やかになる（熟練の効果）
        /// </summary>
        private void UpdateFatigue()
        {
            float deltaTime = Time.deltaTime;

            switch (CurrentState)
            {
                case StaffBehaviorState.Working:
                case StaffBehaviorState.MovingToTask:
                    // スキルが高いほど疲労蓄積が緩やか (スキル5で60%に軽減)
                    float fatigueReduction = 1f - (SkillLevel - 1) * 0.1f;
                    Fatigue += BaseFatigueIncreaseRate * fatigueReduction * deltaTime;
                    timeSinceLastRest += deltaTime;

                    // 作業経験値の蓄積（Working状態のみ）
                    if (CurrentState == StaffBehaviorState.Working)
                        AccumulateExperience(deltaTime);
                    break;

                case StaffBehaviorState.Resting:
                    Fatigue -= BaseFatigueDecreaseRate * deltaTime;
                    timeSinceLastRest = 0f;
                    highFatigueAccumulator = 0f;

                    // 十分に回復したら業務復帰
                    if (Fatigue <= RestCompleteThreshold)
                    {
                        OnRestComplete();
                    }
                    break;

                case StaffBehaviorState.Idle:
                    // 待機中はゆっくり疲労回復
                    Fatigue -= BaseFatigueDecreaseRate * 0.3f * deltaTime;
                    break;
            }
        }

        /// <summary>
        /// ストライキ発生条件をチェックする。
        /// 【ゲームデザイン】
        /// ・疲労が90を超えた状態が一定時間続くとストライキが発生する
        /// ・ストライキ中のスタッフは一切の業務を行わない
        /// ・プレイヤーはストライキを解消するために休息場所を確保するか、給料を上げる必要がある
        /// </summary>
        private void CheckStrikeCondition()
        {
            if (Fatigue > StrikeFatigueThreshold)
            {
                highFatigueAccumulator += Time.deltaTime;

                if (highFatigueAccumulator >= StrikeGracePeriod)
                {
                    GoOnStrike();
                }
            }
            else
            {
                highFatigueAccumulator = Mathf.Max(0f, highFatigueAccumulator - Time.deltaTime * 0.5f);
            }
        }

        /// <summary>ストライキ状態に遷移する</summary>
        private void GoOnStrike()
        {
            if (IsOnStrike) return;

            CurrentState = StaffBehaviorState.OnStrike;
            StopNavigation();

            GameEvents.FireStaffWentOnStrike(Id);
            WebGLOptimizer.LogVerbose($"[Staff] {Name} (ID:{Id}) がストライキに入りました！疲労: {Fatigue:F0}");
        }

        /// <summary>
        /// ストライキを解除する。StaffManagerから呼ばれる。
        /// 休息場所への移動を開始する。
        /// </summary>
        public void ResolveStrike()
        {
            if (!IsOnStrike) return;

            highFatigueAccumulator = 0f;
            BeginResting();
            WebGLOptimizer.LogVerbose($"[Staff] {Name} (ID:{Id}) のストライキが解除されました");
        }

        // ============================================================
        // 訓練システム
        // ============================================================

        /// <summary>
        /// スキルレベルを1段階上昇させるのに必要なコスト。
        /// レベルが高いほど訓練コストが増大する。
        ///
        /// 【ゲームデザイン】
        /// ・Lv1→2: 1000  Lv2→3: 2000  Lv3→4: 3000  Lv4→5: 4000
        /// ・投資に見合うだけの効率向上を得られるバランス設計
        /// </summary>
        public float GetTrainingCost()
        {
            if (SkillLevel >= MaxSkillLevel) return 0f;
            return SkillLevel * 1000f;
        }

        /// <summary>
        /// 訓練を実施してスキルレベルを1段階上昇させる。
        /// 費用は呼び出し側（StaffManager経由でEconomyManager）が処理する。
        /// </summary>
        /// <returns>訓練が実行できた場合true</returns>
        public bool Train()
        {
            if (SkillLevel >= MaxSkillLevel)
            {
                WebGLOptimizer.LogVerbose($"[Staff] {Name} は既に最高スキルレベルです");
                return false;
            }

            SkillLevel++;
            _workExperience = 0f;
            WebGLOptimizer.LogVerbose($"[Staff] {Name} のスキルレベルが {SkillLevel} に上昇しました");
            return true;
        }

        /// <summary>
        /// 作業経験値を蓄積し、閾値に達したら自動レベルアップする。
        /// Lv1→2: 300秒、Lv2→3: 600秒、Lv3→4: 900秒、Lv4→5: 1200秒
        /// </summary>
        private void AccumulateExperience(float deltaTime)
        {
            if (SkillLevel >= MaxSkillLevel) return;

            _workExperience += deltaTime;

            float threshold = ExpPerLevelBase * SkillLevel;
            if (_workExperience >= threshold)
            {
                _workExperience = 0f;
                SkillLevel++;
                WebGLOptimizer.LogVerbose($"[Staff] {Name} が経験によりスキルLv{SkillLevel}に昇格");

                if (NotificationSystem.Instance != null)
                {
                    NotificationSystem.Instance.Notify(
                        $"{Name}のスキルがLv{SkillLevel}に上昇！",
                        NotifLevel.Info);
                }
            }
        }

        /// <summary>セーブ用: 経験値を設定する</summary>
        public void SetWorkExperience(float exp) { _workExperience = exp; }

        // ============================================================
        // AI行動ループ
        // ============================================================

        /// <summary>
        /// 毎フレーム呼ばれるAI行動ループの基本構造。
        ///
        /// 【行動優先度】
        /// 1. 疲労が高ければ休息に向かう
        /// 2. タスクがあればタスク地点へ移動して実行
        /// 3. パトロールエリアがあれば巡回
        /// 4. 何もなければ待機
        /// </summary>
        private void ExecuteBehaviorLoop()
        {
            switch (CurrentState)
            {
                case StaffBehaviorState.Idle:
                    HandleIdleState();
                    break;

                case StaffBehaviorState.MovingToTask:
                    HandleMovingToTaskState();
                    break;

                case StaffBehaviorState.Working:
                    HandleWorkingState();
                    break;

                case StaffBehaviorState.Resting:
                    HandleRestingState();
                    break;
            }
        }

        /// <summary>
        /// 待機状態の処理。タスクを探すか、パトロールを行う。
        /// </summary>
        private void HandleIdleState()
        {
            // 疲労が高ければ休息を優先
            if (ShouldRest())
            {
                BeginResting();
                return;
            }

            // サブクラスで定義されたタスク検索
            if (FindAndAssignTask())
            {
                return;
            }

            // パトロール巡回
            if (HasPatrolArea)
            {
                MoveToNextPatrolPoint();
            }
        }

        /// <summary>タスク地点への移動中の処理</summary>
        private void HandleMovingToTaskState()
        {
            if (ShouldRest())
            {
                BeginResting();
                return;
            }

            if (HasReachedDestination())
            {
                CurrentState = StaffBehaviorState.Working;
                OnTaskReached();
            }
        }

        /// <summary>
        /// 作業中の処理。サブクラスに委譲する。
        /// </summary>
        private void HandleWorkingState()
        {
            // 緊急の疲労チェック（疲労が非常に高い場合は作業を中断）
            if (Fatigue > StrikeFatigueThreshold)
            {
                BeginResting();
                return;
            }

            PerformWork();
        }

        /// <summary>休息中の処理</summary>
        private void HandleRestingState()
        {
            // UpdateFatigueで回復処理は行われているため、
            // ここではスタッフルームへの移動が完了しているか確認するのみ
            if (staffRoomTarget != null && !HasReachedDestination())
            {
                // まだスタッフルームに到着していない
                return;
            }
        }

        // ============================================================
        // 休息
        // ============================================================

        /// <summary>休息が必要かどうかを判定する</summary>
        protected bool ShouldRest()
        {
            return Fatigue > 65f;
        }

        /// <summary>
        /// 休息を開始する。スタッフルームが設定されていればそこへ移動する。
        /// </summary>
        protected void BeginResting()
        {
            CurrentState = StaffBehaviorState.Resting;
            currentTaskTarget = null;

            if (staffRoomTarget != null)
            {
                NavigateTo(staffRoomTarget.position);
            }
            else
            {
                StopNavigation();
            }
        }

        /// <summary>休息が完了した際に呼ばれる</summary>
        private void OnRestComplete()
        {
            CurrentState = StaffBehaviorState.Idle;
            WebGLOptimizer.LogVerbose($"[Staff] {Name} の休息が完了しました（疲労: {Fatigue:F0}）");
        }

        /// <summary>スタッフルームの参照を設定する</summary>
        public void SetStaffRoom(Transform room)
        {
            staffRoomTarget = room;
        }

        /// <summary>
        /// 外部（StaffManager）から休息を指示するための公開メソッド。
        /// ストライキ中でない場合に休息状態へ遷移する。
        /// </summary>
        public void SendToRest()
        {
            if (IsOnStrike) return;
            BeginResting();
        }

        // ============================================================
        // パトロール
        // ============================================================

        /// <summary>パトロール地点を設定する</summary>
        public void SetPatrolPoints(List<Transform> points)
        {
            patrolPoints = points ?? new List<Transform>();
            currentPatrolIndex = 0;
        }

        /// <summary>パトロールエリア（Bounds）を設定する</summary>
        public void SetPatrolArea(Bounds area)
        {
            patrolArea = area;
        }

        /// <summary>
        /// 次のパトロール地点へ移動する。
        /// 地点リストがあればリスト順に巡回し、なければエリア内のランダム地点へ向かう。
        /// </summary>
        protected void MoveToNextPatrolPoint()
        {
            if (patrolPoints.Count > 0)
            {
                // 地点リストがnullの要素をスキップ
                int attempts = 0;
                while (attempts < patrolPoints.Count)
                {
                    if (patrolPoints[currentPatrolIndex] != null)
                    {
                        NavigateTo(patrolPoints[currentPatrolIndex].position);
                        CurrentState = StaffBehaviorState.MovingToTask;
                        currentPatrolIndex = (currentPatrolIndex + 1) % patrolPoints.Count;
                        return;
                    }
                    currentPatrolIndex = (currentPatrolIndex + 1) % patrolPoints.Count;
                    attempts++;
                }
            }
            else if (patrolArea.size.sqrMagnitude > 0f)
            {
                // エリア内のランダム地点を選択
                Vector3 randomPoint = new Vector3(
                    UnityEngine.Random.Range(patrolArea.min.x, patrolArea.max.x),
                    patrolArea.center.y,
                    UnityEngine.Random.Range(patrolArea.min.z, patrolArea.max.z)
                );

                if (NavMesh.SamplePosition(randomPoint, out NavMeshHit hit, 10f, NavMesh.AllAreas))
                {
                    NavigateTo(hit.position);
                    CurrentState = StaffBehaviorState.MovingToTask;
                }
            }
        }

        // ============================================================
        // ナビゲーション
        // ============================================================

        /// <summary>指定地点への移動を開始する</summary>
        protected void NavigateTo(Vector3 destination)
        {
            if (navAgent != null && navAgent.isOnNavMesh)
            {
                navAgent.isStopped = false;
                navAgent.SetDestination(destination);
                currentTaskTarget = destination;
            }
        }

        /// <summary>ナビゲーションを停止する</summary>
        protected void StopNavigation()
        {
            if (navAgent != null && navAgent.isOnNavMesh)
            {
                navAgent.isStopped = true;
                navAgent.ResetPath();
            }
        }

        /// <summary>目的地に到着したかどうかを判定する</summary>
        protected bool HasReachedDestination()
        {
            if (navAgent == null || !navAgent.isOnNavMesh) return true;
            if (navAgent.pathPending) return false;
            return navAgent.remainingDistance <= PatrolPointReachDistance && !navAgent.hasPath;
        }

        /// <summary>
        /// 指定位置がこのスタッフのパトロールエリア内かどうかを判定する。
        /// パトロールエリアが未設定の場合はtrueを返す（エリア制限なし）。
        /// </summary>
        public bool IsWithinPatrolArea(Vector3 position)
        {
            if (!HasPatrolArea) return true;

            if (patrolArea.size.sqrMagnitude > 0f)
            {
                return patrolArea.Contains(position);
            }

            // パトロール地点ベースの場合、最寄りの地点からの距離で判定
            float maxRange = 30f;
            foreach (var point in patrolPoints)
            {
                if (point != null && Vector3.Distance(position, point.position) <= maxRange)
                {
                    return true;
                }
            }
            return false;
        }

        // ============================================================
        // タスク完了通知
        // ============================================================

        /// <summary>現在のタスクを完了として処理する</summary>
        protected void CompleteCurrentTask()
        {
            CurrentState = StaffBehaviorState.Idle;
            currentTaskTarget = null;
            GameEvents.FireStaffFinishedTask(Id);
        }

        // ============================================================
        // サブクラスが実装する抽象・仮想メソッド
        // ============================================================

        /// <summary>
        /// タスクを検索し、見つかれば割り当てる。
        /// サブクラスが各職種に応じたタスク検索ロジックを実装する。
        /// </summary>
        /// <returns>タスクが見つかり割り当てられた場合true</returns>
        protected abstract bool FindAndAssignTask();

        /// <summary>
        /// タスク地点に到着した際に呼ばれる。
        /// サブクラスが到着時の初期化処理を実装する。
        /// </summary>
        protected abstract void OnTaskReached();

        /// <summary>
        /// 作業中に毎フレーム呼ばれる。
        /// サブクラスが実際の作業ロジック（修理・清掃等）を実装する。
        /// </summary>
        protected abstract void PerformWork();

        /// <summary>ゲームイベントの購読を行う</summary>
        protected virtual void SubscribeToEvents() { }

        /// <summary>ゲームイベントの購読を解除する</summary>
        protected virtual void UnsubscribeFromEvents() { }

        // ============================================================
        // デバッグ
        // ============================================================

#if UNITY_EDITOR
        protected virtual void OnDrawGizmosSelected()
        {
            // パトロールエリアの可視化
            if (patrolArea.size.sqrMagnitude > 0f)
            {
                Gizmos.color = new Color(0f, 1f, 0f, 0.2f);
                Gizmos.DrawCube(patrolArea.center, patrolArea.size);
                Gizmos.color = Color.green;
                Gizmos.DrawWireCube(patrolArea.center, patrolArea.size);
            }

            // パトロール地点の可視化
            if (patrolPoints != null && patrolPoints.Count > 0)
            {
                Gizmos.color = Color.cyan;
                for (int i = 0; i < patrolPoints.Count; i++)
                {
                    if (patrolPoints[i] == null) continue;
                    Gizmos.DrawSphere(patrolPoints[i].position, 0.5f);
                    if (i < patrolPoints.Count - 1 && patrolPoints[i + 1] != null)
                    {
                        Gizmos.DrawLine(patrolPoints[i].position, patrolPoints[i + 1].position);
                    }
                }
                // 最後→最初を結ぶ
                if (patrolPoints.Count > 1 && patrolPoints[0] != null
                    && patrolPoints[patrolPoints.Count - 1] != null)
                {
                    Gizmos.DrawLine(
                        patrolPoints[patrolPoints.Count - 1].position,
                        patrolPoints[0].position);
                }
            }

            // 現在の状態テキスト
            Gizmos.color = IsOnStrike ? Color.red : Color.white;
            Gizmos.DrawSphere(transform.position + Vector3.up * 2.5f, 0.3f);
        }
#endif
    }
}
