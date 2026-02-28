// ============================================================
// RuntimeHUD - Scoreboard partial
// スコアボード、情報バー、速度パネル
// ============================================================

using UnityEngine;
using UnityEngine.UI;
using ThemeParkGame.Staff;

namespace ThemeParkGame.Core
{
    public partial class RuntimeHUD
    {
        // ---- スコアボード（画面上部中央） ----
        private Text _sbVisitorValue;
        private Text _sbRevenueValue;
        private Text _sbSatisfactionValue;
        private GameObject _sbSatisfactionBar;
        private Image _sbSatisfactionFill;

        // ---- トップバー（スコアボード下） ----
        private Text _moneyText;
        private Text _timeWeatherText;
        private Text _staffText;

        // ---- 速度ボタン ----
        private Image[] _speedBtnBgs;
        private readonly float[] _speedScales = { 0f, 0.5f, 1f, 2f, 5f };
        private readonly string[] _speedLabels = { "\u23F8", "\u00BD", "\u25B6", "\u25B6\u25B6", "\u25B6\u25B6\u25B6" };

        // ================================================================
        // スコアボード（画面上部中央 740x80）
        // 入場者数・総収益・平均満足度を大きく目立つ表示
        // ================================================================

        private void BuildScoreboard(RectTransform root)
        {
            float boardW = 740f;
            float boardH = 80f;

            var bg = MakePanel(root, "Scoreboard", boardW, boardH, BgDark);
            var rt = bg.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 1f);
            rt.pivot = new Vector2(0.5f, 1f);
            rt.anchoredPosition = new Vector2(0f, -4f);

            float colW = boardW / 3f;

            // ---- 入場者数 ----
            var visLabel = MakeLabel(rt, "VisLabel", LocalizationData.LabelVisitors, 13, new Color(0.5f, 0.6f, 0.7f),
                FontStyle.Bold, TextAnchor.MiddleCenter);
            PlaceInParent(visLabel.rectTransform, 0f, boardH - 4f, colW, 20f, new Vector2(0f, 1f));

            _sbVisitorValue = MakeLabel(rt, "VisValue", "0", 34, Color.white,
                FontStyle.Bold, TextAnchor.MiddleCenter);
            PlaceInParent(_sbVisitorValue.rectTransform, 0f, boardH - 24f, colW, 42f, new Vector2(0f, 1f));

            var visSub = MakeLabel(rt, "VisSub", "\u4EBA", 12, Muted, FontStyle.Normal, TextAnchor.MiddleCenter);
            PlaceInParent(visSub.rectTransform, 0f, boardH - 66f, colW, 16f, new Vector2(0f, 1f));

            // ---- 区切り線 1 ----
            var sep1 = MakePanel(rt, "Sep1", 2f, boardH - 16f, new Color(0.3f, 0.35f, 0.45f, 0.5f));
            var sep1Rt = sep1.GetComponent<RectTransform>();
            sep1Rt.anchorMin = sep1Rt.anchorMax = new Vector2(0f, 0.5f);
            sep1Rt.pivot = new Vector2(0.5f, 0.5f);
            sep1Rt.anchoredPosition = new Vector2(colW, 0f);

            // ---- 総収益 ----
            var revLabel = MakeLabel(rt, "RevLabel", LocalizationData.LabelRevenue, 13, new Color(0.5f, 0.6f, 0.7f),
                FontStyle.Bold, TextAnchor.MiddleCenter);
            PlaceInParent(revLabel.rectTransform, colW, boardH - 4f, colW, 20f, new Vector2(0f, 1f));

            _sbRevenueValue = MakeLabel(rt, "RevValue", "$0", 34, Green,
                FontStyle.Bold, TextAnchor.MiddleCenter);
            PlaceInParent(_sbRevenueValue.rectTransform, colW, boardH - 24f, colW, 42f, new Vector2(0f, 1f));

            var revSub = MakeLabel(rt, "RevSub", "", 12, Muted, FontStyle.Normal, TextAnchor.MiddleCenter);
            PlaceInParent(revSub.rectTransform, colW, boardH - 66f, colW, 16f, new Vector2(0f, 1f));

            // ---- 区切り線 2 ----
            var sep2 = MakePanel(rt, "Sep2", 2f, boardH - 16f, new Color(0.3f, 0.35f, 0.45f, 0.5f));
            var sep2Rt = sep2.GetComponent<RectTransform>();
            sep2Rt.anchorMin = sep2Rt.anchorMax = new Vector2(0f, 0.5f);
            sep2Rt.pivot = new Vector2(0.5f, 0.5f);
            sep2Rt.anchoredPosition = new Vector2(colW * 2f, 0f);

            // ---- 平均満足度 ----
            var satLabel = MakeLabel(rt, "SatLabel", LocalizationData.LabelSatisfaction, 13, new Color(0.5f, 0.6f, 0.7f),
                FontStyle.Bold, TextAnchor.MiddleCenter);
            PlaceInParent(satLabel.rectTransform, colW * 2f, boardH - 4f, colW, 20f, new Vector2(0f, 1f));

            _sbSatisfactionValue = MakeLabel(rt, "SatValue", "0%", 34, Green,
                FontStyle.Bold, TextAnchor.MiddleCenter);
            PlaceInParent(_sbSatisfactionValue.rectTransform, colW * 2f, boardH - 24f, colW, 42f, new Vector2(0f, 1f));

            // 満足度バー
            float barW = colW - 40f;
            float barH = 8f;
            float barX = colW * 2f + 20f;
            float barY = boardH - 70f;
            var barBg = MakePanel(rt, "SatBarBg", barW, barH, new Color(0.15f, 0.18f, 0.25f));
            PlaceInParent(barBg.GetComponent<RectTransform>(), barX, barY, barW, barH, new Vector2(0f, 1f));

