// ============================================================
// ThemeParkGame - RuntimeBuildPanel
// コードベースの建設パネルUI（プレハブ/TMPro不要）
// attractions.json / shops.json / facilities.json から
// 建設可能アイテムを読み込み、配置操作を提供する
// ============================================================

using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using ThemeParkGame.Core;
using ThemeParkGame.Attraction;
using ThemeParkGame.Park;

namespace ThemeParkGame.UI
{
    /// <summary>
    /// ランタイムで完全にコードから構築される建設パネルUI。
    /// カテゴリ選択 → アイテム一覧 → 配置モード の流れで
    /// プレイヤーがパーク内に新しい施設を建設できる。
    /// </summary>
    public class RuntimeBuildPanel : MonoBehaviour
    {
        public static RuntimeBuildPanel Instance { get; private set; }

        // ---- Canvas ----
        private Canvas _canvas;
        private RectTransform _canvasRoot;
        private GameObject _panelRoot;
        private bool _isOpen;

        // ---- カテゴリタブ ----
        private readonly string[] _categoryNames = {
            "アトラクション", "フード", "ドリンク", "おみやげ",
            "トイレ", "ベンチ", "ゴミ箱", "案内板",
            "通路", "装飾", "休憩室", "研究所"
        };
        private readonly string[] _categoryTypes = {
            "Attraction", "FoodShop", "DrinkShop", "SouvenirShop",
            "Toilet", "Bench", "TrashCan", "InfoBoard",
            "Pathway", "Decoration", "StaffRoom", "ResearchLab"
        };
        private Button[] _tabButtons;
        private int _selectedCategoryIndex;

        // ---- アイテムリスト ----
        private RectTransform _itemListContainer;
        private readonly List<GameObject> _itemCards = new List<GameObject>();

        // ---- 詳細パネル ----
        private GameObject _detailPanel;
        private Text _detailName;
        private Text _detailDesc;
        private Text _detailCost;
        private Text _detailStats;
        private Button _placeButton;
        private Text _placeButtonText;

        // ---- 配置モード ----
        private bool _isPlacing;
        private BuildableItem _selectedItem;
        private GameObject _placementPreview;
        private bool _placementValid;
        private Text _placementHint;
        private GameObject _placementHintGo;

        // ---- データ ----
        private List<BuildableItem> _allItems;

        // ---- カラー定数 ----
        private static readonly Color BgDark = new Color(0.06f, 0.09f, 0.16f, 0.94f);
        private static readonly Color BgMedium = new Color(0.12f, 0.15f, 0.22f, 0.94f);
        private static readonly Color BgLight = new Color(0.18f, 0.22f, 0.3f, 0.9f);
        private static readonly Color AccentBlue = new Color(0.3f, 0.65f, 0.95f);
        private static readonly Color AccentGreen = new Color(0.35f, 0.9f, 0.45f);
        private static readonly Color AccentRed = new Color(0.95f, 0.35f, 0.35f);
        private static readonly Color TextWhite = new Color(0.92f, 0.93f, 0.95f);
        private static readonly Color TextMuted = new Color(0.6f, 0.62f, 0.68f);
        private static readonly Color TabActive = new Color(0.25f, 0.55f, 0.85f, 0.95f);
        private static readonly Color TabNormal = new Color(0.15f, 0.18f, 0.25f, 0.9f);

        // ================================================================
        // データモデル
        // ================================================================

        [Serializable]
        public class BuildableItem
        {
            public string Id;
            public string NameJa;
            public string NameEn;
            public string Description;
            public string Type; // Attraction / FoodShop / DrinkShop / etc.
            public string Category; // GForce, Observation, etc. (attractions only)
            public string ThemeZone;
            public int BuildCost;
            public int MaintenanceCost;
            public float ExcitementRating;
            public float NauseaFactor;
            public int Capacity;
            public float RideDuration;
            public int GridWidth;
            public int GridHeight;
            public int SuggestedPrice;
            public int WholesalePrice;
            public string RequiredResearchId;
        }

        // JSON wrapper classes
        [Serializable] private class AttractionListWrapper { public AttractionEntry[] attractions; }
        [Serializable] private class ShopListWrapper { public ShopEntry[] shops; }
        [Serializable] private class FacilityListWrapper { public FacilityEntry[] facilities; }

        [Serializable]
        private class AttractionEntry
        {
            public string id;
            public string nameJa;
            public string nameEn;
            public string description;
            public string category;
            public string themeZone;
            public float excitementRating;
            public float nauseaFactor;
            public int capacity;
            public float rideDurationSeconds;
            public int buildCost;
            public int maintenanceCostPerMonth;
            public int gridWidth;
            public int gridHeight;
            public string requiredResearchId;
        }

        [Serializable]
        private class ShopEntry
        {
            public string id;
            public string nameJa;
            public string nameEn;
            public string type;
            public string themeZone;
            public int buildCost;
            public int wholesalePrice;
            public int suggestedRetailPrice;
            public string requiredResearchId;
            public string description;
        }

        [Serializable]
        private class FacilityEntry
        {
            public string id;
            public string nameJa;
            public string nameEn;
            public string type;
            public int buildCost;
            public int maintenanceCostPerMonth;
            public int gridWidth;
            public int gridHeight;
            public int capacity;
            public string requiredResearchId;
            public string description;
        }

        // ================================================================
        // ライフサイクル
        // ================================================================

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(this); return; }
            Instance = this;
            LoadBuildData();
            BuildUI();
            _panelRoot.SetActive(false);
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        private void Update()
        {
            if (!_isOpen) return;

            if (_isPlacing)
            {
                UpdatePlacementPreview();

                // ESCキーまたは右クリックでキャンセル
                if (Input.GetKeyDown(KeyCode.Escape) || Input.GetMouseButtonDown(1))
                {
                    CancelPlacement();
                }
            }
        }

        // ================================================================
        // 公開API
        // ================================================================

        public bool IsOpen => _isOpen;

