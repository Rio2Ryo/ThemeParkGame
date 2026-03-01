// ============================================================================
// InteractiveAttractionSystem.cs
// インタラクティブアトラクション管理システム
// 参加型・体験型アトラクションの統合管理を行うシングルトン
// ============================================================================

using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using ThemeParkGame.Core;

namespace ThemeParkGame.Attraction
{
    // ========================================================================
    // インタラクティブアトラクションデータ定義
    // ========================================================================
    [Serializable]
    public class InteractiveAttractionData
    {
        public string Id;
        public string DisplayName;
        public string Description;
        public InteractiveMode Mode;
        public AttractionCategory BaseCategory;
        public ThemeZone Zone;
        public int Capacity;
        public float Duration;
        public float BuildCost;
        public float MaintenanceCost;
        public float TicketPrice;
        public float ExcitementRating;
        public float NauseaRate;
        public float InteractionBonus;
        public float ScoreMultiplier;
        public string Size;

        public InteractiveAttractionData(
            string id,
            string displayName,
            string description,
            InteractiveMode mode,
            AttractionCategory baseCategory,
            ThemeZone zone,
            int capacity,
            float duration,
            float buildCost,
            float maintenanceCost,
            float ticketPrice,
            float excitementRating,
            float nauseaRate,
            float interactionBonus,
            float scoreMultiplier,
            string size)
        {
            Id = id;
            DisplayName = displayName;
            Description = description;
            Mode = mode;
            BaseCategory = baseCategory;
            Zone = zone;
            Capacity = capacity;
            Duration = duration;
            BuildCost = buildCost;
            MaintenanceCost = maintenanceCost;
            TicketPrice = ticketPrice;
            ExcitementRating = excitementRating;
            NauseaRate = nauseaRate;
            InteractionBonus = interactionBonus;
            ScoreMultiplier = scoreMultiplier;
            Size = size;
        }
    }

    // ========================================================================
    // インタラクティブセッション（進行中のアトラクション体験）
    // ========================================================================
    [Serializable]
    public class InteractiveSession
    {
        public string AttractionId;
        public InteractiveMode Mode;
        public List<int> ParticipantVisitorIds;
        public Dictionary<int, float> Scores;
        public float ElapsedTime;
        public bool IsComplete;
        public float BonusSatisfaction;

        public InteractiveSession(string attractionId, InteractiveMode mode, List<int> visitorIds)
        {
            AttractionId = attractionId;
            Mode = mode;
            ParticipantVisitorIds = new List<int>(visitorIds);
            Scores = new Dictionary<int, float>();
            ElapsedTime = 0f;
            IsComplete = false;
            BonusSatisfaction = 0f;

            foreach (int visitorId in ParticipantVisitorIds)
            {
                Scores[visitorId] = 0f;
            }
        }
    }

    // ========================================================================
    // インタラクティブアトラクションシステム（シングルトン）
    // ========================================================================
    public class InteractiveAttractionSystem : MonoBehaviour
    {
        // --------------------------------------------------------------------
        // シングルトンインスタンス
        // --------------------------------------------------------------------
        private static InteractiveAttractionSystem _instance;
        public static InteractiveAttractionSystem Instance
        {
            get
            {
                if (_instance == null)
                {
                    WebGLOptimizer.LogVerbose("InteractiveAttractionSystem: インスタンスが見つかりません");
                }
                return _instance;
            }
        }

        // --------------------------------------------------------------------
        // データベースとセッション管理
        // --------------------------------------------------------------------
        private Dictionary<string, InteractiveAttractionData> _database;
        private Dictionary<string, InteractiveSession> _activeSessions;
        private HashSet<string> _builtAttractions;

        // --------------------------------------------------------------------
        // イベント
        // --------------------------------------------------------------------
        public Action<string> OnSessionStarted;
        public Action<string, Dictionary<int, float>> OnSessionCompleted;

        // --------------------------------------------------------------------
        // 定数
        // --------------------------------------------------------------------
        private const float MIN_SCORE = 0f;
        private const float MAX_SCORE = 100f;
        private const float BASE_SCORE_MIN = 30f;
        private const float BASE_SCORE_MAX = 80f;
        private const float PERSONALITY_INFLUENCE = 20f;
        private const float COMPETITIVE_VARIANCE = 15f;

