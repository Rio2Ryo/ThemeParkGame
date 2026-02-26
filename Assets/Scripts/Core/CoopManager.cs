// ============================================================
// ThemeParkGame - CoopManager
// 協力型Co-opモード基盤 - ルーム管理＆状態同期
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
    /// Co-opモードの基盤システム。
    /// Cloudflare Workerと連携してルームの作成・参加・状態同期を行う。
    ///
    /// 【ゲームデザイン】
    /// ・ホストがルームを作成し、ルームコードを共有
    /// ・最大4人のプレイヤーが同時接続
    /// ・ホストのパーク状態を共有し、他プレイヤーのアクションをリアルタイム反映
    /// ・ポーリングベースの同期（WebSocket代替、WebGL互換）
    /// </summary>
    public class CoopManager : MonoBehaviour
    {
        // ============================================================
        // 定数
        // ============================================================

        private const string API_BASE = "https://themeparkgame-api.common-gifted-tokyo.workers.dev";
        private const float SYNC_INTERVAL = 3f; // 3秒ごとにポーリング
        private const float STATE_SYNC_INTERVAL = 10f; // 10秒ごとにパーク状態同期

        // ============================================================
        // データクラス
        // ============================================================

        [Serializable]
        public class CoopPlayer
        {
            public string id;
            public string name;
            public long joinedAt;
        }

        [Serializable]
        public class CoopAction
        {
            public string playerId;
            public string type;
            public string data;
            public long timestamp;
        }

        [Serializable]
        public class RoomInfo
        {
            public string roomCode;
            public string hostName;
            public int playerCount;
        }

        // JSON wrapper classes for Unity's JsonUtility
        [Serializable] private class CreateRequest { public string playerId; public string playerName; }
        [Serializable] private class JoinRequest { public string roomCode; public string playerId; public string playerName; }
        [Serializable] private class StateRequest { public string roomCode; public long since; }
        [Serializable] private class SyncRequest { public string roomCode; public string playerId; public ActionData action; }
        [Serializable] private class LeaveRequest { public string roomCode; public string playerId; }
        [Serializable] private class ActionData { public string type; public string data; }

        [Serializable] private class CreateResponse { public bool success; public string roomCode; }
        [Serializable] private class JoinResponse { public bool success; }
        [Serializable] private class StateResponse
        {
            public bool success;
            public CoopPlayer[] players;
            public CoopAction[] actions;
            public long lastActivity;
        }
        [Serializable] private class RoomListResponse { public bool success; public RoomInfo[] rooms; }
        [Serializable] private class ErrorResponse { public string error; }

        // ============================================================
        // フィールド
        // ============================================================

        private string _playerId;
        private string _playerName;
        private string _currentRoomCode;
        private bool _isHost;
        private bool _isConnected;
        private float _syncTimer;
        private float _stateSyncTimer;
        private long _lastActionTimestamp;
        private List<CoopPlayer> _players = new List<CoopPlayer>();
        private Queue<CoopAction> _pendingActions = new Queue<CoopAction>();

        // UI
        private GameObject _uiPanel;
        private Text _statusText;
        private Text _playerListText;
        private InputField _roomCodeInput;
        private GameObject _lobbyPanel;
        private GameObject _connectedPanel;
        private bool _uiVisible;

        // ============================================================
        // プロパティ
        // ============================================================

        public static CoopManager Instance { get; private set; }
        public bool IsConnected => _isConnected;
        public bool IsHost => _isHost;
        public string CurrentRoomCode => _currentRoomCode;
        public IReadOnlyList<CoopPlayer> Players => _players;

        // ============================================================
        // イベント
        // ============================================================

        public event Action<CoopAction> OnActionReceived;
        public event Action<List<CoopPlayer>> OnPlayersChanged;

        // ============================================================
        // ライフサイクル
        // ============================================================

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            _playerId = SystemInfo.deviceUniqueIdentifier;
            if (_playerId == SystemInfo.unsupportedIdentifier || string.IsNullOrEmpty(_playerId))
            {
                _playerId = "player_" + UnityEngine.Random.Range(100000, 999999);
            }
            _playerName = "Player" + UnityEngine.Random.Range(100, 999);
        }

        private void Update()
        {
            if (!_isConnected || string.IsNullOrEmpty(_currentRoomCode)) return;

            _syncTimer += Time.unscaledDeltaTime;
            if (_syncTimer >= SYNC_INTERVAL)
            {
                _syncTimer = 0f;
                StartCoroutine(PollRoomState());
            }

            _stateSyncTimer += Time.unscaledDeltaTime;
            if (_stateSyncTimer >= STATE_SYNC_INTERVAL && _isHost)
            {
                _stateSyncTimer = 0f;
                // ホストはパーク状態を定期的に同期
                SendAction("park_state_update", "{}");
            }
        }

        // ============================================================
        // ルーム操作
        // ============================================================

        public void CreateRoom()
        {
            StartCoroutine(CreateRoomCoroutine());
        }

        public void JoinRoom(string roomCode)
        {
            if (string.IsNullOrEmpty(roomCode)) return;
            StartCoroutine(JoinRoomCoroutine(roomCode.ToUpper().Trim()));
        }

        public void LeaveRoom()
        {
            if (!_isConnected) return;
            StartCoroutine(LeaveRoomCoroutine());
        }

        public void SendAction(string actionType, string actionData)
        {
            if (!_isConnected) return;
            StartCoroutine(SyncActionCoroutine(actionType, actionData));
        }

        // ============================================================
        // API通信
        // ============================================================

        private IEnumerator CreateRoomCoroutine()
        {
            var req = new CreateRequest { playerId = _playerId, playerName = _playerName };
            string json = JsonUtility.ToJson(req);

            using (var www = new UnityWebRequest($"{API_BASE}/api/coop/create", "POST"))
            {
                www.uploadHandler = new UploadHandlerRaw(System.Text.Encoding.UTF8.GetBytes(json));
                www.downloadHandler = new DownloadHandlerBuffer();
                www.SetRequestHeader("Content-Type", "application/json");
                yield return www.SendWebRequest();

                if (www.result == UnityWebRequest.Result.Success)
                {
                    var resp = JsonUtility.FromJson<CreateResponse>(www.downloadHandler.text);
                    if (resp.success)
                    {
                        _currentRoomCode = resp.roomCode;
                        _isHost = true;
                        _isConnected = true;
                        _lastActionTimestamp = 0;
                        _players.Clear();
                        _players.Add(new CoopPlayer { id = _playerId, name = _playerName });

                        GameManager.Instance?.ShowNotification(
                            $"ルーム作成成功！ コード: {_currentRoomCode}", NotifLevel.Success);
                        WebGLOptimizer.LogVerbose($"[Coop] ルーム作成: {_currentRoomCode}");
                        RefreshUI();
                    }
                }
                else
                {
                    GameManager.Instance?.ShowNotification(
                        "ルーム作成に失敗しました", NotifLevel.Warning);
                }
            }
        }

        private IEnumerator JoinRoomCoroutine(string roomCode)
        {
            var req = new JoinRequest { roomCode = roomCode, playerId = _playerId, playerName = _playerName };
            string json = JsonUtility.ToJson(req);

            using (var www = new UnityWebRequest($"{API_BASE}/api/coop/join", "POST"))
            {
                www.uploadHandler = new UploadHandlerRaw(System.Text.Encoding.UTF8.GetBytes(json));
                www.downloadHandler = new DownloadHandlerBuffer();
                www.SetRequestHeader("Content-Type", "application/json");
                yield return www.SendWebRequest();

                if (www.result == UnityWebRequest.Result.Success)
                {
                    var resp = JsonUtility.FromJson<JoinResponse>(www.downloadHandler.text);
                    if (resp.success)
                    {
                        _currentRoomCode = roomCode;
                        _isHost = false;
                        _isConnected = true;
                        _lastActionTimestamp = 0;

                        GameManager.Instance?.ShowNotification(
                            $"ルーム {roomCode} に参加しました！", NotifLevel.Success);
                        WebGLOptimizer.LogVerbose($"[Coop] ルーム参加: {roomCode}");
                        RefreshUI();
                    }
                }
                else
                {
                    GameManager.Instance?.ShowNotification(
                        "ルームへの参加に失敗しました", NotifLevel.Warning);
                }
            }
        }

        private IEnumerator LeaveRoomCoroutine()
        {
            var req = new LeaveRequest { roomCode = _currentRoomCode, playerId = _playerId };
            string json = JsonUtility.ToJson(req);

            using (var www = new UnityWebRequest($"{API_BASE}/api/coop/leave", "POST"))
            {
                www.uploadHandler = new UploadHandlerRaw(System.Text.Encoding.UTF8.GetBytes(json));
                www.downloadHandler = new DownloadHandlerBuffer();
                www.SetRequestHeader("Content-Type", "application/json");
                yield return www.SendWebRequest();
            }

            string oldCode = _currentRoomCode;
            _currentRoomCode = null;
            _isConnected = false;
            _isHost = false;
            _players.Clear();

            GameManager.Instance?.ShowNotification(
                $"ルーム {oldCode} から退出しました", NotifLevel.Info);
            RefreshUI();
        }

        private IEnumerator PollRoomState()
        {
            var req = new StateRequest { roomCode = _currentRoomCode, since = _lastActionTimestamp };
            string json = JsonUtility.ToJson(req);

            using (var www = new UnityWebRequest($"{API_BASE}/api/coop/state", "POST"))
            {
                www.uploadHandler = new UploadHandlerRaw(System.Text.Encoding.UTF8.GetBytes(json));
                www.downloadHandler = new DownloadHandlerBuffer();
                www.SetRequestHeader("Content-Type", "application/json");
                yield return www.SendWebRequest();

                if (www.result == UnityWebRequest.Result.Success)
                {
                    var resp = JsonUtility.FromJson<StateResponse>(www.downloadHandler.text);
                    if (resp.success)
                    {
                        // プレイヤーリスト更新
                        if (resp.players != null)
                        {
                            _players.Clear();
                            _players.AddRange(resp.players);
                            OnPlayersChanged?.Invoke(_players);
                        }

                        // 新しいアクションを処理
                        if (resp.actions != null)
                        {
                            foreach (var action in resp.actions)
                            {
                                if (action.playerId != _playerId)
                                {
                                    OnActionReceived?.Invoke(action);
                                }
                                if (action.timestamp > _lastActionTimestamp)
                                {
                                    _lastActionTimestamp = action.timestamp;
                                }
                            }
                        }

                        RefreshUI();
                    }
                }
            }
        }

        private IEnumerator SyncActionCoroutine(string actionType, string actionData)
        {
            var req = new SyncRequest
            {
                roomCode = _currentRoomCode,
                playerId = _playerId,
                action = new ActionData { type = actionType, data = actionData }
            };
            string json = JsonUtility.ToJson(req);

            using (var www = new UnityWebRequest($"{API_BASE}/api/coop/sync", "POST"))
            {
                www.uploadHandler = new UploadHandlerRaw(System.Text.Encoding.UTF8.GetBytes(json));
                www.downloadHandler = new DownloadHandlerBuffer();
                www.SetRequestHeader("Content-Type", "application/json");
                yield return www.SendWebRequest();
            }
        }

        // ============================================================
        // UI
        // ============================================================

        public void ShowUI()
        {
            if (_uiPanel == null) CreateUI();
            RefreshUI();
            _uiPanel.SetActive(true);
            _uiVisible = true;
        }

        public void HideUI()
        {
            if (_uiPanel != null) _uiPanel.SetActive(false);
            _uiVisible = false;
        }

        public void ToggleUI()
        {
            if (_uiVisible) HideUI();
            else ShowUI();
        }

        private void CreateUI()
        {
            var canvas = FindObjectOfType<Canvas>();
            if (canvas == null) return;

            _uiPanel = new GameObject("CoopPanel");
            _uiPanel.transform.SetParent(canvas.transform, false);
            var panelRect = _uiPanel.AddComponent<RectTransform>();
            panelRect.anchorMin = new Vector2(0.25f, 0.15f);
            panelRect.anchorMax = new Vector2(0.75f, 0.85f);
            panelRect.offsetMin = Vector2.zero;
            panelRect.offsetMax = Vector2.zero;

            var panelImg = _uiPanel.AddComponent<Image>();
            panelImg.color = new Color(0.08f, 0.1f, 0.18f, 0.96f);

            // タイトル
            CreateText(_uiPanel.transform, "Title",
                new Vector2(0f, 0.9f), new Vector2(0.85f, 1f),
                "Co-op マルチプレイ", 22, FontStyle.Bold, Color.white);

            // 閉じるボタン
            CreateBtn(_uiPanel.transform, "CloseBtn",
                new Vector2(0.88f, 0.92f), new Vector2(0.98f, 0.99f),
                "X", new Color(0.7f, 0.15f, 0.15f), HideUI);

            // --- ロビーパネル ---
            _lobbyPanel = new GameObject("Lobby");
            _lobbyPanel.transform.SetParent(_uiPanel.transform, false);
            var lobbyRect = _lobbyPanel.AddComponent<RectTransform>();
            lobbyRect.anchorMin = new Vector2(0.02f, 0.02f);
            lobbyRect.anchorMax = new Vector2(0.98f, 0.88f);
            lobbyRect.offsetMin = Vector2.zero;
            lobbyRect.offsetMax = Vector2.zero;

            // ルーム作成ボタン
            CreateBtn(_lobbyPanel.transform, "CreateBtn",
                new Vector2(0.1f, 0.75f), new Vector2(0.9f, 0.9f),
                "ルームを作成する", new Color(0.2f, 0.45f, 0.2f), CreateRoom);

            // ルームコード入力
            CreateText(_lobbyPanel.transform, "JoinLabel",
                new Vector2(0.05f, 0.58f), new Vector2(0.4f, 0.68f),
                "ルームコード:", 16, FontStyle.Normal, Color.white);

            var inputObj = new GameObject("RoomCodeInput");
            inputObj.transform.SetParent(_lobbyPanel.transform, false);
            var inputRect = inputObj.AddComponent<RectTransform>();
            inputRect.anchorMin = new Vector2(0.4f, 0.58f);
            inputRect.anchorMax = new Vector2(0.7f, 0.68f);
            inputRect.offsetMin = Vector2.zero;
            inputRect.offsetMax = Vector2.zero;
            var inputImg = inputObj.AddComponent<Image>();
            inputImg.color = new Color(0.2f, 0.2f, 0.25f);
            _roomCodeInput = inputObj.AddComponent<InputField>();
            _roomCodeInput.characterLimit = 6;
            var inputText = new GameObject("Text").AddComponent<Text>();
            inputText.transform.SetParent(inputObj.transform, false);
            var itRect = inputText.GetComponent<RectTransform>();
            itRect.anchorMin = Vector2.zero;
            itRect.anchorMax = Vector2.one;
            itRect.offsetMin = new Vector2(5f, 0f);
            itRect.offsetMax = new Vector2(-5f, 0f);
            inputText.font = FontManager.Regular;
            inputText.fontSize = 18;
            inputText.color = Color.white;
            inputText.alignment = TextAnchor.MiddleCenter;
            _roomCodeInput.textComponent = inputText;

            // 参加ボタン
            CreateBtn(_lobbyPanel.transform, "JoinBtn",
                new Vector2(0.72f, 0.58f), new Vector2(0.9f, 0.68f),
                "参加", new Color(0.2f, 0.3f, 0.55f),
                () => JoinRoom(_roomCodeInput != null ? _roomCodeInput.text : ""));

            // 説明文
            CreateText(_lobbyPanel.transform, "Desc",
                new Vector2(0.05f, 0.05f), new Vector2(0.95f, 0.45f),
                "Co-opモードでは最大4人のプレイヤーが\n" +
                "同じパークを共同で運営できます。\n\n" +
                "ホストがルームを作成し、他のプレイヤーは\n" +
                "ルームコードを入力して参加します。\n\n" +
                "建設・雇用・価格設定などのアクションが\n" +
                "リアルタイムで共有されます。",
                14, FontStyle.Normal, new Color(0.7f, 0.7f, 0.8f));

            // --- 接続中パネル ---
            _connectedPanel = new GameObject("Connected");
            _connectedPanel.transform.SetParent(_uiPanel.transform, false);
            var connRect = _connectedPanel.AddComponent<RectTransform>();
            connRect.anchorMin = new Vector2(0.02f, 0.02f);
            connRect.anchorMax = new Vector2(0.98f, 0.88f);
            connRect.offsetMin = Vector2.zero;
            connRect.offsetMax = Vector2.zero;

            // ステータス
            var statusObj = CreateText(_connectedPanel.transform, "Status",
                new Vector2(0.05f, 0.8f), new Vector2(0.95f, 0.95f),
                "", 16, FontStyle.Bold, new Color(0.5f, 1f, 0.5f));
            _statusText = statusObj.GetComponent<Text>();

            // プレイヤーリスト
            var plObj = CreateText(_connectedPanel.transform, "PlayerList",
                new Vector2(0.05f, 0.3f), new Vector2(0.95f, 0.75f),
                "", 14, FontStyle.Normal, Color.white);
            _playerListText = plObj.GetComponent<Text>();

            // 退出ボタン
            CreateBtn(_connectedPanel.transform, "LeaveBtn",
                new Vector2(0.25f, 0.05f), new Vector2(0.75f, 0.18f),
                "ルームを退出する", new Color(0.6f, 0.15f, 0.15f), LeaveRoom);

            _uiPanel.SetActive(false);
        }

        private void RefreshUI()
        {
            if (_uiPanel == null) return;

            if (_lobbyPanel != null) _lobbyPanel.SetActive(!_isConnected);
            if (_connectedPanel != null) _connectedPanel.SetActive(_isConnected);

            if (_isConnected)
            {
                if (_statusText != null)
                {
                    _statusText.text = $"ルーム: {_currentRoomCode}  |  " +
                        $"{(_isHost ? "ホスト" : "ゲスト")}  |  " +
                        $"プレイヤー: {_players.Count}/4";
                }

                if (_playerListText != null)
                {
                    string list = "--- 参加プレイヤー ---\n";
                    for (int i = 0; i < _players.Count; i++)
                    {
                        var p = _players[i];
                        string role = (p.id == _playerId) ? " (あなた)" : "";
                        list += $"{i + 1}. {p.name}{role}\n";
                    }
                    _playerListText.text = list;
                }
            }
        }

        // ============================================================
        // UIヘルパー
        // ============================================================

        private GameObject CreateText(Transform parent, string name,
            Vector2 anchorMin, Vector2 anchorMax,
            string content, int fontSize, FontStyle style, Color color)
        {
            var obj = new GameObject(name);
            obj.transform.SetParent(parent, false);
            var rect = obj.AddComponent<RectTransform>();
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.offsetMin = new Vector2(5f, 0f);
            rect.offsetMax = new Vector2(-5f, 0f);
            var txt = obj.AddComponent<Text>();
            txt.text = content;
            txt.font = FontManager.Regular;
            txt.fontSize = fontSize;
            txt.fontStyle = style;
            txt.color = color;
            txt.alignment = TextAnchor.MiddleLeft;
            return obj;
        }

        private void CreateBtn(Transform parent, string name,
            Vector2 anchorMin, Vector2 anchorMax,
            string text, Color bgColor, UnityEngine.Events.UnityAction onClick)
        {
            var obj = new GameObject(name);
            obj.transform.SetParent(parent, false);
            var rect = obj.AddComponent<RectTransform>();
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            var img = obj.AddComponent<Image>();
            img.color = bgColor;
            var btn = obj.AddComponent<Button>();
            btn.targetGraphic = img;
            btn.onClick.AddListener(onClick);

            var txtObj = new GameObject("Text");
            txtObj.transform.SetParent(obj.transform, false);
            var txtRect = txtObj.AddComponent<RectTransform>();
            txtRect.anchorMin = Vector2.zero;
            txtRect.anchorMax = Vector2.one;
            txtRect.offsetMin = Vector2.zero;
            txtRect.offsetMax = Vector2.zero;
            var txt = txtObj.AddComponent<Text>();
            txt.text = text;
            txt.font = FontManager.Regular;
            txt.fontSize = 14;
            txt.color = Color.white;
            txt.alignment = TextAnchor.MiddleCenter;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }
    }
}
