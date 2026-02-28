// ============================================================
// ThemeParkGame - BuildPanelUI
// 建設インターフェース：カテゴリタブ、アイテム一覧、詳細表示、
// 配置プレビュー、グリッドスナップ、コスト判定
// ============================================================

using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using ThemeParkGame.Core;
using ThemeParkGame.Attraction;

namespace ThemeParkGame.UI
{
    /// <summary>
    /// 建設パネルUI。
    /// プレイヤーがアトラクションや施設をパーク内に配置するための
    /// 全操作を管理する建設インターフェース。
    /// </summary>
    public class BuildPanelUI : MonoBehaviour
    {
        // ============================================================
        // カテゴリタブ
        // ============================================================

        [Header("カテゴリタブ")]
        [SerializeField] private Transform categoryTabContainer;
        [SerializeField] private GameObject categoryTabPrefab;

        /// <summary>カテゴリタブの定義（表示順序を制御）</summary>
        [Serializable]
        public struct CategoryTabDefinition
        {
            public string displayName;
            public BuildCategory category;
            public Sprite icon;
        }

        [SerializeField] private CategoryTabDefinition[] categoryDefinitions;

        /// <summary>
        /// 建設カテゴリ。AttractionCategory + FacilityType を統合した
        /// ビルドパネル専用の分類。
        /// </summary>
        public enum BuildCategory
        {
            // アトラクションカテゴリ（AttractionCategory対応）
            GForce,
            VerticalRotation,
            HorizontalRotation,
            Observation,
            ShowAttraction,
            RideAttraction,
            // 施設カテゴリ（FacilityType対応）
            FoodShop,
            DrinkShop,
            SouvenirShop,
            Toilet,
            Bench,
            TrashCan,
            InfoBoard,
            Pathway,
            Decoration,
            StaffRoom,
            ResearchLab
        }

        // ============================================================
        // アイテムグリッド
        // ============================================================

        [Header("アイテムグリッド")]
        [SerializeField] private Transform itemGridContainer;
        [SerializeField] private GameObject itemCardPrefab;
        [SerializeField] private ScrollRect itemGridScrollRect;

        // ============================================================
        // アイテム詳細パネル
        // ============================================================

        [Header("アイテム詳細パネル")]
        [SerializeField] private GameObject detailPanel;
        [SerializeField] private TextMeshProUGUI detailNameText;
        [SerializeField] private TextMeshProUGUI detailCostText;
        [SerializeField] private TextMeshProUGUI detailDescriptionText;
        [SerializeField] private Image detailThumbnail;

        [Header("アイテム詳細 - ステータスバー")]
        [SerializeField] private Slider detailExcitementBar;
        [SerializeField] private Slider detailIntensityBar;
        [SerializeField] private Slider detailNauseaBar;
        [SerializeField] private Slider detailCapacityBar;
        [SerializeField] private TextMeshProUGUI detailExcitementValue;
        [SerializeField] private TextMeshProUGUI detailIntensityValue;
        [SerializeField] private TextMeshProUGUI detailNauseaValue;
        [SerializeField] private TextMeshProUGUI detailCapacityValue;

        // ============================================================
        // 配置プレビュー
        // ============================================================

        [Header("配置プレビュー")]
        [SerializeField] private Material validPlacementMaterial;
        [SerializeField] private Material invalidPlacementMaterial;
        [SerializeField] private float gridSize = 1f;
        [SerializeField] private LayerMask groundLayer;

        // ============================================================
        // 確認/キャンセルボタン
        // ============================================================

        [Header("操作ボタン")]
        [SerializeField] private Button confirmButton;
        [SerializeField] private Button cancelButton;
        [SerializeField] private Button rotateButton;
        [SerializeField] private TextMeshProUGUI confirmButtonText;

        // ============================================================
        // テーマゾーンフィルタ
        // ============================================================

        [Header("テーマゾーンフィルタ")]
        [SerializeField] private TMP_Dropdown themeZoneDropdown;

        // ============================================================
        // 内部状態
        // ============================================================

        /// <summary>現在選択中のカテゴリ</summary>
        private BuildCategory _selectedCategory = BuildCategory.GForce;

        /// <summary>現在選択中のアイテムデータ</summary>
        private BuildItemData _selectedItem;

        /// <summary>配置プレビュー用のゲームオブジェクト</summary>
        private GameObject _placementPreview;

        /// <summary>配置プレビューの現在の回転角度（Y軸）</summary>
        private float _previewRotation;

        /// <summary>配置が有効かどうか</summary>
        private bool _isPlacementValid;

        /// <summary>配置モードが有効かどうか</summary>
        private bool _isInPlacementMode;

        /// <summary>現在のテーマゾーンフィルタ（nullの場合は全表示）</summary>
        private ThemeZone? _currentZoneFilter;

        /// <summary>生成されたタブボタンのキャッシュ</summary>
        private readonly Dictionary<BuildCategory, Button> _tabButtons = new();

        /// <summary>生成されたアイテムカードのキャッシュ</summary>
        private readonly List<GameObject> _spawnedItemCards = new();

        // ============================================================
        // Unity ライフサイクル
        // ============================================================

        private void OnEnable()
        {
            BindButtons();
            InitializeCategoryTabs();
            InitializeThemeZoneFilter();
            SelectCategory(_selectedCategory);
            HideDetailPanel();
        }

