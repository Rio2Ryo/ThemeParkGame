// ============================================================
// ThemeParkGame - ParkExpansionSystem
// 公園拡張システム - 隣接土地の購入によるパーク拡大
// ============================================================

using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using ThemeParkGame.Core;
using ThemeParkGame.Economy;

namespace ThemeParkGame.Park
{
    /// <summary>
    /// 隣接する土地を購入してパークの建設可能エリアを拡大するシステム。
    ///
    /// 【ゲームデザイン】
    /// ・初期パークは中央エリアのみ使用可能
    /// ・東西南北に隣接する土地区画を資金で購入可能
    /// ・購入するとグリッドが拡張され、施設を配置できるようになる
    /// ・遠い区画ほど価格が高い（段階的な拡張を促進）
    /// ・拡張するとパーク評価にボーナスが付与される
    /// </summary>
    public class ParkExpansionSystem : MonoBehaviour
    {
        // ============================================================
        // 定数
        // ============================================================

        private const float BaseLandPrice = 5000f;
        private const float PriceMultiplierPerTier = 1.8f;
        private const float ExpansionRatingBonus = 2f;
        private const int PlotSize = 16; // 1区画 = 16x16グリッド
        private const int MaxExpansionTier = 3; // 最大3段階拡張

        // ============================================================
        // データクラス
        // ============================================================

        [Serializable]
        public class LandPlot
        {
            public string PlotId;
            public string Direction; // N, S, E, W, NE, NW, SE, SW
            public int Tier; // 1=隣接, 2=その次, 3=最遠
            public float Price;
            public bool IsPurchased;
            public int GridOffsetX;
            public int GridOffsetY;

            public string DisplayName
            {
                get
                {
                    string dirName = Direction switch
                    {
                        "N" => "北",
                        "S" => "南",
                        "E" => "東",
                        "W" => "西",
                        "NE" => "北東",
                        "NW" => "北西",
                        "SE" => "南東",
                        "SW" => "南西",
                        _ => Direction
                    };
                    return $"{dirName}エリア Tier{Tier}";
                }
            }
        }

        // ============================================================
        // フィールド
        // ============================================================

        private readonly List<LandPlot> _allPlots = new List<LandPlot>();
        private bool _isInitialized;

        // UI
        private GameObject _uiPanel;
        private GameObject _plotListContent;
        private Text _titleText;
        private Text _totalAreaText;
        private bool _uiVisible;

        // ============================================================
        // プロパティ
        // ============================================================

        public static ParkExpansionSystem Instance { get; private set; }
        public int PurchasedPlotCount { get; private set; }
        public int TotalBuildableArea => (1 + PurchasedPlotCount) * PlotSize * PlotSize;
        public IReadOnlyList<LandPlot> AllPlots => _allPlots;

        // ============================================================
        // 初期化
        // ============================================================

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        public void Initialize()
        {
            if (_isInitialized) return;

            GenerateLandPlots();
            _isInitialized = true;
            WebGLOptimizer.LogVerbose($"[ParkExpansion] 初期化完了。{_allPlots.Count}区画を生成");
        }

        private void GenerateLandPlots()
        {
            _allPlots.Clear();

            // 方角ごとのグリッドオフセット定義
            var directions = new (string dir, int ox, int oy)[]
            {
                ("N", 0, 1), ("S", 0, -1), ("E", 1, 0), ("W", -1, 0),
                ("NE", 1, 1), ("NW", -1, 1), ("SE", 1, -1), ("SW", -1, -1)
            };

            foreach (var (dir, ox, oy) in directions)
            {
                for (int tier = 1; tier <= MaxExpansionTier; tier++)
                {
                    float price = BaseLandPrice * Mathf.Pow(PriceMultiplierPerTier, tier - 1);

                    // 斜め方向は割増
                    if (dir.Length == 2) price *= 1.3f;

                    _allPlots.Add(new LandPlot
                    {
                        PlotId = $"{dir}_T{tier}",
                        Direction = dir,
                        Tier = tier,
                        Price = Mathf.Round(price / 100f) * 100f,
                        IsPurchased = false,
                        GridOffsetX = ox * tier * PlotSize,
                        GridOffsetY = oy * tier * PlotSize
                    });
                }
            }
        }

        // ============================================================
        // 土地購入
        // ============================================================

