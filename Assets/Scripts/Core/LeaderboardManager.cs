// ============================================================
// ThemeParkGame - LeaderboardManager
// オンラインリーダーボード（Cloudflare Worker API連携）
// ============================================================

using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;

namespace ThemeParkGame.Core
{
    /// <summary>
    /// オンラインリーダーボードの送受信とUI表示を管理する。
    /// Cloudflare Worker APIと通信し、スコアの登録・取得を行う。
    /// </summary>
    public class LeaderboardManager : MonoBehaviour
    {
        public static LeaderboardManager Instance { get; private set; }

        private const string API_BASE = "https://themeparkgame-api.common-gifted-tokyo.workers.dev";
        private const string PREF_PLAYER_ID = "LeaderboardPlayerId";
        private const string PREF_PLAYER_NAME = "LeaderboardPlayerName";

        // UI
        private Canvas _lbCanvas;
        private GameObject _lbPanel;
        private Text _lbTitle;
        private Text _lbContent;
        private Text _lbStatus;
        private Button _closeBtn;
        private Button _submitBtn;
        private InputField _nameInput;
        private bool _isSubmitting;

        // データ
        private List<LeaderboardEntry> _entries = new List<LeaderboardEntry>();

        public string PlayerId
        {
            get
            {
                string id = PlayerPrefs.GetString(PREF_PLAYER_ID, "");
                if (string.IsNullOrEmpty(id))
                {
                    id = Guid.NewGuid().ToString("N").Substring(0, 16);
                    PlayerPrefs.SetString(PREF_PLAYER_ID, id);
                    PlayerPrefs.Save();
                }
                return id;
            }
        }

        public string PlayerName
        {
            get => PlayerPrefs.GetString(PREF_PLAYER_NAME, "Player");
            set
            {
                PlayerPrefs.SetString(PREF_PLAYER_NAME, value);
                PlayerPrefs.Save();
            }
        }

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        // ================================================================
        // API通信
        // ================================================================

        /// <summary>スコアをリーダーボードに送信する</summary>
        public void SubmitScore(Action<bool, int> onComplete = null)
        {
            if (_isSubmitting) return;
            var gm = GameManager.Instance;
            if (gm == null) { onComplete?.Invoke(false, -1); return; }

            float score = CalculateScore(gm);
            var data = new ScoreSubmitData
            {
                playerName = PlayerName,
                score = score,
                difficulty = gm.CurrentDifficulty.ToString(),
                parkRating = gm.ParkManager?.Rating?.OverallRating ?? 0f,
                totalVisitors = gm.ParkManager?.Stats?.TotalVisitorsEver ?? 0,
                playTimeMinutes = gm.TimeManager != null
                    ? (gm.TimeManager.CurrentYear - 1) * 360 + (gm.TimeManager.CurrentMonth - 1) * 30 + gm.TimeManager.CurrentDay
                    : 0
            };

            StartCoroutine(PostScoreCoroutine(data, onComplete));
        }

        private float CalculateScore(GameManager gm)
        {
            float money = gm.EconomyManager?.CurrentMoney ?? 0f;
            float rating = gm.ParkManager?.Rating?.OverallRating ?? 0f;
            int visitors = gm.ParkManager?.Stats?.TotalVisitorsEver ?? 0;
            return money + (rating * 100f) + (visitors * 10f);
        }

        private IEnumerator PostScoreCoroutine(ScoreSubmitData data, Action<bool, int> onComplete)
        {
            _isSubmitting = true;
            if (_lbStatus != null) _lbStatus.text = "送信中...";

            string json = JsonUtility.ToJson(data);
            using (var req = new UnityWebRequest($"{API_BASE}/api/leaderboard/submit", "POST"))
            {
                req.uploadHandler = new UploadHandlerRaw(System.Text.Encoding.UTF8.GetBytes(json));
                req.downloadHandler = new DownloadHandlerBuffer();
                req.SetRequestHeader("Content-Type", "application/json");
                req.timeout = 10;

                yield return req.SendWebRequest();

                _isSubmitting = false;

                if (req.result == UnityWebRequest.Result.Success)
                {
                    var resp = JsonUtility.FromJson<ScoreSubmitResponse>(req.downloadHandler.text);
                    if (_lbStatus != null) _lbStatus.text = $"送信完了！ ランキング: {resp.rank}位";
                    onComplete?.Invoke(true, resp.rank);
                    // 送信後にランキングを再取得
                    StartCoroutine(FetchLeaderboardCoroutine());
                }
                else
                {
                    if (_lbStatus != null) _lbStatus.text = "送信失敗...";
                    onComplete?.Invoke(false, -1);
                    WebGLOptimizer.LogVerbose($"[Leaderboard] Submit failed: {req.error}");
                }
            }
        }