        public void Toggle()
        {
            if (_isOpen) Close();
            else Open();
        }

        public void Open()
        {
            if (_isOpen) return;
            _isOpen = true;
            _panelRoot.SetActive(true);
            SelectCategory(0);
        }

        public void Close()
        {
            if (!_isOpen) return;
            CancelPlacement();
            _isOpen = false;
            _panelRoot.SetActive(false);
        }

        // ================================================================
        // データ読み込み
        // ================================================================

        private void LoadBuildData()
        {
            _allItems = new List<BuildableItem>();

            // attractions.json
            var attrJson = Resources.Load<TextAsset>("Config/attractions");
            if (attrJson != null)
            {
                try
                {
                    var wrapper = JsonUtility.FromJson<AttractionListWrapper>(attrJson.text);
                    if (wrapper?.attractions != null)
                    {
                        foreach (var a in wrapper.attractions)
                        {
                            _allItems.Add(new BuildableItem
                            {
                                Id = a.id,
                                NameJa = a.nameJa,
                                NameEn = a.nameEn,
                                Description = a.description,
                                Type = "Attraction",
                                Category = a.category,
                                ThemeZone = a.themeZone,
                                BuildCost = a.buildCost,
                                MaintenanceCost = a.maintenanceCostPerMonth,
                                ExcitementRating = a.excitementRating,
                                NauseaFactor = a.nauseaFactor,
                                Capacity = a.capacity,
                                RideDuration = a.rideDurationSeconds,
                                GridWidth = a.gridWidth,
                                GridHeight = a.gridHeight,
                                SuggestedPrice = Mathf.RoundToInt(a.excitementRating * 6f),
                                RequiredResearchId = a.requiredResearchId
                            });
                        }
                    }
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"[RuntimeBuildPanel] attractions.json parse error: {e.Message}");
                }
            }