        /// <summary>土地区画を購入する</summary>
        public bool PurchasePlot(string plotId)
        {
            var plot = _allPlots.Find(p => p.PlotId == plotId);
            if (plot == null)
            {
                WebGLOptimizer.LogVerbose($"[ParkExpansion] 区画 {plotId} が見つかりません");
                return false;
            }

            if (plot.IsPurchased)
            {
                WebGLOptimizer.LogVerbose($"[ParkExpansion] {plot.DisplayName} は既に購入済み");
                return false;
            }

            // 前のTierが購入済みか確認（Tier2はTier1が必要、Tier3はTier2が必要）
            if (plot.Tier > 1)
            {
                string prevPlotId = $"{plot.Direction}_T{plot.Tier - 1}";
                var prevPlot = _allPlots.Find(p => p.PlotId == prevPlotId);
                if (prevPlot != null && !prevPlot.IsPurchased)
                {
                    WebGLOptimizer.LogVerbose($"[ParkExpansion] 先に {prevPlot.DisplayName} を購入してください");
                    return false;
                }
            }

            // 資金確認・支払い
            var econ = GameManager.Instance?.EconomyManager;
            if (econ == null) return false;

            if (!econ.PayExpense(plot.Price, ExpenseCategory.Construction))
            {
                WebGLOptimizer.LogVerbose($"[ParkExpansion] 資金不足。必要: ${plot.Price:F0}");
                return false;
            }

            plot.IsPurchased = true;
            PurchasedPlotCount++;

            // パーク評価にボーナス
            var pm = GameManager.Instance?.ParkManager;
            if (pm?.Rating != null)
            {
                pm.Rating.ApplyExternalBonus(CertificateCategory.Fame, ExpansionRatingBonus);
            }

            // 通知
            GameManager.Instance?.ShowNotification(
                $"🏗 {plot.DisplayName} を購入しました！", NotifLevel.Success);

            WebGLOptimizer.LogVerbose(
                $"[ParkExpansion] {plot.DisplayName} を購入 (${plot.Price:F0})。合計{PurchasedPlotCount}区画");

            RefreshUI();
            return true;
        }

        /// <summary>購入可能な区画のリストを取得する</summary>
        public List<LandPlot> GetAvailablePlots()
        {
            var available = new List<LandPlot>();
            foreach (var plot in _allPlots)
            {
                if (plot.IsPurchased) continue;

                // Tier1は常に購入可能、Tier2以降は前のTierが必要
                if (plot.Tier == 1)
                {
                    available.Add(plot);
                }
                else
                {
                    string prevId = $"{plot.Direction}_T{plot.Tier - 1}";
                    var prev = _allPlots.Find(p => p.PlotId == prevId);
                    if (prev != null && prev.IsPurchased)
                    {
                        available.Add(plot);
                    }
                }
            }
            return available;
        }

        /// <summary>特定の区画が購入済みかを確認する</summary>
        public bool IsPlotPurchased(string plotId)
        {
            var plot = _allPlots.Find(p => p.PlotId == plotId);
            return plot != null && plot.IsPurchased;
        }

        // ============================================================
        // セーブ/ロード
        // ============================================================

        /// <summary>購入済み区画IDのリストを返す（セーブ用）</summary>
        public List<string> GetPurchasedPlotIds()
        {
            var ids = new List<string>();
            foreach (var plot in _allPlots)
            {
                if (plot.IsPurchased) ids.Add(plot.PlotId);
            }
            return ids;
        }

