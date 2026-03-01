// ============================================================
// ThemeParkGame - PvPMatchSystem
// リアルタイム対戦モード（パーク経営バトル）
// ============================================================

using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace ThemeParkGame.Core
{
    /// <summary>PvP対戦マッチの設定パラメータ</summary>
    [Serializable]
    public class PvPMatchConfig
    {
        public float MatchDurationMinutes = 30f;
        public float InitialFunds = 50000f;
        public int SharedVisitorPool = 200;
        public int MaxSabotagesPerMatch = 5;
        public bool EnableSabotage = true;
    }

    /// <summary>PvP対戦スコア情報。対戦中リアルタイム更新され最終順位判定に使用する。</summary>
    [Serializable]
    public class PvPScore
    {
        public string PlayerId;
        public string PlayerName;
        public float TotalRevenue;
        public float ParkRating;
        public int TotalVisitors;
        public float AverageHappiness;
        public int AttractionsBuilt;
        public float FinalScore;
    }

    /// <summary>妨害アクション定義（コスト・効果時間・クールダウン）</summary>
    [Serializable]
    public class SabotageDefinition
    {
        public PvPSabotageType Type;
        public string DisplayName;
        public string Description;
        public float Cost;
        public float DurationSeconds;
        public float CooldownSeconds;
    }

    /// <summary>実行中の妨害エフェクト</summary>
    [Serializable]
    public class ActiveSabotage
    {
        public PvPSabotageType Type;
        public float RemainingDuration;
        public string CasterPlayerId;
    }

    // ---- ネットワークメッセージ ----
    [Serializable] public class PvPSabotageMessage { public string sabotageType; public string casterId; public string targetId; }
    [Serializable] public class PvPScoreMessage { public string playerId; public float totalRevenue; public float parkRating; public int totalVisitors; public float averageHappiness; public int attractionsBuilt; public float finalScore; }
    [Serializable] public class PvPMatchEndMessage { public string winnerId; public string winnerName; public float winnerScore; public float loserScore; }

    /// <summary>
    /// PvP対戦モードの中央管理システム。
    /// 2人のプレイヤーが同一マップ上で競い合うパーク経営バトルを制御する。
    ///
    /// 【ゲームデザイン】
    /// ・CoopManagerのルーム機能でマッチング
    /// ・共有来場者プールを評価比率で配分し、経営成績をリアルタイム比較
    /// ・妨害アクション（サボタージュ）で相手を妨害可能
    /// ・FinalScore = TotalRevenue*0.3 + ParkRating*200*0.25
    ///             + TotalVisitors*50*0.25 + AverageHappiness*100*0.2
    /// </summary>
    public class PvPMatchSystem : MonoBehaviour
    {
        // ============================================================
        // シングルトン / 定数
        // ============================================================

        public static PvPMatchSystem Instance { get; private set; }

        private const float COUNTDOWN_SECONDS = 5f;
        private const float SCORE_SYNC_INTERVAL = 5f;
        private const float SABOTAGE_COOLDOWN = 120f;

        // スコア重み
        private const float W_REV = 0.3f;
        private const float W_RATE = 0.25f;
        private const float W_VIS = 0.25f;
        private const float W_HAP = 0.2f;

        // ============================================================
        // フィールド
        // ============================================================

        private PvPMatchConfig _config = new PvPMatchConfig();
        private PvPMatchState _matchState = PvPMatchState.Lobby;
        private PvPScore _myScore = new PvPScore();
        private PvPScore _opponentScore = new PvPScore();

        private float _matchTimer;
        private float _countdownTimer;
        private float _scoreSyncTimer;

        private readonly List<SabotageDefinition> _sabDefs = new List<SabotageDefinition>();
        private readonly List<ActiveSabotage> _activeSabs = new List<ActiveSabotage>();
        private readonly Dictionary<PvPSabotageType, float> _sabCooldowns = new Dictionary<PvPSabotageType, float>();
        private int _sabotagesUsed;
        private int _myVisitorShare;
        private int _opponentVisitorShare;

        // UI
        private GameObject _hudPanel;
        private Text _timerText, _myScoreText, _opponentScoreText;
        private GameObject _sabPanel, _resultPanel, _countdownPanel;
        private Text _countdownText, _resultTitleText, _resultMyText, _resultOpText, _resultSummary;
        private readonly Dictionary<PvPSabotageType, Button> _sabButtons = new Dictionary<PvPSabotageType, Button>();
        private readonly Dictionary<PvPSabotageType, Text> _sabCdTexts = new Dictionary<PvPSabotageType, Text>();

        // ============================================================
        // プロパティ
        // ============================================================

        public PvPMatchState MatchState => _matchState;
        public PvPMatchConfig Config => _config;
        public PvPScore MyScore => _myScore;
        public PvPScore OpponentScore => _opponentScore;
        public float RemainingSeconds => _matchTimer;
        public int MyVisitorShare => _myVisitorShare;
        public int OpponentVisitorShare => _opponentVisitorShare;
        public int SabotagesUsed => _sabotagesUsed;

        // ============================================================
        // イベント
        // ============================================================

        public event Action<PvPMatchState> OnMatchStateChanged;
        public event Action<PvPSabotageType> OnSabotageReceived;
        public event Action<string> OnMatchEnded;

        // ============================================================
        // ライフサイクル
        // ============================================================

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            InitSabotageDefs();
        }

        private void OnDestroy()
        {
            if (Instance == this) { UnsubCoop(); Instance = null; }
        }

        private void Update()
        {
            if (_matchState == PvPMatchState.Countdown) UpdateCountdown();
            else if (_matchState == PvPMatchState.InProgress) UpdateMatch();
        }

        // ============================================================
        // 妨害定義初期化
        // ============================================================

        /// <summary>4種の妨害アクションにコスト・効果時間・説明文を設定する</summary>
        private void InitSabotageDefs()
        {
            _sabDefs.Clear();
            _sabDefs.Add(new SabotageDefinition {
                Type = PvPSabotageType.AdBlitz, DisplayName = "広告攻勢",
                Description = "相手の来場者を15%奪う（60秒間）",
                Cost = 3000f, DurationSeconds = 60f, CooldownSeconds = SABOTAGE_COOLDOWN });
            _sabDefs.Add(new SabotageDefinition {
                Type = PvPSabotageType.StaffPoaching, DisplayName = "スタッフ引き抜き",
                Description = "相手のスタッフ効率を20%低下（90秒間）",
                Cost = 5000f, DurationSeconds = 90f, CooldownSeconds = SABOTAGE_COOLDOWN });
            _sabDefs.Add(new SabotageDefinition {
                Type = PvPSabotageType.PriceWar, DisplayName = "価格競争",
                Description = "相手の来場者期待値UP 満足度-10（60秒間）",
                Cost = 2000f, DurationSeconds = 60f, CooldownSeconds = SABOTAGE_COOLDOWN });
            _sabDefs.Add(new SabotageDefinition {
                Type = PvPSabotageType.EventSteal, DisplayName = "イベント横取り",
                Description = "相手の開催中イベントを1つキャンセル",
                Cost = 4000f, DurationSeconds = 0f, CooldownSeconds = SABOTAGE_COOLDOWN });

            foreach (var d in _sabDefs) _sabCooldowns[d.Type] = 0f;
        }

        // ============================================================
        // マッチ操作
        // ============================================================

        /// <summary>PvPマッチを開始する。CoopManager接続済みが前提。</summary>
        public void StartMatch(PvPMatchConfig config = null)
        {
            if (config != null) _config = config;
            var coop = CoopManager.Instance;
            if (coop == null || !coop.IsConnected)
            {
                GameManager.Instance?.ShowNotification(
                    "Co-op接続が必要です。先にルームに参加してください。", NotifLevel.Warning);
                return;
            }

            _myScore = new PvPScore { PlayerId = coop.CurrentRoomCode + "_self", PlayerName = "あなた" };
            _opponentScore = new PvPScore { PlayerId = coop.CurrentRoomCode + "_opponent", PlayerName = "対戦相手" };

            if (coop.Players != null && coop.Players.Count >= 2)
            {
                for (int i = 0; i < coop.Players.Count; i++)
                {
                    if (coop.Players[i].name != _myScore.PlayerName)
                    { _opponentScore.PlayerName = coop.Players[i].name; break; }
                }
            }

            _sabotagesUsed = 0;
            _activeSabs.Clear();
            foreach (var k in new List<PvPSabotageType>(_sabCooldowns.Keys)) _sabCooldowns[k] = 0f;
            _myVisitorShare = _config.SharedVisitorPool / 2;
            _opponentVisitorShare = _config.SharedVisitorPool - _myVisitorShare;

            SubCoop();
            SetState(PvPMatchState.Countdown);
            _countdownTimer = COUNTDOWN_SECONDS;

            BuildMatchHUD();
            ShowCountdown();

            WebGLOptimizer.LogVerbose("[PvP] マッチ開始 - カウントダウン開始");
            GameManager.Instance?.ShowNotification("PvP対戦を開始します！", NotifLevel.Success);
        }

        /// <summary>マッチを強制終了する（切断時等）</summary>
        public void ForceEndMatch()
        {
            if (_matchState == PvPMatchState.Finished || _matchState == PvPMatchState.Lobby) return;
            WebGLOptimizer.LogVerbose("[PvP] マッチを強制終了しました");
            GameManager.Instance?.ShowNotification("PvP対戦が中断されました", NotifLevel.Warning);
            EndMatch();
        }

        /// <summary>結果画面を閉じてロビーに戻る</summary>
        public void ReturnToLobby()
        {
            SetState(PvPMatchState.Lobby);
            DestroyMatchHUD();
            WebGLOptimizer.LogVerbose("[PvP] ロビーに戻りました");
        }

        // ============================================================
        // マッチ更新ループ
        // ============================================================

        /// <summary>5秒カウントダウン後にInProgressへ遷移</summary>
        private void UpdateCountdown()
        {
            _countdownTimer -= Time.unscaledDeltaTime;
            if (_countdownText != null)
            {
                int s = Mathf.CeilToInt(_countdownTimer);
                _countdownText.text = s > 0 ? s.ToString() : "START!";
            }
            if (_countdownTimer <= 0f)
            {
                _matchTimer = _config.MatchDurationMinutes * 60f;
                _scoreSyncTimer = 0f;
                SetState(PvPMatchState.InProgress);
                HideCountdown();
                ShowMatchHUD();
                WebGLOptimizer.LogVerbose("[PvP] 対戦開始！");
                GameManager.Instance?.ShowNotification("対戦開始！ 最高のパークを作りましょう！", NotifLevel.Success);
            }
        }

        /// <summary>対戦中メインループ（タイマー・スコア・妨害・来場者配分）</summary>
        private void UpdateMatch()
        {
            _matchTimer -= Time.unscaledDeltaTime;
            if (_matchTimer <= 0f) { _matchTimer = 0f; EndMatch(); return; }

            UpdateSabCooldowns();
            UpdateActiveSabs();
            RecalcVisitorDistribution();
            UpdateMyScore();

            _scoreSyncTimer += Time.unscaledDeltaTime;
            if (_scoreSyncTimer >= SCORE_SYNC_INTERVAL)
            { _scoreSyncTimer = 0f; SyncScore(); }

            RefreshMatchHUD();
        }

        // ============================================================
        // スコア計算
        // ============================================================

        /// <summary>ゲーム状態から自分のスコアを収集・更新する</summary>
        private void UpdateMyScore()
        {
            var gm = GameManager.Instance;
            if (gm == null) return;

            if (gm.EconomyManager != null) _myScore.TotalRevenue = gm.EconomyManager.TotalRevenueEarned;
            if (gm.ParkManager != null)
            {
                _myScore.ParkRating = gm.ParkManager.GetOverallRating();
                if (gm.ParkManager.Stats != null) _myScore.AttractionsBuilt = gm.ParkManager.Stats.TotalAttractions;
            }
            if (gm.VisitorManager != null)
            {
                _myScore.TotalVisitors = gm.VisitorManager.TotalVisitorsToday;
                _myScore.AverageHappiness = gm.VisitorManager.AverageHappiness;
            }
            _myScore.FinalScore = CalcFinalScore(_myScore);
        }

        /// <summary>加重合成スコアを計算する</summary>
        private float CalcFinalScore(PvPScore sc)
        {
            return sc.TotalRevenue * W_REV
                 + sc.ParkRating * 200f * W_RATE
                 + sc.TotalVisitors * 50f * W_VIS
                 + sc.AverageHappiness * 100f * W_HAP;
        }

        // ============================================================
        // 来場者配分
        // ============================================================

        /// <summary>パーク評価比率で共有来場者プールを配分する</summary>
        private void RecalcVisitorDistribution()
        {
            float myR = Mathf.Max(_myScore.ParkRating, 1f);
            float opR = Mathf.Max(_opponentScore.ParkRating, 1f);
            float ratio = myR / (myR + opR);

            // AdBlitz被害: 自分の来場者を15%減少
            for (int i = 0; i < _activeSabs.Count; i++)
            {
                if (_activeSabs[i].Type == PvPSabotageType.AdBlitz)
                { ratio = Mathf.Max(0.1f, ratio - 0.15f); break; }
            }

            _myVisitorShare = Mathf.RoundToInt(_config.SharedVisitorPool * ratio);
            _opponentVisitorShare = _config.SharedVisitorPool - _myVisitorShare;
        }

        // ============================================================
        // 妨害システム
        // ============================================================

        /// <summary>妨害アクションを実行する</summary>
        public bool ExecuteSabotage(PvPSabotageType type)
        {
            if (_matchState != PvPMatchState.InProgress) return false;
            if (!_config.EnableSabotage)
            { GameManager.Instance?.ShowNotification("このマッチでは妨害が無効です", NotifLevel.Warning); return false; }
            if (_sabotagesUsed >= _config.MaxSabotagesPerMatch)
            { GameManager.Instance?.ShowNotification("妨害回数の上限に達しました", NotifLevel.Warning); return false; }
            if (_sabCooldowns.ContainsKey(type) && _sabCooldowns[type] > 0f)
            {
                GameManager.Instance?.ShowNotification(
                    $"クールダウン中です（残り{Mathf.CeilToInt(_sabCooldowns[type])}秒）", NotifLevel.Warning);
                return false;
            }

            SabotageDefinition def = FindSabDef(type);
            if (def == null) return false;

            var economy = GameManager.Instance?.EconomyManager;
            if (economy == null) return false;
            if (!economy.PayExpense(def.Cost, Economy.ExpenseCategory.Other))
            {
                GameManager.Instance?.ShowNotification(
                    $"資金が不足しています（必要: ${def.Cost:N0}）", NotifLevel.Warning);
                return false;
            }

            _sabCooldowns[type] = def.CooldownSeconds;
            _sabotagesUsed++;

            var msg = new PvPSabotageMessage
            { sabotageType = type.ToString(), casterId = _myScore.PlayerId, targetId = _opponentScore.PlayerId };
            CoopManager.Instance?.SendAction("pvp_sabotage", JsonUtility.ToJson(msg));

            WebGLOptimizer.LogVerbose($"[PvP] 妨害実行: {def.DisplayName} (コスト: ${def.Cost:N0})");
            GameManager.Instance?.ShowNotification($"{def.DisplayName}を発動しました！", NotifLevel.Success);
            return true;
        }

        /// <summary>相手から受信した妨害を自分に適用する</summary>
        private void ApplySabotageEffect(PvPSabotageType type)
        {
            SabotageDefinition def = FindSabDef(type);
            if (def == null) return;

            if (type == PvPSabotageType.EventSteal)
            {
                // 即時: アクティブイベントを1つキャンセル
                var es = GameManager.Instance?.ParkEventSystem;
                if (es != null && es.ActiveEventCount > 0)
                {
                    var evts = es.CurrentEvents;
                    if (evts != null && evts.Count > 0)
                    {
                        string eName = evts[0].Data != null ? evts[0].Data.DisplayName : "不明";
                        GameManager.Instance?.ShowNotification(
                            $"イベント「{eName}」が横取りされました！", NotifLevel.Warning);
                        WebGLOptimizer.LogVerbose($"[PvP] EventSteal: 「{eName}」キャンセル");
                    }
                }
            }
            else
            {
                _activeSabs.Add(new ActiveSabotage
                { Type = type, RemainingDuration = def.DurationSeconds, CasterPlayerId = _opponentScore.PlayerId });
            }

            OnSabotageReceived?.Invoke(type);
            GameManager.Instance?.ShowNotification($"対戦相手が{def.DisplayName}を発動しました！", NotifLevel.Warning);
            WebGLOptimizer.LogVerbose($"[PvP] 妨害を受けた: {def.DisplayName}");
        }

        private SabotageDefinition FindSabDef(PvPSabotageType type)
        {
            for (int i = 0; i < _sabDefs.Count; i++)
                if (_sabDefs[i].Type == type) return _sabDefs[i];
            return null;
        }

        private void UpdateSabCooldowns()
        {
            float dt = Time.unscaledDeltaTime;
            var keys = new List<PvPSabotageType>(_sabCooldowns.Keys);
            for (int i = 0; i < keys.Count; i++)
                if (_sabCooldowns[keys[i]] > 0f)
                    _sabCooldowns[keys[i]] = Mathf.Max(0f, _sabCooldowns[keys[i]] - dt);
        }

        private void UpdateActiveSabs()
        {
            float dt = Time.unscaledDeltaTime;
            for (int i = _activeSabs.Count - 1; i >= 0; i--)
            {
                _activeSabs[i].RemainingDuration -= dt;
                if (_activeSabs[i].RemainingDuration <= 0f)
                {
                    WebGLOptimizer.LogVerbose($"[PvP] 妨害エフェクト終了: {_activeSabs[i].Type}");
                    _activeSabs.RemoveAt(i);
                }
            }
        }

        /// <summary>指定妨害がアクティブかどうかを返す</summary>
        public bool IsSabotageActive(PvPSabotageType type)
        {
            for (int i = 0; i < _activeSabs.Count; i++)
                if (_activeSabs[i].Type == type) return true;
            return false;
        }

        /// <summary>StaffPoaching妨害によるスタッフ効率低下率（0.0～1.0）</summary>
        public float GetStaffEfficiencyPenalty()
            => IsSabotageActive(PvPSabotageType.StaffPoaching) ? 0.2f : 0f;

        /// <summary>PriceWar妨害による来場者満足度ペナルティ</summary>
        public float GetSatisfactionPenalty()
            => IsSabotageActive(PvPSabotageType.PriceWar) ? 10f : 0f;

        // ============================================================
        // ネットワーク通信
        // ============================================================

        private void SubCoop()
        {
            var c = CoopManager.Instance;
            if (c != null) c.OnActionReceived += HandleCoopAction;
        }

        private void UnsubCoop()
        {
            var c = CoopManager.Instance;
            if (c != null) c.OnActionReceived -= HandleCoopAction;
        }

        /// <summary>CoopManagerから受信したアクションをPvP用に振り分ける</summary>
        private void HandleCoopAction(CoopManager.CoopAction action)
        {
            if (action == null || string.IsNullOrEmpty(action.type)) return;
            switch (action.type)
            {
                case "pvp_sabotage": HandleSabotageMsg(action.data); break;
                case "pvp_score_update": HandleScoreMsg(action.data); break;
                case "pvp_match_end": HandleMatchEnd(action.data); break;
            }
        }

        private void HandleSabotageMsg(string json)
        {
            if (string.IsNullOrEmpty(json)) return;
            try
            {
                var msg = JsonUtility.FromJson<PvPSabotageMessage>(json);
                if (msg == null) return;
                var type = (PvPSabotageType)Enum.Parse(typeof(PvPSabotageType), msg.sabotageType);
                ApplySabotageEffect(type);
            }
            catch (Exception e)
            { WebGLOptimizer.LogVerbose($"[PvP] 妨害メッセージのパースに失敗: {e.Message}"); }
        }

        private void HandleScoreMsg(string json)
        {
            if (string.IsNullOrEmpty(json)) return;
            try
            {
                var m = JsonUtility.FromJson<PvPScoreMessage>(json);
                if (m == null) return;
                _opponentScore.TotalRevenue = m.totalRevenue;
                _opponentScore.ParkRating = m.parkRating;
                _opponentScore.TotalVisitors = m.totalVisitors;
                _opponentScore.AverageHappiness = m.averageHappiness;
                _opponentScore.AttractionsBuilt = m.attractionsBuilt;
                _opponentScore.FinalScore = m.finalScore;
            }
            catch (Exception e)
            { WebGLOptimizer.LogVerbose($"[PvP] スコアメッセージのパースに失敗: {e.Message}"); }
        }

        private void HandleMatchEnd(string json)
        {
            if (_matchState == PvPMatchState.Finished) return;
            WebGLOptimizer.LogVerbose("[PvP] 対戦相手からマッチ終了通知を受信");
            EndMatch();
        }

        /// <summary>自分のスコアを対戦相手へ送信する</summary>
        private void SyncScore()
        {
            var coop = CoopManager.Instance;
            if (coop == null || !coop.IsConnected) return;
            var msg = new PvPScoreMessage
            {
                playerId = _myScore.PlayerId, totalRevenue = _myScore.TotalRevenue,
                parkRating = _myScore.ParkRating, totalVisitors = _myScore.TotalVisitors,
                averageHappiness = _myScore.AverageHappiness, attractionsBuilt = _myScore.AttractionsBuilt,
                finalScore = _myScore.FinalScore
            };
            coop.SendAction("pvp_score_update", JsonUtility.ToJson(msg));
        }

        // ============================================================
        // マッチ終了
        // ============================================================

        /// <summary>マッチを終了し結果画面を表示・リーダーボード送信</summary>
        private void EndMatch()
        {
            if (_matchState == PvPMatchState.Finished) return;

            UpdateMyScore();
            _myScore.FinalScore = CalcFinalScore(_myScore);
            _opponentScore.FinalScore = CalcFinalScore(_opponentScore);

            bool win = _myScore.FinalScore >= _opponentScore.FinalScore;
            string winnerId = win ? _myScore.PlayerId : _opponentScore.PlayerId;
            string winnerName = win ? _myScore.PlayerName : _opponentScore.PlayerName;

            var endMsg = new PvPMatchEndMessage
            {
                winnerId = winnerId, winnerName = winnerName,
                winnerScore = win ? _myScore.FinalScore : _opponentScore.FinalScore,
                loserScore = win ? _opponentScore.FinalScore : _myScore.FinalScore
            };
            CoopManager.Instance?.SendAction("pvp_match_end", JsonUtility.ToJson(endMsg));

            SetState(PvPMatchState.Finished);
            UnsubCoop();
            LeaderboardManager.Instance?.SubmitScore();
            ShowResultScreen(win);
            OnMatchEnded?.Invoke(winnerId);

            string resultMsg = win ? "勝利おめでとうございます！" : "惜しくも敗北...";
            GameManager.Instance?.ShowNotification(resultMsg, win ? NotifLevel.Success : NotifLevel.Info);
            WebGLOptimizer.LogVerbose($"[PvP] マッチ終了 - 勝者: {winnerName} ({endMsg.winnerScore:N0} vs {endMsg.loserScore:N0})");
        }

        private void SetState(PvPMatchState s)
        {
            _matchState = s;
            OnMatchStateChanged?.Invoke(s);
            WebGLOptimizer.LogVerbose($"[PvP] 状態遷移: {s}");
        }

        // ============================================================
        // UI構築
        // ============================================================

        private static readonly Color BgDark = new Color(0.06f, 0.09f, 0.16f, 0.92f);
        private static readonly Color ColGold = new Color(0.95f, 0.85f, 0.35f);
        private static readonly Color ColGreen = new Color(0.4f, 0.9f, 0.4f);
        private static readonly Color ColRed = new Color(0.95f, 0.35f, 0.35f);
        private static readonly Color ColCyan = new Color(0.4f, 0.85f, 0.95f);
        private static readonly Color ColGray = new Color(0.55f, 0.55f, 0.65f);
        private static readonly Color BtnSab = new Color(0.5f, 0.2f, 0.15f);
        private static readonly Color BtnSabOff = new Color(0.25f, 0.25f, 0.3f);
        private static readonly Color BtnGray = new Color(0.3f, 0.3f, 0.4f);

        /// <summary>マッチHUDオーバーレイを構築する（トップバー・妨害パネル・結果画面）</summary>
        private void BuildMatchHUD()
        {
            if (_hudPanel != null) return;
            var canvas = CanvasCache.Get();
            if (canvas == null) return;

            _hudPanel = new GameObject("PvPMatchHUD");
            _hudPanel.transform.SetParent(canvas.transform, false);
            var rt = _hudPanel.AddComponent<RectTransform>();
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;

            BuildTopBar();
            BuildSabotagePanel();
            BuildCountdownPanel();
            BuildResultPanel();
            _hudPanel.SetActive(true);
        }

        private void BuildTopBar()
        {
            var bar = MakeChild(_hudPanel, "TopBar", new Vector2(0.15f, 0.92f), new Vector2(0.85f, 1f));
            bar.AddComponent<Image>().color = BgDark;

            _timerText = MkLabel(bar.transform, "Timer", new Vector2(0.4f, 0f), new Vector2(0.6f, 1f),
                "30:00", 20, FontStyle.Bold, ColGold, TextAnchor.MiddleCenter);
            _myScoreText = MkLabel(bar.transform, "MyScore", new Vector2(0.02f, 0f), new Vector2(0.38f, 1f),
                "あなた: 0", 16, FontStyle.Bold, ColCyan, TextAnchor.MiddleLeft);
            _opponentScoreText = MkLabel(bar.transform, "OpScore", new Vector2(0.62f, 0f), new Vector2(0.98f, 1f),
                "対戦相手: 0", 16, FontStyle.Bold, ColRed, TextAnchor.MiddleRight);
        }

        private void BuildSabotagePanel()
        {
            _sabPanel = MakeChild(_hudPanel, "SabPanel", new Vector2(0.78f, 0.4f), new Vector2(0.98f, 0.88f));
            _sabPanel.AddComponent<Image>().color = BgDark;

            MkLabel(_sabPanel.transform, "SabTitle", new Vector2(0.05f, 0.88f), new Vector2(0.95f, 1f),
                "妨害アクション", 14, FontStyle.Bold, ColGold, TextAnchor.MiddleCenter);
            MkLabel(_sabPanel.transform, "SabCount", new Vector2(0.05f, 0.78f), new Vector2(0.95f, 0.88f),
                $"残り: {_config.MaxSabotagesPerMatch}/{_config.MaxSabotagesPerMatch}",
                11, FontStyle.Normal, ColGray, TextAnchor.MiddleCenter);

            float h = 0.17f, gap = 0.02f, y = 0.75f;
            for (int i = 0; i < _sabDefs.Count; i++)
            {
                float yMax = y - i * (h + gap); float yMin = yMax - h;
                MakeSabBtn(_sabDefs[i], new Vector2(0.05f, yMin), new Vector2(0.95f, yMax));
            }
            _sabPanel.SetActive(false);
        }

        private void MakeSabBtn(SabotageDefinition def, Vector2 aMin, Vector2 aMax)
        {
            var obj = MakeChild(_sabPanel, $"Sab_{def.Type}", aMin, aMax);
            var img = obj.AddComponent<Image>(); img.color = BtnSab;
            var btn = obj.AddComponent<Button>(); btn.targetGraphic = img;

            MkLabel(obj.transform, "Lbl", new Vector2(0f, 0.3f), new Vector2(1f, 1f),
                $"{def.DisplayName}\n${def.Cost:N0}", 11, FontStyle.Normal, Color.white, TextAnchor.MiddleCenter);
            var cd = MkLabel(obj.transform, "CD", new Vector2(0f, 0f), new Vector2(1f, 0.3f),
                "準備完了", 10, FontStyle.Normal, ColGreen, TextAnchor.MiddleCenter);

            PvPSabotageType t = def.Type;
            btn.onClick.AddListener(() => ExecuteSabotage(t));
            _sabButtons[def.Type] = btn;
            _sabCdTexts[def.Type] = cd;
        }

        private void BuildCountdownPanel()
        {
            _countdownPanel = MakeChild(_hudPanel, "Countdown", new Vector2(0.3f, 0.3f), new Vector2(0.7f, 0.7f));
            _countdownPanel.AddComponent<Image>().color = BgDark;
            _countdownText = MkLabel(_countdownPanel.transform, "Num", new Vector2(0.1f, 0.1f), new Vector2(0.9f, 0.9f),
                "5", 72, FontStyle.Bold, ColGold, TextAnchor.MiddleCenter);
            _countdownPanel.SetActive(false);
        }

        private void BuildResultPanel()
        {
            _resultPanel = MakeChild(_hudPanel, "Result", new Vector2(0.15f, 0.1f), new Vector2(0.85f, 0.9f));
            _resultPanel.AddComponent<Image>().color = BgDark;

            _resultTitleText = MkLabel(_resultPanel.transform, "Title",
                new Vector2(0.05f, 0.85f), new Vector2(0.95f, 0.98f),
                "対戦結果", 28, FontStyle.Bold, ColGold, TextAnchor.MiddleCenter);

            MkLabel(_resultPanel.transform, "MyH", new Vector2(0.05f, 0.72f), new Vector2(0.48f, 0.82f),
                "あなた", 18, FontStyle.Bold, ColCyan, TextAnchor.MiddleCenter);
            _resultMyText = MkLabel(_resultPanel.transform, "MyD",
                new Vector2(0.05f, 0.3f), new Vector2(0.48f, 0.72f),
                "", 14, FontStyle.Normal, Color.white, TextAnchor.UpperLeft);

            MkLabel(_resultPanel.transform, "OpH", new Vector2(0.52f, 0.72f), new Vector2(0.95f, 0.82f),
                "対戦相手", 18, FontStyle.Bold, ColRed, TextAnchor.MiddleCenter);
            _resultOpText = MkLabel(_resultPanel.transform, "OpD",
                new Vector2(0.52f, 0.3f), new Vector2(0.95f, 0.72f),
                "", 14, FontStyle.Normal, Color.white, TextAnchor.UpperLeft);

            _resultSummary = MkLabel(_resultPanel.transform, "Sum",
                new Vector2(0.05f, 0.15f), new Vector2(0.95f, 0.28f),
                "", 16, FontStyle.Bold, ColGold, TextAnchor.MiddleCenter);

            MkButton(_resultPanel.transform, "Close",
                new Vector2(0.35f, 0.03f), new Vector2(0.65f, 0.12f),
                "ロビーに戻る", BtnGray, () => ReturnToLobby());

            _resultPanel.SetActive(false);
        }

        // ============================================================
        // UI表示切替・更新
        // ============================================================

        private void ShowCountdown()
        {
            if (_countdownPanel != null) _countdownPanel.SetActive(true);
            if (_sabPanel != null) _sabPanel.SetActive(false);
            if (_resultPanel != null) _resultPanel.SetActive(false);
        }

        private void HideCountdown() { if (_countdownPanel != null) _countdownPanel.SetActive(false); }

        private void ShowMatchHUD()
        {
            if (_sabPanel != null && _config.EnableSabotage) _sabPanel.SetActive(true);
        }

        /// <summary>結果画面にスコア内訳を表示する</summary>
        private void ShowResultScreen(bool win)
        {
            if (_sabPanel != null) _sabPanel.SetActive(false);
            if (_resultPanel == null) return;
            _resultPanel.SetActive(true);

            if (_resultTitleText != null)
            { _resultTitleText.text = win ? "勝利！" : "敗北..."; _resultTitleText.color = win ? ColGold : ColGray; }
            if (_resultMyText != null) _resultMyText.text = FmtBreakdown(_myScore);
            if (_resultOpText != null) _resultOpText.text = FmtBreakdown(_opponentScore);
            if (_resultSummary != null)
                _resultSummary.text = $"最終スコア: {_myScore.FinalScore:N0} vs {_opponentScore.FinalScore:N0}";
        }

        private string FmtBreakdown(PvPScore sc)
        {
            return $"収益: ${sc.TotalRevenue:N0}\n" +
                   $"  x{W_REV} = {sc.TotalRevenue * W_REV:N0}\n\n" +
                   $"パーク評価: {sc.ParkRating:F1}\n" +
                   $"  x200x{W_RATE} = {sc.ParkRating * 200f * W_RATE:N0}\n\n" +
                   $"来場者: {sc.TotalVisitors}\n" +
                   $"  x50x{W_VIS} = {sc.TotalVisitors * 50f * W_VIS:N0}\n\n" +
                   $"幸福度: {sc.AverageHappiness:F1}\n" +
                   $"  x100x{W_HAP} = {sc.AverageHappiness * 100f * W_HAP:N0}\n\n" +
                   $"アトラクション数: {sc.AttractionsBuilt}\n" +
                   $"合計: {sc.FinalScore:N0}";
        }

        /// <summary>タイマー・スコア・妨害クールダウンを最新化</summary>
        private void RefreshMatchHUD()
        {
            if (_timerText != null)
            {
                int tot = Mathf.CeilToInt(_matchTimer);
                _timerText.text = $"{tot / 60:D2}:{tot % 60:D2}";
                _timerText.color = tot <= 60 ? ColRed : ColGold;
            }
            if (_myScoreText != null)
                _myScoreText.text = $"{_myScore.PlayerName}: {_myScore.FinalScore:N0}";
            if (_opponentScoreText != null)
                _opponentScoreText.text = $"{_opponentScore.PlayerName}: {_opponentScore.FinalScore:N0}";

            RefreshSabButtons();
        }

        private void RefreshSabButtons()
        {
            foreach (var def in _sabDefs)
            {
                if (!_sabButtons.ContainsKey(def.Type)) continue;
                var btn = _sabButtons[def.Type];
                float cd = _sabCooldowns.ContainsKey(def.Type) ? _sabCooldowns[def.Type] : 0f;
                bool ok = cd <= 0f && _sabotagesUsed < _config.MaxSabotagesPerMatch
                       && _matchState == PvPMatchState.InProgress;
                btn.interactable = ok;

                var img = btn.GetComponent<Image>();
                if (img != null) img.color = ok ? BtnSab : BtnSabOff;

                if (_sabCdTexts.ContainsKey(def.Type))
                {
                    var t = _sabCdTexts[def.Type];
                    if (cd > 0f) { t.text = $"CD: {Mathf.CeilToInt(cd)}s"; t.color = ColRed; }
                    else if (_sabotagesUsed >= _config.MaxSabotagesPerMatch) { t.text = "上限到達"; t.color = ColGray; }
                    else { t.text = "準備完了"; t.color = ColGreen; }
                }
            }
        }

        private void DestroyMatchHUD()
        {
            if (_hudPanel != null) { Destroy(_hudPanel); _hudPanel = null; }
            _timerText = _myScoreText = _opponentScoreText = null;
            _sabPanel = _resultPanel = _countdownPanel = null;
            _countdownText = _resultTitleText = _resultMyText = _resultOpText = _resultSummary = null;
            _sabButtons.Clear();
            _sabCdTexts.Clear();
        }

        // ============================================================
        // UIヘルパー
        // ============================================================

        private GameObject MakeChild(GameObject parent, string name, Vector2 aMin, Vector2 aMax)
        {
            var obj = new GameObject(name);
            obj.transform.SetParent(parent.transform, false);
            var r = obj.AddComponent<RectTransform>();
            r.anchorMin = aMin; r.anchorMax = aMax;
            r.offsetMin = Vector2.zero; r.offsetMax = Vector2.zero;
            return obj;
        }

        private Text MkLabel(Transform parent, string name,
            Vector2 aMin, Vector2 aMax, string content,
            int fontSize, FontStyle style, Color color, TextAnchor anchor)
        {
            var obj = new GameObject(name);
            obj.transform.SetParent(parent, false);
            var r = obj.AddComponent<RectTransform>();
            r.anchorMin = aMin; r.anchorMax = aMax;
            r.offsetMin = new Vector2(4f, 0f); r.offsetMax = new Vector2(-4f, 0f);
            var t = obj.AddComponent<Text>();
            t.text = content; t.font = FontManager.Regular;
            t.fontSize = fontSize; t.fontStyle = style;
            t.color = color; t.alignment = anchor;
            return t;
        }

        private void MkButton(Transform parent, string name,
            Vector2 aMin, Vector2 aMax, string label,
            Color bgColor, UnityEngine.Events.UnityAction onClick)
        {
            var obj = new GameObject(name);
            obj.transform.SetParent(parent, false);
            var r = obj.AddComponent<RectTransform>();
            r.anchorMin = aMin; r.anchorMax = aMax;
            r.offsetMin = Vector2.zero; r.offsetMax = Vector2.zero;
            var img = obj.AddComponent<Image>(); img.color = bgColor;
            var btn = obj.AddComponent<Button>(); btn.targetGraphic = img;
            btn.onClick.AddListener(onClick);

            var tObj = new GameObject("Text");
            tObj.transform.SetParent(obj.transform, false);
            var tr = tObj.AddComponent<RectTransform>();
            tr.anchorMin = Vector2.zero; tr.anchorMax = Vector2.one;
            tr.offsetMin = Vector2.zero; tr.offsetMax = Vector2.zero;
            var t = tObj.AddComponent<Text>();
            t.text = label; t.font = FontManager.Regular;
            t.fontSize = 14; t.color = Color.white;
            t.alignment = TextAnchor.MiddleCenter;
        }
    }
}