            var barFill = MakePanel(rt, "SatBarFill", barW, barH, Green);
            PlaceInParent(barFill.GetComponent<RectTransform>(), barX, barY, barW, barH, new Vector2(0f, 1f));
            _sbSatisfactionBar = barBg;
            _sbSatisfactionFill = barFill.GetComponent<Image>();
        }

        // ================================================================
        // 情報バー（スコアボード下、資金・時間・天候・スタッフ）
        // ================================================================

        private void BuildInfoBar(RectTransform root)
        {
            float barW = 740f;
            float barH = 28f;

            var bg = MakePanel(root, "InfoBar", barW, barH, new Color(0.05f, 0.07f, 0.13f, 0.85f));
            var rt = bg.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 1f);
            rt.pivot = new Vector2(0.5f, 1f);
            rt.anchoredPosition = new Vector2(0f, -88f);

            _moneyText = MakeLabel(rt, "Money", "", 14, Gold, FontStyle.Bold, TextAnchor.MiddleLeft);
            PlaceInParent(_moneyText.rectTransform, 12f, barH, barW * 0.22f, barH, new Vector2(0f, 1f));

            _timeWeatherText = MakeLabel(rt, "TimeWeather", "", 14, Color.white, FontStyle.Normal, TextAnchor.MiddleCenter);
            PlaceInParent(_timeWeatherText.rectTransform, barW * 0.22f, barH, barW * 0.3f, barH, new Vector2(0f, 1f));

            // 混雑度インジケーター
            _congestionText = MakeLabel(rt, "Congestion", "通路: 空き", 13, Green, FontStyle.Normal, TextAnchor.MiddleLeft);
            PlaceInParent(_congestionText.rectTransform, barW * 0.52f, barH, 85f, barH, new Vector2(0f, 1f));

            // 混雑度バー
            var barBg = MakePanel(rt, "CongBarBg", 60f, 10f, new Color(0.15f, 0.15f, 0.2f));
            var barBgRt = barBg.GetComponent<RectTransform>();
            barBgRt.anchorMin = barBgRt.anchorMax = new Vector2(0f, 0.5f);
            barBgRt.pivot = new Vector2(0f, 0.5f);
            barBgRt.anchoredPosition = new Vector2(barW * 0.52f + 86f, 0f);

            var fill = MakePanel(barBgRt, "CongBarFill", 60f, 10f, Green);
            var fillRt = fill.GetComponent<RectTransform>();
            fillRt.anchorMin = new Vector2(0f, 0f);
            fillRt.anchorMax = new Vector2(0f, 1f);
            fillRt.pivot = new Vector2(0f, 0.5f);
            fillRt.anchoredPosition = Vector2.zero;
            fillRt.sizeDelta = new Vector2(0f, 0f); // 初期は0幅
            _congestionBarFill = fill.GetComponent<Image>();

            _staffText = MakeLabel(rt, "Staff", "", 14, Muted, FontStyle.Normal, TextAnchor.MiddleRight);
            PlaceInParent(_staffText.rectTransform, barW * 0.76f, barH, barW * 0.22f, barH, new Vector2(0f, 1f));
        }

        // ================================================================
        // 速度ボタンパネル（右上）
        // ================================================================

        private void BuildSpeedPanel(RectTransform root)
        {
            float panelW = 276f;
            float panelH = 44f;

            var bg = MakePanel(root, "SpeedPanel", panelW, panelH, BgDark);
            var rt = bg.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(1f, 1f);
            rt.pivot = new Vector2(1f, 1f);
            rt.anchoredPosition = new Vector2(-10f, -122f);

            _speedBtnBgs = new Image[5];
            float btnW = 48f;
            float gap = 4f;
            float startX = 6f;

            for (int i = 0; i < 5; i++)
            {
                float bx = startX + i * (btnW + gap);

                var btnBg = MakePanel(rt, $"SpeedBtn{i}", btnW, 34f, BtnNormal);
                var btnRt = btnBg.GetComponent<RectTransform>();
                btnRt.anchorMin = btnRt.anchorMax = new Vector2(0f, 0.5f);
                btnRt.pivot = new Vector2(0f, 0.5f);
                btnRt.anchoredPosition = new Vector2(bx, 0f);

                var btnImg = btnBg.GetComponent<Image>();
                btnImg.raycastTarget = true;
                var btn = btnBg.AddComponent<Button>();
                var colors = btn.colors;
                colors.highlightedColor = new Color(0.35f, 0.4f, 0.5f);
                colors.pressedColor = BtnActive;
                btn.colors = colors;
                btn.targetGraphic = btnImg;

                var label = MakeLabel(btnRt, "Label", _speedLabels[i], 16, Color.white, FontStyle.Bold, TextAnchor.MiddleCenter);
                StretchFill(label.rectTransform);

                int idx = i;
                btn.onClick.AddListener(() => OnSpeedClicked(idx));

                _speedBtnBgs[i] = btnBg.GetComponent<Image>();
            }
        }

        // ================================================================
        // 速度ボタン
        // ================================================================

        private void OnSpeedClicked(int index)
        {
            if (GameManager.Instance == null) return;
            GameManager.Instance.SetTimeScale(_speedScales[index]);
            _currentSpeed = _speedScales[index];
            HighlightActiveSpeed();
        }

        private void HighlightActiveSpeed()
        {
            for (int i = 0; i < _speedScales.Length; i++)
            {
                if (_speedBtnBgs[i] != null)
                    _speedBtnBgs[i].color = (Mathf.Approximately(_speedScales[i], _currentSpeed)) ? BtnActive : BtnNormal;
            }
        }
    }
}
