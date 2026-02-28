// ============================================================
// ThemeParkGame - CloudSaveManager
// クラウドセーブ（Cloudflare Worker API連携）
// ============================================================

using System;
using System.Collections;
using System.Security.Cryptography;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;

namespace ThemeParkGame.Core
{
    /// <summary>
    /// クラウドセーブの送受信とUI管理を行う。
    /// Cloudflare Worker APIを通じてセーブデータをサーバーに保存/復元する。
    /// </summary>
    public class CloudSaveManager : MonoBehaviour
    {
        public static CloudSaveManager Instance { get; private set; }

        private const string API_BASE = "https://themeparkgame-api.common-gifted-tokyo.workers.dev";

        // UI
        private GameObject _cloudPanel;
        private Text _statusText;
        private bool _isBusy;

        public string PlayerId => LeaderboardManager.Instance?.PlayerId ?? "unknown";

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

        /// <summary>現在のゲーム状態をクラウドにアップロードする</summary>
        public void CloudSave(int slot = 0, Action<bool> onComplete = null)
        {
            if (_isBusy) return;
            StartCoroutine(UploadCoroutine(slot, onComplete));
        }

        /// <summary>クラウドからセーブデータをダウンロードして適用する</summary>
        public void CloudLoad(int slot = 0, Action<bool> onComplete = null)
        {
            if (_isBusy) return;
            StartCoroutine(DownloadCoroutine(slot, onComplete));
        }

        private IEnumerator UploadCoroutine(int slot, Action<bool> onComplete)
        {
            _isBusy = true;
            SetStatus("クラウドに保存中...");

            // ローカルセーブからJSONを取得
            string saveKey = $"SaveSlot_{slot}";
            string localJson = PlayerPrefs.GetString(saveKey, "");

            if (string.IsNullOrEmpty(localJson))
            {
                // ローカルにデータがなければ新規収集
                if (!SaveSystem.Save(slot))
                {
                    SetStatus("ローカルセーブ失敗");
                    _isBusy = false;
                    onComplete?.Invoke(false);
                    yield break;
                }
                localJson = PlayerPrefs.GetString(saveKey, "");
            }

            // チェックサム計算
            string checksum = ComputeHash(localJson);
            WebGLOptimizer.LogVerbose("[CloudSave] Upload checksum: " + checksum);

            // API送信用ラッパー
            var wrapper = new CloudSaveUploadRequest
            {
                playerId = PlayerId,
                slot = slot,
                saveDataJson = localJson,
                checksum = checksum
            };

            string json = $"{{\"playerId\":\"{wrapper.playerId}\",\"slot\":{wrapper.slot},\"saveData\":{wrapper.saveDataJson},\"checksum\":\"{wrapper.checksum}\"}}";

            using (var req = new UnityWebRequest($"{API_BASE}/api/cloudsave/upload", "POST"))
            {
                req.uploadHandler = new UploadHandlerRaw(System.Text.Encoding.UTF8.GetBytes(json));
                req.downloadHandler = new DownloadHandlerBuffer();
                req.SetRequestHeader("Content-Type", "application/json");
                req.timeout = 15;

                yield return req.SendWebRequest();

                _isBusy = false;

                if (req.result == UnityWebRequest.Result.Success)
                {
                    SetStatus("クラウドに保存しました！");
                    if (NotificationSystem.Instance != null)
                        NotificationSystem.Instance.Notify("クラウドセーブ完了", NotifLevel.Success);
                    onComplete?.Invoke(true);
                }
                else
                {
                    SetStatus("クラウド保存失敗");
                    WebGLOptimizer.LogVerbose($"[CloudSave] Upload failed: {req.error}");
                    onComplete?.Invoke(false);
                }
            }
        }