        private void OnDisable()
        {
            UnbindButtons();
            CancelPlacement();
        }

        private void Update()
        {
            if (_isInPlacementMode)
            {
                UpdatePlacementPreview();
                HandlePlacementInput();
            }
        }

        // ============================================================
        // 初期化
        // ============================================================

        /// <summary>ボタンのイベントをバインドする</summary>
        private void BindButtons()
        {
            // 既存リスナーを除去してから追加（蓄積防止）
            confirmButton?.onClick.RemoveListener(OnConfirmClicked);
            cancelButton?.onClick.RemoveListener(OnCancelClicked);
            rotateButton?.onClick.RemoveListener(OnRotateClicked);

            confirmButton?.onClick.AddListener(OnConfirmClicked);
            cancelButton?.onClick.AddListener(OnCancelClicked);
            rotateButton?.onClick.AddListener(OnRotateClicked);
        }

        /// <summary>ボタンのイベントを解除する</summary>
        private void UnbindButtons()
        {
            confirmButton?.onClick.RemoveListener(OnConfirmClicked);
            cancelButton?.onClick.RemoveListener(OnCancelClicked);
            rotateButton?.onClick.RemoveListener(OnRotateClicked);
            themeZoneDropdown?.onValueChanged.RemoveListener(OnThemeZoneFilterChanged);
        }

        /// <summary>カテゴリタブを生成する</summary>
        private void InitializeCategoryTabs()
        {
            if (categoryTabContainer == null || categoryTabPrefab == null) return;

            // 既存タブをクリア
            foreach (Transform child in categoryTabContainer)
            {
                Destroy(child.gameObject);
            }
            _tabButtons.Clear();

            // カテゴリ定義が設定されていない場合はデフォルトを使用
            if (categoryDefinitions == null || categoryDefinitions.Length == 0)
            {
                categoryDefinitions = GenerateDefaultCategoryDefinitions();
            }

            foreach (var def in categoryDefinitions)
            {
                GameObject tabObj = Instantiate(categoryTabPrefab, categoryTabContainer);
                Button tabButton = tabObj.GetComponent<Button>();
                TextMeshProUGUI tabText = tabObj.GetComponentInChildren<TextMeshProUGUI>();
                Image tabIcon = tabObj.transform.Find("Icon")?.GetComponent<Image>();

                if (tabText != null) tabText.text = def.displayName;
                if (tabIcon != null && def.icon != null) tabIcon.sprite = def.icon;

                BuildCategory capturedCategory = def.category;
                tabButton?.onClick.AddListener(() => SelectCategory(capturedCategory));

                if (tabButton != null)
                {
                    _tabButtons[def.category] = tabButton;
                }
            }
        }

        /// <summary>デフォルトのカテゴリタブ定義を生成する</summary>
        private CategoryTabDefinition[] GenerateDefaultCategoryDefinitions()
        {
            return new CategoryTabDefinition[]
            {
                new() { displayName = "G系",     category = BuildCategory.GForce },
                new() { displayName = "縦回転",   category = BuildCategory.VerticalRotation },
                new() { displayName = "横回転",   category = BuildCategory.HorizontalRotation },
                new() { displayName = "展望",     category = BuildCategory.Observation },
                new() { displayName = "見せ物",   category = BuildCategory.ShowAttraction },
                new() { displayName = "乗り物",   category = BuildCategory.RideAttraction },
                new() { displayName = "フード",   category = BuildCategory.FoodShop },
                new() { displayName = "ドリンク", category = BuildCategory.DrinkShop },
                new() { displayName = "おみやげ", category = BuildCategory.SouvenirShop },
                new() { displayName = "トイレ",   category = BuildCategory.Toilet },
                new() { displayName = "ベンチ",   category = BuildCategory.Bench },
                new() { displayName = "ゴミ箱",   category = BuildCategory.TrashCan },
                new() { displayName = "案内板",   category = BuildCategory.InfoBoard },
                new() { displayName = "通路",     category = BuildCategory.Pathway },
                new() { displayName = "装飾",     category = BuildCategory.Decoration },
                new() { displayName = "休憩室",   category = BuildCategory.StaffRoom },
                new() { displayName = "研究所",   category = BuildCategory.ResearchLab },
            };
        }

        /// <summary>テーマゾーンフィルタのドロップダウンを初期化する</summary>
        private void InitializeThemeZoneFilter()
        {
            if (themeZoneDropdown == null) return;

            themeZoneDropdown.ClearOptions();

            var options = new List<string> { "全テーマ" };
            options.Add("ロストキングダム");
            options.Add("ハロウィーンワールド");
            options.Add("ワンダーランド");
            options.Add("スペースゾーン");

            themeZoneDropdown.AddOptions(options);
            themeZoneDropdown.onValueChanged.RemoveListener(OnThemeZoneFilterChanged);
            themeZoneDropdown.onValueChanged.AddListener(OnThemeZoneFilterChanged);
        }

        // ============================================================
        // カテゴリ選択
        // ============================================================

