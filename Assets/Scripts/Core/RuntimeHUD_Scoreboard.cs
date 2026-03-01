// ============================================================
// RuntimeHUD - Scoreboard partial (PS1「新テーマパーク」スタイル)
// 濃紺ステータスバー: 総資金・年度・月日のみ表示
// ============================================================

using UnityEngine;
using UnityEngine.UI;
using ThemeParkGame.Staff;

namespace ThemeParkGame.Core
{
    public partial class RuntimeHUD
    {
        // ---- 旧フィールド（互換性維持: RefreshAllから参照される） ----
        private Text _sbVisitorValue;
        private Text _sbRevenueValue;
        private Text _sbSatisfactionValue;
        private GameObject _sbSatisfactionBar;
        private Image _sbSatisfactionFill;

        // ---- PS1風ステータスバー ----
        private Text _ps1MoneyValue;
        private Text _ps1YearValue;
        private Text _ps1DateValue;

        // ---- トップバー互換フィールド ----
        private Text _moneyText;
        private Text _timeWeatherText;
        private Text _staffText;

        // ---- 速度ボタン ----
        private Image[] _speedBtnBgs;
        private readonly float[] _speedScales = { 0f, 0.5f, 1f, 2f, 5f };
        private readonly string[] _speedLabels = { "\u23F8", "\u00BD", "\u25B6", "\u25B6\u25B6", "\u25B6\u25B6\u25B6" };

        // ================================================================
        // PS1風ステータスバー（画面最上部・全幅）
        // ================================================================

        private void BuildScoreboard(RectTransform root)
        {
            float barH = 54f;

            // ---- 全幅背景 (濃紺 #102060) ----
            var bg = new GameObject("PS1StatusBar");
            bg.transform.SetParent(root, false);
            var bgRt = bg.AddComponent<RectTransform>();
            bgRt.anchorMin = new Vector2(0f, 1f);
            bgRt.anchorMax = new Vector2(1f, 1f);
            bgRt.pivot = new Vector2(0.5f, 1f);
            bgRt.anchoredPosition = Vector2.zero;
            bgRt.sizeDelta = new Vector2(0f, barH);
            var bgImg = bg.AddComponent<Image>();
            bgImg.color = new Color(0.063f, 0.125f, 0.376f, 1f);
            bgImg.raycastTarget = false;

            Color goldYellow = new Color(1f, 0.843f, 0f);  // #FFD700

            // ---- セクション1: 総資金（左寄り） ----
            // Label
            var moneyLabel = MakeLabel(bgRt, "MoneyLabel", "総資金: ", 20, goldYellow,
                FontStyle.Bold, TextAnchor.MiddleRight);
            var mlRt = moneyLabel.rectTransform;
            mlRt.anchorMin = new Vector2(0f, 0f);
            mlRt.anchorMax = new Vector2(0f, 1f);
            mlRt.pivot = new Vector2(0f, 0.5f);
            mlRt.anchoredPosition = new Vector2(20f, 0f);
            mlRt.sizeDelta = new Vector2(110f, 0f);

            // Value
            _ps1MoneyValue = MakeLabel(bgRt, "MoneyValue", "\u00A50", 22, Color.white,
                FontStyle.Bold, TextAnchor.MiddleLeft);
            var mvRt = _ps1MoneyValue.rectTransform;
            mvRt.anchorMin = new Vector2(0f, 0f);
            mvRt.anchorMax = new Vector2(0f, 1f);
            mvRt.pivot = new Vector2(0f, 0.5f);
            mvRt.anchoredPosition = new Vector2(132f, 0f);
            mvRt.sizeDelta = new Vector2(200f, 0f);

            // ---- 区切り線1 ----
            var sep1 = MakePanel(bgRt, "Sep1", 2f, barH - 16f, new Color(0.3f, 0.4f, 0.6f, 0.7f));
            var s1Rt = sep1.GetComponent<RectTransform>();
            s1Rt.anchorMin = s1Rt.anchorMax = new Vector2(0.38f, 0.5f);
            s1Rt.pivot = new Vector2(0.5f, 0.5f);
            s1Rt.anchoredPosition = Vector2.zero;

            // ---- セクション2: 年度（中央） ----
            _ps1YearValue = MakeLabel(bgRt, "YearValue", "1年目", 22, goldYellow,
                FontStyle.Bold, TextAnchor.MiddleCenter);
            var yvRt = _ps1YearValue.rectTransform;
            yvRt.anchorMin = new Vector2(0.38f, 0f);
            yvRt.anchorMax = new Vector2(0.62f, 1f);
            yvRt.pivot = new Vector2(0.5f, 0.5f);
            yvRt.anchoredPosition = Vector2.zero;
            yvRt.sizeDelta = Vector2.zero;

            // ---- 区切り線2 ----
            var sep2 = MakePanel(bgRt, "Sep2", 2f, barH - 16f, new Color(0.3f, 0.4f, 0.6f, 0.7f));
            var s2Rt = sep2.GetComponent<RectTransform>();
            s2Rt.anchorMin = s2Rt.anchorMax = new Vector2(0.62f, 0.5f);
            s2Rt.pivot = new Vector2(0.5f, 0.5f);
            s2Rt.anchoredPosition = Vector2.zero;

            // ---- セクション3: 月日（右寄り） ----
            _ps1DateValue = MakeLabel(bgRt, "DateValue", "4月2日", 22, Color.white,
                FontStyle.Bold, TextAnchor.MiddleCenter);
            var dvRt = _ps1DateValue.rectTransform;
            dvRt.anchorMin = new Vector2(0.62f, 0f);
            dvRt.anchorMax = new Vector2(1f, 1f);
            dvRt.pivot = new Vector2(0.5f, 0.5f);
            dvRt.anchoredPosition = Vector2.zero;
            dvRt.sizeDelta = Vector2.zero;

            // ---- 旧フィールド互換: 非表示ダミーテキスト ----
            _sbVisitorValue = MakeLabel(bgRt, "_compat_vis", "", 1, Color.clear, FontStyle.Normal, TextAnchor.MiddleCenter);
            _sbVisitorValue.gameObject.SetActive(false);
            _sbRevenueValue = MakeLabel(bgRt, "_compat_rev", "", 1, Color.clear, FontStyle.Normal, TextAnchor.MiddleCenter);
            _sbRevenueValue.gameObject.SetActive(false);
            _sbSatisfactionValue = MakeLabel(bgRt, "_compat_sat", "", 1, Color.clear, FontStyle.Normal, TextAnchor.MiddleCenter);
            _sbSatisfactionValue.gameObject.SetActive(false);
        }