        private IEnumerator DownloadCoroutine(int slot, Action<bool> onComplete)
        {
            _isBusy = true;
            SetStatus("クラウドから読み込み中...");

            string json = $"{{\"playerId\":\"{PlayerId}\",\"slot\":{slot}}}";

            using (var req = new UnityWebRequest($"{API_BASE}/api/cloudsave/download", "POST"))
            {
                req.uploadHandler = new UploadHandlerRaw(System.Text.Encoding.UTF8.GetBytes(json));
                req.downloadHandler = new DownloadHandlerBuffer();
                req.SetRequestHeader("Content-Type", "application/json");
                req.timeout = 15;

                yield return req.SendWebRequest();

                _isBusy = false;

                if (req.result == UnityWebRequest.Result.Success)
                {
                    // レスポンスからsaveDataを抽出
                    string responseText = req.downloadHandler.text;
                    var resp = JsonUtility.FromJson<CloudSaveDownloadResponse>(responseText);

                    if (resp != null && !string.IsNullOrEmpty(resp.saveDataRaw))
                    {
                        // データ整合性チェック
                        if (!VerifyIntegrity(resp.saveDataRaw, resp.checksum))
                        {
                            SetStatus("データ整合性エラー");
                            if (NotificationSystem.Instance != null)
                                NotificationSystem.Instance.Notify("データ改ざんの可能性があります。ロードを中止しました", NotifLevel.Warning);
                            WebGLOptimizer.LogVerbose("[CloudSave] Integrity verification failed, aborting load");
                            onComplete?.Invoke(false);
                            yield break;
                        }

                        // ローカルのPlayerPrefsに書き込んでからLoad
                        string saveKey = $"SaveSlot_{slot}";
                        PlayerPrefs.SetString(saveKey, resp.saveDataRaw);
                        PlayerPrefs.Save();

                        if (SaveSystem.Load(slot))
                        {
                            SetStatus("クラウドからロード完了！");
                            if (NotificationSystem.Instance != null)
                                NotificationSystem.Instance.Notify("クラウドセーブをロードしました", NotifLevel.Success);
                            onComplete?.Invoke(true);
                        }
                        else
                        {
                            SetStatus("データ復元失敗");
                            onComplete?.Invoke(false);
                        }
                    }
                    else
                    {
                        // JSONパースでsaveDataを取り出す（JsonUtilityの制限回避）
                        int idx = responseText.IndexOf("\"saveData\":", StringComparison.Ordinal);
                        if (idx >= 0)
                        {
                            string saveDataJson = responseText.Substring(idx + 11);
                            // 末尾の } を除去
                            if (saveDataJson.EndsWith("}"))
                                saveDataJson = saveDataJson.Substring(0, saveDataJson.Length - 1);

                            string saveKey = $"SaveSlot_{slot}";
                            PlayerPrefs.SetString(saveKey, saveDataJson);
                            PlayerPrefs.Save();

                            if (SaveSystem.Load(slot))
                            {
                                SetStatus("クラウドからロード完了！");
                                onComplete?.Invoke(true);
                                yield break;
                            }
                        }
                        SetStatus("クラウドにデータなし");
                        onComplete?.Invoke(false);
                    }
                }
                else
                {
                    SetStatus("クラウドロード失敗");
                    WebGLOptimizer.LogVerbose($"[CloudSave] Download failed: {req.error}");
                    onComplete?.Invoke(false);
                }
            }
        }

        // ================================================================
        // UI
        // ================================================================

        public void ShowCloudSaveUI()
        {
            if (_cloudPanel == null) BuildUI();
            _cloudPanel.SetActive(true);
        }

        public void HideCloudSaveUI()
        {
            if (_cloudPanel != null) _cloudPanel.SetActive(false);
        }

        private void SetStatus(string msg)
        {
            if (_statusText != null) _statusText.text = msg;
        }

        private void BuildUI()
        {
            var canvasGo = new GameObject("CloudSaveCanvas");
            canvasGo.transform.SetParent(transform, false);
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 92;
            var scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            canvasGo.AddComponent<GraphicRaycaster>();

            _cloudPanel = new GameObject("CloudPanel");
            _cloudPanel.transform.SetParent(canvasGo.transform, false);
            var panelRt = _cloudPanel.AddComponent<RectTransform>();
            panelRt.anchorMin = panelRt.anchorMax = new Vector2(0.5f, 0.5f);
            panelRt.sizeDelta = new Vector2(380f, 300f);
            var bg = _cloudPanel.AddComponent<Image>();
            bg.color = new Color(0.04f, 0.06f, 0.14f, 0.96f);
            bg.raycastTarget = true;

            MakeLabel(panelRt, "Title", "クラウドセーブ", 22,
                new Color(0.4f, 0.85f, 0.95f), FontStyle.Bold, TextAnchor.MiddleCenter,
                new Vector2(0f, 115f), new Vector2(360f, 32f));

            MakeLabel(panelRt, "Desc", "セーブデータをクラウドに保存/読み込み\nどのデバイスからでもプレイを再開できます", 13,
                new Color(0.6f, 0.65f, 0.75f), FontStyle.Normal, TextAnchor.MiddleCenter,
                new Vector2(0f, 75f), new Vector2(340f, 40f));

            MakeLabel(panelRt, "IdLabel", $"プレイヤーID: {PlayerId}", 11,
                new Color(0.4f, 0.45f, 0.55f), FontStyle.Normal, TextAnchor.MiddleCenter,
                new Vector2(0f, 45f), new Vector2(340f, 20f));

            // アップロードボタン
            MakeBtn(panelRt, "UploadBtn", "クラウドセーブ（保存）",
                new Color(0.2f, 0.55f, 0.35f),
                new Vector2(0f, 5f), new Vector2(280f, 40f),
                () => CloudSave());

            // ダウンロードボタン
            MakeBtn(panelRt, "DownloadBtn", "クラウドロード（読込）",
                new Color(0.25f, 0.45f, 0.65f),
                new Vector2(0f, -45f), new Vector2(280f, 40f),
                () => CloudLoad());

            _statusText = MakeLabel(panelRt, "Status", "", 13,
                new Color(0.7f, 0.8f, 0.9f), FontStyle.Normal, TextAnchor.MiddleCenter,
                new Vector2(0f, -90f), new Vector2(340f, 24f));

            MakeBtn(panelRt, "CloseBtn", "閉じる",
                new Color(0.5f, 0.25f, 0.2f),
                new Vector2(0f, -125f), new Vector2(120f, 32f),
                HideCloudSaveUI);

            _cloudPanel.SetActive(false);
        }