        /// <summary>カテゴリを選択してアイテム一覧を更新する</summary>
        public void SelectCategory(BuildCategory category)
        {
            _selectedCategory = category;

            // タブのビジュアル更新
            UpdateTabVisuals(category);

            // アイテムグリッド更新
            PopulateItemGrid(category);

            // 詳細パネルをリセット
            HideDetailPanel();

            // 配置モードをキャンセル
            CancelPlacement();
        }

        /// <summary>タブのビジュアルを選択状態に更新する</summary>
        private void UpdateTabVisuals(BuildCategory activeCategory)
        {
            foreach (var kvp in _tabButtons)
            {
                if (kvp.Value == null) continue;

                // 選択中のタブを強調表示
                ColorBlock colors = kvp.Value.colors;
                colors.normalColor = kvp.Key == activeCategory
                    ? new Color(0.3f, 0.7f, 1f, 1f)
                    : Color.white;
                kvp.Value.colors = colors;
            }
        }

        // ============================================================
        // アイテムグリッド
        // ============================================================

        /// <summary>選択カテゴリのアイテムカードをグリッドに配置する</summary>
        private void PopulateItemGrid(BuildCategory category)
        {
            ClearItemGrid();

            if (itemGridContainer == null || itemCardPrefab == null) return;

            // BuildItemDatabase から該当カテゴリのアイテムを取得
            List<BuildItemData> items = BuildItemDatabase.GetItemsByCategory(category);

            // テーマゾーンフィルタ適用
            if (_currentZoneFilter.HasValue)
            {
                items = items.Where(item =>
                    item.CompatibleZones.Contains(_currentZoneFilter.Value)
                ).ToList();
            }

            float currentMoney = GameManager.Instance?.EconomyManager?.CurrentMoney ?? 0f;

            foreach (var itemData in items)
            {
                GameObject cardObj = Instantiate(itemCardPrefab, itemGridContainer);
                _spawnedItemCards.Add(cardObj);

                SetupItemCard(cardObj, itemData, currentMoney);
            }
        }

        /// <summary>アイテムカードの表示を設定する</summary>
        private void SetupItemCard(GameObject cardObj, BuildItemData itemData, float currentMoney)
        {
            TextMeshProUGUI nameText = cardObj.transform.Find("NameText")?.GetComponent<TextMeshProUGUI>();
            TextMeshProUGUI costText = cardObj.transform.Find("CostText")?.GetComponent<TextMeshProUGUI>();
            Image thumbnail = cardObj.transform.Find("Thumbnail")?.GetComponent<Image>();
            Image lockOverlay = cardObj.transform.Find("LockOverlay")?.GetComponent<Image>();
            Button cardButton = cardObj.GetComponent<Button>();
            CanvasGroup canvasGroup = cardObj.GetComponent<CanvasGroup>();

            // 名前とコスト
            if (nameText != null) nameText.text = itemData.DisplayName;
            if (costText != null) costText.text = $"¥{itemData.Cost:N0}";

            // サムネイル
            if (thumbnail != null && itemData.Thumbnail != null)
            {
                thumbnail.sprite = itemData.Thumbnail;
            }

            // ロック状態の判定
            bool isUnlocked = itemData.IsUnlocked;
            bool canAfford = currentMoney >= itemData.Cost;

            if (lockOverlay != null)
            {
                lockOverlay.gameObject.SetActive(!isUnlocked);
            }

            // 購入不可の場合はグレーアウト
            if (canvasGroup != null)
            {
                canvasGroup.alpha = (isUnlocked && canAfford) ? 1f : 0.4f;
            }

            // コストテキストの色（購入不可の場合は赤）
            if (costText != null && isUnlocked)
            {
                costText.color = canAfford ? Color.white : Color.red;
            }

            // ボタンイベント
            if (cardButton != null)
            {
                cardButton.interactable = isUnlocked;
                BuildItemData capturedItem = itemData;
                cardButton.onClick.AddListener(() => OnItemCardClicked(capturedItem));
            }
        }

        /// <summary>アイテムグリッドをクリアする</summary>
        private void ClearItemGrid()
        {
            foreach (var card in _spawnedItemCards)
            {
                if (card != null) Destroy(card);
            }
            _spawnedItemCards.Clear();
        }

        // ============================================================
        // アイテム詳細パネル
        // ============================================================

        /// <summary>アイテムカードがクリックされた時の処理</summary>
        private void OnItemCardClicked(BuildItemData itemData)
        {
            _selectedItem = itemData;
            ShowDetailPanel(itemData);
        }

        /// <summary>アイテム詳細パネルを表示する</summary>
        private void ShowDetailPanel(BuildItemData itemData)
        {
            if (detailPanel == null) return;

            detailPanel.SetActive(true);

            if (detailNameText != null) detailNameText.text = itemData.DisplayName;
            if (detailCostText != null) detailCostText.text = $"建設費: ¥{itemData.Cost:N0}";
            if (detailDescriptionText != null) detailDescriptionText.text = itemData.Description;
            if (detailThumbnail != null && itemData.Thumbnail != null)
            {
                detailThumbnail.sprite = itemData.Thumbnail;
            }

            // ステータスバー更新
            SetStatBar(detailExcitementBar, detailExcitementValue, "興奮度", itemData.Excitement);
            SetStatBar(detailIntensityBar, detailIntensityValue, "激しさ", itemData.Intensity);
            SetStatBar(detailNauseaBar, detailNauseaValue, "酔い度", itemData.Nausea);
            SetStatBar(detailCapacityBar, detailCapacityValue, "定員", itemData.Capacity, 50f);

            // 確認ボタンの状態更新
            UpdateConfirmButton(itemData);
        }

