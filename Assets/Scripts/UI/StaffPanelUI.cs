// ============================================================
// ThemeParkGame - StaffPanelUI
// スタッフ管理パネル：タブ別一覧、雇用、訓練、解雇、
// パトロール指定、休憩指示
// ============================================================

using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using ThemeParkGame.Core;
using ThemeParkGame.Staff;

namespace ThemeParkGame.UI
{
    /// <summary>
    /// スタッフ管理パネル。
    /// 各種スタッフの雇用・管理・配置を行うインターフェース。
    /// スタッフの疲労管理やスキル向上を通じてパークの品質を維持する。
    /// </summary>
    public class StaffPanelUI : MonoBehaviour
    {
        // ============================================================
        // スタッフタイプタブ
        // ============================================================

        [Header("スタッフタイプタブ")]
        [SerializeField] private Button mechanicTabButton;
        [SerializeField] private Button cleanerTabButton;
        [SerializeField] private Button entertainerTabButton;
        [SerializeField] private Button guardTabButton;
        [SerializeField] private Button scientistTabButton;

        [Header("タブカウントテキスト")]
        [SerializeField] private TextMeshProUGUI mechanicCountText;
        [SerializeField] private TextMeshProUGUI cleanerCountText;
        [SerializeField] private TextMeshProUGUI entertainerCountText;
        [SerializeField] private TextMeshProUGUI guardCountText;
        [SerializeField] private TextMeshProUGUI scientistCountText;

        [Tooltip("選択中タブの色")]
        [SerializeField] private Color activeTabColor = new Color(0.3f, 0.7f, 1f, 1f);
        [SerializeField] private Color inactiveTabColor = new Color(0.7f, 0.7f, 0.7f, 1f);

        // ============================================================
        // 雇用セクション
        // ============================================================

        [Header("雇用セクション")]
        [SerializeField] private Button hireButton;
        [SerializeField] private TextMeshProUGUI hireButtonText;
        [SerializeField] private TextMeshProUGUI salaryDisplayText;
        [SerializeField] private TextMeshProUGUI hireCostText;

        // ============================================================
        // スタッフリスト
        // ============================================================

        [Header("スタッフリスト")]
        [SerializeField] private Transform staffListContainer;
        [SerializeField] private GameObject staffListItemPrefab;
        [SerializeField] private ScrollRect staffListScrollRect;
        [SerializeField] private TextMeshProUGUI emptyListMessage;

        // ============================================================
        // スタッフ詳細（選択時）
        // ============================================================

        [Header("スタッフ詳細")]
        [SerializeField] private GameObject staffDetailPanel;
        [SerializeField] private TextMeshProUGUI detailNameText;
        [SerializeField] private TextMeshProUGUI detailTypeText;
        [SerializeField] private TextMeshProUGUI detailStatusText;
        [SerializeField] private Slider detailSkillBar;
        [SerializeField] private TextMeshProUGUI detailSkillText;
        [SerializeField] private Slider detailFatigueBar;
        [SerializeField] private TextMeshProUGUI detailFatigueText;
        [SerializeField] private TextMeshProUGUI detailSalaryText;
        [SerializeField] private Image detailPortrait;

        // ============================================================
        // アクションボタン
        // ============================================================

        [Header("アクションボタン")]
        [SerializeField] private Button trainButton;
        [SerializeField] private TextMeshProUGUI trainButtonText;
        [SerializeField] private TextMeshProUGUI trainingCostText;
        [SerializeField] private Button fireButton;
        [SerializeField] private Button assignPatrolButton;
        [SerializeField] private TextMeshProUGUI assignPatrolButtonText;
        [SerializeField] private Button restButton;
        [SerializeField] private TextMeshProUGUI restButtonText;

        // ============================================================
        // 解雇確認ダイアログ
        // ============================================================

