// ============================================================
// RuntimeHUD - Attractions partial
// アトラクションパネル、施設詳細パネル
// ============================================================

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using ThemeParkGame.Attraction;
using ThemeParkGame.Economy;
using ThemeParkGame.Visitor;

namespace ThemeParkGame.Core
{
    public partial class RuntimeHUD
    {
        // ---- アトラクションパネル ----
        private RectTransform _attrPanelRt;
        private readonly List<Text> _attrLines = new List<Text>();

        // ---- 施設詳細パネル ----
        private GameObject _facilityInfoPanel;
        private Text _fiName;
        private Text _fiType;
        private Text _fiStatus;
        private Text _fiPrice;
        private Text _fiQueue;
        private Text _fiStats;
        private Button _fiPriceUp;
        private Button _fiPriceDown;
        private Button _fiDemolishBtn;
        private Button _fiUpgradeBtn;
        private Text _fiUpgradeText;
        private FacilityBase _selectedFacility;

        // ================================================================
        // アトラクションパネル（右下 320xN）
        // ================================================================

        private void BuildAttractionPanel(RectTransform root)
        {
            float panelW = 320f;
            float panelH = 40f;

            var bg = MakePanel(root, "AttractionPanel", panelW, panelH, BgDark);
            _attrPanelRt = bg.GetComponent<RectTransform>();
            _attrPanelRt.anchorMin = _attrPanelRt.anchorMax = new Vector2(1f, 0f);
            _attrPanelRt.pivot = new Vector2(1f, 0f);
            _attrPanelRt.anchoredPosition = new Vector2(-10f, 10f);

            var header = MakeLabel(_attrPanelRt, "Header", "アトラクション状況", 16, Gold, FontStyle.Bold, TextAnchor.MiddleCenter);
            PlaceInParent(header.rectTransform, 0f, panelH - 4f, panelW, 24f, new Vector2(0f, 1f));
        }

        // ================================================================
        // 施設詳細パネル（画面中央右 280x340）
        // ================================================================