        /// <summary>ステータスバーを設定する</summary>
        private void SetStatBar(Slider bar, TextMeshProUGUI valueText, string label, float value, float maxValue = 10f)
        {
            if (bar != null)
            {
                bar.maxValue = maxValue;
                bar.value = value;
            }
            if (valueText != null)
            {
                valueText.text = $"{label}: {value:F1}";
            }
        }

        /// <summary>詳細パネルを非表示にする</summary>
        private void HideDetailPanel()
        {
            if (detailPanel != null)
            {
                detailPanel.SetActive(false);
            }
            _selectedItem = null;
        }

        /// <summary>確認ボタンの有効/無効とテキストを更新する</summary>
        private void UpdateConfirmButton(BuildItemData itemData)
        {
            if (confirmButton == null) return;

            float currentMoney = GameManager.Instance?.EconomyManager?.CurrentMoney ?? 0f;
            bool canAfford = currentMoney >= itemData.Cost;

            confirmButton.interactable = canAfford && itemData.IsUnlocked;

            if (confirmButtonText != null)
            {
                confirmButtonText.text = canAfford
                    ? "配置する"
                    : "資金不足";
            }
        }

        // ============================================================
        // テーマゾーンフィルタ
        // ============================================================

        /// <summary>テーマゾーンフィルタが変更された時の処理</summary>
        private void OnThemeZoneFilterChanged(int index)
        {
            if (index == 0)
            {
                // 全テーマ（フィルタなし）
                _currentZoneFilter = null;
            }
            else
            {
                // index-1 がThemeZoneの列挙値に対応
                _currentZoneFilter = (ThemeZone)(index - 1);
            }

            // アイテムグリッドを再構築
            PopulateItemGrid(_selectedCategory);
        }

        // ============================================================
        // 配置プレビュー
        // ============================================================

        /// <summary>確認ボタン押下 - 配置モードを開始する</summary>
        private void OnConfirmClicked()
        {
            if (_selectedItem == null) return;

            float currentMoney = GameManager.Instance?.EconomyManager?.CurrentMoney ?? 0f;
            if (currentMoney < _selectedItem.Cost)
            {
                WebGLOptimizer.LogWarning("[BuildPanelUI] 資金が不足しています");
                return;
            }

            StartPlacementMode(_selectedItem);
        }

        /// <summary>キャンセルボタン押下</summary>
        private void OnCancelClicked()
        {
            if (_isInPlacementMode)
            {
                CancelPlacement();
            }
            else
            {
                HideDetailPanel();
            }
        }

        /// <summary>回転ボタン押下</summary>
        private void OnRotateClicked()
        {
            if (!_isInPlacementMode || _placementPreview == null) return;

            _previewRotation += 90f;
            if (_previewRotation >= 360f) _previewRotation = 0f;
            _placementPreview.transform.rotation = Quaternion.Euler(0f, _previewRotation, 0f);
        }

        /// <summary>配置モードを開始する</summary>
        private void StartPlacementMode(BuildItemData itemData)
        {
            _isInPlacementMode = true;
            _previewRotation = 0f;

            // プレビューオブジェクトを生成
            if (itemData.PreviewPrefab != null)
            {
                _placementPreview = Instantiate(itemData.PreviewPrefab);
            }
            else
            {
                // PreviewPrefabがnullの場合はプロシージャルメッシュでフォールバック
                _placementPreview = CreateProceduralPreview(itemData);
            }

            ApplyPreviewMaterial(_placementPreview, validPlacementMaterial);

            WebGLOptimizer.LogVerbose($"[BuildPanelUI] 配置モード開始: {itemData.DisplayName}");
        }

        /// <summary>配置プレビューを毎フレーム更新する</summary>
        private void UpdatePlacementPreview()
        {
            if (_placementPreview == null) return;

            // マウス位置からレイキャスト
            Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
            if (Physics.Raycast(ray, out RaycastHit hit, Mathf.Infinity, groundLayer))
            {
                // グリッドスナップ
                Vector3 snappedPos = SnapToGrid(hit.point);
                _placementPreview.transform.position = snappedPos;

                // 配置可否の判定
                bool wasValid = _isPlacementValid;
                _isPlacementValid = CheckPlacementValidity(snappedPos);

                if (wasValid != _isPlacementValid)
                {
                    Material mat = _isPlacementValid ? validPlacementMaterial : invalidPlacementMaterial;
                    ApplyPreviewMaterial(_placementPreview, mat);
                }
            }
        }

        /// <summary>配置入力を処理する</summary>
        private void HandlePlacementInput()
        {
            // 左クリックで配置確定
            if (Input.GetMouseButtonDown(0) && _isPlacementValid)
            {
                ConfirmPlacement();
            }

            // 右クリックまたはEscでキャンセル
            if (Input.GetMouseButtonDown(1) || Input.GetKeyDown(KeyCode.Escape))
            {
                CancelPlacement();
            }

            // Rキーで回転
            if (Input.GetKeyDown(KeyCode.R))
            {
                OnRotateClicked();
            }
        }