        [Header("解雇確認ダイアログ")]
        [SerializeField] private GameObject fireConfirmDialog;
        [SerializeField] private TextMeshProUGUI fireConfirmMessage;
        [SerializeField] private Button fireConfirmYesButton;
        [SerializeField] private Button fireConfirmNoButton;

        // ============================================================
        // 閉じるボタン
        // ============================================================

        [Header("パネル制御")]
        [SerializeField] private Button closeButton;

        // ============================================================
        // 内部状態
        // ============================================================

        /// <summary>現在選択中のスタッフタイプ</summary>
        private StaffType _selectedStaffType = StaffType.Mechanic;

        /// <summary>現在選択中のスタッフID</summary>
        private int _selectedStaffId = -1;

        /// <summary>生成済みリストアイテム</summary>
        private readonly List<GameObject> _spawnedListItems = new();

        /// <summary>タブボタンとカウントテキストのマッピング</summary>
        private Dictionary<StaffType, (Button button, TextMeshProUGUI countText)> _tabMapping;

        /// <summary>ボタンバインド済みフラグ（多重バインド防止）</summary>
        private bool _buttonsAreBound;

        /// <summary>各スタッフタイプの基本給与テーブル</summary>
        private static readonly Dictionary<StaffType, int> BaseSalaryTable = new()
        {
            { StaffType.Mechanic,    1500 },
            { StaffType.Cleaner,     1000 },
            { StaffType.Entertainer, 1200 },
            { StaffType.Guard,       1300 },
            { StaffType.Scientist,   2000 }
        };

        /// <summary>各スタッフタイプの雇用コスト</summary>
        private static readonly Dictionary<StaffType, int> HireCostTable = new()
        {
            { StaffType.Mechanic,    3000 },
            { StaffType.Cleaner,     2000 },
            { StaffType.Entertainer, 2500 },
            { StaffType.Guard,       2800 },
            { StaffType.Scientist,   5000 }
        };

        /// <summary>スキル訓練コスト（レベルに応じて上昇）</summary>
        private const int BaseTrainingCost = 1000;

        // ============================================================
        // Unity ライフサイクル
        // ============================================================

        private void Awake()
        {
            InitializeTabMapping();
        }

        private void OnEnable()
        {
            SubscribeToEvents();
            BindButtons();
            SelectStaffType(_selectedStaffType);
            HideDetailPanel();
            HideFireConfirmDialog();
        }

        private void OnDisable()
        {
            UnbindButtons();
            UnsubscribeFromEvents();
        }

        // ============================================================
        // 初期化
        // ============================================================

        /// <summary>タブマッピングを初期化する</summary>
        private void InitializeTabMapping()
        {
            _tabMapping = new Dictionary<StaffType, (Button, TextMeshProUGUI)>
            {
                { StaffType.Mechanic,    (mechanicTabButton,    mechanicCountText) },
                { StaffType.Cleaner,     (cleanerTabButton,     cleanerCountText) },
                { StaffType.Entertainer, (entertainerTabButton, entertainerCountText) },
                { StaffType.Guard,       (guardTabButton,       guardCountText) },
                { StaffType.Scientist,   (scientistTabButton,   scientistCountText) }
            };
        }

