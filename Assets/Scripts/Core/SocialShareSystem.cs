// ============================================================
// ThemeParkGame - SocialShareSystem
// スクリーンショット撮影 & SNSシェア機能
// ============================================================

using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace ThemeParkGame.Core
{
    /// <summary>
    /// パークのスクリーンショットを撮影し、Twitter/Xにシェアする機能。
    ///
    /// 【ゲームデザイン】
    /// ・ワンボタンでスクリーンショットを撮影
    /// ・パーク名、スコア、評価をテキストに含めてTwitter/Xへシェア
    /// ・WebGLではApplication.OpenURLでブラウザのシェアダイアログを開く
    /// ・撮影時にUIを一時非表示にして綺麗なスクリーンショットを取得
    /// </summary>
    public class SocialShareSystem : MonoBehaviour
    {
        private const string HASHTAG = "ThemeParkGame";
        private const string GAME_URL = "https://rio2ryo.github.io/ThemeParkGame/";

        private GameObject _uiPanel;
        private RawImage _previewImage;
        private Text _shareText;
        private Text _statusText;
        private bool _visible;
        private Texture2D _lastScreenshot;
        private bool _capturing;

        public static SocialShareSystem Instance { get; private set; }

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
        }

        public void ToggleUI()
        {
            if (_visible) HideUI();
            else ShowUI();
        }

        public void ShowUI()
        {
            if (_uiPanel == null) BuildUI();
            _uiPanel.SetActive(true);
            _visible = true;
            UpdateShareText();
        }

        public void HideUI()
        {
            if (_uiPanel != null) _uiPanel.SetActive(false);
            _visible = false;
        }

        /// <summary>スクリーンショットを撮影する</summary>
        public void CaptureScreenshot()
        {
            if (_capturing) return;
            StartCoroutine(CaptureCoroutine());
        }

        /// <summary>Twitter/Xにシェアする</summary>
        public void ShareToTwitter()
        {
            string text = BuildShareText();
            string encoded = Uri.EscapeDataString(text);
            string url = $"https://twitter.com/intent/tweet?text={encoded}";
            Application.OpenURL(url);

            GameManager.Instance?.ShowNotification("シェア画面を開きました", NotifLevel.Info);
            WebGLOptimizer.LogVerbose("[SocialShare] Twitter/Xシェアを開始");
        }

        private IEnumerator CaptureCoroutine()
        {
            _capturing = true;

            if (_statusText != null)
                _statusText.text = "撮影中...";

            // UIを一時非表示
            if (_uiPanel != null) _uiPanel.SetActive(false);

            // 1フレーム待機（UIが消えた状態でレンダリング）
            yield return new WaitForEndOfFrame();

            // スクリーンショット取得
            var tex = new Texture2D(Screen.width, Screen.height, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, Screen.width, Screen.height), 0, 0);
            tex.Apply();

            // 古いスクリーンショットを解放
            if (_lastScreenshot != null)
                Destroy(_lastScreenshot);

            _lastScreenshot = tex;

            // UIを再表示
            if (_uiPanel != null) _uiPanel.SetActive(true);
            _visible = true;

            // プレビューを更新
            if (_previewImage != null)
            {
                _previewImage.texture = _lastScreenshot;
                _previewImage.color = Color.white;
            }

            if (_statusText != null)
                _statusText.text = "撮影完了！";

            GameManager.Instance?.ShowNotification("スクリーンショットを撮影しました", NotifLevel.Success);
            _capturing = false;
        }

        private string BuildShareText()
        {
            var gm = GameManager.Instance;
            if (gm == null)
                return $"テーマパークゲームをプレイ中！ #{HASHTAG}\n{GAME_URL}";

            string parkName = "マイテーマパーク";
            float rating = gm.ParkManager?.Rating?.OverallRating ?? 0f;
            int visitors = gm.ParkManager?.Stats?.TotalVisitorsEver ?? 0;
            float money = gm.EconomyManager?.CurrentMoney ?? 0f;
            int year = gm.TimeManager?.CurrentYear ?? 1;

            string ratingStars = "";
            int starCount = Mathf.FloorToInt(rating / 20f);
            for (int i = 0; i < starCount; i++) ratingStars += "*";

            return $"[{parkName}] {year}年目\n" +
                   $"評価: {rating:F1}/100 {ratingStars}\n" +
                   $"来場者: {visitors:N0} | 所持金: ${money:N0}\n" +
                   $"#{HASHTAG}\n{GAME_URL}";
        }

        private void UpdateShareText()
        {
            if (_shareText != null)
                _shareText.text = BuildShareText();
        }

        // ============================================================
        // UI構築
        // ============================================================

        private void BuildUI()
        {
            var canvas = FindObjectOfType<Canvas>();
            if (canvas == null) return;

            _uiPanel = new GameObject("SocialSharePanel");
            _uiPanel.transform.SetParent(canvas.transform, false);
            var rt = _uiPanel.AddComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.2f, 0.1f);
            rt.anchorMax = new Vector2(0.8f, 0.9f);
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;

            var bg = _uiPanel.AddComponent<Image>();
            bg.color = new Color(0.06f, 0.08f, 0.14f, 0.96f);

            // タイトル
            MakeText(_uiPanel.transform, "Title",
                new Vector2(0.02f, 0.92f), new Vector2(0.8f, 1f),
                "パークをシェア", 22, FontStyle.Bold, Color.white);

            // 閉じる
            MakeBtn(_uiPanel.transform, "CloseBtn",
                new Vector2(0.9f, 0.93f), new Vector2(0.98f, 0.99f),
                "X", new Color(0.7f, 0.15f, 0.15f), HideUI);

            // スクリーンショットプレビュー
            var previewObj = new GameObject("Preview");
            previewObj.transform.SetParent(_uiPanel.transform, false);
            var prevRt = previewObj.AddComponent<RectTransform>();
            prevRt.anchorMin = new Vector2(0.05f, 0.45f);
            prevRt.anchorMax = new Vector2(0.95f, 0.9f);
            prevRt.offsetMin = Vector2.zero;
            prevRt.offsetMax = Vector2.zero;
            _previewImage = previewObj.AddComponent<RawImage>();
            _previewImage.color = new Color(0.15f, 0.15f, 0.2f);

            MakeText(previewObj.transform, "PreviewLabel",
                Vector2.zero, Vector2.one,
                "[ スクリーンショット プレビュー ]", 16, FontStyle.Normal, new Color(0.5f, 0.5f, 0.6f))
                .GetComponent<Text>().alignment = TextAnchor.MiddleCenter;

            // シェアテキスト
            var stObj = MakeText(_uiPanel.transform, "ShareText",
                new Vector2(0.05f, 0.2f), new Vector2(0.95f, 0.42f),
                "", 13, FontStyle.Normal, new Color(0.7f, 0.8f, 0.9f));
            _shareText = stObj.GetComponent<Text>();

            // ステータス
            var statusObj = MakeText(_uiPanel.transform, "Status",
                new Vector2(0.05f, 0.14f), new Vector2(0.95f, 0.2f),
                "", 13, FontStyle.Normal, new Color(0.5f, 1f, 0.5f));
            _statusText = statusObj.GetComponent<Text>();
            _statusText.alignment = TextAnchor.MiddleCenter;

            // ボタン: スクリーンショット撮影
            MakeBtn(_uiPanel.transform, "CaptureBtn",
                new Vector2(0.05f, 0.03f), new Vector2(0.35f, 0.12f),
                "スクリーンショット", new Color(0.2f, 0.4f, 0.6f), CaptureScreenshot);

            // ボタン: Twitter/Xでシェア
            MakeBtn(_uiPanel.transform, "ShareBtn",
                new Vector2(0.4f, 0.03f), new Vector2(0.7f, 0.12f),
                "Xでシェア", new Color(0.1f, 0.1f, 0.1f), ShareToTwitter);

            // ボタン: URLコピー
            MakeBtn(_uiPanel.transform, "CopyBtn",
                new Vector2(0.75f, 0.03f), new Vector2(0.95f, 0.12f),
                "URLコピー", new Color(0.3f, 0.3f, 0.5f), () =>
                {
                    GUIUtility.systemCopyBuffer = GAME_URL;
                    if (_statusText != null) _statusText.text = "URLをコピーしました！";
                    GameManager.Instance?.ShowNotification("URLをクリップボードにコピーしました", NotifLevel.Info);
                });

            _uiPanel.SetActive(false);
        }

        private GameObject MakeText(Transform parent, string name,
            Vector2 aMin, Vector2 aMax, string content, int size, FontStyle style, Color color)
        {
            var obj = new GameObject(name);
            obj.transform.SetParent(parent, false);
            var r = obj.AddComponent<RectTransform>();
            r.anchorMin = aMin; r.anchorMax = aMax;
            r.offsetMin = new Vector2(5f, 0f);
            r.offsetMax = new Vector2(-5f, 0f);
            var t = obj.AddComponent<Text>();
            t.text = content;
            t.font = FontManager.Regular;
            t.fontSize = size;
            t.fontStyle = style;
            t.color = color;
            t.alignment = TextAnchor.UpperLeft;
            return obj;
        }

        private void MakeBtn(Transform parent, string name,
            Vector2 aMin, Vector2 aMax, string text, Color bg, UnityEngine.Events.UnityAction onClick)
        {
            var obj = new GameObject(name);
            obj.transform.SetParent(parent, false);
            var r = obj.AddComponent<RectTransform>();
            r.anchorMin = aMin; r.anchorMax = aMax;
            r.offsetMin = Vector2.zero; r.offsetMax = Vector2.zero;
            var img = obj.AddComponent<Image>();
            img.color = bg;
            var btn = obj.AddComponent<Button>();
            btn.targetGraphic = img;
            btn.onClick.AddListener(onClick);
            var txt = new GameObject("Text").AddComponent<Text>();
            txt.transform.SetParent(obj.transform, false);
            var tr = txt.GetComponent<RectTransform>();
            tr.anchorMin = Vector2.zero; tr.anchorMax = Vector2.one;
            tr.offsetMin = Vector2.zero; tr.offsetMax = Vector2.zero;
            txt.font = FontManager.Regular;
            txt.fontSize = 14; txt.color = Color.white;
            txt.alignment = TextAnchor.MiddleCenter;
            txt.text = text;
        }

        private void OnDestroy()
        {
            if (_lastScreenshot != null) Destroy(_lastScreenshot);
            if (Instance == this) Instance = null;
        }
    }
}