        // ====================================================================
        // ライフサイクル
        // ====================================================================

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                WebGLOptimizer.LogVerbose(
                    "InteractiveAttractionSystem: 重複インスタンスを検出しました。破棄します。");
                Destroy(gameObject);
                return;
            }

            _instance = this;
            _database = new Dictionary<string, InteractiveAttractionData>();
            _activeSessions = new Dictionary<string, InteractiveSession>();
            _builtAttractions = new HashSet<string>();

            InitializeDatabase();

            WebGLOptimizer.LogVerbose(
                "InteractiveAttractionSystem: 初期化完了。登録アトラクション数=" + _database.Count);
        }

        private void OnDestroy()
        {
            if (_instance == this)
            {
                _instance = null;
                WebGLOptimizer.LogVerbose(
                    "InteractiveAttractionSystem: インスタンスを破棄しました。");
            }
        }

        private void Update()
        {
            if (_activeSessions.Count > 0)
            {
                UpdateSessions(Time.deltaTime);
            }
        }

        // ====================================================================
        // データベース初期化
        // ====================================================================

        /// <summary>
        /// 全インタラクティブアトラクションをデータベースに登録する
        /// </summary>
        public void InitializeDatabase()
        {
            _database.Clear();

            // ----------------------------------------------------------------
            // ダークハント（シューティングライド）
            // ----------------------------------------------------------------
            RegisterAttraction(new InteractiveAttractionData(
                id: "IA_SHOOTING_GALLERY",
                displayName: "ダークハント",
                description: "闇に潜む幽霊たちをレーザーガンで撃退するシューティングライド。" +
                             "ライドに乗りながら次々と現れるターゲットを狙い撃て！",
                mode: InteractiveMode.ShootingRide,
                baseCategory: AttractionCategory.Ride,
                zone: ThemeZone.HalloweenWorld,
                capacity: 12,
                duration: 120f,
                buildCost: 8000f,
                maintenanceCost: 400f,
                ticketPrice: 600f,
                excitementRating: 7.0f,
                nauseaRate: 0.15f,
                interactionBonus: 20f,
                scoreMultiplier: 1.2f,
                size: "4x5"
            ));

            // ----------------------------------------------------------------
            // アドベンチャーナビ（ステアリングライド）
            // ----------------------------------------------------------------
            RegisterAttraction(new InteractiveAttractionData(
                id: "IA_STEER_EXPLORER",
                displayName: "アドベンチャーナビ",
                description: "自分でハンドルを操作して冒険の世界を探索するステアリングライド。" +
                             "選んだルートによって異なる体験が待っている！",
                mode: InteractiveMode.SteeringRide,
                baseCategory: AttractionCategory.Ride,
                zone: ThemeZone.Wonderland,
                capacity: 8,
                duration: 150f,
                buildCost: 10000f,
                maintenanceCost: 500f,
                ticketPrice: 700f,
                excitementRating: 7.5f,
                nauseaRate: 0.2f,
                interactionBonus: 15f,
                scoreMultiplier: 1.0f,
                size: "5x6"
            ));

            // ----------------------------------------------------------------
            // ストーリーシアター（投票型ショー）
            // ----------------------------------------------------------------
            RegisterAttraction(new InteractiveAttractionData(
                id: "IA_VOTE_THEATER",
                displayName: "ストーリーシアター",
                description: "観客の投票でストーリーの展開が変わるインタラクティブシアター。" +
                             "あなたの選択が物語の結末を決める！",
                mode: InteractiveMode.VoteShow,
                baseCategory: AttractionCategory.Show,
                zone: ThemeZone.FutureCity,
                capacity: 50,
                duration: 180f,
                buildCost: 12000f,
                maintenanceCost: 600f,
                ticketPrice: 500f,
                excitementRating: 6.0f,
                nauseaRate: 0.0f,
                interactionBonus: 25f,
                scoreMultiplier: 0.8f,
                size: "6x8"
            ));

            // ----------------------------------------------------------------
            // レーシングサンダー（競争型ライド）
            // ----------------------------------------------------------------
            RegisterAttraction(new InteractiveAttractionData(
                id: "IA_RACE_COASTER",
                displayName: "レーシングサンダー",
                description: "他のゲストとスピードを競い合うレーシングコースター。" +
                             "最高速度を目指してブーストを使いこなせ！",
                mode: InteractiveMode.CompetitiveRide,
                baseCategory: AttractionCategory.Ride,
                zone: ThemeZone.SpaceZone,
                capacity: 16,
                duration: 90f,
                buildCost: 15000f,
                maintenanceCost: 750f,
                ticketPrice: 900f,
                excitementRating: 9.0f,
                nauseaRate: 0.4f,
                interactionBonus: 30f,
                scoreMultiplier: 1.5f,
                size: "8x10"
            ));

            // ----------------------------------------------------------------
            // スプラッシュバトル（競争型ウォーターライド）
            // ----------------------------------------------------------------
            RegisterAttraction(new InteractiveAttractionData(
                id: "IA_WATER_BATTLE",
                displayName: "スプラッシュバトル",
                description: "水鉄砲を使って他のボートと水合戦を繰り広げるウォーターアトラクション。" +
                             "びしょ濡れ覚悟で挑め！",
                mode: InteractiveMode.CompetitiveRide,
                baseCategory: AttractionCategory.Ride,
                zone: ThemeZone.Wonderland,
                capacity: 20,
                duration: 120f,
                buildCost: 9000f,
                maintenanceCost: 450f,
                ticketPrice: 650f,
                excitementRating: 7.5f,
                nauseaRate: 0.1f,
                interactionBonus: 22f,
                scoreMultiplier: 1.3f,
                size: "5x7"
            ));

            // ----------------------------------------------------------------
            // ミステリーラビリンス（ステアリング型迷路）
            // ----------------------------------------------------------------
            RegisterAttraction(new InteractiveAttractionData(
                id: "IA_MYSTERY_MAZE",
                displayName: "ミステリーラビリンス",
                description: "自分で進路を選びながら謎を解き明かすステアリング型迷路。" +
                             "隠された宝を見つけ出せるか？",
                mode: InteractiveMode.SteeringRide,
                baseCategory: AttractionCategory.Ride,
                zone: ThemeZone.LostKingdom,
                capacity: 10,
                duration: 200f,
                buildCost: 7000f,
                maintenanceCost: 350f,
                ticketPrice: 550f,
                excitementRating: 6.5f,
                nauseaRate: 0.05f,
                interactionBonus: 18f,
                scoreMultiplier: 1.1f,
                size: "6x6"
            ));

            WebGLOptimizer.LogVerbose(
                "InteractiveAttractionSystem: データベース初期化完了。" +
                "登録数=" + _database.Count);
        }

        /// <summary>
        /// アトラクションデータをデータベースに登録する
        /// </summary>
        private void RegisterAttraction(InteractiveAttractionData data)
        {
            if (data == null || string.IsNullOrEmpty(data.Id))
            {
                WebGLOptimizer.LogVerbose(
                    "InteractiveAttractionSystem: 無効なアトラクションデータの登録を拒否しました。");
                return;
            }

            if (_database.ContainsKey(data.Id))
            {
                WebGLOptimizer.LogVerbose(
                    "InteractiveAttractionSystem: 重複ID「" + data.Id + "」の登録を拒否しました。");
                return;
            }

            _database[data.Id] = data;
            WebGLOptimizer.LogVerbose(
                "InteractiveAttractionSystem: アトラクション「" + data.DisplayName +
                "」を登録しました。ID=" + data.Id);
        }

        // ====================================================================
        // データ取得メソッド
        // ====================================================================

        /// <summary>
        /// 指定IDのアトラクションデータを取得する
        /// </summary>
        public InteractiveAttractionData GetAttractionData(string id)
        {
            if (string.IsNullOrEmpty(id))
            {
                WebGLOptimizer.LogVerbose(
                    "InteractiveAttractionSystem: GetAttractionData - IDが空です。");
                return null;
            }

            if (_database.TryGetValue(id, out InteractiveAttractionData data))
            {
                return data;
            }

            WebGLOptimizer.LogVerbose(
                "InteractiveAttractionSystem: アトラクションID「" + id + "」が見つかりません。");
            return null;
        }

        /// <summary>
        /// 全アトラクションデータのリストを取得する
        /// </summary>
        public List<InteractiveAttractionData> GetAllAttractions()
        {
            List<InteractiveAttractionData> result = new List<InteractiveAttractionData>(_database.Values);
            WebGLOptimizer.LogVerbose(
                "InteractiveAttractionSystem: 全アトラクション取得。件数=" + result.Count);
            return result;
        }

        /// <summary>
        /// 指定モードのアトラクションをフィルタリングして取得する
        /// </summary>
        public List<InteractiveAttractionData> GetAttractionsByMode(InteractiveMode mode)
        {
            List<InteractiveAttractionData> result = new List<InteractiveAttractionData>();

            foreach (var kvp in _database)
            {
                if (kvp.Value.Mode == mode)
                {
                    result.Add(kvp.Value);
                }
            }

            WebGLOptimizer.LogVerbose(
                "InteractiveAttractionSystem: モード「" + mode +
                "」のアトラクション取得。件数=" + result.Count);
            return result;
        }

        // ====================================================================
        // セッション管理
        // ====================================================================

        /// <summary>
        /// 新しいインタラクティブセッションを開始する
        /// </summary>
        public bool StartSession(string attractionId, List<int> visitorIds)
        {
            if (string.IsNullOrEmpty(attractionId))
            {
                WebGLOptimizer.LogVerbose(
                    "InteractiveAttractionSystem: StartSession - アトラクションIDが空です。");
                return false;
            }

            if (visitorIds == null || visitorIds.Count == 0)
            {
                WebGLOptimizer.LogVerbose(
                    "InteractiveAttractionSystem: StartSession - 参加者がいません。" +
                    "アトラクションID=" + attractionId);
                return false;
            }

            InteractiveAttractionData data = GetAttractionData(attractionId);
            if (data == null)
            {
                WebGLOptimizer.LogVerbose(
                    "InteractiveAttractionSystem: StartSession - " +
                    "アトラクションデータが見つかりません。ID=" + attractionId);
                return false;
            }

            if (_activeSessions.ContainsKey(attractionId))
            {
                WebGLOptimizer.LogVerbose(
                    "InteractiveAttractionSystem: StartSession - " +
                    "既にセッションが進行中です。ID=" + attractionId);
                return false;
            }

            if (!_builtAttractions.Contains(attractionId))
            {
                WebGLOptimizer.LogVerbose(
                    "InteractiveAttractionSystem: StartSession - " +
                    "アトラクションが未建設です。ID=" + attractionId);
                return false;
            }

            // 定員チェック
            List<int> participants = visitorIds;
            if (visitorIds.Count > data.Capacity)
            {
                WebGLOptimizer.LogVerbose(
                    "InteractiveAttractionSystem: 定員超過のため先着" + data.Capacity +
                    "名に制限します。アトラクション=" + data.DisplayName);
                participants = visitorIds.GetRange(0, data.Capacity);
            }

            InteractiveSession session = new InteractiveSession(
                attractionId, data.Mode, participants);

            _activeSessions[attractionId] = session;

            WebGLOptimizer.LogVerbose(
                "InteractiveAttractionSystem: セッション開始。" +
                "アトラクション「" + data.DisplayName + "」" +
                "参加者数=" + participants.Count +
                " モード=" + data.Mode);

            OnSessionStarted?.Invoke(attractionId);
            GameEvents.NotifyAttractionEvent(
                attractionId, "セッション開始: " + data.DisplayName);

            return true;
        }

        /// <summary>
        /// 全アクティブセッションの経過時間を更新する
        /// </summary>
        public void UpdateSessions(float dt)
        {
            if (_activeSessions.Count == 0)
            {
                return;
            }

            List<string> completedSessionIds = new List<string>();

            foreach (var kvp in _activeSessions)
            {
                string attractionId = kvp.Key;
                InteractiveSession session = kvp.Value;

                if (session.IsComplete)
                {
                    continue;
                }

                session.ElapsedTime += dt;

                // セッション中のスコア更新（競争モードの場合）
                InteractiveAttractionData data = GetAttractionData(attractionId);
                if (data != null && IsCompetitiveMode(session.Mode))
                {
                    UpdateCompetitiveScores(session, data, dt);
                }

                // セッション完了チェック
                if (data != null && session.ElapsedTime >= data.Duration)
                {
                    completedSessionIds.Add(attractionId);
                }
            }

            // 完了したセッションを終了処理
            foreach (string completedId in completedSessionIds)
            {
                EndSession(completedId);
            }
        }

        /// <summary>
        /// 競争モードのスコアをリアルタイム更新する
        /// </summary>
        private void UpdateCompetitiveScores(
            InteractiveSession session, InteractiveAttractionData data, float dt)
        {
            foreach (int visitorId in session.ParticipantVisitorIds)
            {
                float scoreIncrement = UnityEngine.Random.Range(0.5f, 2.0f) * dt;
                scoreIncrement *= data.ScoreMultiplier;

                if (session.Scores.ContainsKey(visitorId))
                {
                    session.Scores[visitorId] += scoreIncrement;
                }
            }
        }

        /// <summary>
        /// セッションを終了し、スコア計算とボーナス適用を行う
        /// </summary>
        public void EndSession(string attractionId)
        {
            if (string.IsNullOrEmpty(attractionId))
            {
                WebGLOptimizer.LogVerbose(
                    "InteractiveAttractionSystem: EndSession - アトラクションIDが空です。");
                return;
            }

            if (!_activeSessions.TryGetValue(attractionId, out InteractiveSession session))
            {
                WebGLOptimizer.LogVerbose(
                    "InteractiveAttractionSystem: EndSession - " +
                    "アクティブセッションが見つかりません。ID=" + attractionId);
                return;
            }

            InteractiveAttractionData data = GetAttractionData(attractionId);
            if (data == null)
            {
                WebGLOptimizer.LogVerbose(
                    "InteractiveAttractionSystem: EndSession - " +
                    "アトラクションデータが見つかりません。ID=" + attractionId);
                _activeSessions.Remove(attractionId);
                return;
            }

            session.IsComplete = true;

            // 各参加者の最終スコアを計算
            Dictionary<int, float> finalScores = new Dictionary<int, float>();
            float totalBonusSatisfaction = 0f;

            foreach (int visitorId in session.ParticipantVisitorIds)
            {
                float score = CalculateInteractionScore(visitorId, session);
                finalScores[visitorId] = score;
                session.Scores[visitorId] = score;

                float bonus = GetSatisfactionBonus(data, score);
                totalBonusSatisfaction += bonus;

                WebGLOptimizer.LogVerbose(
                    "InteractiveAttractionSystem: 参加者スコア計算完了。" +
                    "ビジターID=" + visitorId +
                    " スコア=" + score.ToString("F1") +
                    " 満足度ボーナス=" + bonus.ToString("F1"));
            }

            // 平均ボーナス満足度を計算
            if (session.ParticipantVisitorIds.Count > 0)
            {
                session.BonusSatisfaction =
                    totalBonusSatisfaction / session.ParticipantVisitorIds.Count;
            }

            WebGLOptimizer.LogVerbose(
                "InteractiveAttractionSystem: セッション完了。" +
                "アトラクション「" + data.DisplayName + "」" +
                "参加者数=" + session.ParticipantVisitorIds.Count +
                " 平均ボーナス満足度=" + session.BonusSatisfaction.ToString("F1"));

            // イベント発火
            OnSessionCompleted?.Invoke(attractionId, finalScores);
            GameEvents.NotifyAttractionEvent(
                attractionId, "セッション完了: " + data.DisplayName);

            NotificationSystem.Show(
                "「" + data.DisplayName + "」のセッションが完了しました！",
                NotifLevel.Info);

            // セッションを削除
            _activeSessions.Remove(attractionId);
        }

        // ====================================================================
        // スコアと満足度計算
        // ====================================================================

        /// <summary>
        /// 参加者のインタラクションスコアを計算する
        /// ビジターの個性とランダム要素で変動
        /// </summary>
        public float CalculateInteractionScore(int visitorId, InteractiveSession session)
        {
            if (session == null)
            {
                WebGLOptimizer.LogVerbose(
                    "InteractiveAttractionSystem: CalculateInteractionScore - " +
                    "セッションがnullです。");
                return 0f;
            }

            // 基本スコア（ランダム範囲）
            float baseScore = UnityEngine.Random.Range(BASE_SCORE_MIN, BASE_SCORE_MAX);

            // ビジターIDに基づくパーソナリティ影響（疑似的な個性反映）
            float personalitySeed = (visitorId * 7919) % 1000 / 1000f;
            float personalityModifier = (personalitySeed - 0.5f) * PERSONALITY_INFLUENCE;

            // モードに基づくスコア調整
            float modeModifier = 0f;
            switch (session.Mode)
            {
                case InteractiveMode.ShootingRide:
                    // シューティングは精度が求められる
                    modeModifier = UnityEngine.Random.Range(-10f, 15f);
                    break;
                case InteractiveMode.SteeringRide:
                    // ステアリングは探索度合いで変動
                    modeModifier = UnityEngine.Random.Range(-5f, 10f);
                    break;
                case InteractiveMode.VoteShow:
                    // 投票型は参加度で均等にボーナス
                    modeModifier = UnityEngine.Random.Range(5f, 15f);
                    break;
                case InteractiveMode.CompetitiveRide:
                    // 競争型は大きく変動
                    float competitiveRandom =
                        UnityEngine.Random.Range(-COMPETITIVE_VARIANCE, COMPETITIVE_VARIANCE);
                    modeModifier = competitiveRandom;
                    // 既に蓄積されたリアルタイムスコアを加味
                    if (session.Scores.ContainsKey(visitorId))
                    {
                        modeModifier += Mathf.Clamp(session.Scores[visitorId] * 0.1f, 0f, 10f);
                    }
                    break;
            }

            // 参加時間ボーナス（最後まで参加していた場合）
            InteractiveAttractionData data = GetAttractionData(session.AttractionId);
            float timeBonus = 0f;
            if (data != null && session.ElapsedTime >= data.Duration * 0.9f)
            {
                timeBonus = 5f;
            }

            // 最終スコアを算出してクランプ
            float finalScore = baseScore + personalityModifier + modeModifier + timeBonus;
            finalScore = Mathf.Clamp(finalScore, MIN_SCORE, MAX_SCORE);

            WebGLOptimizer.LogVerbose(
                "InteractiveAttractionSystem: スコア計算。" +
                "ビジターID=" + visitorId +
                " 基本=" + baseScore.ToString("F1") +
                " 個性=" + personalityModifier.ToString("F1") +
                " モード=" + modeModifier.ToString("F1") +
                " 時間ボーナス=" + timeBonus.ToString("F1") +
                " 最終=" + finalScore.ToString("F1"));

            return finalScore;
        }

        /// <summary>
        /// スコアを満足度ボーナスにマッピングする
        /// </summary>
        public float GetSatisfactionBonus(InteractiveAttractionData data, float score)
        {
            if (data == null)
            {
                WebGLOptimizer.LogVerbose(
                    "InteractiveAttractionSystem: GetSatisfactionBonus - " +
                    "アトラクションデータがnullです。");
                return 0f;
            }

            // スコアを0-1の比率に変換
            float scoreRatio = Mathf.Clamp01(score / MAX_SCORE);

            // インタラクションボーナスにスコア比率を乗算
            float bonus = data.InteractionBonus * scoreRatio;

            // 競争モードの場合、スコアマルチプライヤーを適用
            if (IsCompetitiveMode(data.Mode))
            {
                bonus *= data.ScoreMultiplier;
            }

            // ボーナス上限を適用（0-30の範囲内）
            bonus = Mathf.Clamp(bonus, 0f, 30f);

            return bonus;
        }

        /// <summary>
        /// 競争型モードかどうかを判定する
        /// </summary>
        private bool IsCompetitiveMode(InteractiveMode mode)
        {
            return mode == InteractiveMode.CompetitiveRide;
        }

        // ====================================================================
        // 建設管理
        // ====================================================================

        /// <summary>
        /// 指定アトラクションの建設が可能かチェックする
        /// </summary>
        public bool CanBuildAttraction(string id)
        {
            if (string.IsNullOrEmpty(id))
            {
                WebGLOptimizer.LogVerbose(
                    "InteractiveAttractionSystem: CanBuildAttraction - IDが空です。");
                return false;
            }

            InteractiveAttractionData data = GetAttractionData(id);
            if (data == null)
            {
                WebGLOptimizer.LogVerbose(
                    "InteractiveAttractionSystem: CanBuildAttraction - " +
                    "アトラクションID「" + id + "」が見つかりません。");
                return false;
            }

            if (_builtAttractions.Contains(id))
            {
                WebGLOptimizer.LogVerbose(
                    "InteractiveAttractionSystem: CanBuildAttraction - " +
                    "既に建設済みです。ID=" + id);
                return false;
            }

            bool canAfford = GameManager.Instance.EconomyManager.CanAfford(data.BuildCost);
            if (!canAfford)
            {
                WebGLOptimizer.LogVerbose(
                    "InteractiveAttractionSystem: CanBuildAttraction - " +
                    "資金不足です。必要額=" + data.BuildCost.ToString("F0") +
                    " アトラクション=" + data.DisplayName);
            }

            return canAfford;
        }

        /// <summary>
        /// アトラクションを建設する（費用を支払い登録する）
        /// </summary>
        public bool BuildAttraction(string id)
        {
            if (!CanBuildAttraction(id))
            {
                WebGLOptimizer.LogVerbose(
                    "InteractiveAttractionSystem: BuildAttraction - " +
                    "建設条件を満たしていません。ID=" + id);
                return false;
            }

            InteractiveAttractionData data = GetAttractionData(id);
            if (data == null)
            {
                return false;
            }

            // 建設費用を支払い
            GameManager.Instance.EconomyManager.PayExpense(
                data.BuildCost, Economy.ExpenseCategory.Other);

            _builtAttractions.Add(id);

            WebGLOptimizer.LogVerbose(
                "InteractiveAttractionSystem: アトラクション建設完了。" +
                "名前「" + data.DisplayName + "」" +
                "費用=" + data.BuildCost.ToString("F0") +
                " ゾーン=" + data.Zone);

            NotificationSystem.Show(
                "「" + data.DisplayName + "」を建設しました！" +
                "（費用: ¥" + data.BuildCost.ToString("N0") + "）",
                NotifLevel.Info);

            GameEvents.NotifyAttractionEvent(
                id, "建設完了: " + data.DisplayName);

            return true;
        }

        /// <summary>
        /// 建設済みインタラクティブアトラクションの数を取得する
        /// </summary>
        public int GetBuiltCount()
        {
            return _builtAttractions.Count;
        }

        // ====================================================================
        // ユーティリティメソッド
        // ====================================================================

        /// <summary>
        /// 指定アトラクションのセッションがアクティブかどうかを確認する
        /// </summary>
        public bool IsSessionActive(string attractionId)
        {
            return _activeSessions.ContainsKey(attractionId);
        }

        /// <summary>
        /// 指定アトラクションの現在のセッションを取得する
        /// </summary>
        public InteractiveSession GetActiveSession(string attractionId)
        {
            if (_activeSessions.TryGetValue(attractionId, out InteractiveSession session))
            {
                return session;
            }
            return null;
        }

        /// <summary>
        /// アクティブなセッション数を取得する
        /// </summary>
        public int GetActiveSessionCount()
        {
            return _activeSessions.Count;
        }

        /// <summary>
        /// 指定アトラクションが建設済みかどうかを確認する
        /// </summary>
        public bool IsBuilt(string attractionId)
        {
            return _builtAttractions.Contains(attractionId);
        }

        /// <summary>
        /// 指定ゾーンの建設済みアトラクション一覧を取得する
        /// </summary>
        public List<InteractiveAttractionData> GetBuiltAttractionsByZone(ThemeZone zone)
        {
            List<InteractiveAttractionData> result = new List<InteractiveAttractionData>();

            foreach (string builtId in _builtAttractions)
            {
                InteractiveAttractionData data = GetAttractionData(builtId);
                if (data != null && data.Zone == zone)
                {
                    result.Add(data);
                }
            }

            WebGLOptimizer.LogVerbose(
                "InteractiveAttractionSystem: ゾーン「" + zone +
                "」の建設済みアトラクション取得。件数=" + result.Count);

            return result;
        }

        /// <summary>
        /// 全セッションを強制終了する（ゲーム終了時など）
        /// </summary>
        public void ForceEndAllSessions()
        {
            List<string> sessionIds = new List<string>(_activeSessions.Keys);

            foreach (string sessionId in sessionIds)
            {
                WebGLOptimizer.LogVerbose(
                    "InteractiveAttractionSystem: セッションを強制終了します。ID=" + sessionId);
                EndSession(sessionId);
            }

            WebGLOptimizer.LogVerbose(
                "InteractiveAttractionSystem: 全セッションの強制終了が完了しました。");
        }
    }
}