        /// <summary>ボタンのイベントをバインドする（多重バインド防止付き）</summary>
        private void BindButtons()
        {
            if (_buttonsAreBound) return;
            _buttonsAreBound = true;

            // タブボタン（ラムダのため RemoveAllListeners で一括クリア後に追加）
            if (mechanicTabButton != null)    { mechanicTabButton.onClick.RemoveAllListeners();    mechanicTabButton.onClick.AddListener(() => SelectStaffType(StaffType.Mechanic)); }
            if (cleanerTabButton != null)     { cleanerTabButton.onClick.RemoveAllListeners();     cleanerTabButton.onClick.AddListener(() => SelectStaffType(StaffType.Cleaner)); }
            if (entertainerTabButton != null) { entertainerTabButton.onClick.RemoveAllListeners(); entertainerTabButton.onClick.AddListener(() => SelectStaffType(StaffType.Entertainer)); }
            if (guardTabButton != null)       { guardTabButton.onClick.RemoveAllListeners();       guardTabButton.onClick.AddListener(() => SelectStaffType(StaffType.Guard)); }
            if (scientistTabButton != null)   { scientistTabButton.onClick.RemoveAllListeners();   scientistTabButton.onClick.AddListener(() => SelectStaffType(StaffType.Scientist)); }

            // 雇用ボタン
            hireButton?.onClick.RemoveListener(OnHireClicked);
            hireButton?.onClick.AddListener(OnHireClicked);

            // アクションボタン
            trainButton?.onClick.RemoveListener(OnTrainClicked);
            trainButton?.onClick.AddListener(OnTrainClicked);
            fireButton?.onClick.RemoveListener(OnFireClicked);
            fireButton?.onClick.AddListener(OnFireClicked);
            assignPatrolButton?.onClick.RemoveListener(OnAssignPatrolClicked);
            assignPatrolButton?.onClick.AddListener(OnAssignPatrolClicked);
            restButton?.onClick.RemoveListener(OnRestClicked);
            restButton?.onClick.AddListener(OnRestClicked);

            // 解雇確認ダイアログ
            fireConfirmYesButton?.onClick.RemoveListener(OnFireConfirmed);
            fireConfirmYesButton?.onClick.AddListener(OnFireConfirmed);
            fireConfirmNoButton?.onClick.RemoveListener(HideFireConfirmDialog);
            fireConfirmNoButton?.onClick.AddListener(HideFireConfirmDialog);

            // 閉じる
            closeButton?.onClick.RemoveListener(OnCloseClicked);
            closeButton?.onClick.AddListener(OnCloseClicked);
        }

        /// <summary>ボタンのイベントを解除する</summary>
        private void UnbindButtons()
        {
            if (!_buttonsAreBound) return;
            _buttonsAreBound = false;

            // タブボタン（ラムダなので RemoveAllListeners）
            mechanicTabButton?.onClick.RemoveAllListeners();
            cleanerTabButton?.onClick.RemoveAllListeners();
            entertainerTabButton?.onClick.RemoveAllListeners();
            guardTabButton?.onClick.RemoveAllListeners();
            scientistTabButton?.onClick.RemoveAllListeners();

            // 名前付きメソッドは RemoveListener で解除
            hireButton?.onClick.RemoveListener(OnHireClicked);
            trainButton?.onClick.RemoveListener(OnTrainClicked);
            fireButton?.onClick.RemoveListener(OnFireClicked);
            assignPatrolButton?.onClick.RemoveListener(OnAssignPatrolClicked);
            restButton?.onClick.RemoveListener(OnRestClicked);
            fireConfirmYesButton?.onClick.RemoveListener(OnFireConfirmed);
            fireConfirmNoButton?.onClick.RemoveListener(HideFireConfirmDialog);
            closeButton?.onClick.RemoveListener(OnCloseClicked);
        }

        // ============================================================
        // イベント購読
        // ============================================================

        private void SubscribeToEvents()
        {
            GameEvents.OnStaffHired += HandleStaffHired;
            GameEvents.OnStaffFired += HandleStaffFired;
            GameEvents.OnStaffWentOnStrike += HandleStaffWentOnStrike;
            GameEvents.OnStaffFinishedTask += HandleStaffFinishedTask;
            GameEvents.OnMoneyChanged += HandleMoneyChanged;
        }

        private void UnsubscribeFromEvents()
        {
            GameEvents.OnStaffHired -= HandleStaffHired;
            GameEvents.OnStaffFired -= HandleStaffFired;
            GameEvents.OnStaffWentOnStrike -= HandleStaffWentOnStrike;
            GameEvents.OnStaffFinishedTask -= HandleStaffFinishedTask;
            GameEvents.OnMoneyChanged -= HandleMoneyChanged;
        }

        // ============================================================
        // タブ選択
        // ============================================================

