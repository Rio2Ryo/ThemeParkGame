using System;
using System.Collections.Generic;
using UnityEngine;
using ThemeParkGame.Core;

namespace ThemeParkGame.Park
{
    /// <summary>
    /// 災害イベントのデータクラス
    /// 各災害の状態・パラメータを保持する
    /// </summary>
    [System.Serializable]
    public class DisasterEvent
    {
        /// <summary>災害の種類</summary>
        public DisasterType Type;

        /// <summary>現在のフェーズ</summary>
        public DisasterPhase Phase;

        /// <summary>表示名（日本語）</summary>
        public string DisplayName;

        /// <summary>説明文（日本語）</summary>
        public string Description;

        /// <summary>アクティブフェーズの持続時間（秒）</summary>
        public float Duration;

        /// <summary>警告フェーズの持続時間（秒）</summary>
        public float WarningDuration;

        /// <summary>復旧フェーズの持続時間（秒）</summary>
        public float RecoveryDuration;

        /// <summary>アトラクションへのダメージ倍率</summary>
        public float DamageMultiplier;

        /// <summary>毎秒の幸福度減少量</summary>
        public float HappinessPenalty;

        /// <summary>来場者スポーンのブロック率（0～1）</summary>
        public float SpawnBlockRate;

        /// <summary>災害後の基本修理費用</summary>
        public float RepairCost;

        /// <summary>現在のフェーズ内での経過時間</summary>
        public float ElapsedTime;

        /// <summary>影響を受けたアトラクション数</summary>
        public int AffectedAttractionCount;

        /// <summary>影響を受けるゾーン（nullの場合はパーク全体）</summary>
        public ThemeZone? AffectedZone;
    }

    /// <summary>
    /// 災害イベントシステム
    /// テーマパーク内の災害発生・進行・復旧を管理するシングルトン
    /// </summary>
    public class DisasterEventSystem : MonoBehaviour
    {
        // =====================================================================
        // シングルトン
        // =====================================================================
        public static DisasterEventSystem Instance { get; private set; }

        // =====================================================================
        // イベント
        // =====================================================================

        /// <summary>災害が開始されたときに発火</summary>
        public event Action<DisasterEvent> OnDisasterStarted;

        /// <summary>災害が終了したときに発火</summary>
        public event Action<DisasterType> OnDisasterEnded;

        /// <summary>災害のフェーズが変わったときに発火</summary>
        public event Action<DisasterPhase> OnPhaseChanged;

        // =====================================================================
        // 定数
        // =====================================================================

        /// <summary>災害間の最小クールダウン（ゲーム内日数）</summary>
        private const int DISASTER_COOLDOWN_DAYS = 2;

        /// <summary>難易度別の災害発生確率（1日あたり）</summary>
        private const float CHANCE_EASY = 0.02f;
        private const float CHANCE_NORMAL = 0.05f;
        private const float CHANCE_HARD = 0.10f;

        /// <summary>パンデミック時のスタッフ疲労増加率</summary>
        private const float PANDEMIC_STAFF_FATIGUE_BOOST = 0.30f;

        // =====================================================================
        // プロパティ・フィールド
        // =====================================================================

        /// <summary>現在発生中の災害（なければnull）</summary>
        public DisasterEvent CurrentDisaster { get; private set; }

        /// <summary>災害が発生中かどうか</summary>
        public bool IsDisasterActive => CurrentDisaster != null;

        /// <summary>災害クールダウンタイマー（ゲーム内日数）</summary>
        public float DisasterCooldownTimer { get; private set; }

        /// <summary>災害定義テンプレート辞書</summary>
        private Dictionary<DisasterType, DisasterEvent> disasterTemplates;

        /// <summary>最後に災害が発生した日</summary>
        private int lastDisasterDay;

        /// <summary>累積ダメージ追跡用</summary>
        private float totalDamageApplied;

        /// <summary>累積幸福度ペナルティ追跡用</summary>
        private float totalHappinessDrained;

        // =====================================================================
        // Unity ライフサイクル
        // =====================================================================

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                WebGLOptimizer.LogWarning("DisasterEventSystem: 重複インスタンスを破棄します。");
                Destroy(gameObject);
                return;
            }

            Instance = this;
            InitializeDisasterTemplates();
            DisasterCooldownTimer = 0f;
            lastDisasterDay = -DISASTER_COOLDOWN_DAYS;
            totalDamageApplied = 0f;
            totalHappinessDrained = 0f;