        /// <summary>セーブデータから購入状態を復元する</summary>
        public void RestorePurchasedPlots(List<string> plotIds)
        {
            if (plotIds == null) return;

            PurchasedPlotCount = 0;
            foreach (var plot in _allPlots)
            {
                plot.IsPurchased = plotIds.Contains(plot.PlotId);
                if (plot.IsPurchased) PurchasedPlotCount++;
            }

            WebGLOptimizer.LogVerbose($"[ParkExpansion] {PurchasedPlotCount}区画を復元");
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
            var canvas = UnityEngine.Object.FindObjectOfType<Canvas>();
            if (canvas == null) return;

            // メインパネル
            _uiPanel = new GameObject("ParkExpansionPanel");
            _uiPanel.transform.SetParent(canvas.transform, false);
            var panelRect = _uiPanel.AddComponent<RectTransform>();
            panelRect.anchorMin = new Vector2(0.2f, 0.1f);
            panelRect.anchorMax = new Vector2(0.8f, 0.9f);
            panelRect.offsetMin = Vector2.zero;
            panelRect.offsetMax = Vector2.zero;

            var panelImg = _uiPanel.AddComponent<Image>();
            panelImg.color = new Color(0.1f, 0.12f, 0.18f, 0.95f);

            // タイトル
            var titleObj = new GameObject("Title");
            titleObj.transform.SetParent(_uiPanel.transform, false);
            var titleRect = titleObj.AddComponent<RectTransform>();
            titleRect.anchorMin = new Vector2(0f, 0.9f);
            titleRect.anchorMax = new Vector2(1f, 1f);
            titleRect.offsetMin = new Vector2(10f, 0f);
            titleRect.offsetMax = new Vector2(-60f, -5f);
            _titleText = titleObj.AddComponent<Text>();
            _titleText.text = "パーク拡張 - 土地購入";
            _titleText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            _titleText.fontSize = 22;
            _titleText.color = Color.white;
            _titleText.alignment = TextAnchor.MiddleCenter;
            _titleText.fontStyle = FontStyle.Bold;

            // 閉じるボタン
            var closeObj = new GameObject("CloseBtn");
            closeObj.transform.SetParent(_uiPanel.transform, false);
            var closeRect = closeObj.AddComponent<RectTransform>();
            closeRect.anchorMin = new Vector2(1f, 0.9f);
            closeRect.anchorMax = new Vector2(1f, 1f);
            closeRect.offsetMin = new Vector2(-50f, 0f);
            closeRect.offsetMax = new Vector2(-5f, -5f);
            var closeBtnImg = closeObj.AddComponent<Image>();
            closeBtnImg.color = new Color(0.8f, 0.2f, 0.2f);
            var closeBtn = closeObj.AddComponent<Button>();
            closeBtn.targetGraphic = closeBtnImg;
            closeBtn.onClick.AddListener(HideUI);
            var closeTxt = new GameObject("Text").AddComponent<Text>();
            closeTxt.transform.SetParent(closeObj.transform, false);
            var closeTxtRect = closeTxt.GetComponent<RectTransform>();
            closeTxtRect.anchorMin = Vector2.zero;
            closeTxtRect.anchorMax = Vector2.one;
            closeTxtRect.offsetMin = Vector2.zero;
            closeTxtRect.offsetMax = Vector2.zero;
            closeTxt.text = "X";
            closeTxt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            closeTxt.fontSize = 20;
            closeTxt.color = Color.white;
            closeTxt.alignment = TextAnchor.MiddleCenter;

            // 総面積表示
            var areaObj = new GameObject("TotalArea");
            areaObj.transform.SetParent(_uiPanel.transform, false);
            var areaRect = areaObj.AddComponent<RectTransform>();
            areaRect.anchorMin = new Vector2(0f, 0.83f);
            areaRect.anchorMax = new Vector2(1f, 0.9f);
            areaRect.offsetMin = new Vector2(10f, 0f);
            areaRect.offsetMax = new Vector2(-10f, 0f);
            _totalAreaText = areaObj.AddComponent<Text>();
            _totalAreaText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            _totalAreaText.fontSize = 16;
            _totalAreaText.color = new Color(0.7f, 0.9f, 1f);
            _totalAreaText.alignment = TextAnchor.MiddleCenter;

            // スクロールリスト
            var scrollObj = new GameObject("ScrollView");
            scrollObj.transform.SetParent(_uiPanel.transform, false);
            var scrollRect = scrollObj.AddComponent<RectTransform>();
            scrollRect.anchorMin = new Vector2(0.02f, 0.02f);
            scrollRect.anchorMax = new Vector2(0.98f, 0.82f);
            scrollRect.offsetMin = Vector2.zero;
            scrollRect.offsetMax = Vector2.zero;

            var scrollView = scrollObj.AddComponent<ScrollRect>();
            var scrollImg = scrollObj.AddComponent<Image>();
            scrollImg.color = new Color(0.08f, 0.08f, 0.12f, 0.8f);
            scrollObj.AddComponent<Mask>().showMaskGraphic = true;

            var contentObj = new GameObject("Content");
            contentObj.transform.SetParent(scrollObj.transform, false);
            var contentRect = contentObj.AddComponent<RectTransform>();
            contentRect.anchorMin = new Vector2(0f, 1f);
            contentRect.anchorMax = new Vector2(1f, 1f);
            contentRect.pivot = new Vector2(0.5f, 1f);
            contentRect.offsetMin = Vector2.zero;
            contentRect.offsetMax = Vector2.zero;

            var layout = contentObj.AddComponent<VerticalLayoutGroup>();
            layout.childAlignment = TextAnchor.UpperCenter;
            layout.spacing = 4f;
            layout.padding = new RectOffset(5, 5, 5, 5);
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;

            var fitter = contentObj.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            scrollView.content = contentRect;
            scrollView.vertical = true;
            scrollView.horizontal = false;

            _plotListContent = contentObj;
            _uiPanel.SetActive(false);
        }