        /// <summary>スタッフタイプタブを選択する</summary>
        public void SelectStaffType(StaffType type)
        {
            _selectedStaffType = type;

            UpdateTabVisuals(type);
            UpdateHireSection(type);
            PopulateStaffList(type);
            HideDetailPanel();

            RefreshAllTabCounts();
        }

        /// <summary>タブの選択状態ビジュアルを更新する</summary>
        private void UpdateTabVisuals(StaffType activeType)
        {
            foreach (var kvp in _tabMapping)
            {
                if (kvp.Value.button == null) continue;

                ColorBlock colors = kvp.Value.button.colors;
                colors.normalColor = kvp.Key == activeType ? activeTabColor : inactiveTabColor;
                kvp.Value.button.colors = colors;
            }
        }

        /// <summary>全タブの人数表示を更新する</summary>
        private void RefreshAllTabCounts()
        {
            var staffManager = GameManager.Instance?.StaffManager;
            if (staffManager == null) return;

            foreach (var kvp in _tabMapping)
            {
                if (kvp.Value.countText == null) continue;

                int count = staffManager.GetStaffCount(kvp.Key);
                kvp.Value.countText.text = $"{GetStaffTypeDisplayName(kvp.Key)} ({count})";
            }
        }

        // ============================================================
        // 雇用セクション
        // ============================================================

        /// <summary>雇用セクションの表示を更新する</summary>
        private void UpdateHireSection(StaffType type)
        {
            int salary = BaseSalaryTable.GetValueOrDefault(type, 1000);
            int hireCost = HireCostTable.GetValueOrDefault(type, 2000);
            float currentMoney = GameManager.Instance?.EconomyManager?.CurrentMoney ?? 0f;
            bool canAfford = currentMoney >= hireCost;

            if (salaryDisplayText != null)
                salaryDisplayText.text = $"月給: ¥{salary:N0}";

            if (hireCostText != null)
            {
                hireCostText.text = $"雇用費: ¥{hireCost:N0}";
                hireCostText.color = canAfford ? Color.white : Color.red;
            }

            if (hireButton != null)
                hireButton.interactable = canAfford;

            if (hireButtonText != null)
                hireButtonText.text = canAfford ? "雇う" : "資金不足";
        }

        /// <summary>雇用ボタン押下</summary>
        private void OnHireClicked()
        {
            var gm = GameManager.Instance;
            if (gm?.StaffManager == null || gm.EconomyManager == null) return;

            // 資金チェック（StaffManager.HireStaff内でも行われるが、UIフィードバック用に事前チェック）
            float hireCost = gm.StaffManager.GetHiringCost(_selectedStaffType);
            if (!gm.EconomyManager.CanAfford(hireCost))
            {
                WebGLOptimizer.LogWarning("[StaffPanelUI] 雇用資金が不足しています");
                return;
            }

            // 雇用実行（費用はStaffManager内で処理される）
            // スポーン位置はパークの入口付近をデフォルトとする
            Vector3 spawnPosition = gm.transform.position;
            StaffMember newStaff = gm.StaffManager.HireStaff(_selectedStaffType, spawnPosition);

            if (newStaff != null)
            {
                WebGLOptimizer.LogVerbose($"[StaffPanelUI] スタッフ雇用完了: {_selectedStaffType}, ID={newStaff.Id}");
            }

            // リスト更新
            PopulateStaffList(_selectedStaffType);
            RefreshAllTabCounts();
            UpdateHireSection(_selectedStaffType);
        }

        // ============================================================
        // スタッフリスト
        // ============================================================