            // shops.json
            var shopJson = Resources.Load<TextAsset>("Config/shops");
            if (shopJson != null)
            {
                try
                {
                    var wrapper = JsonUtility.FromJson<ShopListWrapper>(shopJson.text);
                    if (wrapper?.shops != null)
                    {
                        foreach (var s in wrapper.shops)
                        {
                            _allItems.Add(new BuildableItem
                            {
                                Id = s.id,
                                NameJa = s.nameJa,
                                NameEn = s.nameEn,
                                Description = s.description,
                                Type = s.type,
                                ThemeZone = s.themeZone,
                                BuildCost = s.buildCost,
                                WholesalePrice = s.wholesalePrice,
                                SuggestedPrice = s.suggestedRetailPrice,
                                RequiredResearchId = s.requiredResearchId
                            });
                        }
                    }
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"[RuntimeBuildPanel] shops.json parse error: {e.Message}");
                }
            }

            // facilities.json
            var facJson = Resources.Load<TextAsset>("Config/facilities");
            if (facJson != null)
            {
                try
                {
                    var wrapper = JsonUtility.FromJson<FacilityListWrapper>(facJson.text);
                    if (wrapper?.facilities != null)
                    {
                        foreach (var f in wrapper.facilities)
                        {
                            _allItems.Add(new BuildableItem
                            {
                                Id = f.id,
                                NameJa = f.nameJa,
                                NameEn = f.nameEn,
                                Description = f.description,
                                Type = f.type,
                                BuildCost = f.buildCost,
                                MaintenanceCost = f.maintenanceCostPerMonth,
                                Capacity = f.capacity,
                                GridWidth = f.gridWidth,
                                GridHeight = f.gridHeight,
                                RequiredResearchId = f.requiredResearchId
                            });
                        }
                    }
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"[RuntimeBuildPanel] facilities.json parse error: {e.Message}");
                }
            }

            Debug.Log($"[RuntimeBuildPanel] Loaded {_allItems.Count} buildable items");
        }

        // ================================================================
        // UI構築
        // ================================================================

        private void BuildUI()
        {
            // Canvas
            var cGo = new GameObject("BuildPanel_Canvas");
            cGo.transform.SetParent(transform, false);
            _canvas = cGo.AddComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.sortingOrder = 55; // HUD(50)より上

            var scaler = cGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;

            cGo.AddComponent<GraphicRaycaster>();
            _canvasRoot = cGo.GetComponent<RectTransform>();

            // パネルルート（画面下部）
            _panelRoot = MakePanel(_canvasRoot, "BuildPanelRoot", 0f, 380f, BgDark);
            var panelRt = _panelRoot.GetComponent<RectTransform>();
            panelRt.anchorMin = new Vector2(0f, 0f);
            panelRt.anchorMax = new Vector2(1f, 0f);
            panelRt.pivot = new Vector2(0.5f, 0f);
            panelRt.anchoredPosition = Vector2.zero;
            panelRt.sizeDelta = new Vector2(0f, 380f);

            // ヘッダー
            BuildHeader(panelRt);

            // カテゴリタブ（左側）
            BuildCategoryTabs(panelRt);

            // アイテムリスト（中央）
            BuildItemList(panelRt);

            // 詳細パネル（右側）
            BuildDetailPanel(panelRt);

            // 配置ヒント
            BuildPlacementHint();
        }

        private void BuildHeader(RectTransform parent)
        {
            var header = MakePanel(parent, "Header", 0f, 40f, new Color(0.1f, 0.13f, 0.2f, 0.98f));
            var hrt = header.GetComponent<RectTransform>();
            hrt.anchorMin = new Vector2(0f, 1f);
            hrt.anchorMax = new Vector2(1f, 1f);
            hrt.pivot = new Vector2(0.5f, 1f);
            hrt.anchoredPosition = Vector2.zero;
            hrt.sizeDelta = new Vector2(0f, 40f);

            var title = MakeLabel(hrt, "Title", "BUILD - 建設パネル", 20,
                AccentBlue, FontStyle.Bold, TextAnchor.MiddleLeft);
            var trt = title.rectTransform;
            trt.anchorMin = new Vector2(0f, 0f);
            trt.anchorMax = new Vector2(0.5f, 1f);
            trt.offsetMin = new Vector2(16f, 0f);
            trt.offsetMax = Vector2.zero;

            // 閉じるボタン
            var closeGo = MakePanel(hrt, "CloseBtn", 80f, 30f, AccentRed);
            var closeRt = closeGo.GetComponent<RectTransform>();
            closeRt.anchorMin = closeRt.anchorMax = new Vector2(1f, 0.5f);
            closeRt.pivot = new Vector2(1f, 0.5f);
            closeRt.anchoredPosition = new Vector2(-10f, 0f);
            var closeImg = closeGo.GetComponent<Image>();
            closeImg.raycastTarget = true;
            var closeBtn = closeGo.AddComponent<Button>();
            closeBtn.targetGraphic = closeImg;
            closeBtn.onClick.AddListener(Close);
            var closeLabel = MakeLabel(closeRt, "X", "CLOSE", 14,
                Color.white, FontStyle.Bold, TextAnchor.MiddleCenter);
            StretchFill(closeLabel.rectTransform);

            // 所持金表示
            var moneyLabel = MakeLabel(hrt, "Money", "", 16,
                new Color(0.95f, 0.88f, 0.45f), FontStyle.Bold, TextAnchor.MiddleRight);
            var mrt = moneyLabel.rectTransform;
            mrt.anchorMin = new Vector2(0.5f, 0f);
            mrt.anchorMax = new Vector2(0.85f, 1f);
            mrt.offsetMin = Vector2.zero;
            mrt.offsetMax = new Vector2(-10f, 0f);
            _headerMoneyText = moneyLabel;
        }

        private Text _headerMoneyText;

        private void BuildCategoryTabs(RectTransform parent)
        {
            float tabW = 140f;
            float tabH = 28f;

            var tabArea = MakePanel(parent, "TabArea", tabW, 336f, new Color(0.08f, 0.1f, 0.16f, 0.9f));
            var tarRt = tabArea.GetComponent<RectTransform>();
            tarRt.anchorMin = new Vector2(0f, 0f);
            tarRt.anchorMax = new Vector2(0f, 1f);
            tarRt.pivot = new Vector2(0f, 1f);
            tarRt.anchoredPosition = new Vector2(0f, -40f);
            tarRt.sizeDelta = new Vector2(tabW, -40f);

            _tabButtons = new Button[_categoryNames.Length];

            for (int i = 0; i < _categoryNames.Length; i++)
            {
                float y = -(i * (tabH + 2f));
                var tabGo = MakePanel(tarRt, $"Tab_{i}", tabW - 4f, tabH, TabNormal);
                var trt = tabGo.GetComponent<RectTransform>();
                trt.anchorMin = trt.anchorMax = new Vector2(0.5f, 1f);
                trt.pivot = new Vector2(0.5f, 1f);
                trt.anchoredPosition = new Vector2(0f, y - 2f);

                var img = tabGo.GetComponent<Image>();
                img.raycastTarget = true;
                var btn = tabGo.AddComponent<Button>();
                btn.targetGraphic = img;
                _tabButtons[i] = btn;

                var label = MakeLabel(trt, "Label", _categoryNames[i], 13,
                    TextWhite, FontStyle.Normal, TextAnchor.MiddleCenter);
                StretchFill(label.rectTransform);

                int capturedIndex = i;
                btn.onClick.AddListener(() => SelectCategory(capturedIndex));
            }
        }

        private void BuildItemList(RectTransform parent)
        {
            // ScrollRect領域
            float leftOffset = 144f;
            float rightOffset = 320f;

            var scrollArea = new GameObject("ItemScrollArea");
            scrollArea.transform.SetParent(parent, false);
            var scrollRt = scrollArea.AddComponent<RectTransform>();
            scrollRt.anchorMin = new Vector2(0f, 0f);
            scrollRt.anchorMax = new Vector2(1f, 1f);
            scrollRt.offsetMin = new Vector2(leftOffset, 4f);
            scrollRt.offsetMax = new Vector2(-rightOffset, -44f);

            var scrollImg = scrollArea.AddComponent<Image>();
            scrollImg.color = new Color(0.1f, 0.12f, 0.18f, 0.85f);

            var scrollRect = scrollArea.AddComponent<ScrollRect>();
            scrollRect.horizontal = false;
            scrollRect.vertical = true;
            scrollRect.movementType = ScrollRect.MovementType.Clamped;

            // Mask
            scrollArea.AddComponent<Mask>().showMaskGraphic = true;

            // Content
            var content = new GameObject("Content");
            content.transform.SetParent(scrollArea.transform, false);
            _itemListContainer = content.AddComponent<RectTransform>();
            _itemListContainer.anchorMin = new Vector2(0f, 1f);
            _itemListContainer.anchorMax = new Vector2(1f, 1f);
            _itemListContainer.pivot = new Vector2(0.5f, 1f);
            _itemListContainer.anchoredPosition = Vector2.zero;
            _itemListContainer.sizeDelta = new Vector2(0f, 0f);

            // ContentSizeFitter
            var fitter = content.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            // VerticalLayoutGroup
            var vlg = content.AddComponent<VerticalLayoutGroup>();
            vlg.spacing = 2f;
            vlg.padding = new RectOffset(4, 4, 4, 4);
            vlg.childForceExpandWidth = true;
            vlg.childForceExpandHeight = false;
            vlg.childControlWidth = true;
            vlg.childControlHeight = true;

            scrollRect.content = _itemListContainer;
        }

        private void BuildDetailPanel(RectTransform parent)
        {
            float panelW = 316f;

            _detailPanel = MakePanel(parent, "DetailPanel", panelW, 0f, BgMedium);
            var dprt = _detailPanel.GetComponent<RectTransform>();
            dprt.anchorMin = new Vector2(1f, 0f);
            dprt.anchorMax = new Vector2(1f, 1f);
            dprt.pivot = new Vector2(1f, 0.5f);
            dprt.anchoredPosition = Vector2.zero;
            dprt.sizeDelta = new Vector2(panelW, -40f);
            dprt.offsetMin = new Vector2(-panelW, 4f);
            dprt.offsetMax = new Vector2(0f, -44f);

            float y = -10f;
            float spacing = 22f;

            _detailName = MakeLabel(dprt, "Name", "", 18,
                AccentBlue, FontStyle.Bold, TextAnchor.UpperLeft);
            PlaceInParent(_detailName.rectTransform, 10f, y, panelW - 20f, 26f);
            y -= 30f;

            _detailDesc = MakeLabel(dprt, "Desc", "", 12,
                TextMuted, FontStyle.Normal, TextAnchor.UpperLeft);
            _detailDesc.horizontalOverflow = HorizontalWrapMode.Wrap;
            _detailDesc.verticalOverflow = VerticalWrapMode.Truncate;
            PlaceInParent(_detailDesc.rectTransform, 10f, y, panelW - 20f, 60f);
            y -= 65f;

            _detailCost = MakeLabel(dprt, "Cost", "", 16,
                new Color(0.95f, 0.88f, 0.45f), FontStyle.Bold, TextAnchor.MiddleLeft);
            PlaceInParent(_detailCost.rectTransform, 10f, y, panelW - 20f, spacing);
            y -= spacing + 4f;

            _detailStats = MakeLabel(dprt, "Stats", "", 13,
                TextWhite, FontStyle.Normal, TextAnchor.UpperLeft);
            _detailStats.horizontalOverflow = HorizontalWrapMode.Wrap;
            PlaceInParent(_detailStats.rectTransform, 10f, y, panelW - 20f, 100f);
            y -= 105f;

            // 配置ボタン
            var placeBtnGo = MakePanel(dprt, "PlaceBtn", panelW - 20f, 40f, AccentGreen);
            var pbrt = placeBtnGo.GetComponent<RectTransform>();
            pbrt.anchorMin = pbrt.anchorMax = new Vector2(0.5f, 0f);
            pbrt.pivot = new Vector2(0.5f, 0f);
            pbrt.anchoredPosition = new Vector2(0f, 10f);
            var placeImg = placeBtnGo.GetComponent<Image>();
            placeImg.raycastTarget = true;
            _placeButton = placeBtnGo.AddComponent<Button>();
            _placeButton.targetGraphic = placeImg;
            _placeButton.onClick.AddListener(OnPlaceClicked);

            _placeButtonText = MakeLabel(pbrt, "Label", "配置する", 16,
                new Color(0.05f, 0.1f, 0.05f), FontStyle.Bold, TextAnchor.MiddleCenter);
            StretchFill(_placeButtonText.rectTransform);

            _detailPanel.SetActive(false);
        }

        private void BuildPlacementHint()
        {
            _placementHintGo = MakePanel(_canvasRoot, "PlacementHint", 400f, 36f,
                new Color(0.05f, 0.08f, 0.15f, 0.9f));
            var hrt = _placementHintGo.GetComponent<RectTransform>();
            hrt.anchorMin = hrt.anchorMax = new Vector2(0.5f, 0.5f);
            hrt.pivot = new Vector2(0.5f, 0.5f);
            hrt.anchoredPosition = new Vector2(0f, 200f);

            _placementHint = MakeLabel(hrt, "Hint",
                "クリックで配置 | 右クリック/ESCでキャンセル", 15,
                AccentBlue, FontStyle.Bold, TextAnchor.MiddleCenter);
            StretchFill(_placementHint.rectTransform);

            _placementHintGo.SetActive(false);
        }

        // ================================================================
        // カテゴリ選択
        // ================================================================

        private void SelectCategory(int index)
        {
            _selectedCategoryIndex = index;

            // タブの色を更新
            for (int i = 0; i < _tabButtons.Length; i++)
            {
                if (_tabButtons[i] == null) continue;
                var img = _tabButtons[i].GetComponent<Image>();
                if (img != null) img.color = (i == index) ? TabActive : TabNormal;
            }

            PopulateItemList();
            _detailPanel.SetActive(false);
        }

        // ================================================================
        // アイテムリスト更新
        // ================================================================

        private void PopulateItemList()
        {
            // 既存カードをクリア
            foreach (var card in _itemCards)
                if (card != null) Destroy(card);
            _itemCards.Clear();

            string targetType = _categoryTypes[_selectedCategoryIndex];

            // アトラクションカテゴリは全サブカテゴリをまとめて表示
            var filteredItems = new List<BuildableItem>();
            foreach (var item in _allItems)
            {
                if (targetType == "Attraction" && item.Type == "Attraction")
                    filteredItems.Add(item);
                else if (item.Type == targetType)
                    filteredItems.Add(item);
            }

            float currentMoney = GameManager.Instance?.EconomyManager?.CurrentBalance ?? 0f;

            foreach (var item in filteredItems)
            {
                var card = CreateItemCard(item, currentMoney);
                _itemCards.Add(card);
            }

            // 所持金更新
            if (_headerMoneyText != null)
                _headerMoneyText.text = $"所持金: ${currentMoney:N0}";
        }

        private bool IsResearchUnlocked(string requiredResearchId)
        {
            if (string.IsNullOrEmpty(requiredResearchId)) return true;
            var rm = GameManager.Instance?.ResearchManager;
            if (rm == null) return true; // no research system = all unlocked
            return rm.IsResearchCompleted(requiredResearchId);
        }

        private GameObject CreateItemCard(BuildableItem item, float currentMoney)
        {
            bool researched = IsResearchUnlocked(item.RequiredResearchId);
            bool canAfford = currentMoney >= item.BuildCost;
            bool canBuild = researched && canAfford;
            float cardH = 52f;

            var cardGo = new GameObject($"Card_{item.Id}");
            cardGo.transform.SetParent(_itemListContainer, false);
            var cardRt = cardGo.AddComponent<RectTransform>();
            cardRt.sizeDelta = new Vector2(0f, cardH);

            var le = cardGo.AddComponent<LayoutElement>();
            le.minHeight = cardH;
            le.preferredHeight = cardH;

            var bgImg = cardGo.AddComponent<Image>();
            bgImg.color = !researched ? new Color(0.12f, 0.12f, 0.15f, 0.6f)
                         : canAfford ? BgLight
                         : new Color(0.15f, 0.15f, 0.18f, 0.7f);
            bgImg.raycastTarget = true;

            var btn = cardGo.AddComponent<Button>();
            btn.targetGraphic = bgImg;
            btn.interactable = researched;
            var colors = btn.colors;
            colors.highlightedColor = new Color(0.25f, 0.35f, 0.5f, 0.95f);
            colors.pressedColor = new Color(0.2f, 0.25f, 0.4f, 0.95f);
            btn.colors = colors;

            BuildableItem capturedItem = item;
            btn.onClick.AddListener(() => SelectItem(capturedItem));

            // 名前（未研究はロック表示）
            string displayName = researched ? item.NameJa : $"[要研究] {item.NameJa}";
            var nameText = MakeLabel(cardRt, "Name", displayName, 14,
                !researched ? new Color(0.45f, 0.45f, 0.5f)
                : canAfford ? TextWhite : TextMuted,
                FontStyle.Bold, TextAnchor.MiddleLeft);
            PlaceInParent(nameText.rectTransform, 10f, -4f, 300f, 22f);

            // サブ情報（タイプ/ゾーン）
            string subInfo = "";
            if (item.Type == "Attraction" && !string.IsNullOrEmpty(item.Category))
                subInfo = $"[{item.Category}]";
            if (!string.IsNullOrEmpty(item.ThemeZone))
                subInfo += $" {item.ThemeZone}";
            var subText = MakeLabel(cardRt, "Sub", subInfo, 11,
                TextMuted, FontStyle.Normal, TextAnchor.MiddleLeft);
            PlaceInParent(subText.rectTransform, 10f, -26f, 300f, 18f);

            // コスト
            string costLabel = researched ? $"${item.BuildCost:N0}" : "LOCKED";
            var costText = MakeLabel(cardRt, "Cost", costLabel, 14,
                !researched ? new Color(0.6f, 0.4f, 0.4f)
                : canAfford ? new Color(0.95f, 0.88f, 0.45f) : AccentRed,
                FontStyle.Bold, TextAnchor.MiddleRight);
            var crt = costText.rectTransform;
            crt.anchorMin = new Vector2(1f, 0f);
            crt.anchorMax = new Vector2(1f, 1f);
            crt.pivot = new Vector2(1f, 0.5f);
            crt.anchoredPosition = new Vector2(-10f, 0f);
            crt.sizeDelta = new Vector2(120f, 0f);

            return cardGo;
        }

        // ================================================================
        // アイテム詳細表示
        // ================================================================

        private void SelectItem(BuildableItem item)
        {
            _selectedItem = item;
            _detailPanel.SetActive(true);

            _detailName.text = item.NameJa;
            _detailDesc.text = item.Description ?? "";

            float currentMoney = GameManager.Instance?.EconomyManager?.CurrentBalance ?? 0f;
            bool canAfford = currentMoney >= item.BuildCost;

            _detailCost.text = $"建設費: ${item.BuildCost:N0}";
            if (item.MaintenanceCost > 0)
                _detailCost.text += $"  (維持費: ${item.MaintenanceCost:N0}/月)";

            // ステータス
            string stats = "";
            if (item.Type == "Attraction")
            {
                stats += $"興奮度: {item.ExcitementRating:F1}/10\n";
                stats += $"酔い度: {item.NauseaFactor:F2}\n";
                stats += $"定員: {item.Capacity}人\n";
                stats += $"乗車時間: {item.RideDuration:F0}秒\n";
                stats += $"サイズ: {item.GridWidth}x{item.GridHeight}";
            }
            else if (item.Type == "FoodShop" || item.Type == "DrinkShop" || item.Type == "SouvenirShop")
            {
                stats += $"推奨販売価格: ${item.SuggestedPrice}\n";
                if (item.WholesalePrice > 0)
                    stats += $"仕入値: ${item.WholesalePrice}\n";
                stats += $"テーマ: {item.ThemeZone ?? "共通"}";
            }
            else
            {
                if (item.Capacity > 0)
                    stats += $"容量: {item.Capacity}\n";
                if (item.GridWidth > 0)
                    stats += $"サイズ: {item.GridWidth}x{item.GridHeight}\n";
                if (item.MaintenanceCost > 0)
                    stats += $"月間維持費: ${item.MaintenanceCost:N0}";
            }
            _detailStats.text = stats;

            // ボタン状態
            _placeButton.interactable = canAfford;
            _placeButtonText.text = canAfford ? "配置する" : "資金不足";
            var btnImg = _placeButton.GetComponent<Image>();
            if (btnImg != null)
                btnImg.color = canAfford ? AccentGreen : new Color(0.4f, 0.4f, 0.4f);
        }

        // ================================================================
        // 配置モード
        // ================================================================

        private void OnPlaceClicked()
        {
            if (_selectedItem == null) return;

            float currentMoney = GameManager.Instance?.EconomyManager?.CurrentBalance ?? 0f;
            if (currentMoney < _selectedItem.BuildCost) return;

            StartPlacement(_selectedItem);
        }

        private void StartPlacement(BuildableItem item)
        {
            _isPlacing = true;

            // パネルを隠して配置に集中
            _panelRoot.SetActive(false);
            _placementHintGo.SetActive(true);

            _placementHint.text = $"{item.NameJa} を配置中 | クリックで確定 | 右クリック/ESCでキャンセル";

            // プレビューオブジェクト作成
            float sizeX = Mathf.Max(item.GridWidth, 2f);
            float sizeZ = Mathf.Max(item.GridHeight, 2f);
            float sizeY = item.Type == "Attraction" ? 4f : 3f;

            _placementPreview = GameObject.CreatePrimitive(PrimitiveType.Cube);
            _placementPreview.name = "PlacementPreview";
            _placementPreview.transform.localScale = new Vector3(sizeX, sizeY, sizeZ);

            // 半透明マテリアル
            var renderer = _placementPreview.GetComponent<Renderer>();
            if (renderer != null)
            {
                var shader = Shader.Find("Standard");
                if (shader == null) shader = Shader.Find("UI/Default");
                if (shader != null)
                {
                    var mat = new Material(shader);
                    mat.color = new Color(0.3f, 0.7f, 1.0f, 0.4f);
                    // 半透明レンダリングモード
                    mat.SetFloat("_Mode", 3); // Transparent
                    mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                    mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                    mat.SetInt("_ZWrite", 0);
                    mat.DisableKeyword("_ALPHATEST_ON");
                    mat.EnableKeyword("_ALPHABLEND_ON");
                    mat.DisableKeyword("_ALPHAPREMULTIPLY_ON");
                    mat.renderQueue = 3000;
                    renderer.material = mat;
                }
            }

            // Collider除去（配置チェックに干渉しないよう）
            var col = _placementPreview.GetComponent<Collider>();
            if (col != null) Destroy(col);
        }

        private void UpdatePlacementPreview()
        {
            if (_placementPreview == null || _selectedItem == null) return;

            var cam = Camera.main;
            if (cam == null) return;

            // UI上のクリックは無視
            if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
                return;

            Ray ray = cam.ScreenPointToRay(Input.mousePosition);
            if (Physics.Raycast(ray, out RaycastHit hit, 500f))
            {
                // グリッドスナップ（2mグリッド）
                float gridSize = 2f;
                float x = Mathf.Round(hit.point.x / gridSize) * gridSize;
                float z = Mathf.Round(hit.point.z / gridSize) * gridSize;
                float y = _placementPreview.transform.localScale.y * 0.5f;
                _placementPreview.transform.position = new Vector3(x, y, z);

                // 衝突チェック
                Vector3 halfExtents = _placementPreview.transform.localScale * 0.45f;
                var overlaps = Physics.OverlapBox(
                    _placementPreview.transform.position,
                    halfExtents,
                    Quaternion.identity,
                    ~(1 << 5) // UI Layer以外
                );

                _placementValid = true;
                foreach (var o in overlaps)
                {
                    if (o.gameObject.name != "Ground")
                    {
                        _placementValid = false;
                        break;
                    }
                }

                // プレビュー色を変更
                var renderer = _placementPreview.GetComponent<Renderer>();
                if (renderer != null)
                {
                    renderer.material.color = _placementValid
                        ? new Color(0.3f, 0.7f, 1.0f, 0.4f)
                        : new Color(1.0f, 0.3f, 0.3f, 0.4f);
                }
            }

            // 左クリックで配置確定
            if (Input.GetMouseButtonDown(0) && _placementValid)
            {
                if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
                    return;
                ConfirmPlacement();
            }
        }

        private void ConfirmPlacement()
        {
            if (_selectedItem == null || _placementPreview == null) return;

            var gm = GameManager.Instance;
            if (gm == null) return;

            Vector3 position = _placementPreview.transform.position;
            position.y = 0f;

            // コスト支払い
            if (!gm.EconomyManager.SpendMoney(_selectedItem.BuildCost))
            {
                Debug.LogWarning("[RuntimeBuildPanel] 資金不足で配置できません");
                CancelPlacement();
                return;
            }

            // オブジェクト生成
            GameObject placed = CreatePlacedObject(_selectedItem, position);

            // イベント発火
            if (placed != null)
            {
                GameEvents.FireAttractionBuilt(placed.GetInstanceID());
                if (gm.NotificationSystem != null)
                    NotificationSystem.Instance?.Notify(
                        $"{_selectedItem.NameJa} を建設しました！ (-${_selectedItem.BuildCost:N0})",
                        NotifLevel.Success);
            }

            Debug.Log($"[RuntimeBuildPanel] Placed {_selectedItem.NameJa} at {position}");

            // プレビュー破棄して配置モード終了
            CancelPlacement();

            // パネルを再表示してアイテムリスト更新
            Open();
        }

        private void CancelPlacement()
        {
            if (_placementPreview != null)
            {
                Destroy(_placementPreview);
                _placementPreview = null;
            }
            _isPlacing = false;
            _placementValid = false;
            _placementHintGo?.SetActive(false);

            if (_isOpen && _panelRoot != null)
                _panelRoot.SetActive(true);
        }

        // ================================================================
        // オブジェクト生成（RuntimeGameSetupのパターンを踏襲）
        // ================================================================

        private GameObject CreatePlacedObject(BuildableItem item, Vector3 position)
        {
            if (item.Type == "Attraction")
                return CreateAttractionObject(item, position);
            if (item.Type == "FoodShop" || item.Type == "DrinkShop" || item.Type == "SouvenirShop")
                return CreateShopObject(item, position);
            return CreateFacilityObject(item, position);
        }

        private GameObject CreateAttractionObject(BuildableItem item, Vector3 position)
        {
            // AttractionDataをランタイム生成
            var data = ScriptableObject.CreateInstance<AttractionData>();
            data.AttractionId = item.Id;
            data.NameJP = item.NameJa;
            data.NameEN = item.NameEn ?? item.NameJa;
            data.ExcitementRating = item.ExcitementRating;
            data.NauseaFactor = item.NauseaFactor;
            data.Capacity = item.Capacity;
            data.RideDuration = item.RideDuration;
            data.BuildCost = item.BuildCost;
            data.SuggestedTicketPrice = item.SuggestedPrice;
            data.MaintenanceCost = item.MaintenanceCost;
            data.BaseBreakdownRate = 0.01f;
            data.TimeToMaxBreakdownRate = 600f;
            data.TimeToAccidentAfterBreakdown = 180f;
            data.Size = new Vector2Int(item.GridWidth, item.GridHeight);

            // カテゴリ設定
            if (Enum.TryParse(item.Category, out AttractionCategory cat))
                data.Category = cat;

            // GameObject作成
            var parent = GameObject.Find("--- Attractions ---")?.transform;
            var go = new GameObject(item.NameJa);
            if (parent != null) go.transform.SetParent(parent);
            go.transform.position = position;
            go.tag = "Attraction";
            go.layer = 9;

            float sizeX = Mathf.Max(item.GridWidth, 3f);
            float sizeZ = Mathf.Max(item.GridHeight, 3f);

            var col = go.AddComponent<BoxCollider>();
            col.size = new Vector3(sizeX, 4f, sizeZ);
            col.center = new Vector3(0f, 2f, 0f);

            // ビジュアル
            var visual = GameObject.CreatePrimitive(PrimitiveType.Cube);
            visual.name = "Visual";
            visual.transform.SetParent(go.transform);
            visual.transform.localPosition = new Vector3(0f, 2f, 0f);
            visual.transform.localScale = new Vector3(sizeX - 0.5f, 4f, sizeZ - 0.5f);
            UnityEngine.Object.Destroy(visual.GetComponent<Collider>());

            var renderer = visual.GetComponent<Renderer>();
            if (renderer != null)
            {
                Color color = GetAttractionColor(data.Category);
                var shader = Shader.Find("Standard");
                if (shader == null) shader = Shader.Find("UI/Default");
                if (shader != null)
                    renderer.material = new Material(shader) { color = color };
            }

            go.AddComponent<FacilityDirt>();

            var attraction = go.AddComponent<Attraction.Attraction>();
            attraction.SetAttractionData(data);
            attraction.TicketPrice = item.SuggestedPrice;
            attraction.MaxQueueLength = Mathf.Min(item.Capacity, 15);
            attraction.FacilityId = go.GetInstanceID();

            int gx = Mathf.Max(0, (int)(position.x + 50f));
            int gz = Mathf.Max(0, (int)(position.z + 50f));
            attraction.Place(new Vector2Int(gx, gz), ParseThemeZone(item.ThemeZone));
            go.transform.position = position;

            // 浮遊ラベル
            AddFloatingLabel(go, item.NameJa, 5f, Color.white);

            // NavMesh再Bake
            RebakeNavMesh();

            return go;
        }

        private GameObject CreateShopObject(BuildableItem item, Vector3 position)
        {
            string tag;
            ShopType shopType;
            FacilityType facType;
            switch (item.Type)
            {
                case "DrinkShop":
                    tag = "DrinkShop"; shopType = ShopType.DrinkShop; facType = FacilityType.DrinkShop; break;
                case "SouvenirShop":
                    tag = "SouvenirShop"; shopType = ShopType.SouvenirShop; facType = FacilityType.SouvenirShop; break;
                default:
                    tag = "FoodShop"; shopType = ShopType.FoodShop; facType = FacilityType.FoodShop; break;
            }

            var parent = GameObject.Find("--- Shops ---")?.transform;
            var go = new GameObject(item.NameJa);
            if (parent != null) go.transform.SetParent(parent);
            go.transform.position = position;
            go.tag = tag;
            go.layer = 9;

            var col = go.AddComponent<BoxCollider>();
            col.size = new Vector3(4f, 3f, 4f);
            col.center = new Vector3(0f, 1.5f, 0f);

            var visual = GameObject.CreatePrimitive(PrimitiveType.Cube);
            visual.name = "Visual";
            visual.transform.SetParent(go.transform);
            visual.transform.localPosition = new Vector3(0f, 1.5f, 0f);
            visual.transform.localScale = new Vector3(3.5f, 3f, 3.5f);
            UnityEngine.Object.Destroy(visual.GetComponent<Collider>());

            var renderer = visual.GetComponent<Renderer>();
            if (renderer != null)
            {
                Color color;
                switch (facType)
                {
                    case FacilityType.FoodShop: color = new Color(1.0f, 0.6f, 0.2f); break;
                    case FacilityType.DrinkShop: color = new Color(0.2f, 0.8f, 1.0f); break;
                    default: color = new Color(1.0f, 0.4f, 0.8f); break;
                }
                var shader = Shader.Find("Standard");
                if (shader == null) shader = Shader.Find("UI/Default");
                if (shader != null)
                    renderer.material = new Material(shader) { color = color };
            }

            go.AddComponent<FacilityDirt>();

            var shop = go.AddComponent<Shop>();
            shop.FacilityId = go.GetInstanceID();
            int price = item.SuggestedPrice > 0 ? item.SuggestedPrice : 10;
            int wholesale = item.WholesalePrice > 0 ? item.WholesalePrice : 5;
            shop.ConfigureRuntime(item.NameJa, shopType, wholesale, price, 100);

            int gx = Mathf.Max(0, (int)(position.x + 50f));
            int gz = Mathf.Max(0, (int)(position.z + 50f));
            shop.Place(new Vector2Int(gx, gz), ParseThemeZone(item.ThemeZone));
            go.transform.position = position;

            // 浮遊ラベル
            AddFloatingLabel(go, item.NameJa, 4f, new Color(1f, 0.9f, 0.5f));

            RebakeNavMesh();
            return go;
        }

        private GameObject CreateFacilityObject(BuildableItem item, Vector3 position)
        {
            string tag = "Untagged";
            Color color = Color.gray;
            Vector3 size = new Vector3(2f, 2f, 2f);

            switch (item.Type)
            {
                case "Toilet":
                    tag = "Toilet"; color = Color.white; size = new Vector3(3f, 3f, 3f); break;
                case "Bench":
                    tag = "Bench"; color = new Color(0.6f, 0.4f, 0.2f); size = new Vector3(2f, 1f, 1f); break;
                case "TrashCan":
                    tag = "Untagged"; color = new Color(0.4f, 0.4f, 0.4f); size = new Vector3(0.6f, 1f, 0.6f); break;
                case "InfoBoard":
                    tag = "InfoBoard"; color = new Color(0.3f, 0.6f, 0.9f); size = new Vector3(1f, 2f, 0.3f); break;
                case "Pathway":
                    tag = "Untagged"; color = new Color(0.6f, 0.55f, 0.5f); size = new Vector3(2f, 0.1f, 2f); break;
                case "Decoration":
                    tag = "Untagged"; color = new Color(0.5f, 0.8f, 0.5f); size = new Vector3(2f, 2f, 2f); break;
                case "StaffRoom":
                    tag = "Untagged"; color = new Color(0.4f, 0.6f, 0.4f); size = new Vector3(4f, 3f, 3f); break;
                case "ResearchLab":
                    tag = "Untagged"; color = new Color(0.7f, 0.7f, 0.3f); size = new Vector3(5f, 3f, 4f); break;
            }

            var parent = GameObject.Find("--- Facilities ---")?.transform;
            var go = new GameObject(item.NameJa);
            if (parent != null) go.transform.SetParent(parent);
            go.transform.position = position;
            go.tag = tag;
            go.layer = 9;

            var col = go.AddComponent<BoxCollider>();
            col.size = size;
            col.center = new Vector3(0f, size.y * 0.5f, 0f);

            var visual = GameObject.CreatePrimitive(PrimitiveType.Cube);
            visual.name = "Visual";
            visual.transform.SetParent(go.transform);
            visual.transform.localPosition = col.center;
            visual.transform.localScale = size * 0.9f;
            UnityEngine.Object.Destroy(visual.GetComponent<Collider>());

            var renderer = visual.GetComponent<Renderer>();
            if (renderer != null)
            {
                var shader = Shader.Find("Standard");
                if (shader == null) shader = Shader.Find("UI/Default");
                if (shader != null)
                    renderer.material = new Material(shader) { color = color };
            }

            // スタッフルームの場合、StaffManagerに登録
            if (item.Type == "StaffRoom")
            {
                var sm = GameManager.Instance?.StaffManager;
                if (sm != null) sm.RegisterStaffRoom(go.transform);
            }

            // 浮遊ラベル
            AddFloatingLabel(go, item.NameJa, size.y + 1f, new Color(0.8f, 0.9f, 1f));

            RebakeNavMesh();
            return go;
        }

        // ================================================================
        // ユーティリティ
        // ================================================================

        private void AddFloatingLabel(GameObject parent, string text, float height, Color color)
        {
            var labelGo = new GameObject("Label");
            labelGo.transform.SetParent(parent.transform);
            labelGo.transform.localPosition = new Vector3(0f, height, 0f);

            var tm = labelGo.AddComponent<TextMesh>();
            tm.text = text;
            tm.fontSize = 48;
            tm.characterSize = 0.12f;
            tm.anchor = TextAnchor.MiddleCenter;
            tm.alignment = TextAlignment.Center;
            tm.color = color;
            tm.fontStyle = FontStyle.Bold;

            labelGo.AddComponent<FacingCamera>();
        }

        private void RebakeNavMesh()
        {
            try
            {
                var ground = GameObject.Find("Ground");
                if (ground == null) return;
                var surface = ground.GetComponent<Unity.AI.Navigation.NavMeshSurface>();
                if (surface != null)
                    surface.BuildNavMesh();
            }
            catch (Exception) { /* NavMesh unavailable */ }
        }

        private static ThemeZone ParseThemeZone(string zone)
        {
            if (string.IsNullOrEmpty(zone)) return ThemeZone.LostKingdom;
            switch (zone)
            {
                case "HalloweenWorld": return ThemeZone.HalloweenWorld;
                case "Wonderland": return ThemeZone.Wonderland;
                case "SpaceZone": return ThemeZone.SpaceZone;
                default: return ThemeZone.LostKingdom;
            }
        }

        private static Color GetAttractionColor(AttractionCategory category)
        {
            switch (category)
            {
                case AttractionCategory.GForce: return new Color(0.9f, 0.2f, 0.2f);
                case AttractionCategory.Observation: return new Color(0.2f, 0.5f, 0.9f);
                case AttractionCategory.HorizontalRotation: return new Color(0.9f, 0.7f, 0.2f);
                case AttractionCategory.VerticalRotation: return new Color(0.6f, 0.3f, 0.9f);
                case AttractionCategory.RideAttraction: return new Color(0.3f, 0.9f, 0.5f);
                case AttractionCategory.ShowAttraction: return new Color(0.5f, 0.2f, 0.8f);
                default: return new Color(0.5f, 0.5f, 0.5f);
            }
        }

        // ---- UI構築ヘルパー（RuntimeHUDと同パターン） ----

        private static Font _font;
        private static Font CachedFont()
        {
            if (_font != null) return _font;
            _font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            if (_font == null)
                _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            return _font;
        }

        private static GameObject MakePanel(RectTransform parent, string name, float w, float h, Color bg)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var rt = go.AddComponent<RectTransform>();
            rt.sizeDelta = new Vector2(w, h);
            var img = go.AddComponent<Image>();
            img.color = bg;
            img.raycastTarget = false;
            return go;
        }

        private static Text MakeLabel(RectTransform parent, string name, string content,
            int fontSize, Color color, FontStyle style, TextAnchor align)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.AddComponent<RectTransform>();
            var t = go.AddComponent<Text>();
            t.text = content;
            t.font = CachedFont();
            t.fontSize = fontSize;
            t.color = color;
            t.fontStyle = style;
            t.alignment = align;
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            t.raycastTarget = false;
            return t;
        }

        private static void PlaceInParent(RectTransform rt, float x, float y, float w, float h)
        {
            rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0f, 1f);
            rt.anchoredPosition = new Vector2(x, y);
            rt.sizeDelta = new Vector2(w, h);
        }

        private static void StretchFill(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }
    }
}