        // ================================================================
        // 情報バー（PS1スタイルでは非表示 — 互換性のため空メソッド）
        // ================================================================

        private void BuildInfoBar(RectTransform root)
        {
            // PS1スタイルでは上部ステータスバーに統合済み
            // 旧フィールドはnullのまま（RefreshAllのnullチェックで安全）
        }

        // ================================================================
        // PS1ステータスバー更新
        // ================================================================

        /// <summary>PS1風ステータスバーの表示を更新する</summary>
        private void RefreshPS1StatusBar()
        {
            var gm = GameManager.Instance;
            if (gm == null) return;

            // 総資金
            if (_ps1MoneyValue != null && gm.EconomyManager != null)
            {
                _ps1MoneyValue.text = $"\u00A5{gm.EconomyManager.CurrentMoney:N0}";
            }

            // 年度
            if (_ps1YearValue != null && gm.TimeManager != null)
            {
                _ps1YearValue.text = $"{gm.TimeManager.CurrentYear}年目";
            }

            // 月日
            if (_ps1DateValue != null && gm.TimeManager != null)
            {
                _ps1DateValue.text = $"{gm.TimeManager.CurrentMonth}月{gm.TimeManager.CurrentDay}日";
            }
        }

        // ================================================================
        // 速度ボタンパネル（PS1バーの下、右寄り）
        // ================================================================

        private void BuildSpeedPanel(RectTransform root)
        {
            float panelW = 276f;
            float panelH = 44f;

            var bg = MakePanel(root, "SpeedPanel", panelW, panelH, new Color(0.063f, 0.125f, 0.376f, 0.9f));
            var rt = bg.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(1f, 1f);
            rt.pivot = new Vector2(1f, 1f);
            rt.anchoredPosition = new Vector2(-10f, -60f);

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