        /// <summary>選択タイプのスタッフ一覧を表示する</summary>
        private void PopulateStaffList(StaffType type)
        {
            ClearStaffList();

            var staffManager = GameManager.Instance?.StaffManager;
            if (staffManager == null) return;

            List<StaffDisplayData> staffList = staffManager.GetStaffListByType(type);

            if (staffList == null || staffList.Count == 0)
            {
                if (emptyListMessage != null)
                {
                    emptyListMessage.gameObject.SetActive(true);
                    emptyListMessage.text = $"{GetStaffTypeDisplayName(type)}はまだ雇用されていません";
                }
                return;
            }

            if (emptyListMessage != null)
                emptyListMessage.gameObject.SetActive(false);

            foreach (var staffData in staffList)
            {
                CreateStaffListItem(staffData);
            }
        }

        /// <summary>スタッフリストアイテムを生成する</summary>
        private void CreateStaffListItem(StaffDisplayData data)
        {
            if (staffListContainer == null || staffListItemPrefab == null) return;

            GameObject itemObj = Instantiate(staffListItemPrefab, staffListContainer);
            _spawnedListItems.Add(itemObj);

            // UIコンポーネントの取得と設定
            TextMeshProUGUI nameText = itemObj.transform.Find("NameText")?.GetComponent<TextMeshProUGUI>();
            TextMeshProUGUI statusText = itemObj.transform.Find("StatusText")?.GetComponent<TextMeshProUGUI>();
            Slider skillSlider = itemObj.transform.Find("SkillBar")?.GetComponent<Slider>();
            TextMeshProUGUI skillText = itemObj.transform.Find("SkillText")?.GetComponent<TextMeshProUGUI>();
            Slider fatigueSlider = itemObj.transform.Find("FatigueBar")?.GetComponent<Slider>();
            Image fatigueBarFill = fatigueSlider?.fillRect?.GetComponent<Image>();
            Button selectButton = itemObj.GetComponent<Button>();

            // 名前
            if (nameText != null)
                nameText.text = data.Name;

            // 状態テキスト
            if (statusText != null)
            {
                statusText.text = GetStaffBehaviorDisplayText(data.BehaviorState);
                statusText.color = data.BehaviorState == StaffBehaviorState.OnStrike
                    ? Color.red
                    : Color.white;
            }

            // スキルレベルバー
            if (skillSlider != null)
            {
                skillSlider.maxValue = 1f;
                skillSlider.value = data.SkillLevel;
            }
            if (skillText != null)
                skillText.text = $"スキル: Lv.{Mathf.CeilToInt(data.SkillLevel * 5)}";

            // 疲労バー
            if (fatigueSlider != null)
            {
                fatigueSlider.maxValue = 1f;
                fatigueSlider.value = data.Fatigue;
            }
            if (fatigueBarFill != null)
            {
                // 疲労度に応じて色を変更（緑→黄→赤）
                fatigueBarFill.color = Color.Lerp(Color.green, Color.red, data.Fatigue);
            }

            // 選択ボタン
            if (selectButton != null)
            {
                int capturedId = data.StaffId;
                selectButton.onClick.AddListener(() => SelectStaff(capturedId));
            }
        }

        /// <summary>スタッフリストをクリアする</summary>
        private void ClearStaffList()
        {
            foreach (var item in _spawnedListItems)
            {
                if (item != null) Destroy(item);
            }
            _spawnedListItems.Clear();
        }

        // ============================================================
        // スタッフ詳細
        // ============================================================

        /// <summary>スタッフを選択して詳細パネルを表示する</summary>
        private void SelectStaff(int staffId)
        {
            _selectedStaffId = staffId;

            var staffManager = GameManager.Instance?.StaffManager;
            if (staffManager == null) return;

            var data = staffManager.GetStaffData(staffId);
            if (data == null)
            {
                HideDetailPanel();
                return;
            }

            ShowDetailPanel(data);
        }

