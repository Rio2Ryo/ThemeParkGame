// ============================================================
// ThemeParkGame - WeatherForecastUI
// 天気予報UI - 現在の天候・季節・予報・来園率影響を表示
// ============================================================

using UnityEngine;
using UnityEngine.UI;
using ThemeParkGame.Core;
using ThemeParkGame.Park;

namespace ThemeParkGame.UI
{
    /// <summary>
    /// 天気予報の詳細UIパネル。
    /// 現在の天候、季節、数時間先の予報、来場者への影響度を表示する。
    /// </summary>
    public class WeatherForecastUI : MonoBehaviour
    {
        private GameObject _panel;
        private Text _currentWeatherText;
        private Text _seasonText;
        private Text _effectText;
        private Text _forecastText;
        private bool _visible;
        private float _refreshTimer;

        public static WeatherForecastUI Instance { get; private set; }

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
            if (_panel == null) BuildUI();
            RefreshUI();
            _panel.SetActive(true);
            _visible = true;
        }

        public void HideUI()
        {
            if (_panel != null) _panel.SetActive(false);
            _visible = false;
        }

        private void Update()
        {
            if (!_visible) return;
            _refreshTimer += Time.unscaledDeltaTime;
            if (_refreshTimer >= 2f)
            {
                _refreshTimer = 0f;
                RefreshUI();
            }
        }

        private void BuildUI()
        {
            var canvas = FindObjectOfType<Canvas>();
            if (canvas == null) return;

            _panel = new GameObject("WeatherForecastPanel");
            _panel.transform.SetParent(canvas.transform, false);
            var rt = _panel.AddComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.6f, 0.55f);
            rt.anchorMax = new Vector2(0.95f, 0.95f);
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;

            var bg = _panel.AddComponent<Image>();
            bg.color = new Color(0.05f, 0.08f, 0.18f, 0.95f);

            // タイトル
            MakeText(_panel.transform, "Title", new Vector2(0f, 0.88f), new Vector2(0.8f, 1f),
                "天気予報", 20, FontStyle.Bold, Color.white);

            // 閉じるボタン
            var closeObj = new GameObject("CloseBtn");
            closeObj.transform.SetParent(_panel.transform, false);
            var closeRt = closeObj.AddComponent<RectTransform>();
            closeRt.anchorMin = new Vector2(0.88f, 0.9f);
            closeRt.anchorMax = new Vector2(0.98f, 0.99f);
            closeRt.offsetMin = Vector2.zero;
            closeRt.offsetMax = Vector2.zero;
            var closeImg = closeObj.AddComponent<Image>();
            closeImg.color = new Color(0.7f, 0.15f, 0.15f);
            var closeBtn = closeObj.AddComponent<Button>();
            closeBtn.targetGraphic = closeImg;
            closeBtn.onClick.AddListener(HideUI);
            var closeTxt = MakeText(closeObj.transform, "X", Vector2.zero, Vector2.one, "X", 16, FontStyle.Bold, Color.white);

            // 現在の天候
            var cwObj = MakeText(_panel.transform, "CurrentWeather",
                new Vector2(0.03f, 0.72f), new Vector2(0.97f, 0.88f),
                "", 24, FontStyle.Bold, new Color(1f, 0.95f, 0.6f));
            _currentWeatherText = cwObj.GetComponent<Text>();
            _currentWeatherText.alignment = TextAnchor.MiddleCenter;

            // 季節
            var sObj = MakeText(_panel.transform, "Season",
                new Vector2(0.03f, 0.62f), new Vector2(0.97f, 0.72f),
                "", 16, FontStyle.Normal, new Color(0.7f, 0.85f, 1f));
            _seasonText = sObj.GetComponent<Text>();
            _seasonText.alignment = TextAnchor.MiddleCenter;

            // 効果
            var eObj = MakeText(_panel.transform, "Effects",
                new Vector2(0.03f, 0.38f), new Vector2(0.97f, 0.62f),
                "", 13, FontStyle.Normal, Color.white);
            _effectText = eObj.GetComponent<Text>();