        private Text MakeLabel(RectTransform p, string name, string text, int size,
            Color color, FontStyle style, TextAnchor anchor, Vector2 pos, Vector2 sz)
        {
            var go = new GameObject(name);
            go.transform.SetParent(p, false);
            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = pos;
            rt.sizeDelta = sz;
            var t = go.AddComponent<Text>();
            t.text = text;
            t.font = GetFont();
            t.fontSize = size;
            t.color = color;
            t.fontStyle = style;
            t.alignment = anchor;
            return t;
        }

        private void MakeBtn(RectTransform p, string name, string label,
            Color bgColor, Vector2 pos, Vector2 sz, UnityEngine.Events.UnityAction onClick)
        {
            var go = new GameObject(name);
            go.transform.SetParent(p, false);
            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = pos;
            rt.sizeDelta = sz;
            var img = go.AddComponent<Image>();
            img.color = bgColor;
            var btn = go.AddComponent<Button>();
            btn.targetGraphic = img;
            btn.onClick.AddListener(onClick);

            var lbl = new GameObject("Label");
            lbl.transform.SetParent(go.transform, false);
            var lblRt = lbl.AddComponent<RectTransform>();
            lblRt.anchorMin = Vector2.zero; lblRt.anchorMax = Vector2.one;
            lblRt.offsetMin = Vector2.zero; lblRt.offsetMax = Vector2.zero;
            var t = lbl.AddComponent<Text>();
            t.text = label;
            t.font = GetFont();
            t.fontSize = 15;
            t.fontStyle = FontStyle.Bold;
            t.alignment = TextAnchor.MiddleCenter;
            t.color = Color.white;
        }

        private static Font GetFont()
        {
            return FontManager.Regular;
        }

        // ================================================================
        // データ整合性チェック
        // ================================================================

        private static string ComputeHash(string data)
        {
            using (var sha256 = System.Security.Cryptography.SHA256.Create())
            {
                byte[] bytes = System.Text.Encoding.UTF8.GetBytes(data);
                byte[] hash = sha256.ComputeHash(bytes);
                var sb = new System.Text.StringBuilder(64);
                for (int i = 0; i < hash.Length; i++)
                    sb.Append(hash[i].ToString("x2"));
                return sb.ToString();
            }
        }

        private bool VerifyIntegrity(string saveData, string expectedChecksum)
        {
            if (string.IsNullOrEmpty(expectedChecksum))
            {
                WebGLOptimizer.LogVerbose("[CloudSave] No checksum found (legacy data), skipping verification");
                return true;
            }
            string computed = ComputeHash(saveData);
            bool valid = computed == expectedChecksum;
            if (!valid)
                WebGLOptimizer.LogVerbose("[CloudSave] Integrity check FAILED! Expected=" + expectedChecksum + ", Got=" + computed);
            return valid;
        }

        // ================================================================
        // データ構造
        // ================================================================

        [Serializable]
        private class CloudSaveUploadRequest
        {
            public string playerId;
            public int slot;
            public string saveDataJson;
            public string checksum;
        }

        [Serializable]
        private class CloudSaveDownloadResponse
        {
            public bool success;
            public string saveDataRaw;
            public string checksum;
        }
    }
}