        /// <summary>スタッフ詳細パネルを表示する</summary>
        private void ShowDetailPanel(StaffDisplayData data)
        {
            if (staffDetailPanel == null) return;

            staffDetailPanel.SetActive(true);

            if (detailNameText != null)
                detailNameText.text = data.Name;

            if (detailTypeText != null)
                detailTypeText.text = GetStaffTypeDisplayName(data.Type);

            if (detailStatusText != null)
            {
                detailStatusText.text = GetStaffBehaviorDisplayText(data.BehaviorState);
                detailStatusText.color = data.BehaviorState == StaffBehaviorState.OnStrike
                    ? Color.red
                    : Color.white;
            }

            // スキルバー
            if (detailSkillBar != null)
            {
                detailSkillBar.maxValue = 1f;
                detailSkillBar.value = data.SkillLevel;
            }
            if (detailSkillText != null)
                detailSkillText.text = $"スキルレベル: Lv.{Mathf.CeilToInt(data.SkillLevel * 5)} ({data.SkillLevel:P0})";

            // 疲労バー
            if (detailFatigueBar != null)
            {
                detailFatigueBar.maxValue = 1f;
                detailFatigueBar.value = data.Fatigue;
            }
            if (detailFatigueText != null)
                detailFatigueText.text = $"疲労度: {data.Fatigue:P0}";

            // 給与
            if (detailSalaryText != null)
            {
                int baseSalary = BaseSalaryTable.GetValueOrDefault(data.Type, 1000);
                int actualSalary = Mathf.RoundToInt(baseSalary * (1f + data.SkillLevel * 0.5f));
                detailSalaryText.text = $"月給: ¥{actualSalary:N0}";
            }

            // ポートレート
            if (detailPortrait != null && data.Portrait != null)
                detailPortrait.sprite = data.Portrait;

            // アクションボタン更新
            UpdateActionButtons(data);
        }

        /// <summary>詳細パネルを非表示にする</summary>
        private void HideDetailPanel()
        {
            if (staffDetailPanel != null)
                staffDetailPanel.SetActive(false);
            _selectedStaffId = -1;
        }

        /// <summary>アクションボタンの状態を更新する</summary>
        private void UpdateActionButtons(StaffDisplayData data)
        {
            float currentMoney = GameManager.Instance?.EconomyManager?.CurrentMoney ?? 0f;

            // 訓練ボタン
            int trainingCost = CalculateTrainingCost(data.SkillLevel);
            bool canTrain = currentMoney >= trainingCost && data.SkillLevel < 1f;

            if (trainButton != null)
                trainButton.interactable = canTrain;

            if (trainButtonText != null)
                trainButtonText.text = data.SkillLevel >= 1f ? "スキルMAX" : "訓練する";

            if (trainingCostText != null)
            {
                trainingCostText.text = data.SkillLevel >= 1f
                    ? ""
                    : $"訓練費: ¥{trainingCost:N0}";
                trainingCostText.color = canTrain ? Color.white : Color.red;
            }

            // 休憩ボタン
            bool canRest = data.BehaviorState != StaffBehaviorState.Resting
                        && data.BehaviorState != StaffBehaviorState.OnStrike;

            if (restButton != null)
                restButton.interactable = canRest;

            if (restButtonText != null)
                restButtonText.text = data.BehaviorState == StaffBehaviorState.Resting
                    ? "休憩中…"
                    : "休憩させる";

            // パトロール指定ボタン
            if (assignPatrolButton != null)
                assignPatrolButton.interactable = data.BehaviorState != StaffBehaviorState.OnStrike;

            if (assignPatrolButtonText != null)
                assignPatrolButtonText.text = "パトロール指定";

            // 解雇ボタン（常に有効）
            if (fireButton != null)
                fireButton.interactable = true;
        }

        /// <summary>スキルレベルに応じた訓練コストを計算する</summary>
        private int CalculateTrainingCost(float currentSkillLevel)
        {
            // スキルが高いほど訓練コストが増加
            int level = Mathf.CeilToInt(currentSkillLevel * 5);
            return BaseTrainingCost * (level + 1);
        }

        // ============================================================
        // アクション実行
        // ============================================================