        private void BuildFacilityInfoPanel(RectTransform root)
        {
            float panelW = 280f;
            float panelH = 340f;

            _facilityInfoPanel = MakePanel(root, "FacilityInfo", panelW, panelH, BgDark);
            var rt = _facilityInfoPanel.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(0.7f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = Vector2.zero;

            float y = -8f;
            _fiName = MakeLabel(rt, "FIName", "", 18, Gold, FontStyle.Bold, TextAnchor.MiddleLeft);
            PlaceInParent(_fiName.rectTransform, 10f, y, panelW - 20f, 24f, new Vector2(0f, 1f));
            y -= 26f;

            _fiType = MakeLabel(rt, "FIType", "", 13, Muted, FontStyle.Normal, TextAnchor.MiddleLeft);
            PlaceInParent(_fiType.rectTransform, 10f, y, panelW - 20f, 18f, new Vector2(0f, 1f));
            y -= 22f;

            // 区切り線
            var line = MakePanel(rt, "Line", panelW - 20f, 1f, new Color(0.3f, 0.35f, 0.4f));
            PlaceInParent(line.GetComponent<RectTransform>(), 10f, y, panelW - 20f, 1f, new Vector2(0f, 1f));
            y -= 6f;

            _fiStatus = MakeLabel(rt, "FIStatus", "", 14, Green, FontStyle.Normal, TextAnchor.MiddleLeft);
            PlaceInParent(_fiStatus.rectTransform, 10f, y, panelW - 20f, 20f, new Vector2(0f, 1f));
            y -= 22f;

            _fiQueue = MakeLabel(rt, "FIQueue", "", 14, Cyan, FontStyle.Normal, TextAnchor.MiddleLeft);
            PlaceInParent(_fiQueue.rectTransform, 10f, y, panelW - 20f, 20f, new Vector2(0f, 1f));
            y -= 24f;

            _fiStats = MakeLabel(rt, "FIStats", "", 13, new Color(0.85f, 0.85f, 0.9f),
                FontStyle.Normal, TextAnchor.UpperLeft);
            _fiStats.horizontalOverflow = HorizontalWrapMode.Wrap;
            PlaceInParent(_fiStats.rectTransform, 10f, y, panelW - 20f, 70f, new Vector2(0f, 1f));
            y -= 74f;

            // 価格調整
            _fiPrice = MakeLabel(rt, "FIPrice", "", 16, Gold, FontStyle.Bold, TextAnchor.MiddleCenter);
            PlaceInParent(_fiPrice.rectTransform, 10f, y, panelW - 20f, 22f, new Vector2(0f, 1f));
            y -= 28f;

            // -5 ボタン
            var downGo = MakePanel(rt, "PriceDown", 80f, 30f, new Color(0.7f, 0.25f, 0.25f, 0.9f));
            var downRt = downGo.GetComponent<RectTransform>();
            downRt.anchorMin = downRt.anchorMax = new Vector2(0f, 0f);
            downRt.pivot = new Vector2(0f, 0f);
            downRt.anchoredPosition = new Vector2(30f, 10f);
            var downImg = downGo.GetComponent<Image>();
            downImg.raycastTarget = true;
            _fiPriceDown = downGo.AddComponent<Button>();
            _fiPriceDown.targetGraphic = downImg;
            _fiPriceDown.onClick.AddListener(OnFacilityPriceDown);
            var downLabel = MakeLabel(downRt, "L", "-$5", 16, Color.white, FontStyle.Bold, TextAnchor.MiddleCenter);
            StretchFill(downLabel.rectTransform);

            // +5 ボタン
            var upGo = MakePanel(rt, "PriceUp", 80f, 30f, new Color(0.25f, 0.6f, 0.25f, 0.9f));
            var upRt = upGo.GetComponent<RectTransform>();
            upRt.anchorMin = upRt.anchorMax = new Vector2(1f, 0f);
            upRt.pivot = new Vector2(1f, 0f);
            upRt.anchoredPosition = new Vector2(-30f, 10f);
            var upImg = upGo.GetComponent<Image>();
            upImg.raycastTarget = true;
            _fiPriceUp = upGo.AddComponent<Button>();
            _fiPriceUp.targetGraphic = upImg;
            _fiPriceUp.onClick.AddListener(OnFacilityPriceUp);
            var upLabel = MakeLabel(upRt, "L", "+$5", 16, Color.white, FontStyle.Bold, TextAnchor.MiddleCenter);
            StretchFill(upLabel.rectTransform);

            // アップグレードボタン
            var upgradeGo = MakePanel(rt, "UpgradeBtn", panelW - 60f, 30f, new Color(0.2f, 0.5f, 0.65f, 0.95f));
            var upgradeRt = upgradeGo.GetComponent<RectTransform>();
            upgradeRt.anchorMin = upgradeRt.anchorMax = new Vector2(0.5f, 0f);
            upgradeRt.pivot = new Vector2(0.5f, 0f);
            upgradeRt.anchoredPosition = new Vector2(0f, 84f);
            var upgradeImg = upgradeGo.GetComponent<Image>();
            upgradeImg.raycastTarget = true;
            _fiUpgradeBtn = upgradeGo.AddComponent<Button>();
            _fiUpgradeBtn.targetGraphic = upgradeImg;
            var upgradeColors = _fiUpgradeBtn.colors;
            upgradeColors.highlightedColor = new Color(0.3f, 0.6f, 0.75f);
            upgradeColors.pressedColor = new Color(0.15f, 0.38f, 0.5f);
            _fiUpgradeBtn.colors = upgradeColors;
            _fiUpgradeBtn.onClick.AddListener(OnFacilityUpgrade);
            _fiUpgradeText = MakeLabel(upgradeRt, "L", LocalizationData.LabelUpgrade, 14, Color.white, FontStyle.Bold, TextAnchor.MiddleCenter);
            StretchFill(_fiUpgradeText.rectTransform);

            // 撤去ボタン
            var demolishGo = MakePanel(rt, "DemolishBtn", panelW - 60f, 30f, new Color(0.7f, 0.15f, 0.15f, 0.95f));
            var demolishRt = demolishGo.GetComponent<RectTransform>();
            demolishRt.anchorMin = demolishRt.anchorMax = new Vector2(0.5f, 0f);
            demolishRt.pivot = new Vector2(0.5f, 0f);
            demolishRt.anchoredPosition = new Vector2(0f, 48f);
            var demolishImg = demolishGo.GetComponent<Image>();
            demolishImg.raycastTarget = true;
            _fiDemolishBtn = demolishGo.AddComponent<Button>();
            _fiDemolishBtn.targetGraphic = demolishImg;
            _fiDemolishBtn.onClick.AddListener(OnFacilityDemolish);
            var demolishLabel = MakeLabel(demolishRt, "L", "撤去する", 15, Color.white, FontStyle.Bold, TextAnchor.MiddleCenter);
            StretchFill(demolishLabel.rectTransform);

            // 閉じるボタン
            var closeGo = MakePanel(rt, "CloseBtn", 24f, 24f, new Color(0.8f, 0.2f, 0.2f, 0.9f));
            var closeRt = closeGo.GetComponent<RectTransform>();
            closeRt.anchorMin = closeRt.anchorMax = new Vector2(1f, 1f);
            closeRt.pivot = new Vector2(1f, 1f);
            closeRt.anchoredPosition = new Vector2(-4f, -4f);
            var closeImg = closeGo.GetComponent<Image>();
            closeImg.raycastTarget = true;
            var closeBtn = closeGo.AddComponent<Button>();
            closeBtn.targetGraphic = closeImg;
            closeBtn.onClick.AddListener(HideFacilityInfo);
            var closeLabel = MakeLabel(closeRt, "X", "X", 14, Color.white, FontStyle.Bold, TextAnchor.MiddleCenter);
            StretchFill(closeLabel.rectTransform);

            _facilityInfoPanel.SetActive(false);
        }

        private void ShowFacilityInfo(FacilityBase facility)
        {
            _selectedFacility = facility;
            _facilityInfoPanel.SetActive(true);
            RefreshFacilityInfo();
        }

        private void HideFacilityInfo()
        {
            _selectedFacility = null;
            _facilityInfoPanel.SetActive(false);
        }

        private void RefreshFacilityInfo()
        {
            if (_selectedFacility == null)
            {
                HideFacilityInfo();
                return;
            }

            var attraction = _selectedFacility as Attraction.Attraction;
            var shop = _selectedFacility as Shop;

            _fiName.text = _selectedFacility.gameObject.name;

            if (attraction != null)
            {
                _fiType.text = attraction.Data != null
                    ? $"アトラクション [{attraction.Data.Category}] Lv.{attraction.UpgradeLevel}"
                    : "アトラクション";

                string cycleStr = CycleLabel(attraction.CurrentCycleState);
                _fiStatus.text = $"状態: {cycleStr}";
                _fiStatus.color = attraction.CurrentCycleState == RideCycleState.BrokenDown
                    ? Red : Green;

                _fiQueue.text = $"待ち行列: {attraction.QueueLength}/{attraction.MaxQueueLength}人";

                string stats = "";
                if (attraction.Data != null)
                {
                    stats += $"興奮度: {attraction.EffectiveExcitement:F1}/10\n";
                    stats += $"酔い度: {attraction.Data.NauseaFactor:F2}\n";
                    stats += $"定員: {attraction.EffectiveCapacity}人\n";
                    stats += $"総搭乗数: {attraction.TotalRiderCount}";
                }
                _fiStats.text = stats;

                _fiPrice.text = $"チケット: ${attraction.TicketPrice}";
                _fiPriceUp.gameObject.SetActive(true);
                _fiPriceDown.gameObject.SetActive(true);

                // アップグレードボタン
                int upgradeCost = attraction.GetNextUpgradeCost();
                if (upgradeCost > 0)
                {
                    _fiUpgradeBtn.gameObject.SetActive(true);
                    float money = GameManager.Instance?.EconomyManager?.CurrentBalance ?? 0f;
                    bool canAfford = money >= upgradeCost;
                    _fiUpgradeBtn.interactable = canAfford;
                    _fiUpgradeText.text = canAfford
                        ? $"アップグレード (${upgradeCost:N0})"
                        : $"アップグレード (${upgradeCost:N0}) - 資金不足";
                    _fiUpgradeBtn.GetComponent<Image>().color = canAfford
                        ? new Color(0.2f, 0.5f, 0.65f, 0.95f)
                        : new Color(0.3f, 0.3f, 0.3f, 0.8f);
                }
                else
                {
                    _fiUpgradeBtn.gameObject.SetActive(true);
                    _fiUpgradeBtn.interactable = false;
                    _fiUpgradeText.text = LocalizationData.LabelMaxLevel;
                    _fiUpgradeBtn.GetComponent<Image>().color = new Color(0.3f, 0.3f, 0.3f, 0.8f);
                }
            }
            else if (shop != null)
            {
                string shopTypeName;
                switch (shop.ShopType)
                {
                    case ShopType.FoodShop: shopTypeName = "フード"; break;
                    case ShopType.DrinkShop: shopTypeName = "ドリンク"; break;
                    case ShopType.SouvenirShop: shopTypeName = "おみやげ"; break;
                    default: shopTypeName = shop.ShopType.ToString(); break;
                }
                _fiType.text = $"ショップ [{shopTypeName}]";
                _fiStatus.text = "営業中";
                _fiStatus.color = Green;
                _fiQueue.text = $"在庫: {shop.CurrentStock}/{shop.MaxStock}";

                string stats = $"売価: ${shop.SellingPrice}\n";
                stats += $"総売上: ${shop.TotalRevenue:N0}";
                _fiStats.text = stats;

                _fiPrice.text = $"販売価格: ${shop.SellingPrice}";
                _fiPriceUp.gameObject.SetActive(true);
                _fiPriceDown.gameObject.SetActive(true);
                _fiUpgradeBtn.gameObject.SetActive(false);
            }
            else
            {
                _fiType.text = "施設";
                _fiStatus.text = "";
                _fiQueue.text = "";
                _fiStats.text = "";
                _fiPrice.text = "";
                _fiPriceUp.gameObject.SetActive(false);
                _fiPriceDown.gameObject.SetActive(false);
                _fiUpgradeBtn.gameObject.SetActive(false);
            }
        }

        private void OnFacilityPriceUp()
        {
            if (_selectedFacility == null) return;

            var attraction = _selectedFacility as Attraction.Attraction;
            if (attraction != null)
            {
                attraction.TicketPrice = Mathf.Min(attraction.TicketPrice + 5, 500);
                RefreshFacilityInfo();
                return;
            }

            var shop = _selectedFacility as Shop;
            if (shop != null)
            {
                shop.SellingPrice += 5;
                RefreshFacilityInfo();
            }
        }

        private void OnFacilityPriceDown()
        {
            if (_selectedFacility == null) return;

            var attraction = _selectedFacility as Attraction.Attraction;
            if (attraction != null)
            {
                attraction.TicketPrice = Mathf.Max(attraction.TicketPrice - 5, 0);
                RefreshFacilityInfo();
                return;
            }

            var shop = _selectedFacility as Shop;
            if (shop != null)
            {
                shop.SellingPrice = Mathf.Max(shop.SellingPrice - 5, 0);
                RefreshFacilityInfo();
            }
        }

        private void OnFacilityDemolish()
        {
            if (_selectedFacility == null) return;

            var facilityGo = _selectedFacility.gameObject;

            // FacilityBase.Demolish()を呼ぶ（キューのクリア等を行う）
            _selectedFacility.Demolish();

            // ParkManagerからグリッド登録を解除
            if (GameManager.Instance?.ParkManager != null)
            {
                GameManager.Instance.ParkManager.RemoveFacility(_selectedFacility.FacilityId);
            }

            // 来場者の施設キャッシュを無効化
            VisitorAI.InvalidateFacilityCache();

            // GameObjectを破棄
            Destroy(facilityGo);

            // NavMesh再構築
            try
            {
                var ground = GameObject.Find("Ground");
                if (ground != null)
                {
                    var surface = ground.GetComponent<Unity.AI.Navigation.NavMeshSurface>();
                    if (surface != null)
                        surface.BuildNavMesh();
                }
            }
            catch (System.Exception) { }

            // パネルを閉じる
            HideFacilityInfo();
        }

        private void OnFacilityUpgrade()
        {
            if (_selectedFacility == null) return;

            var attraction = _selectedFacility as Attraction.Attraction;
            if (attraction == null) return;

            int cost = attraction.GetNextUpgradeCost();
            if (cost <= 0) return;

            var em = GameManager.Instance?.EconomyManager;
            if (em == null || !em.CanAfford(cost)) return;

            em.PayExpense(cost, ThemeParkGame.Economy.ExpenseCategory.Construction, attraction.FacilityId);

            if (attraction.TryUpgrade())
            {
                NotificationSystem.Instance?.Notify(
                    $"{attraction.DisplayName} をLv.{attraction.UpgradeLevel}にアップグレード！ (-${cost:N0})",
                    NotifLevel.Success);
                RefreshFacilityInfo();
            }
        }

        // ================================================================
        // アトラクション一覧更新
        // ================================================================

        private void RefreshAttractions()
        {
            _attrCacheTimer -= UpdateInterval;
            if (_attrCacheTimer <= 0f)
            {
                _attrCacheTimer = 2f;
                _attractions = FindObjectsOfType<Attraction.Attraction>();
            }

            if (_attractions == null || _attractions.Length == 0)
            {
                if (_attrPanelRt != null) _attrPanelRt.gameObject.SetActive(false);
                return;
            }
            _attrPanelRt.gameObject.SetActive(true);

            int linesPerAttr = 3;
            int needed = _attractions.Length * linesPerAttr;
            while (_attrLines.Count < needed)
            {
                int idx = _attrLines.Count;
                int lineType = idx % linesPerAttr;
                bool isName = (lineType == 0);
                bool isRevenue = (lineType == 2);
                var t = MakeLabel(_attrPanelRt, $"AL{idx}", "",
                    isName ? 14 : 12,
                    isName ? Color.white : (isRevenue ? Gold : Muted),
                    isName ? FontStyle.Bold : FontStyle.Normal,
                    TextAnchor.MiddleLeft);
                _attrLines.Add(t);
            }

            float lineH = 18f;
            float blockH = lineH * linesPerAttr + 6f;
            float panelH = 34f + _attractions.Length * blockH;
            _attrPanelRt.sizeDelta = new Vector2(320f, panelH);

            for (int i = 0; i < _attractions.Length; i++)
            {
                var attr = _attractions[i];
                int nameIdx = i * linesPerAttr;
                int detailIdx = nameIdx + 1;
                int revenueIdx = nameIdx + 2;

                float baseY = panelH - 32f - i * blockH;

                PlaceInParent(_attrLines[nameIdx].rectTransform, 10f, baseY, 300f, lineH, new Vector2(0f, 1f));
                PlaceInParent(_attrLines[detailIdx].rectTransform, 10f, baseY - lineH, 300f, lineH, new Vector2(0f, 1f));
                PlaceInParent(_attrLines[revenueIdx].rectTransform, 10f, baseY - lineH * 2, 300f, lineH, new Vector2(0f, 1f));

                if (attr != null)
                {
                    _attrLines[nameIdx].text = attr.DisplayName;

                    string st = CycleLabel(attr.CurrentCycleState);
                    _attrLines[detailIdx].text = $"  {st}  Q:{attr.QueueLength}/{attr.MaxQueueLength}  乗車:{attr.TotalRiderCount}";
                    _attrLines[detailIdx].color = CycleColor(attr.CurrentCycleState);

                    _attrLines[revenueIdx].text = $"  チケット:${attr.TicketPrice}  今日:${attr.TodayRevenue:N0}  累計:${attr.TotalRevenue:N0}";
                    _attrLines[revenueIdx].color = attr.TotalRevenue > 0 ? Gold : Muted;
                }
            }
        }
    }
}