        /// <summary>リーダーボードを取得して表示する</summary>
        public void FetchAndShowLeaderboard()
        {
            ShowUI();
            StartCoroutine(FetchLeaderboardCoroutine());
        }

        private IEnumerator FetchLeaderboardCoroutine()
        {
            if (_lbStatus != null) _lbStatus.text = "読み込み中...";

            using (var req = UnityWebRequest.Get($"{API_BASE}/api/leaderboard/top?limit=20"))
            {
                req.timeout = 10;
                yield return req.SendWebRequest();

                if (req.result == UnityWebRequest.Result.Success)
                {
                    var resp = JsonUtility.FromJson<LeaderboardResponse>(req.downloadHandler.text);
                    _entries = resp.entries ?? new List<LeaderboardEntry>();
                    RefreshUI();
                    if (_lbStatus != null) _lbStatus.text = $"全{resp.total}件";
                }
                else
                {
                    if (_lbStatus != null) _lbStatus.text = "取得失敗";
                    WebGLOptimizer.LogVerbose($"[Leaderboard] Fetch failed: {req.error}");
                }
            }
        }

        // ================================================================
        // UI構築
        // ================================================================

        public void ShowUI()
        {
            if (_lbPanel == null) BuildUI();
            _lbPanel.SetActive(true);
        }

        public void HideUI()
        {
            if (_lbPanel != null) _lbPanel.SetActive(false);
        }

        private void BuildUI()
        {
            // Canvas
            var canvasGo = new GameObject("LeaderboardCanvas");
            canvasGo.transform.SetParent(transform, false);
            _lbCanvas = canvasGo.AddComponent<Canvas>();
            _lbCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _lbCanvas.sortingOrder = 90;
            var scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            canvasGo.AddComponent<GraphicRaycaster>();

            // パネル
            _lbPanel = new GameObject("LBPanel");
            _lbPanel.transform.SetParent(canvasGo.transform, false);
            var panelRt = _lbPanel.AddComponent<RectTransform>();
            panelRt.anchorMin = panelRt.anchorMax = new Vector2(0.5f, 0.5f);
            panelRt.sizeDelta = new Vector2(500f, 600f);
            var bg = _lbPanel.AddComponent<Image>();
            bg.color = new Color(0.04f, 0.06f, 0.14f, 0.96f);
            bg.raycastTarget = true;

            // タイトル
            _lbTitle = MakeText(panelRt, "Title", "オンラインランキング", 24,
                new Color(0.95f, 0.88f, 0.45f), FontStyle.Bold, TextAnchor.MiddleCenter,
                new Vector2(0f, 260f), new Vector2(460f, 40f));

            // 名前入力
            var nameRow = new GameObject("NameRow");
            nameRow.transform.SetParent(panelRt, false);
            var nrRt = nameRow.AddComponent<RectTransform>();
            nrRt.anchorMin = nrRt.anchorMax = new Vector2(0.5f, 0.5f);
            nrRt.anchoredPosition = new Vector2(0f, 218f);
            nrRt.sizeDelta = new Vector2(460f, 30f);

            MakeText(nrRt, "NameLabel", "名前:", 14,
                new Color(0.7f, 0.75f, 0.85f), FontStyle.Normal, TextAnchor.MiddleLeft,
                new Vector2(-180f, 0f), new Vector2(50f, 28f));

            // InputField
            var inputGo = new GameObject("NameInput");
            inputGo.transform.SetParent(nrRt, false);
            var inputRt = inputGo.AddComponent<RectTransform>();
            inputRt.anchorMin = inputRt.anchorMax = new Vector2(0.5f, 0.5f);
            inputRt.anchoredPosition = new Vector2(-40f, 0f);
            inputRt.sizeDelta = new Vector2(200f, 28f);
            var inputBg = inputGo.AddComponent<Image>();
            inputBg.color = new Color(0.1f, 0.12f, 0.22f);
            _nameInput = inputGo.AddComponent<InputField>();
            _nameInput.characterLimit = 20;
            _nameInput.text = PlayerName;
            var inputTextGo = new GameObject("InputText");
            inputTextGo.transform.SetParent(inputGo.transform, false);
            var inputText = inputTextGo.AddComponent<Text>();
            inputText.font = GetBuiltinFont();
            inputText.fontSize = 14;
            inputText.color = Color.white;
            inputText.alignment = TextAnchor.MiddleLeft;
            var itRt = inputTextGo.GetComponent<RectTransform>();
            itRt.anchorMin = Vector2.zero; itRt.anchorMax = Vector2.one;
            itRt.offsetMin = new Vector2(4f, 0f); itRt.offsetMax = new Vector2(-4f, 0f);
            _nameInput.textComponent = inputText;
            _nameInput.onEndEdit.AddListener(s => { if (!string.IsNullOrEmpty(s)) PlayerName = s; });

            // Submit Button
            _submitBtn = MakeButton(nrRt, "SubmitBtn", "スコア送信",
                new Color(0.2f, 0.55f, 0.35f), new Vector2(150f, 0f), new Vector2(130f, 28f),
                () =>
                {
                    if (!string.IsNullOrEmpty(_nameInput.text)) PlayerName = _nameInput.text;
                    SubmitScore();
                });

            // ランキング表示エリア
            _lbContent = MakeText(panelRt, "Content", "読み込み中...", 14,
                new Color(0.85f, 0.88f, 0.92f), FontStyle.Normal, TextAnchor.UpperLeft,
                new Vector2(0f, -10f), new Vector2(440f, 380f));

            // ステータス
            _lbStatus = MakeText(panelRt, "Status", "", 12,
                new Color(0.5f, 0.6f, 0.7f), FontStyle.Normal, TextAnchor.MiddleCenter,
                new Vector2(0f, -240f), new Vector2(400f, 24f));

            // 閉じるボタン
            _closeBtn = MakeButton(panelRt, "CloseBtn", "閉じる",
                new Color(0.5f, 0.25f, 0.2f), new Vector2(0f, -270f), new Vector2(140f, 36f),
                HideUI);

            _lbPanel.SetActive(false);
        }