        /// <summary>訓練ボタン押下</summary>
        private void OnTrainClicked()
        {
            if (_selectedStaffId < 0) return;

            var gm = GameManager.Instance;
            if (gm?.StaffManager == null || gm.EconomyManager == null) return;

            var data = gm.StaffManager.GetStaffData(_selectedStaffId);
            if (data == null || data.SkillLevel >= 1f) return;

            int cost = CalculateTrainingCost(data.SkillLevel);
            if (gm.EconomyManager.CurrentMoney < cost)
            {
                WebGLOptimizer.LogWarning("[StaffPanelUI] 訓練資金が不足しています");
                return;
            }

            // 訓練実行
            gm.EconomyManager.SpendMoney(cost);
            gm.StaffManager.TrainStaff(_selectedStaffId);

            WebGLOptimizer.LogVerbose($"[StaffPanelUI] スタッフ訓練完了: ID={_selectedStaffId}, 費用=¥{cost}");

            // 表示更新
            RefreshSelectedStaffDetail();
            PopulateStaffList(_selectedStaffType);
        }

        /// <summary>解雇ボタン押下 - 確認ダイアログを表示する</summary>
        private void OnFireClicked()
        {
            if (_selectedStaffId < 0) return;

            var data = GameManager.Instance?.StaffManager?.GetStaffData(_selectedStaffId);
            if (data == null) return;

            // 確認ダイアログ表示
            ShowFireConfirmDialog(data);
        }

        /// <summary>解雇確認ダイアログを表示する</summary>
        private void ShowFireConfirmDialog(StaffDisplayData data)
        {
            if (fireConfirmDialog == null) return;

            fireConfirmDialog.SetActive(true);

            if (fireConfirmMessage != null)
            {
                fireConfirmMessage.text = $"{data.Name}（{GetStaffTypeDisplayName(data.Type)}）を\n本当に解雇しますか？";
            }
        }

        /// <summary>解雇確認ダイアログを非表示にする</summary>
        private void HideFireConfirmDialog()
        {
            if (fireConfirmDialog != null)
                fireConfirmDialog.SetActive(false);
        }

        /// <summary>解雇確定</summary>
        private void OnFireConfirmed()
        {
            if (_selectedStaffId < 0) return;

            var gm = GameManager.Instance;
            if (gm?.StaffManager == null) return;

            var data = gm.StaffManager.GetStaffData(_selectedStaffId);
            StaffType firedType = data?.Type ?? _selectedStaffType;

            gm.StaffManager.FireStaff(_selectedStaffId);
            GameEvents.FireStaffFired(_selectedStaffId, firedType);

            WebGLOptimizer.LogVerbose($"[StaffPanelUI] スタッフ解雇: ID={_selectedStaffId}");

            HideFireConfirmDialog();
            HideDetailPanel();
            PopulateStaffList(_selectedStaffType);
            RefreshAllTabCounts();
        }

        /// <summary>パトロール指定ボタン押下</summary>
        private void OnAssignPatrolClicked()
        {
            if (_selectedStaffId < 0) return;

            // パトロールエリア指定モードに遷移
            // ゲームフィールド上でエリアを選択させる
            var staffManager = GameManager.Instance?.StaffManager;
            staffManager?.StartPatrolAreaAssignment(_selectedStaffId);

            WebGLOptimizer.LogVerbose($"[StaffPanelUI] パトロールエリア指定モード開始: ID={_selectedStaffId}");
        }

        /// <summary>休憩ボタン押下</summary>
        private void OnRestClicked()
        {
            if (_selectedStaffId < 0) return;

            var staffManager = GameManager.Instance?.StaffManager;
            if (staffManager == null) return;

            staffManager.SendToStaffRoom(_selectedStaffId);

            WebGLOptimizer.LogVerbose($"[StaffPanelUI] スタッフを休憩室へ送りました: ID={_selectedStaffId}");

            RefreshSelectedStaffDetail();
            PopulateStaffList(_selectedStaffType);
        }

        /// <summary>閉じるボタン押下</summary>
        private void OnCloseClicked()
        {
            gameObject.SetActive(false);
        }