        /// <summary>座標をグリッドにスナップする</summary>
        private Vector3 SnapToGrid(Vector3 position)
        {
            float x = Mathf.Round(position.x / gridSize) * gridSize;
            float z = Mathf.Round(position.z / gridSize) * gridSize;
            return new Vector3(x, position.y, z);
        }

        /// <summary>指定位置への配置が有効かどうかを判定する</summary>
        private bool CheckPlacementValidity(Vector3 position)
        {
            // 他のオブジェクトとの衝突判定
            if (_selectedItem == null) return false;

            Vector3 halfExtents = _selectedItem.PlacementSize * 0.5f;
            Collider[] overlaps = Physics.OverlapBox(
                position + Vector3.up * halfExtents.y,
                halfExtents,
                Quaternion.Euler(0f, _previewRotation, 0f)
            );

            // 自身のプレビューコライダーは除外
            foreach (var col in overlaps)
            {
                if (col.gameObject != _placementPreview &&
                    col.gameObject.layer != LayerMask.NameToLayer("Ground"))
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>プレビューオブジェクトにマテリアルを適用する</summary>
        private void ApplyPreviewMaterial(GameObject obj, Material material)
        {
            if (obj == null || material == null) return;

            var renderers = obj.GetComponentsInChildren<Renderer>();
            foreach (var renderer in renderers)
            {
                Material[] mats = new Material[renderer.materials.Length];
                for (int i = 0; i < mats.Length; i++)
                {
                    mats[i] = material;
                }
                renderer.materials = mats;
            }
        }

        /// <summary>配置を確定する</summary>
        private void ConfirmPlacement()
        {
            if (_selectedItem == null || _placementPreview == null) return;

            Vector3 position = _placementPreview.transform.position;
            Quaternion rotation = _placementPreview.transform.rotation;

            // プレビューを削除
            Destroy(_placementPreview);
            _placementPreview = null;
            _isInPlacementMode = false;

            // 実際のオブジェクトを配置
            GameObject placed;
            if (_selectedItem.ActualPrefab != null)
            {
                placed = Instantiate(_selectedItem.ActualPrefab, position, rotation);
            }
            else
            {
                // ActualPrefabがnullの場合はプロシージャル生成でフォールバック
                placed = CreateProceduralFacility(_selectedItem, position, rotation);
            }

            // コスト支払い
            GameManager.Instance?.EconomyManager?.SpendMoney(_selectedItem.Cost);

            // ParkManagerに配置を通知
            var parkManager = GameManager.Instance?.ParkManager;
            var facility = placed?.GetComponent<FacilityBase>();
            if (facility != null && parkManager != null)
            {
                Vector2Int gridPos = new Vector2Int(
                    Mathf.RoundToInt(position.x / gridSize),
                    Mathf.RoundToInt(position.z / gridSize));
                ThemeZone zone = _currentZoneFilter ?? ThemeZone.LostKingdom;
                facility.Place(gridPos, zone);
            }

            // 建設イベント発火
            GameEvents.FireAttractionBuilt(_selectedItem.ItemId);

            WebGLOptimizer.LogVerbose($"[BuildPanelUI] 配置完了: {_selectedItem.DisplayName} at {position}");

            // アイテムグリッドを再更新（資金反映）
            PopulateItemGrid(_selectedCategory);
        }

        /// <summary>配置をキャンセルする</summary>
        private void CancelPlacement()
        {
            if (_placementPreview != null)
            {
                Destroy(_placementPreview);
                _placementPreview = null;
            }
            _isInPlacementMode = false;
            _isPlacementValid = false;
        }

        // ============================================================
        // プロシージャル生成フォールバック
        // ============================================================

        /// <summary>プレビュー用のプロシージャルメッシュを生成する</summary>
        private GameObject CreateProceduralPreview(BuildItemData itemData)
        {
            PrimitiveType shape = IsAttractionCategory(itemData.Category)
                ? PrimitiveType.Cylinder : PrimitiveType.Cube;
            GameObject obj = GameObject.CreatePrimitive(shape);
            obj.name = $"Preview_{itemData.DisplayName}";
            obj.transform.localScale = itemData.PlacementSize;
            return obj;
        }

        /// <summary>
        /// ActualPrefabがnullの場合にプロシージャルメッシュで施設を生成する。
        /// カテゴリに応じた色・タグ・コンポーネントを付与する。
        /// </summary>
        private GameObject CreateProceduralFacility(BuildItemData itemData, Vector3 position, Quaternion rotation)
        {
            string objName = itemData.DisplayName ?? $"Facility_{itemData.Category}";
            PrimitiveType shape = IsAttractionCategory(itemData.Category)
                ? PrimitiveType.Cylinder : PrimitiveType.Cube;

            GameObject obj = GameObject.CreatePrimitive(shape);
            obj.name = objName;
            obj.transform.position = position;
            obj.transform.rotation = rotation;
            obj.transform.localScale = itemData.PlacementSize;

            // タグ設定
            string tag = GetTagForCategory(itemData.Category);
            try { obj.tag = tag; }
            catch (UnityException) { /* タグが未登録の場合は無視 */ }

            // マテリアル設定
            var renderer = obj.GetComponent<Renderer>();
            if (renderer != null)
            {
                var mat = new Material(Shader.Find("Standard") ?? Shader.Find("Sprites/Default"));
                mat.color = GetColorForCategory(itemData.Category);
                renderer.material = mat;
            }

            // カテゴリに応じたコンポーネントのアタッチ
            AttachComponentsForCategory(obj, itemData);

            return obj;
        }

        /// <summary>カテゴリに応じたゲームコンポーネントをアタッチする</summary>
        private void AttachComponentsForCategory(GameObject obj, BuildItemData itemData)
        {
            switch (itemData.Category)
            {
                case BuildCategory.GForce:
                case BuildCategory.VerticalRotation:
                case BuildCategory.HorizontalRotation:
                case BuildCategory.Observation:
                case BuildCategory.ShowAttraction:
                case BuildCategory.RideAttraction:
                    obj.AddComponent<ThemeParkGame.Attraction.Attraction>();
                    obj.AddComponent<FacilityDirt>();
                    break;

                case BuildCategory.FoodShop:
                {
                    var shop = obj.AddComponent<Shop>();
                    shop.ConfigureRuntime(itemData.DisplayName ?? "フードショップ",
                        ShopType.FoodShop, 50, 120, 30);
                    obj.AddComponent<FacilityDirt>();
                    break;
                }
                case BuildCategory.DrinkShop:
                {
                    var shop = obj.AddComponent<Shop>();
                    shop.ConfigureRuntime(itemData.DisplayName ?? "ドリンクショップ",
                        ShopType.DrinkShop, 30, 80, 40);
                    obj.AddComponent<FacilityDirt>();
                    break;
                }
                case BuildCategory.SouvenirShop:
                {
                    var shop = obj.AddComponent<Shop>();
                    shop.ConfigureRuntime(itemData.DisplayName ?? "お土産ショップ",
                        ShopType.SouvenirShop, 100, 250, 20);
                    obj.AddComponent<FacilityDirt>();
                    break;
                }

                case BuildCategory.Toilet:
                    obj.AddComponent<ToiletFacility>();
                    obj.AddComponent<FacilityDirt>();
                    break;
                case BuildCategory.Bench:
                    obj.AddComponent<BenchFacility>();
                    break;
                case BuildCategory.TrashCan:
                case BuildCategory.InfoBoard:
                case BuildCategory.StaffRoom:
                case BuildCategory.ResearchLab:
                case BuildCategory.Pathway:
                case BuildCategory.Decoration:
                {
                    var generic = obj.AddComponent<GenericFacility>();
                    generic.SetFacilityType(GetFacilityTypeForCategory(itemData.Category));
                    generic.SetDisplayName(itemData.DisplayName ?? itemData.Category.ToString());
                    break;
                }
            }
        }

        /// <summary>カテゴリがアトラクション系かどうか判定する</summary>
        private static bool IsAttractionCategory(BuildCategory category)
        {
            return category == BuildCategory.GForce
                || category == BuildCategory.VerticalRotation
                || category == BuildCategory.HorizontalRotation
                || category == BuildCategory.Observation
                || category == BuildCategory.ShowAttraction
                || category == BuildCategory.RideAttraction;
        }

        /// <summary>カテゴリに対応するタグ名を返す</summary>
        private static string GetTagForCategory(BuildCategory category)
        {
            switch (category)
            {
                case BuildCategory.GForce:
                case BuildCategory.VerticalRotation:
                case BuildCategory.HorizontalRotation:
                case BuildCategory.Observation:
                case BuildCategory.ShowAttraction:
                case BuildCategory.RideAttraction:
                    return "Attraction";
                case BuildCategory.FoodShop:    return "FoodShop";
                case BuildCategory.DrinkShop:   return "DrinkShop";
                case BuildCategory.SouvenirShop: return "SouvenirShop";
                case BuildCategory.Toilet:      return "Toilet";
                case BuildCategory.Bench:       return "Bench";
                case BuildCategory.TrashCan:    return "TrashCan";
                case BuildCategory.InfoBoard:   return "InfoBoard";
                case BuildCategory.StaffRoom:   return "StaffRoom";
                case BuildCategory.ResearchLab: return "ResearchLab";
                case BuildCategory.Pathway:     return "Pathway";
                case BuildCategory.Decoration:  return "Untagged";
                default: return "Untagged";
            }
        }

        /// <summary>カテゴリに対応するFacilityTypeを返す</summary>
        private static FacilityType GetFacilityTypeForCategory(BuildCategory category)
        {
            switch (category)
            {
                case BuildCategory.TrashCan:    return FacilityType.TrashCan;
                case BuildCategory.InfoBoard:   return FacilityType.InfoBoard;
                case BuildCategory.StaffRoom:   return FacilityType.StaffRoom;
                case BuildCategory.ResearchLab: return FacilityType.ResearchLab;
                case BuildCategory.Pathway:     return FacilityType.Pathway;
                case BuildCategory.Decoration:  return FacilityType.Decoration;
                default: return FacilityType.TrashCan;
            }
        }

        /// <summary>カテゴリに対応するプロシージャル色を返す</summary>
        private static Color GetColorForCategory(BuildCategory category)
        {
            switch (category)
            {
                case BuildCategory.GForce:             return new Color(0.9f, 0.2f, 0.2f);
                case BuildCategory.VerticalRotation:   return new Color(0.8f, 0.3f, 0.1f);
                case BuildCategory.HorizontalRotation: return new Color(0.9f, 0.5f, 0.1f);
                case BuildCategory.Observation:        return new Color(0.2f, 0.6f, 0.9f);
                case BuildCategory.ShowAttraction:     return new Color(0.8f, 0.2f, 0.8f);
                case BuildCategory.RideAttraction:     return new Color(0.3f, 0.8f, 0.3f);
                case BuildCategory.FoodShop:           return new Color(1.0f, 0.7f, 0.2f);
                case BuildCategory.DrinkShop:          return new Color(0.3f, 0.7f, 1.0f);
                case BuildCategory.SouvenirShop:       return new Color(0.9f, 0.4f, 0.7f);
                case BuildCategory.Toilet:             return new Color(0.8f, 0.8f, 0.9f);
                case BuildCategory.Bench:              return new Color(0.6f, 0.4f, 0.2f);
                case BuildCategory.TrashCan:           return new Color(0.4f, 0.5f, 0.4f);
                case BuildCategory.InfoBoard:          return new Color(0.3f, 0.5f, 0.8f);
                case BuildCategory.Pathway:            return new Color(0.7f, 0.7f, 0.7f);
                case BuildCategory.Decoration:         return new Color(0.5f, 0.8f, 0.5f);
                case BuildCategory.StaffRoom:          return new Color(0.6f, 0.6f, 0.7f);
                case BuildCategory.ResearchLab:        return new Color(0.3f, 0.9f, 0.9f);
                default: return Color.gray;
            }
        }
    }

    // ============================================================
    // BuildItemData - 建設アイテムのデータクラス
    // ============================================================

    /// <summary>
    /// 建設可能なアイテムのデータ構造。
    /// ScriptableObjectから読み込むか、データベースから動的に取得される。
    /// </summary>
    [Serializable]
    public class BuildItemData
    {
        /// <summary>アイテム固有ID</summary>
        public int ItemId;

        /// <summary>表示名（日本語）</summary>
        public string DisplayName;

        /// <summary>説明文（日本語）</summary>
        public string Description;

        /// <summary>建設コスト</summary>
        public float Cost;

        /// <summary>カテゴリ</summary>
        public BuildPanelUI.BuildCategory Category;

        /// <summary>対応テーマゾーン</summary>
        public ThemeZone[] CompatibleZones;

        /// <summary>サムネイル画像</summary>
        public Sprite Thumbnail;

        /// <summary>プレビュー用プレハブ（半透明）</summary>
        public GameObject PreviewPrefab;

        /// <summary>実体プレハブ</summary>
        public GameObject ActualPrefab;

        /// <summary>興奮度 (0-10)</summary>
        public float Excitement;

        /// <summary>激しさ (0-10)</summary>
        public float Intensity;

        /// <summary>酔い度 (0-10)</summary>
        public float Nausea;

        /// <summary>定員</summary>
        public float Capacity;

        /// <summary>配置サイズ（グリッド単位）</summary>
        public Vector3 PlacementSize = Vector3.one;

        /// <summary>アンロック済みかどうか（研究完了/初期解放）</summary>
        public bool IsUnlocked;
    }

    // ============================================================
    // BuildItemDatabase - 建設アイテムデータベース（スタブ）
    // ============================================================

    /// <summary>
    /// 建設アイテムのマスタデータを管理する静的クラス。
    /// 実装時にScriptableObjectベースのデータベースに差し替える。
    /// </summary>
    public static class BuildItemDatabase
    {
        // BuildCategory → ResearchCategory のマッピング
        // アトラクション系カテゴリはResearchCategory.Attractions、
        // ショップ系はResearchCategory.Shops、施設系はResearchCategory.Facilities
        private static readonly Dictionary<BuildPanelUI.BuildCategory, ThemeParkGame.Attraction.ResearchCategory> CategoryToResearchCategory
            = new Dictionary<BuildPanelUI.BuildCategory, ThemeParkGame.Attraction.ResearchCategory>
        {
            // アトラクション系
            { BuildPanelUI.BuildCategory.GForce,             ThemeParkGame.Attraction.ResearchCategory.Attractions },
            { BuildPanelUI.BuildCategory.VerticalRotation,   ThemeParkGame.Attraction.ResearchCategory.Attractions },
            { BuildPanelUI.BuildCategory.HorizontalRotation, ThemeParkGame.Attraction.ResearchCategory.Attractions },
            { BuildPanelUI.BuildCategory.Observation,        ThemeParkGame.Attraction.ResearchCategory.Attractions },
            { BuildPanelUI.BuildCategory.ShowAttraction,     ThemeParkGame.Attraction.ResearchCategory.Attractions },
            { BuildPanelUI.BuildCategory.RideAttraction,     ThemeParkGame.Attraction.ResearchCategory.Attractions },
            // ショップ系
            { BuildPanelUI.BuildCategory.FoodShop,           ThemeParkGame.Attraction.ResearchCategory.Shops },
            { BuildPanelUI.BuildCategory.DrinkShop,          ThemeParkGame.Attraction.ResearchCategory.Shops },
            { BuildPanelUI.BuildCategory.SouvenirShop,       ThemeParkGame.Attraction.ResearchCategory.Shops },
            // 施設系
            { BuildPanelUI.BuildCategory.Toilet,             ThemeParkGame.Attraction.ResearchCategory.Facilities },
            { BuildPanelUI.BuildCategory.Bench,              ThemeParkGame.Attraction.ResearchCategory.Facilities },
            { BuildPanelUI.BuildCategory.TrashCan,           ThemeParkGame.Attraction.ResearchCategory.Facilities },
            { BuildPanelUI.BuildCategory.InfoBoard,          ThemeParkGame.Attraction.ResearchCategory.Facilities },
            { BuildPanelUI.BuildCategory.StaffRoom,          ThemeParkGame.Attraction.ResearchCategory.Facilities },
            { BuildPanelUI.BuildCategory.ResearchLab,        ThemeParkGame.Attraction.ResearchCategory.Facilities },
        };

        // BuildCategory → FacilityType のマッピング（研究結果フィルタ用）
        private static readonly Dictionary<BuildPanelUI.BuildCategory, FacilityType> CategoryToFacilityType
            = new Dictionary<BuildPanelUI.BuildCategory, FacilityType>
        {
            { BuildPanelUI.BuildCategory.GForce,             FacilityType.Attraction },
            { BuildPanelUI.BuildCategory.VerticalRotation,   FacilityType.Attraction },
            { BuildPanelUI.BuildCategory.HorizontalRotation, FacilityType.Attraction },
            { BuildPanelUI.BuildCategory.Observation,        FacilityType.Attraction },
            { BuildPanelUI.BuildCategory.ShowAttraction,     FacilityType.Attraction },
            { BuildPanelUI.BuildCategory.RideAttraction,     FacilityType.Attraction },
            { BuildPanelUI.BuildCategory.FoodShop,           FacilityType.FoodShop },
            { BuildPanelUI.BuildCategory.DrinkShop,          FacilityType.DrinkShop },
            { BuildPanelUI.BuildCategory.SouvenirShop,       FacilityType.SouvenirShop },
            { BuildPanelUI.BuildCategory.Toilet,             FacilityType.Toilet },
            { BuildPanelUI.BuildCategory.Bench,              FacilityType.Bench },
            { BuildPanelUI.BuildCategory.TrashCan,           FacilityType.TrashCan },
            { BuildPanelUI.BuildCategory.InfoBoard,          FacilityType.InfoBoard },
            { BuildPanelUI.BuildCategory.StaffRoom,          FacilityType.StaffRoom },
            { BuildPanelUI.BuildCategory.ResearchLab,        FacilityType.ResearchLab },
        };

        /// <summary>カテゴリに一致するアイテム一覧を取得する</summary>
        public static List<BuildItemData> GetItemsByCategory(BuildPanelUI.BuildCategory category)
        {
            var items = new List<BuildItemData>();

            if (GameManager.Instance == null || GameManager.Instance.ResearchManager == null)
                return items;

            var researchManager = GameManager.Instance.ResearchManager;

            // このBuildCategoryに対応するResearchCategoryとFacilityTypeを取得
            if (!CategoryToResearchCategory.TryGetValue(category, out var researchCategory))
                return items;
            if (!CategoryToFacilityType.TryGetValue(category, out var facilityType))
                return items;

            // 対応する研究カテゴリの完了済み研究からアイテムを取得
            foreach (var research in researchManager.GetResearchByCategory(researchCategory))
            {
                // UnlockedFacilityType でフィルタし、このBuildCategoryに該当するもののみ追加
                if (research.CurrentState == ResearchState.Completed &&
                    research.UnlockedFacilityType == facilityType)
                {
                    bool isAttraction = researchCategory == ThemeParkGame.Attraction.ResearchCategory.Attractions;
                    string idSource = isAttraction ? research.UnlockedAttractionId : research.ResearchId;
                    float cost = isAttraction ? research.ResearchCost * 2f : research.ResearchCost;
                    float size = isAttraction ? 3f : 2f;

                    items.Add(new BuildItemData
                    {
                        ItemId = idSource?.GetHashCode() ?? 0,
                        DisplayName = research.NameJP,
                        Description = research.Description,
                        Cost = cost,
                        PlacementSize = new Vector3(size, 1f, size),
                        IsUnlocked = true
                    });
                }
            }

            // トイレとベンチはデフォルトで利用可能（研究不要）
            if (category == BuildPanelUI.BuildCategory.Toilet)
            {
                items.Add(new BuildItemData
                {
                    ItemId = "TOILET".GetHashCode(),
                    DisplayName = "トイレ",
                    Description = "来場者のトイレ欲求を満たす基本施設",
                    Cost = 500f,
                    PlacementSize = new Vector3(1f, 1f, 1f),
                    IsUnlocked = true
                });
            }
            else if (category == BuildPanelUI.BuildCategory.Bench)
            {
                items.Add(new BuildItemData
                {
                    ItemId = "BENCH".GetHashCode(),
                    DisplayName = "ベンチ",
                    Description = "来場者が休憩できるベンチ",
                    Cost = 100f,
                    PlacementSize = new Vector3(1f, 1f, 1f),
                    IsUnlocked = true
                });
            }

            return items;
        }
    }
}