        private void RefreshUI()
        {
            if (_lbContent == null) return;
            var sb = new System.Text.StringBuilder();
            sb.AppendLine("<color=#FFE070>順位  名前                 スコア       評価</color>");
            sb.AppendLine("----  ----                 -----       ------");

            for (int i = 0; i < _entries.Count; i++)
            {
                var e = _entries[i];
                string rank = (i + 1).ToString().PadLeft(3);
                string name = e.playerName.PadRight(20);
                string score = e.score.ToString("N0").PadLeft(10);
                string rating = e.parkRating.ToString("F1").PadLeft(6);
                sb.AppendLine($"  {rank}  {name} {score}  {rating}");
            }

            if (_entries.Count == 0)
            {
                sb.AppendLine("\n  まだスコアがありません。\n  最初のスコアを登録しましょう！");
            }

            _lbContent.text = sb.ToString();
        }

        // ================================================================
        // UIヘルパー
        // ================================================================

        private Text MakeText(RectTransform parent, string name, string text, int fontSize,
            Color color, FontStyle style, TextAnchor anchor, Vector2 pos, Vector2 size)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
            var t = go.AddComponent<Text>();
            t.text = text;
            t.font = GetBuiltinFont();
            t.fontSize = fontSize;
            t.color = color;
            t.fontStyle = style;
            t.alignment = anchor;
            return t;
        }

        private Button MakeButton(RectTransform parent, string name, string label,
            Color bgColor, Vector2 pos, Vector2 size, UnityEngine.Events.UnityAction onClick)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
            var img = go.AddComponent<Image>();
            img.color = bgColor;
            var btn = go.AddComponent<Button>();
            btn.targetGraphic = img;
            btn.onClick.AddListener(onClick);

            var lblGo = new GameObject("Label");
            lblGo.transform.SetParent(go.transform, false);
            var lblRt = lblGo.AddComponent<RectTransform>();
            lblRt.anchorMin = Vector2.zero; lblRt.anchorMax = Vector2.one;
            lblRt.offsetMin = Vector2.zero; lblRt.offsetMax = Vector2.zero;
            var t = lblGo.AddComponent<Text>();
            t.text = label;
            t.font = GetBuiltinFont();
            t.fontSize = 14;
            t.fontStyle = FontStyle.Bold;
            t.alignment = TextAnchor.MiddleCenter;
            t.color = Color.white;

            return btn;
        }

        private static Font GetBuiltinFont()
        {
            return FontManager.Regular;
        }

        // ================================================================
        // データ構造
        // ================================================================

        [Serializable]
        private class ScoreSubmitData
        {
            public string playerName;
            public float score;
            public string difficulty;
            public float parkRating;
            public int totalVisitors;
            public int playTimeMinutes;
        }

        [Serializable]
        private class ScoreSubmitResponse
        {
            public bool success;
            public int rank;
            public int totalEntries;
        }

        [Serializable]
        public class LeaderboardEntry
        {
            public string playerName;
            public float score;
            public string difficulty;
            public float parkRating;
            public int totalVisitors;
            public int playTimeMinutes;
            public long timestamp;
        }

        [Serializable]
        private class LeaderboardResponse
        {
            public List<LeaderboardEntry> entries;
            public int total;
        }
    }
}