            // 予報
            var fObj = MakeText(_panel.transform, "Forecast",
                new Vector2(0.03f, 0.02f), new Vector2(0.97f, 0.38f),
                "", 13, FontStyle.Normal, new Color(0.8f, 0.8f, 0.9f));
            _forecastText = fObj.GetComponent<Text>();

            _panel.SetActive(false);
        }

        private void RefreshUI()
        {
            var ws = GameManager.Instance?.WeatherSystem;
            if (ws == null) return;

            // 現在天候
            string icon = GetWeatherIcon(ws.CurrentWeather);
            string name = WeatherSystem.GetWeatherDisplayName(ws.CurrentWeather);
            if (_currentWeatherText != null)
                _currentWeatherText.text = $"{icon} {name}";

            // 季節
            if (_seasonText != null)
            {
                string seasonName = WeatherSystem.GetSeasonDisplayName(ws.CurrentSeason);
                var tm = GameManager.Instance?.TimeManager;
                string timeStr = tm != null ? $"Year {tm.CurrentYear} {tm.CurrentMonth}月{tm.CurrentDay}日" : "";
                _seasonText.text = $"{seasonName} - {timeStr}";
            }

            // 効果
            if (_effectText != null)
            {
                var effect = ws.GetCurrentWeatherEffect();
                string visitorRate = $"{effect.VisitorCountMultiplier * 100f:F0}%";
                string thirstRate = $"{effect.ThirstRateMultiplier:F1}x";
                string happiness = effect.HappinessModifierPerHour >= 0
                    ? $"+{effect.HappinessModifierPerHour:F1}"
                    : $"{effect.HappinessModifierPerHour:F1}";
                string maint = $"{effect.MaintenanceCostMultiplier * 100f:F0}%";

                Color visColor = effect.VisitorCountMultiplier >= 1f
                    ? new Color(0.5f, 1f, 0.5f) : new Color(1f, 0.6f, 0.4f);

                _effectText.text =
                    $"--- 天候の影響 ---\n" +
                    $"来園率: {visitorRate}\n" +
                    $"渇き速度: {thirstRate}\n" +
                    $"幸福度/時: {happiness}\n" +
                    $"維持費: {maint}";
            }

            // 予報
            if (_forecastText != null)
            {
                var forecasts = ws.GetForecast();
                var sb = new System.Text.StringBuilder();
                sb.AppendLine("--- 今後の予報 ---");
                int count = Mathf.Min(forecasts.Count, 4);
                for (int i = 0; i < count; i++)
                {
                    var f = forecasts[i];
                    string fIcon = GetWeatherIcon(f.PredictedWeather);
                    string fName = WeatherSystem.GetWeatherDisplayName(f.PredictedWeather);
                    int confPct = Mathf.RoundToInt(f.Confidence * 100f);
                    int hour = Mathf.FloorToInt(f.Hour);
                    sb.AppendLine($"  {hour:00}:00  {fIcon}{fName}  (確度{confPct}%)");
                }
                _forecastText.text = sb.ToString();
            }
        }

        private static string GetWeatherIcon(Weather w)
        {
            return w switch
            {
                Weather.Sunny => "[SUN]",
                Weather.Cloudy => "[CLD]",
                Weather.Rainy => "[RAN]",
                Weather.Snowy => "[SNW]",
                Weather.Hot => "[HOT]",
                _ => "[???]"
            };
        }

        private GameObject MakeText(Transform parent, string name,
            Vector2 anchorMin, Vector2 anchorMax,
            string content, int fontSize, FontStyle style, Color color)
        {
            var obj = new GameObject(name);
            obj.transform.SetParent(parent, false);
            var r = obj.AddComponent<RectTransform>();
            r.anchorMin = anchorMin;
            r.anchorMax = anchorMax;
            r.offsetMin = new Vector2(5f, 0f);
            r.offsetMax = new Vector2(-5f, 0f);
            var t = obj.AddComponent<Text>();
            t.text = content;
            t.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            t.fontSize = fontSize;
            t.fontStyle = style;
            t.color = color;
            t.alignment = TextAnchor.UpperLeft;
            return obj;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }
    }
}