        private void RefreshUI()
        {
            if (_plotListContent == null) return;

            // 既存のリストアイテムを削除
            for (int i = _plotListContent.transform.childCount - 1; i >= 0; i--)
            {
                Destroy(_plotListContent.transform.GetChild(i).gameObject);
            }

            // 総面積表示
            if (_totalAreaText != null)
            {
                float money = GameManager.Instance?.EconomyManager?.CurrentMoney ?? 0f;
                _totalAreaText.text = $"建設可能面積: {TotalBuildableArea}マス | " +
                    $"購入済み: {PurchasedPlotCount}/{_allPlots.Count}区画 | " +
                    $"所持金: ${money:N0}";
            }

            // 全区画を表示
            foreach (var plot in _allPlots)
            {
                CreatePlotListItem(plot);
            }
        }

        private void CreatePlotListItem(LandPlot plot)
        {
            var itemObj = new GameObject($"Plot_{plot.PlotId}");
            itemObj.transform.SetParent(_plotListContent.transform, false);
            var itemLayout = itemObj.AddComponent<LayoutElement>();
            itemLayout.preferredHeight = 50f;

            var itemImg = itemObj.AddComponent<Image>();

            if (plot.IsPurchased)
            {
                itemImg.color = new Color(0.15f, 0.35f, 0.15f, 0.8f);
            }
            else
            {
                bool canBuy = CanPurchasePlot(plot);
                itemImg.color = canBuy
                    ? new Color(0.15f, 0.2f, 0.35f, 0.8f)
                    : new Color(0.25f, 0.15f, 0.15f, 0.6f);
            }

            // テキスト
            var textObj = new GameObject("Text");
            textObj.transform.SetParent(itemObj.transform, false);
            var textRect = textObj.AddComponent<RectTransform>();
            textRect.anchorMin = new Vector2(0f, 0f);
            textRect.anchorMax = new Vector2(0.65f, 1f);
            textRect.offsetMin = new Vector2(10f, 0f);
            textRect.offsetMax = Vector2.zero;
            var text = textObj.AddComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = 14;
            text.color = Color.white;
            text.alignment = TextAnchor.MiddleLeft;

            string status = plot.IsPurchased ? "[購入済]" : $"${plot.Price:N0}";
            text.text = $"{plot.DisplayName}  ({PlotSize}x{PlotSize}マス)  {status}";

            // 購入ボタン
            if (!plot.IsPurchased)
            {
                bool canBuy = CanPurchasePlot(plot);

                var btnObj = new GameObject("BuyBtn");
                btnObj.transform.SetParent(itemObj.transform, false);
                var btnRect = btnObj.AddComponent<RectTransform>();
                btnRect.anchorMin = new Vector2(0.7f, 0.1f);
                btnRect.anchorMax = new Vector2(0.98f, 0.9f);
                btnRect.offsetMin = Vector2.zero;
                btnRect.offsetMax = Vector2.zero;

                var btnImg = btnObj.AddComponent<Image>();
                btnImg.color = canBuy
                    ? new Color(0.2f, 0.6f, 0.2f)
                    : new Color(0.4f, 0.4f, 0.4f);

                var btn = btnObj.AddComponent<Button>();
                btn.targetGraphic = btnImg;
                btn.interactable = canBuy;

                string plotId = plot.PlotId;
                btn.onClick.AddListener(() => OnBuyPlotClicked(plotId));

                var btnTxt = new GameObject("Text").AddComponent<Text>();
                btnTxt.transform.SetParent(btnObj.transform, false);
                var btnTxtRect = btnTxt.GetComponent<RectTransform>();
                btnTxtRect.anchorMin = Vector2.zero;
                btnTxtRect.anchorMax = Vector2.one;
                btnTxtRect.offsetMin = Vector2.zero;
                btnTxtRect.offsetMax = Vector2.zero;
                btnTxt.text = canBuy ? "購入" : "不可";
                btnTxt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                btnTxt.fontSize = 14;
                btnTxt.color = Color.white;
                btnTxt.alignment = TextAnchor.MiddleCenter;
            }
        }

        private bool CanPurchasePlot(LandPlot plot)
        {
            if (plot.IsPurchased) return false;

            // 前のTierが必要
            if (plot.Tier > 1)
            {
                string prevId = $"{plot.Direction}_T{plot.Tier - 1}";
                var prev = _allPlots.Find(p => p.PlotId == prevId);
                if (prev == null || !prev.IsPurchased) return false;
            }

            // 資金チェック
            float money = GameManager.Instance?.EconomyManager?.CurrentMoney ?? 0f;
            return money >= plot.Price;
        }

        private void OnBuyPlotClicked(string plotId)
        {
            if (PurchasePlot(plotId))
            {
                RefreshUI();
            }
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }
    }
}