        /// <summary>選択中のスタッフの詳細表示を更新する</summary>
        private void RefreshSelectedStaffDetail()
        {
            if (_selectedStaffId < 0) return;

            var data = GameManager.Instance?.StaffManager?.GetStaffData(_selectedStaffId);
            if (data != null)
            {
                ShowDetailPanel(data);
            }
        }

        // ============================================================
        // イベントハンドラ
        // ============================================================

        /// <summary>スタッフ雇用イベントハンドラ</summary>
        private void HandleStaffHired(int staffId, StaffType type)
        {
            if (type == _selectedStaffType)
            {
                PopulateStaffList(_selectedStaffType);
            }
            RefreshAllTabCounts();
        }

        /// <summary>スタッフ解雇イベントハンドラ</summary>
        private void HandleStaffFired(int staffId, StaffType type)
        {
            if (staffId == _selectedStaffId)
            {
                HideDetailPanel();
            }
            if (type == _selectedStaffType)
            {
                PopulateStaffList(_selectedStaffType);
            }
            RefreshAllTabCounts();
        }

        /// <summary>スタッフストライキイベントハンドラ</summary>
        private void HandleStaffWentOnStrike(int staffId)
        {
            // リスト全体を更新（ストライキ状態の表示更新）
            PopulateStaffList(_selectedStaffType);

            if (staffId == _selectedStaffId)
            {
                RefreshSelectedStaffDetail();
            }
        }

        /// <summary>スタッフタスク完了イベントハンドラ</summary>
        private void HandleStaffFinishedTask(int staffId)
        {
            if (staffId == _selectedStaffId)
            {
                RefreshSelectedStaffDetail();
            }
        }

        /// <summary>資金変更イベントハンドラ - 雇用/訓練ボタンの有効状態を更新</summary>
        private void HandleMoneyChanged(float newAmount)
        {
            UpdateHireSection(_selectedStaffType);

            if (_selectedStaffId >= 0)
            {
                var data = GameManager.Instance?.StaffManager?.GetStaffData(_selectedStaffId);
                if (data != null)
                {
                    UpdateActionButtons(data);
                }
            }
        }

        // ============================================================
        // 表示名ヘルパー
        // ============================================================

        /// <summary>スタッフタイプの日本語表示名を取得する</summary>
        private string GetStaffTypeDisplayName(StaffType type)
        {
            return type switch
            {
                StaffType.Mechanic    => "メカニック",
                StaffType.Cleaner     => "スイーパー",
                StaffType.Entertainer => "エンターテイナー",
                StaffType.Guard       => "ガードマン",
                StaffType.Scientist   => "サイエンティスト",
                _                     => "不明"
            };
        }

        /// <summary>スタッフ行動状態の日本語表示テキストを取得する</summary>
        private string GetStaffBehaviorDisplayText(StaffBehaviorState state)
        {
            return state switch
            {
                StaffBehaviorState.Idle        => "待機中",
                StaffBehaviorState.Working     => "作業中",
                StaffBehaviorState.MovingToTask => "移動中",
                StaffBehaviorState.Resting     => "休憩中",
                StaffBehaviorState.OnStrike    => "ストライキ中！",
                _                              => "不明"
            };
        }
    }

    // ============================================================
    // スタッフ表示用データクラス
    // ============================================================

    /// <summary>
    /// UIに表示するためのスタッフデータ。
    /// StaffManagerから取得される軽量なデータ構造。
    /// </summary>
    public class StaffDisplayData
    {
        public int StaffId;
        public string Name;
        public StaffType Type;
        public Sprite Portrait;

        /// <summary>スキルレベル (0.0 - 1.0)</summary>
        public float SkillLevel;

        /// <summary>疲労度 (0.0 - 1.0, 高いほど疲れている)</summary>
        public float Fatigue;

        /// <summary>現在の行動状態</summary>
        public StaffBehaviorState BehaviorState;
    }
}
