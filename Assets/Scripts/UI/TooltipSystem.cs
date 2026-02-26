// ============================================================
// ThemeParkGame - TooltipSystem
// ホバー時にツールチップを表示するUIシステム
// ============================================================

using UnityEngine;
using UnityEngine.UI;

namespace ThemeParkGame.UI
{
    /// <summary>
    /// 画面上にツールチップを表示するシングルトンUI。
    /// Show(text, screenPos)で表示、Hide()で非表示。
    /// RuntimeHUDから利用される。
    /// </summary>
    public class TooltipSystem : MonoBehaviour
    {
        public static TooltipSystem Instance { get; private set; }

        private GameObject _tooltipGo;
        private RectTransform _tooltipRt;
        private Text _tooltipText;
        private Canvas _canvas;
        private bool _isShowing;

        // デザイン定数
        private static readonly Color BgColor = new Color(0.08f, 0.1f, 0.16f, 0.92f);
        private const float PaddingH = 12f;
        private const float PaddingV = 6f;
        private const float MaxWidth = 300f;
        private const float FontSize = 13;
        private const float OffsetY = 24f;

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(this); return; }
            Instance = this;
            CreateTooltip();
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        private void CreateTooltip()
        {
            // Canvas取得（既存のものがあれば使用）
            _canvas = GetComponentInParent<Canvas>();
            if (_canvas == null)
            {
                _canvas = FindObjectOfType<Canvas>();
            }

            if (_canvas == null) return;

            // ツールチップ背景
            _tooltipGo = new GameObject("Tooltip");
            _tooltipGo.transform.SetParent(_canvas.transform, false);

            var img = _tooltipGo.AddComponent<Image>();
            img.color = BgColor;
            img.raycastTarget = false;

            _tooltipRt = _tooltipGo.GetComponent<RectTransform>();
            _tooltipRt.pivot = new Vector2(0f, 0f);
            _tooltipRt.anchorMin = Vector2.zero;
            _tooltipRt.anchorMax = Vector2.zero;

            // テキスト
            var textGo = new GameObject("TooltipText");
            textGo.transform.SetParent(_tooltipGo.transform, false);

            _tooltipText = textGo.AddComponent<Text>();
            _tooltipText.font = ThemeParkGame.Core.FontManager.GetFont() ?? Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            _tooltipText.fontSize = (int)FontSize;
            _tooltipText.color = new Color(0.92f, 0.93f, 0.96f);
            _tooltipText.alignment = TextAnchor.MiddleLeft;
            _tooltipText.horizontalOverflow = HorizontalWrapMode.Wrap;
            _tooltipText.verticalOverflow = VerticalWrapMode.Overflow;
            _tooltipText.raycastTarget = false;

            var textRt = textGo.GetComponent<RectTransform>();
            textRt.anchorMin = Vector2.zero;
            textRt.anchorMax = Vector2.one;
            textRt.offsetMin = new Vector2(PaddingH, PaddingV);
            textRt.offsetMax = new Vector2(-PaddingH, -PaddingV);

            _tooltipGo.SetActive(false);
        }

        /// <summary>ツールチップを表示する</summary>
        public void Show(string text, Vector2 screenPosition)
        {
            if (_tooltipGo == null || _tooltipText == null) return;

            _tooltipText.text = text;
            _tooltipGo.SetActive(true);
            _isShowing = true;

            // テキストサイズを計算してリサイズ
            Canvas.ForceUpdateCanvases();
            float textWidth = Mathf.Min(_tooltipText.preferredWidth + PaddingH * 2f, MaxWidth);
            float textHeight = _tooltipText.preferredHeight + PaddingV * 2f;

            _tooltipRt.sizeDelta = new Vector2(textWidth, textHeight);

            // 画面内に収まるよう位置調整
            float x = screenPosition.x;
            float y = screenPosition.y + OffsetY;

            if (x + textWidth > Screen.width) x = Screen.width - textWidth - 4f;
            if (y + textHeight > Screen.height) y = screenPosition.y - textHeight - 8f;
            if (x < 0f) x = 4f;
            if (y < 0f) y = 4f;

            _tooltipRt.anchoredPosition = new Vector2(x, y);
        }

        /// <summary>ツールチップを非表示にする</summary>
        public void Hide()
        {
            if (_tooltipGo != null)
                _tooltipGo.SetActive(false);
            _isShowing = false;
        }

        /// <summary>マウス位置に追従（表示中のみ）</summary>
        private void LateUpdate()
        {
            if (!_isShowing || _tooltipRt == null) return;

            Vector2 pos = Input.mousePosition;
            float x = pos.x;
            float y = pos.y + OffsetY;

            if (x + _tooltipRt.sizeDelta.x > Screen.width)
                x = Screen.width - _tooltipRt.sizeDelta.x - 4f;
            if (y + _tooltipRt.sizeDelta.y > Screen.height)
                y = pos.y - _tooltipRt.sizeDelta.y - 8f;

            _tooltipRt.anchoredPosition = new Vector2(x, y);
        }
    }
}