            WebGLOptimizer.LogVerbose("DisasterEventSystem: 初期化完了。");
        }

        private void OnEnable()
        {
            if (GameManager.Instance != null && GameManager.Instance.TimeManager != null)
            {
                GameManager.Instance.TimeManager.OnDayChanged += OnDayChanged;
                WebGLOptimizer.LogVerbose("DisasterEventSystem: TimeManagerのOnDayChangedイベントに登録しました。");
            }
        }

        private void OnDisable()
        {
            if (GameManager.Instance != null && GameManager.Instance.TimeManager != null)
            {
                GameManager.Instance.TimeManager.OnDayChanged -= OnDayChanged;
            }
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
                WebGLOptimizer.LogVerbose("DisasterEventSystem: インスタンスを破棄しました。");
            }
        }

        // =====================================================================
        // 災害テンプレート初期化
        // =====================================================================

        /// <summary>
        /// 5種類の災害テンプレートを定義する
        /// </summary>
        private void InitializeDisasterTemplates()
        {
            disasterTemplates = new Dictionary<DisasterType, DisasterEvent>
            {
                {
                    DisasterType.Earthquake,
                    new DisasterEvent
                    {
                        Type = DisasterType.Earthquake,
                        Phase = DisasterPhase.Warning,
                        DisplayName = "地震",
                        Description = "大規模な地震が発生しました。アトラクションに深刻なダメージを与え、来場者のパニックを引き起こします。",
                        Duration = 30f,
                        WarningDuration = 10f,
                        RecoveryDuration = 120f,
                        DamageMultiplier = 0.3f,
                        HappinessPenalty = -2.0f,
                        SpawnBlockRate = 0.9f,
                        RepairCost = 5000f,
                        ElapsedTime = 0f,
                        AffectedAttractionCount = 0,
                        AffectedZone = null
                    }
                },
                {
                    DisasterType.PowerOutage,
                    new DisasterEvent
                    {
                        Type = DisasterType.PowerOutage,
                        Phase = DisasterPhase.Warning,
                        DisplayName = "停電",
                        Description = "パーク全体で停電が発生しました。アトラクションが停止し、来場者の不満が高まっています。",
                        Duration = 60f,
                        WarningDuration = 5f,
                        RecoveryDuration = 90f,
                        DamageMultiplier = 0.05f,
                        HappinessPenalty = -1.0f,
                        SpawnBlockRate = 0.5f,
                        RepairCost = 2000f,
                        ElapsedTime = 0f,
                        AffectedAttractionCount = 0,
                        AffectedZone = null
                    }
                },
                {
                    DisasterType.Pandemic,
                    new DisasterEvent
                    {
                        Type = DisasterType.Pandemic,
                        Phase = DisasterPhase.Warning,
                        DisplayName = "パンデミック",
                        Description = "感染症がパーク内で蔓延しています。来場者の入場を大幅に制限し、スタッフの30%が勤務不能になります。",
                        Duration = 300f,
                        WarningDuration = 0f,
                        RecoveryDuration = 180f,
                        DamageMultiplier = 0.0f,
                        HappinessPenalty = -0.5f,
                        SpawnBlockRate = 0.95f,
                        RepairCost = 1000f,
                        ElapsedTime = 0f,
                        AffectedAttractionCount = 0,
                        AffectedZone = null
                    }
                },
                {
                    DisasterType.Fire,
                    new DisasterEvent
                    {
                        Type = DisasterType.Fire,
                        Phase = DisasterPhase.Warning,
                        DisplayName = "火災",
                        Description = "パーク内で大規模な火災が発生しました。アトラクションへのダメージが最も深刻で、迅速な対応が必要です。",
                        Duration = 45f,
                        WarningDuration = 15f,
                        RecoveryDuration = 150f,
                        DamageMultiplier = 0.4f,
                        HappinessPenalty = -2.5f,
                        SpawnBlockRate = 0.8f,
                        RepairCost = 7000f,
                        ElapsedTime = 0f,
                        AffectedAttractionCount = 0,
                        AffectedZone = null
                    }
                },
                {
                    DisasterType.Flood,
                    new DisasterEvent
                    {
                        Type = DisasterType.Flood,
                        Phase = DisasterPhase.Warning,
                        DisplayName = "洪水",
                        Description = "豪雨による洪水がパークを襲いました。広範囲にダメージを与え、来場者の移動を困難にします。",
                        Duration = 60f,
                        WarningDuration = 20f,
                        RecoveryDuration = 120f,
                        DamageMultiplier = 0.2f,
                        HappinessPenalty = -1.5f,
                        SpawnBlockRate = 0.7f,
                        RepairCost = 4000f,
                        ElapsedTime = 0f,
                        AffectedAttractionCount = 0,
                        AffectedZone = null
                    }
                }
            };

            WebGLOptimizer.LogVerbose($"DisasterEventSystem: {disasterTemplates.Count}種類の災害テンプレートを登録しました。");
        }

        // =====================================================================
        // メインループ
        // =====================================================================

        private void Update()
        {
            if (CurrentDisaster == null)
            {
                return;
            }

            float dt = Time.deltaTime;
            CurrentDisaster.ElapsedTime += dt;

            switch (CurrentDisaster.Phase)
            {
                case DisasterPhase.Warning:
                    ProcessWarningPhase(dt);
                    break;

                case DisasterPhase.Active:
                    ProcessActivePhase(dt);
                    break;

                case DisasterPhase.Recovery:
                    ProcessRecoveryPhase(dt);
                    break;

                default:
                    WebGLOptimizer.LogWarning($"DisasterEventSystem: 不明なフェーズ {CurrentDisaster.Phase} を検出しました。");
                    break;
            }
        }

        // =====================================================================
        // 災害発生
        // =====================================================================

        /// <summary>
        /// 指定タイプの災害を発生させる
        /// </summary>
        /// <param name="type">発生させる災害の種類</param>
        public void TriggerDisaster(DisasterType type)
        {
            if (CurrentDisaster != null)
            {
                WebGLOptimizer.LogWarning($"DisasterEventSystem: 既に災害 '{CurrentDisaster.DisplayName}' が進行中のため、新しい災害を発生できません。");
                return;
            }

            if (!disasterTemplates.ContainsKey(type))
            {
                WebGLOptimizer.LogWarning($"DisasterEventSystem: 不明な災害タイプ {type} が指定されました。");
                return;
            }

            // テンプレートからコピーして新しいインスタンスを作成
            DisasterEvent template = disasterTemplates[type];
            CurrentDisaster = new DisasterEvent
            {
                Type = template.Type,
                Phase = DisasterPhase.Warning,
                DisplayName = template.DisplayName,
                Description = template.Description,
                Duration = template.Duration,
                WarningDuration = template.WarningDuration,
                RecoveryDuration = template.RecoveryDuration,
                DamageMultiplier = template.DamageMultiplier,
                HappinessPenalty = template.HappinessPenalty,
                SpawnBlockRate = template.SpawnBlockRate,
                RepairCost = template.RepairCost,
                ElapsedTime = 0f,
                AffectedAttractionCount = 0,
                AffectedZone = template.AffectedZone
            };

            // 統計リセット
            totalDamageApplied = 0f;
            totalHappinessDrained = 0f;

            WebGLOptimizer.LogWarning($"DisasterEventSystem: 災害 '{CurrentDisaster.DisplayName}' が発生しました！");

            // 警告時間が0の場合（パンデミック等）は直接アクティブフェーズへ
            if (CurrentDisaster.WarningDuration <= 0f)
            {
                CurrentDisaster.Phase = DisasterPhase.Active;
                CurrentDisaster.ElapsedTime = 0f;

                NotificationSystem.Instance?.Notify(
                    $"【緊急】{CurrentDisaster.DisplayName}が発生しました！",
                    CurrentDisaster.Description,
                    NotifLevel.Critical
                );

                OnPhaseChanged?.Invoke(DisasterPhase.Active);
                WebGLOptimizer.LogWarning($"DisasterEventSystem: '{CurrentDisaster.DisplayName}' は警告なしで直接発生しました。");
            }
            else
            {
                NotificationSystem.Instance?.Notify(
                    $"【警告】{CurrentDisaster.DisplayName}の兆候を検知しました！",
                    $"{CurrentDisaster.DisplayName}が接近しています。{CurrentDisaster.WarningDuration}秒後に到達する見込みです。",
                    NotifLevel.Critical
                );

                OnPhaseChanged?.Invoke(DisasterPhase.Warning);
            }

            // イベント発火
            OnDisasterStarted?.Invoke(CurrentDisaster);
            GameEvents.FireParkEventStarted($"災害:{CurrentDisaster.DisplayName}");

            WebGLOptimizer.LogVerbose($"DisasterEventSystem: 災害イベント開始通知を送信しました。タイプ={type}");
        }

        /// <summary>
        /// 現在の災害を強制終了する（デバッグ・チート用）
        /// </summary>
        public void ForceEndDisaster()
        {
            if (CurrentDisaster == null)
            {
                WebGLOptimizer.LogVerbose("DisasterEventSystem: 強制終了が呼ばれましたが、進行中の災害はありません。");
                return;
            }

            DisasterType endedType = CurrentDisaster.Type;
            string endedName = CurrentDisaster.DisplayName;

            WebGLOptimizer.LogWarning($"DisasterEventSystem: 災害 '{endedName}' を強制終了しました。");

            NotificationSystem.Instance?.Notify(
                $"【通知】{endedName}が強制終了されました",
                "管理者により災害イベントが終了されました。",
                NotifLevel.Critical
            );

            EndDisaster(endedType, endedName);
        }

        // =====================================================================
        // フェーズ処理
        // =====================================================================

        /// <summary>
        /// 警告フェーズの処理: 警告表示とアラーム再生
        /// </summary>
        /// <param name="dt">経過時間（秒）</param>
        public void ProcessWarningPhase(float dt)
        {
            if (CurrentDisaster == null) return;

            float remainingWarning = CurrentDisaster.WarningDuration - CurrentDisaster.ElapsedTime;

            // 警告メッセージの定期的な表示（5秒ごと）
            if (Mathf.FloorToInt(CurrentDisaster.ElapsedTime) % 5 == 0
                && Mathf.FloorToInt(CurrentDisaster.ElapsedTime - dt) % 5 != 0)
            {
                WebGLOptimizer.LogVerbose(
                    $"DisasterEventSystem: 【警告中】{CurrentDisaster.DisplayName} - 残り{remainingWarning:F1}秒で到達します。"
                );
            }

            // 警告フェーズ終了 → アクティブフェーズへ遷移
            if (CurrentDisaster.ElapsedTime >= CurrentDisaster.WarningDuration)
            {
                CurrentDisaster.Phase = DisasterPhase.Active;
                CurrentDisaster.ElapsedTime = 0f;

                NotificationSystem.Instance?.Notify(
                    $"【緊急】{CurrentDisaster.DisplayName}が発生しました！",
                    CurrentDisaster.Description,
                    NotifLevel.Critical
                );

                OnPhaseChanged?.Invoke(DisasterPhase.Active);

                WebGLOptimizer.LogWarning(
                    $"DisasterEventSystem: '{CurrentDisaster.DisplayName}' がアクティブフェーズに移行しました。"
                );
            }
        }

        /// <summary>
        /// アクティブフェーズの処理: ダメージ適用、幸福度減少、スポーンブロック
        /// </summary>
        /// <param name="dt">経過時間（秒）</param>
        public void ProcessActivePhase(float dt)
        {
            if (CurrentDisaster == null) return;

            // ----- アトラクションへのダメージ適用 -----
            if (CurrentDisaster.DamageMultiplier > 0f)
            {
                ApplyAttractionDamage(dt);
            }

            // ----- 来場者の幸福度減少 -----
            if (CurrentDisaster.HappinessPenalty != 0f)
            {
                ApplyHappinessDrain(dt);
            }

            // ----- パンデミック特殊処理: スタッフ疲労増加 -----
            if (CurrentDisaster.Type == DisasterType.Pandemic)
            {
                ApplyPandemicStaffEffect(dt);
            }

            // ----- 進行状況ログ（30秒ごと） -----
            if (Mathf.FloorToInt(CurrentDisaster.ElapsedTime) % 30 == 0
                && Mathf.FloorToInt(CurrentDisaster.ElapsedTime - dt) % 30 != 0)
            {
                float progress = CurrentDisaster.ElapsedTime / CurrentDisaster.Duration;
                WebGLOptimizer.LogVerbose(
                    $"DisasterEventSystem: '{CurrentDisaster.DisplayName}' アクティブフェーズ進行中 " +
                    $"({progress * 100f:F0}%) - 累積ダメージ: {totalDamageApplied:F2}, " +
                    $"累積幸福度減少: {totalHappinessDrained:F2}"
                );
            }

            // ----- アクティブフェーズ終了 → 復旧フェーズへ -----
            if (CurrentDisaster.ElapsedTime >= CurrentDisaster.Duration)
            {
                CurrentDisaster.Phase = DisasterPhase.Recovery;
                CurrentDisaster.ElapsedTime = 0f;

                NotificationSystem.Instance?.Notify(
                    $"【通知】{CurrentDisaster.DisplayName}が収まりました",
                    $"{CurrentDisaster.DisplayName}の直接的な脅威は去りました。復旧作業を開始します。修理費用: ¥{CurrentDisaster.RepairCost:N0}",
                    NotifLevel.Critical
                );

                OnPhaseChanged?.Invoke(DisasterPhase.Recovery);

                WebGLOptimizer.LogWarning(
                    $"DisasterEventSystem: '{CurrentDisaster.DisplayName}' が復旧フェーズに移行しました。"
                );
            }
        }

        /// <summary>
        /// 復旧フェーズの処理: 段階的な復旧と修理費用の処理
        /// </summary>
        /// <param name="dt">経過時間（秒）</param>
        public void ProcessRecoveryPhase(float dt)
        {
            if (CurrentDisaster == null) return;

            float recoveryProgress = Mathf.Clamp01(CurrentDisaster.ElapsedTime / CurrentDisaster.RecoveryDuration);

            // ----- 修理費用の段階的な支払い -----
            float costPerSecond = CurrentDisaster.RepairCost / CurrentDisaster.RecoveryDuration;
            float frameCost = costPerSecond * dt;

            if (GameManager.Instance != null && GameManager.Instance.EconomyManager != null)
            {
                if (GameManager.Instance.EconomyManager.CanAfford(frameCost))
                {
                    GameManager.Instance.EconomyManager.PayExpense(frameCost, Economy.ExpenseCategory.Other);
                }
                else
                {
                    // 資金不足の場合のログ（10秒ごと）
                    if (Mathf.FloorToInt(CurrentDisaster.ElapsedTime) % 10 == 0
                        && Mathf.FloorToInt(CurrentDisaster.ElapsedTime - dt) % 10 != 0)
                    {
                        WebGLOptimizer.LogWarning(
                            $"DisasterEventSystem: 復旧費用が不足しています。修理が遅延する可能性があります。"
                        );

                        NotificationSystem.Instance?.Notify(
                            "【警告】復旧資金が不足しています",
                            "災害復旧に必要な資金が不足しています。修理作業が遅延する可能性があります。",
                            NotifLevel.Critical
                        );
                    }
                }
            }

            // ----- 復旧進捗ログ（30秒ごと） -----
            if (Mathf.FloorToInt(CurrentDisaster.ElapsedTime) % 30 == 0
                && Mathf.FloorToInt(CurrentDisaster.ElapsedTime - dt) % 30 != 0)
            {
                WebGLOptimizer.LogVerbose(
                    $"DisasterEventSystem: '{CurrentDisaster.DisplayName}' 復旧進捗: {recoveryProgress * 100f:F0}%"
                );
            }

            // ----- 復旧フェーズ終了 → 災害完全終了 -----
            if (CurrentDisaster.ElapsedTime >= CurrentDisaster.RecoveryDuration)
            {
                DisasterType endedType = CurrentDisaster.Type;
                string endedName = CurrentDisaster.DisplayName;

                NotificationSystem.Instance?.Notify(
                    $"【通知】{endedName}の復旧が完了しました",
                    $"{endedName}からの復旧作業が完了しました。パークは通常営業に戻ります。",
                    NotifLevel.Critical
                );

                WebGLOptimizer.LogWarning(
                    $"DisasterEventSystem: '{endedName}' の復旧が完了しました。通常営業に戻ります。"
                );

                EndDisaster(endedType, endedName);
            }
        }

        // =====================================================================
        // 効果適用ヘルパー
        // =====================================================================

        /// <summary>
        /// 影響範囲内のアトラクションにダメージを適用する
        /// </summary>
        private void ApplyAttractionDamage(float dt)
        {
            if (CurrentDisaster == null) return;

            float damageThisFrame = CurrentDisaster.DamageMultiplier * dt;
            totalDamageApplied += damageThisFrame;

            // アトラクションマネージャーを通じてConditionを減少させる
            // 影響ゾーンがnullの場合はパーク全体に適用
            var attractions = FindObjectsOfType<Attraction.AttractionController>();
            int affectedCount = 0;

            foreach (var attraction in attractions)
            {
                bool isInZone = true;

                // ゾーンフィルタリング
                if (CurrentDisaster.AffectedZone.HasValue && attraction.Zone != CurrentDisaster.AffectedZone.Value)
                {
                    isInZone = false;
                }

                if (isInZone)
                {
                    attraction.Condition -= damageThisFrame;
                    affectedCount++;
                }
            }

            CurrentDisaster.AffectedAttractionCount = affectedCount;
        }

        /// <summary>
        /// 全来場者に幸福度ペナルティを適用する
        /// </summary>
        private void ApplyHappinessDrain(float dt)
        {
            if (CurrentDisaster == null) return;

            float drainThisFrame = CurrentDisaster.HappinessPenalty * dt;
            totalHappinessDrained += Mathf.Abs(drainThisFrame);

            // ビジターマネージャーを通じて全来場者の幸福度を減少
            var visitors = FindObjectsOfType<Visitor.VisitorController>();

            foreach (var visitor in visitors)
            {
                visitor.Happiness += drainThisFrame; // HappinessPenaltyは負の値
            }
        }

        /// <summary>
        /// パンデミック時のスタッフ疲労効果を適用する
        /// 30%のスタッフが勤務不能になる（疲労増加でシミュレート）
        /// </summary>
        private void ApplyPandemicStaffEffect(float dt)
        {
            var staffMembers = FindObjectsOfType<Staff.StaffController>();
            int affectedStaffCount = Mathf.CeilToInt(staffMembers.Length * PANDEMIC_STAFF_FATIGUE_BOOST);

            for (int i = 0; i < staffMembers.Length && i < affectedStaffCount; i++)
            {
                staffMembers[i].Fatigue += dt * 0.5f; // 疲労を加速させる
            }

            // 初回のみログ
            if (CurrentDisaster.ElapsedTime <= dt * 2f)
            {
                WebGLOptimizer.LogVerbose(
                    $"DisasterEventSystem: パンデミック効果 - {affectedStaffCount}/{staffMembers.Length}名のスタッフに疲労増加を適用中。"
                );
            }
        }

        // =====================================================================
        // 災害終了処理
        // =====================================================================

        /// <summary>
        /// 災害を終了し、後処理を行う
        /// </summary>
        private void EndDisaster(DisasterType endedType, string endedName)
        {
            CurrentDisaster = null;

            // クールダウン開始
            if (GameManager.Instance != null && GameManager.Instance.TimeManager != null)
            {
                lastDisasterDay = GetCurrentGameDay();
            }
            DisasterCooldownTimer = DISASTER_COOLDOWN_DAYS;

            // イベント発火
            OnDisasterEnded?.Invoke(endedType);
            GameEvents.FireParkEventEnded($"災害:{endedName}");

            WebGLOptimizer.LogVerbose(
                $"DisasterEventSystem: 災害 '{endedName}' が完全に終了しました。" +
                $"クールダウン: {DISASTER_COOLDOWN_DAYS}日間"
            );
        }

        // =====================================================================
        // 日次処理（災害発生判定）
        // =====================================================================

        /// <summary>
        /// ゲーム内の日が変わった時の処理
        /// 災害発生のランダム判定を行う
        /// </summary>
        /// <param name="day">現在のゲーム内日数</param>
        public void OnDayChanged(int day)
        {
            WebGLOptimizer.LogVerbose($"DisasterEventSystem: 日次処理開始 - Day {day}");

            // クールダウンタイマーの更新
            if (DisasterCooldownTimer > 0f)
            {
                DisasterCooldownTimer -= 1f;
                WebGLOptimizer.LogVerbose(
                    $"DisasterEventSystem: クールダウン残り {DisasterCooldownTimer:F0} 日"
                );
                return;
            }

            // 既に災害が進行中なら判定しない
            if (IsDisasterActive)
            {
                WebGLOptimizer.LogVerbose("DisasterEventSystem: 災害進行中のため、新規発生判定をスキップします。");
                return;
            }

            // クールダウン期間チェック
            int daysSinceLastDisaster = day - lastDisasterDay;
            if (daysSinceLastDisaster < DISASTER_COOLDOWN_DAYS)
            {
                WebGLOptimizer.LogVerbose(
                    $"DisasterEventSystem: クールダウン中（前回の災害から{daysSinceLastDisaster}日経過、必要: {DISASTER_COOLDOWN_DAYS}日）"
                );
                return;
            }

            // 難易度に応じた発生確率を取得
            float disasterChance = GetDisasterChanceForDifficulty();

            // 天候による補正
            float weatherModifier = GetWeatherDisasterModifier();
            float finalChance = disasterChance * weatherModifier;

            // ランダム判定
            float roll = UnityEngine.Random.value;

            WebGLOptimizer.LogVerbose(
                $"DisasterEventSystem: 災害発生判定 - 確率: {finalChance * 100f:F1}%, " +
                $"判定値: {roll:F3}, 天候補正: x{weatherModifier:F2}"
            );

            if (roll < finalChance)
            {
                // 災害タイプをランダムに選択（天候考慮）
                DisasterType selectedType = SelectRandomDisasterType();
                WebGLOptimizer.LogWarning(
                    $"DisasterEventSystem: 災害発生判定に成功！ タイプ: {selectedType}"
                );
                TriggerDisaster(selectedType);
            }
        }

        // =====================================================================
        // 外部インターフェース
        // =====================================================================

        /// <summary>
        /// 現在のスポーンブロック率を返す
        /// 災害が発生していない場合は0
        /// </summary>
        /// <returns>スポーンブロック率（0～1）</returns>
        public float GetCurrentSpawnBlockRate()
        {
            if (CurrentDisaster == null)
            {
                return 0f;
            }

            // 復旧フェーズでは段階的にブロック率を減少
            if (CurrentDisaster.Phase == DisasterPhase.Recovery)
            {
                float recoveryProgress = Mathf.Clamp01(
                    CurrentDisaster.ElapsedTime / CurrentDisaster.RecoveryDuration
                );
                return CurrentDisaster.SpawnBlockRate * (1f - recoveryProgress);
            }

            // 警告フェーズでは半分のブロック率
            if (CurrentDisaster.Phase == DisasterPhase.Warning)
            {
                return CurrentDisaster.SpawnBlockRate * 0.5f;
            }

            // アクティブフェーズではフルのブロック率
            return CurrentDisaster.SpawnBlockRate;
        }

        /// <summary>
        /// 現在の毎秒幸福度ペナルティを返す
        /// 災害が発生していない場合は0
        /// </summary>
        /// <returns>毎秒幸福度減少量（負の値または0）</returns>
        public float GetCurrentHappinessPenalty()
        {
            if (CurrentDisaster == null)
            {
                return 0f;
            }

            // アクティブフェーズのみペナルティを適用
            if (CurrentDisaster.Phase == DisasterPhase.Active)
            {
                return CurrentDisaster.HappinessPenalty;
            }

            // 復旧フェーズでは段階的にペナルティを減少
            if (CurrentDisaster.Phase == DisasterPhase.Recovery)
            {
                float recoveryProgress = Mathf.Clamp01(
                    CurrentDisaster.ElapsedTime / CurrentDisaster.RecoveryDuration
                );
                return CurrentDisaster.HappinessPenalty * (1f - recoveryProgress);
            }

            return 0f;
        }

        // =====================================================================
        // 内部ユーティリティ
        // =====================================================================

        /// <summary>
        /// 現在の難易度に基づく災害発生確率を返す
        /// </summary>
        private float GetDisasterChanceForDifficulty()
        {
            if (GameManager.Instance == null)
            {
                return CHANCE_NORMAL;
            }

            switch (GameManager.Instance.CurrentDifficulty)
            {
                case GameEnums.Difficulty.Easy:
                    return CHANCE_EASY;
                case GameEnums.Difficulty.Normal:
                    return CHANCE_NORMAL;
                case GameEnums.Difficulty.Hard:
                    return CHANCE_HARD;
                default:
                    return CHANCE_NORMAL;
            }
        }

        /// <summary>
        /// 現在の天候による災害発生確率の補正係数を返す
        /// </summary>
        private float GetWeatherDisasterModifier()
        {
            if (GameManager.Instance == null || GameManager.Instance.WeatherSystem == null)
            {
                return 1.0f;
            }

            Weather currentWeather = GameManager.Instance.WeatherSystem.CurrentWeather;

            switch (currentWeather)
            {
                case Weather.Sunny:
                    return 0.8f; // 晴天時は災害が起きにくい
                case Weather.Cloudy:
                    return 1.0f;
                case Weather.Rainy:
                    return 1.3f; // 雨天時は洪水リスク上昇
                case Weather.Snowy:
                    return 1.1f;
                case Weather.Hot:
                    return 1.2f; // 猛暑時は火災・停電リスク上昇
                case Weather.Typhoon:
                    return 2.0f; // 台風時は災害発生率倍増
                case Weather.Thunderstorm:
                    return 1.8f; // 雷雨時も災害発生率大幅上昇
                default:
                    return 1.0f;
            }
        }

        /// <summary>
        /// 天候を考慮してランダムに災害タイプを選択する
        /// </summary>
        private DisasterType SelectRandomDisasterType()
        {
            // 基本の重み
            Dictionary<DisasterType, float> weights = new Dictionary<DisasterType, float>
            {
                { DisasterType.Earthquake, 1.0f },
                { DisasterType.PowerOutage, 1.5f },
                { DisasterType.Pandemic, 0.8f },
                { DisasterType.Fire, 1.2f },
                { DisasterType.Flood, 1.0f }
            };

            // 天候による重み補正
            if (GameManager.Instance != null && GameManager.Instance.WeatherSystem != null)
            {
                Weather currentWeather = GameManager.Instance.WeatherSystem.CurrentWeather;

                switch (currentWeather)
                {
                    case Weather.Rainy:
                    case Weather.Typhoon:
                        weights[DisasterType.Flood] *= 3.0f;
                        weights[DisasterType.Fire] *= 0.3f;
                        break;

                    case Weather.Thunderstorm:
                        weights[DisasterType.PowerOutage] *= 2.5f;
                        weights[DisasterType.Fire] *= 1.5f;
                        weights[DisasterType.Flood] *= 2.0f;
                        break;

                    case Weather.Hot:
                        weights[DisasterType.Fire] *= 2.5f;
                        weights[DisasterType.PowerOutage] *= 1.8f;
                        weights[DisasterType.Flood] *= 0.3f;
                        break;

                    case Weather.Snowy:
                        weights[DisasterType.PowerOutage] *= 1.5f;
                        weights[DisasterType.Fire] *= 0.5f;
                        break;

                    case Weather.Sunny:
                    case Weather.Cloudy:
                    default:
                        // 補正なし
                        break;
                }
            }

            // 重み付きランダム選択
            float totalWeight = 0f;
            foreach (var kvp in weights)
            {
                totalWeight += kvp.Value;
            }

            float randomValue = UnityEngine.Random.value * totalWeight;
            float cumulative = 0f;

            foreach (var kvp in weights)
            {
                cumulative += kvp.Value;
                if (randomValue <= cumulative)
                {
                    return kvp.Key;
                }
            }

            // フォールバック
            return DisasterType.Earthquake;
        }

        /// <summary>
        /// 現在のゲーム内日数を取得するヘルパー
        /// </summary>
        private int GetCurrentGameDay()
        {
            if (GameManager.Instance != null && GameManager.Instance.TimeManager != null)
            {
                return GameManager.Instance.TimeManager.CurrentDay;
            }
            return 0;
        }

        // =====================================================================
        // デバッグ・情報取得
        // =====================================================================

        /// <summary>
        /// 現在の災害状況を文字列で返す（デバッグ用）
        /// </summary>
        /// <returns>災害状況の概要文字列</returns>
        public string GetDisasterStatusReport()
        {
            if (CurrentDisaster == null)
            {
                return "現在、災害は発生していません。" +
                       $"\nクールダウン残り: {DisasterCooldownTimer:F0}日";
            }

            string phaseText;
            float phaseProgress;

            switch (CurrentDisaster.Phase)
            {
                case DisasterPhase.Warning:
                    phaseText = "警告中";
                    phaseProgress = CurrentDisaster.ElapsedTime / CurrentDisaster.WarningDuration;
                    break;
                case DisasterPhase.Active:
                    phaseText = "発生中";
                    phaseProgress = CurrentDisaster.ElapsedTime / CurrentDisaster.Duration;
                    break;
                case DisasterPhase.Recovery:
                    phaseText = "復旧中";
                    phaseProgress = CurrentDisaster.ElapsedTime / CurrentDisaster.RecoveryDuration;
                    break;
                default:
                    phaseText = "不明";
                    phaseProgress = 0f;
                    break;
            }

            string zoneText = CurrentDisaster.AffectedZone.HasValue
                ? CurrentDisaster.AffectedZone.Value.ToString()
                : "パーク全体";

            return $"【災害レポート】\n" +
                   $"種類: {CurrentDisaster.DisplayName}\n" +
                   $"フェーズ: {phaseText} ({phaseProgress * 100f:F0}%)\n" +
                   $"影響範囲: {zoneText}\n" +
                   $"影響アトラクション数: {CurrentDisaster.AffectedAttractionCount}\n" +
                   $"累積ダメージ: {totalDamageApplied:F2}\n" +
                   $"累積幸福度減少: {totalHappinessDrained:F2}\n" +
                   $"修理費用: ¥{CurrentDisaster.RepairCost:N0}";
        }

        /// <summary>
        /// 指定された災害タイプの情報を取得する
        /// </summary>
        /// <param name="type">災害タイプ</param>
        /// <returns>災害テンプレートのコピー。存在しない場合はnull。</returns>
        public DisasterEvent GetDisasterInfo(DisasterType type)
        {
            if (disasterTemplates != null && disasterTemplates.ContainsKey(type))
            {
                DisasterEvent template = disasterTemplates[type];
                return new DisasterEvent
                {
                    Type = template.Type,
                    DisplayName = template.DisplayName,
                    Description = template.Description,
                    Duration = template.Duration,
                    WarningDuration = template.WarningDuration,
                    RecoveryDuration = template.RecoveryDuration,
                    DamageMultiplier = template.DamageMultiplier,
                    HappinessPenalty = template.HappinessPenalty,
                    SpawnBlockRate = template.SpawnBlockRate,
                    RepairCost = template.RepairCost
                };
            }

            WebGLOptimizer.LogWarning($"DisasterEventSystem: 災害タイプ {type} のテンプレートが見つかりません。");
            return null;
        }

        /// <summary>
        /// 利用可能な全災害タイプのリストを返す
        /// </summary>
        /// <returns>災害タイプの配列</returns>
        public DisasterType[] GetAllDisasterTypes()
        {
            if (disasterTemplates == null) return new DisasterType[0];

            DisasterType[] types = new DisasterType[disasterTemplates.Count];
            disasterTemplates.Keys.CopyTo(types, 0);
            return types;
        }
    }
}
