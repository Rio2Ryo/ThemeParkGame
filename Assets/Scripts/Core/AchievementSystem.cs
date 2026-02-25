// ============================================================
// ThemeParkGame - Achievement System
// 実績・アチーブメント管理（PlayerPrefs永続化、トースト通知UI）
// ============================================================

using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using ThemeParkGame.Staff;

namespace ThemeParkGame.Core
{
    /// <summary>実績カテゴリ</summary>
    public enum AchievementCategory
    {
        Visitor,     // 来場者関連
        Economy,     // 経済関連
        Park,        // パーク建設関連
        Staff,       // スタッフ関連
        Special      // 特殊実績
    }

    /// <summary>個別の実績定義</summary>
    [Serializable]
    public class AchievementDef
    {
        public string Id;
        public string Title;
        public string Description;
        public AchievementCategory Category;
        public string Icon; // テキストアイコン
    }

    /// <summary>
    /// 実績・アチーブメントを管理するシステム。
    /// PlayerPrefsで解除状態を永続化し、解除時にトースト通知を表示する。
    /// </summary>
    public class AchievementSystem : MonoBehaviour
    {
        public static AchievementSystem Instance { get; private set; }

        private const string PREF_PREFIX = "ACH_";
        private const float CHECK_INTERVAL = 3f;
        private const float TOAST_DISPLAY_TIME = 4f;
        private const float TOAST_FADE_TIME = 0.5f;

        // 実績定義
        private readonly List<AchievementDef> _definitions = new List<AchievementDef>();

        // 解除済みID
        private readonly HashSet<string> _unlocked = new HashSet<string>();

        // UI
        private Canvas _toastCanvas;
        private GameObject _toastPanel;
        private Text _toastIcon;
        private Text _toastTitle;
        private Text _toastDesc;
        private Image _toastBg;
        private float _toastTimer;

        // 通知キュー
        private readonly Queue<AchievementDef> _pendingToasts = new Queue<AchievementDef>();
        private bool _showingToast;

        // チェックタイマー
        private float _checkTimer;

        // 黒字連続日数トラッカー
        private int _profitStreakDays;
        private int _lastCheckedDay = -1;

        // 実績一覧パネル
        private GameObject _listPanel;
        private RectTransform _listContent;
        private readonly List<GameObject> _listItems = new List<GameObject>();

        // カラー
        private static readonly Color BgDark = new Color(0.04f, 0.06f, 0.14f, 0.96f);
        private static readonly Color Gold = new Color(0.95f, 0.88f, 0.45f);
        private static readonly Color Green = new Color(0.4f, 0.95f, 0.45f);
        private static readonly Color Muted = new Color(0.5f, 0.52f, 0.6f);
        private static readonly Color Locked = new Color(0.25f, 0.27f, 0.32f);

        // 公開プロパティ
        public int UnlockedCount => _unlocked.Count;
        public int TotalCount => _definitions.Count;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(this);
                return;
            }
            Instance = this;

            RegisterAllAchievements();
            LoadUnlocked();
            BuildToastUI();
            BuildListPanel();
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        // ================================================================
        // 実績定義登録
        // ================================================================

        private void RegisterAllAchievements()
        {
            // ---- 来場者系 ----
            Reg("visitor_10", "はじめてのお客さん", "累計来場者10人達成", AchievementCategory.Visitor, "[V]");
            Reg("visitor_50", "にぎやかなパーク", "累計来場者50人達成", AchievementCategory.Visitor, "[V]");
            Reg("visitor_200", "大人気パーク", "累計来場者200人達成", AchievementCategory.Visitor, "[V]");
            Reg("visitor_500", "伝説のテーマパーク", "累計来場者500人達成", AchievementCategory.Visitor, "[V]");
            Reg("visitor_1000", "メガパーク", "累計来場者1000人達成", AchievementCategory.Visitor, "[V]");
            Reg("happiness_80", "笑顔あふれるパーク", "平均満足度80%以上", AchievementCategory.Visitor, "[H]");
            Reg("happiness_95", "パーフェクトパーク", "平均満足度95%以上", AchievementCategory.Visitor, "[H]");
            Reg("peak_20", "行列のできるパーク", "同時来場者20人以上", AchievementCategory.Visitor, "[P]");
            Reg("peak_50", "超満員御礼", "同時来場者50人以上", AchievementCategory.Visitor, "[P]");

            // ---- 経済系 ----
            Reg("revenue_10k", "はじめての収益", "総収益$10,000達成", AchievementCategory.Economy, "[$]");
            Reg("revenue_50k", "成長するビジネス", "総収益$50,000達成", AchievementCategory.Economy, "[$]");
            Reg("revenue_200k", "大企業への道", "総収益$200,000達成", AchievementCategory.Economy, "[$]");
            Reg("revenue_1m", "億万長者", "総収益$1,000,000達成", AchievementCategory.Economy, "[$]");
            Reg("balance_100k", "貯蓄王", "資金残高$100,000達成", AchievementCategory.Economy, "[$]");
            Reg("profit_monthly", "黒字経営", "月間利益がプラス", AchievementCategory.Economy, "[$]");
            Reg("profit_streak", "堅実経営者", "連続30日間赤字なし", AchievementCategory.Economy, "[$]");

            // ---- パーク建設系 ----
            Reg("attraction_1", "はじめてのアトラクション", "アトラクション1基建設", AchievementCategory.Park, "[A]");
            Reg("attraction_5", "アミューズメントパーク", "アトラクション5基建設", AchievementCategory.Park, "[A]");
            Reg("attraction_10", "テーマパーク帝国", "アトラクション10基建設", AchievementCategory.Park, "[A]");
            Reg("research_3", "研究の成果", "研究3つ完了", AchievementCategory.Park, "[R]");
            Reg("research_10", "テクノロジーマスター", "研究10個完了", AchievementCategory.Park, "[R]");
            Reg("cert_fame", "名声の認定証", "Fame認定証を獲得", AchievementCategory.Park, "[C]");
            Reg("cert_safety", "安全の認定証", "Safety認定証を獲得", AchievementCategory.Park, "[C]");
            Reg("cert_comfort", "快適の認定証", "Comfort認定証を獲得", AchievementCategory.Park, "[C]");
            Reg("cert_excitement", "興奮の認定証", "Excitement認定証を獲得", AchievementCategory.Park, "[C]");
            Reg("cert_mood", "ムードの認定証", "Mood認定証を獲得", AchievementCategory.Park, "[C]");

            // ---- スタッフ系 ----
            Reg("staff_5", "チームワーク", "スタッフ5人雇用", AchievementCategory.Staff, "[S]");
            Reg("staff_15", "大所帯", "スタッフ15人雇用", AchievementCategory.Staff, "[S]");
            Reg("staff_maxlevel", "熟練の職人", "スタッフ1人がスキルLv5到達", AchievementCategory.Staff, "[S]");

            // ---- 特殊系 ----
            Reg("golden_1", "ゴールデンチケット", "ゴールデンチケットを初獲得", AchievementCategory.Special, "[G]");
            Reg("golden_5", "チケットコレクター", "ゴールデンチケット5枚獲得", AchievementCategory.Special, "[G]");
            Reg("year_3", "ベテラン経営者", "ゲーム内3年目到達", AchievementCategory.Special, "[Y]");
            Reg("year_5", "レジェンド", "ゲーム内5年目到達", AchievementCategory.Special, "[Y]");
            Reg("scenario_clear", "シナリオクリア", "シナリオを1つクリア", AchievementCategory.Special, "[!]");
            Reg("all_certs", "パーフェクト認定", "全5種類の認定証を獲得", AchievementCategory.Special, "[*]");
        }

        private void Reg(string id, string title, string desc, AchievementCategory cat, string icon)
        {
            _definitions.Add(new AchievementDef
            {
                Id = id,
                Title = title,
                Description = desc,
                Category = cat,
                Icon = icon
            });
        }

        // ================================================================
        // 永続化
        // ================================================================

        private void LoadUnlocked()
        {
            _unlocked.Clear();
            foreach (var def in _definitions)
            {
                if (PlayerPrefs.GetInt(PREF_PREFIX + def.Id, 0) == 1)
                    _unlocked.Add(def.Id);
            }
            WebGLOptimizer.LogVerbose($"[AchievementSystem] Loaded {_unlocked.Count}/{_definitions.Count} achievements");
        }

        private void SaveUnlocked(string id)
        {
            PlayerPrefs.SetInt(PREF_PREFIX + id, 1);
            PlayerPrefs.Save();
        }

        /// <summary>実績をリセット（デバッグ用）</summary>
        public static void ResetAll()
        {
            foreach (var key in new List<string>())
                PlayerPrefs.DeleteKey(key);

            // 全定義のキーを削除
            if (Instance != null)
            {
                foreach (var def in Instance._definitions)
                    PlayerPrefs.DeleteKey(PREF_PREFIX + def.Id);
                Instance._unlocked.Clear();
            }
            PlayerPrefs.Save();
            WebGLOptimizer.LogVerbose("[AchievementSystem] All achievements reset");
        }

        // ================================================================
        // 実績解除
        // ================================================================

        /// <summary>指定IDの実績を解除する</summary>
        public void Unlock(string achievementId)
        {
            if (_unlocked.Contains(achievementId)) return;

            var def = _definitions.Find(d => d.Id == achievementId);
            if (def == null)
            {
                Debug.LogWarning($"[AchievementSystem] Unknown achievement: {achievementId}");
                return;
            }

            _unlocked.Add(achievementId);
            SaveUnlocked(achievementId);

            // トースト通知キュー
            _pendingToasts.Enqueue(def);

            WebGLOptimizer.LogVerbose($"[AchievementSystem] Unlocked: {def.Title} ({def.Id})");
        }

        public bool IsUnlocked(string id) => _unlocked.Contains(id);

        public IReadOnlyList<AchievementDef> GetAllDefinitions() => _definitions;

        // ================================================================
        // 条件チェック（定期実行）
        // ================================================================

        private void Update()
        {
            // トースト表示
            UpdateToast();

            // ゲーム中のみ実績チェック
            if (GameManager.Instance == null ||
                GameManager.Instance.CurrentState != GameState.Playing)
                return;

            _checkTimer -= Time.unscaledDeltaTime;
            if (_checkTimer > 0f) return;
            _checkTimer = CHECK_INTERVAL;

            CheckAllConditions();
        }

        private void CheckAllConditions()
        {
            var gm = GameManager.Instance;
            if (gm == null) return;

            // ---- 来場者 ----
            if (gm.VisitorManager != null)
            {
                int total = gm.VisitorManager.TotalVisitorsToday;
                float avgHappy = gm.VisitorManager.AverageHappiness;
                float peak = gm.VisitorManager.PeakVisitorCount;

                if (total >= 10) Unlock("visitor_10");
                if (total >= 50) Unlock("visitor_50");
                if (total >= 200) Unlock("visitor_200");
                if (total >= 500) Unlock("visitor_500");
                if (total >= 1000) Unlock("visitor_1000");

                if (avgHappy >= 80f && gm.VisitorManager.ActiveVisitorCount >= 5) Unlock("happiness_80");
                if (avgHappy >= 95f && gm.VisitorManager.ActiveVisitorCount >= 5) Unlock("happiness_95");

                if (peak >= 20f) Unlock("peak_20");
                if (peak >= 50f) Unlock("peak_50");
            }

            // ---- 経済 ----
            if (gm.EconomyManager != null)
            {
                float revenue = gm.EconomyManager.TotalRevenueEarned;
                float balance = gm.EconomyManager.CurrentBalance;
                float monthlyProfit = gm.EconomyManager.GetMonthlyProfit();

                if (revenue >= 10000f) Unlock("revenue_10k");
                if (revenue >= 50000f) Unlock("revenue_50k");
                if (revenue >= 200000f) Unlock("revenue_200k");
                if (revenue >= 1000000f) Unlock("revenue_1m");

                if (balance >= 100000f) Unlock("balance_100k");

                if (monthlyProfit > 0f && gm.TimeManager != null && gm.TimeManager.CurrentMonth > 1)
                    Unlock("profit_monthly");

                // 黒字連続日数トラッキング
                if (gm.TimeManager != null)
                {
                    int day = gm.TimeManager.CurrentDay;
                    if (day != _lastCheckedDay)
                    {
                        _lastCheckedDay = day;
                        if (balance >= 0f && monthlyProfit >= 0f)
                            _profitStreakDays++;
                        else
                            _profitStreakDays = 0;
                    }
                    if (_profitStreakDays >= 30) Unlock("profit_streak");
                }
            }

            // ---- アトラクション ----
            if (gm.AttractionManager != null)
            {
                int count = gm.AttractionManager.TotalCount;
                if (count >= 1) Unlock("attraction_1");
                if (count >= 5) Unlock("attraction_5");
                if (count >= 10) Unlock("attraction_10");
            }

            // ---- 研究 ----
            if (gm.ResearchManager != null)
            {
                int completed = gm.ResearchManager.CompletedResearchCount;
                if (completed >= 3) Unlock("research_3");
                if (completed >= 10) Unlock("research_10");
            }

            // ---- 認定証 ----
            if (gm.ParkManager != null && gm.ParkManager.Rating != null)
            {
                var rating = gm.ParkManager.Rating;
                if (rating.IsCertificateAwarded(CertificateCategory.Fame)) Unlock("cert_fame");
                if (rating.IsCertificateAwarded(CertificateCategory.Safety)) Unlock("cert_safety");
                if (rating.IsCertificateAwarded(CertificateCategory.Comfort)) Unlock("cert_comfort");
                if (rating.IsCertificateAwarded(CertificateCategory.Excitement)) Unlock("cert_excitement");
                if (rating.IsCertificateAwarded(CertificateCategory.Mood)) Unlock("cert_mood");

                if (rating.IsCertificateAwarded(CertificateCategory.Fame) &&
                    rating.IsCertificateAwarded(CertificateCategory.Safety) &&
                    rating.IsCertificateAwarded(CertificateCategory.Comfort) &&
                    rating.IsCertificateAwarded(CertificateCategory.Excitement) &&
                    rating.IsCertificateAwarded(CertificateCategory.Mood))
                {
                    Unlock("all_certs");
                }
            }

            // ---- スタッフ ----
            if (gm.StaffManager != null)
            {
                int staffCount = gm.StaffManager.TotalStaffCount;
                if (staffCount >= 5) Unlock("staff_5");
                if (staffCount >= 15) Unlock("staff_15");

                foreach (var staff in gm.StaffManager.GetAllStaff())
                {
                    if (staff != null && staff.SkillLevel >= StaffMember.MaxSkillLevel)
                    {
                        Unlock("staff_maxlevel");
                        break;
                    }
                }
            }

            // ---- ゴールデンチケット ----
            if (gm.GoldenTickets >= 1) Unlock("golden_1");
            if (gm.GoldenTickets >= 5) Unlock("golden_5");

            // ---- 年数 ----
            if (gm.TimeManager != null)
            {
                int year = gm.TimeManager.CurrentYear;
                if (year >= 3) Unlock("year_3");
                if (year >= 5) Unlock("year_5");
            }

            // ---- シナリオクリア ----
            if (ScenarioManager.Instance != null && ScenarioManager.Instance.IsScenarioCleared)
            {
                Unlock("scenario_clear");
            }
        }

        // ================================================================
        // トーストUI構築
        // ================================================================

        private void BuildToastUI()
        {
            var canvasGo = new GameObject("AchievementToastCanvas");
            canvasGo.transform.SetParent(transform, false);
            _toastCanvas = canvasGo.AddComponent<Canvas>();
            _toastCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _toastCanvas.sortingOrder = 90;

            var scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;
            canvasGo.AddComponent<GraphicRaycaster>();

            var canvasRt = canvasGo.GetComponent<RectTransform>();

            // トーストパネル（画面上部右寄り）
            float panelW = 380f;
            float panelH = 80f;
            _toastPanel = new GameObject("ToastPanel");
            _toastPanel.transform.SetParent(canvasRt, false);
            var panelRt = _toastPanel.AddComponent<RectTransform>();
            panelRt.anchorMin = panelRt.anchorMax = new Vector2(1f, 1f);
            panelRt.pivot = new Vector2(1f, 1f);
            panelRt.anchoredPosition = new Vector2(-10f, -130f);
            panelRt.sizeDelta = new Vector2(panelW, panelH);

            _toastBg = _toastPanel.AddComponent<Image>();
            _toastBg.color = new Color(0.08f, 0.12f, 0.22f, 0.96f);
            _toastBg.raycastTarget = false;

            // 左のアクセントライン
            var accent = MakePanel(panelRt, "Accent", 4f, panelH, Gold);
            var accentRt = accent.GetComponent<RectTransform>();
            accentRt.anchorMin = accentRt.anchorMax = new Vector2(0f, 0.5f);
            accentRt.pivot = new Vector2(0f, 0.5f);
            accentRt.anchoredPosition = Vector2.zero;

            // ラベル「ACHIEVEMENT UNLOCKED」
            var header = MakeLabel(panelRt, "Header", "ACHIEVEMENT UNLOCKED", 11,
                Gold, FontStyle.Bold, TextAnchor.MiddleLeft);
            var hRt = header.rectTransform;
            hRt.anchorMin = hRt.anchorMax = new Vector2(0f, 1f);
            hRt.pivot = new Vector2(0f, 1f);
            hRt.anchoredPosition = new Vector2(14f, -6f);
            hRt.sizeDelta = new Vector2(panelW - 28f, 18f);

            // アイコン
            _toastIcon = MakeLabel(panelRt, "Icon", "[*]", 28, Gold,
                FontStyle.Bold, TextAnchor.MiddleCenter);
            var iRt = _toastIcon.rectTransform;
            iRt.anchorMin = iRt.anchorMax = new Vector2(0f, 0f);
            iRt.pivot = new Vector2(0f, 0f);
            iRt.anchoredPosition = new Vector2(14f, 6f);
            iRt.sizeDelta = new Vector2(44f, 44f);

            // タイトル
            _toastTitle = MakeLabel(panelRt, "Title", "", 20, Color.white,
                FontStyle.Bold, TextAnchor.MiddleLeft);
            var tRt = _toastTitle.rectTransform;
            tRt.anchorMin = tRt.anchorMax = new Vector2(0f, 0f);
            tRt.pivot = new Vector2(0f, 0f);
            tRt.anchoredPosition = new Vector2(64f, 26f);
            tRt.sizeDelta = new Vector2(panelW - 78f, 26f);

            // 説明
            _toastDesc = MakeLabel(panelRt, "Desc", "", 14, new Color(0.7f, 0.72f, 0.8f),
                FontStyle.Normal, TextAnchor.MiddleLeft);
            var dRt = _toastDesc.rectTransform;
            dRt.anchorMin = dRt.anchorMax = new Vector2(0f, 0f);
            dRt.pivot = new Vector2(0f, 0f);
            dRt.anchoredPosition = new Vector2(64f, 6f);
            dRt.sizeDelta = new Vector2(panelW - 78f, 20f);

            _toastPanel.SetActive(false);
        }

        // ================================================================
        // トースト表示制御
        // ================================================================

        private void UpdateToast()
        {
            if (_showingToast)
            {
                _toastTimer -= Time.unscaledDeltaTime;

                // フェードアウト
                if (_toastTimer <= TOAST_FADE_TIME && _toastBg != null)
                {
                    float alpha = Mathf.Clamp01(_toastTimer / TOAST_FADE_TIME);
                    SetToastAlpha(alpha);
                }

                if (_toastTimer <= 0f)
                {
                    _showingToast = false;
                    _toastPanel.SetActive(false);
                }
            }
            else if (_pendingToasts.Count > 0)
            {
                ShowNextToast();
            }
        }

        private void ShowNextToast()
        {
            var def = _pendingToasts.Dequeue();
            _toastIcon.text = def.Icon;
            _toastTitle.text = def.Title;
            _toastDesc.text = def.Description;

            SetToastAlpha(1f);
            _toastPanel.SetActive(true);
            _showingToast = true;
            _toastTimer = TOAST_DISPLAY_TIME;

            // 効果音
            if (AudioManager.Instance != null)
                AudioManager.Instance.PlayAchievementSE();
        }

        private void SetToastAlpha(float alpha)
        {
            if (_toastBg != null)
            {
                var c = _toastBg.color;
                c.a = 0.96f * alpha;
                _toastBg.color = c;
            }
            if (_toastIcon != null)
            {
                var c = _toastIcon.color;
                c.a = alpha;
                _toastIcon.color = c;
            }
            if (_toastTitle != null)
            {
                var c = _toastTitle.color;
                c.a = alpha;
                _toastTitle.color = c;
            }
            if (_toastDesc != null)
            {
                var c = _toastDesc.color;
                c.a = alpha;
                _toastDesc.color = c;
            }
        }

        // ================================================================
        // 実績一覧パネル
        // ================================================================

        private void BuildListPanel()
        {
            var canvasRt = _toastCanvas.GetComponent<RectTransform>();

            float panelW = 560f;
            float panelH = 620f;

            _listPanel = new GameObject("AchievementListPanel");
            _listPanel.transform.SetParent(canvasRt, false);
            var panelRt = _listPanel.AddComponent<RectTransform>();
            panelRt.anchorMin = panelRt.anchorMax = new Vector2(0.5f, 0.5f);
            panelRt.pivot = new Vector2(0.5f, 0.5f);
            panelRt.anchoredPosition = Vector2.zero;
            panelRt.sizeDelta = new Vector2(panelW, panelH);

            var bgImg = _listPanel.AddComponent<Image>();
            bgImg.color = BgDark;
            bgImg.raycastTarget = true;

            // 半透明背景
            var dimGo = new GameObject("Dim");
            dimGo.transform.SetParent(canvasRt, false);
            dimGo.transform.SetAsFirstSibling();
            var dimRt = dimGo.AddComponent<RectTransform>();
            dimRt.anchorMin = Vector2.zero;
            dimRt.anchorMax = Vector2.one;
            dimRt.offsetMin = Vector2.zero;
            dimRt.offsetMax = Vector2.zero;
            var dimImg = dimGo.AddComponent<Image>();
            dimImg.color = new Color(0f, 0f, 0f, 0.5f);
            dimImg.raycastTarget = true;
            var dimBtn = dimGo.AddComponent<Button>();
            dimBtn.targetGraphic = dimImg;
            var dimColors = dimBtn.colors;
            dimColors.highlightedColor = dimImg.color;
            dimColors.pressedColor = dimImg.color;
            dimBtn.colors = dimColors;
            dimBtn.onClick.AddListener(HideAchievementList);

            // タイトル
            var title = MakeLabel(panelRt, "Title", "ACHIEVEMENTS", 32, Gold,
                FontStyle.Bold, TextAnchor.MiddleCenter);
            var titleRt = title.rectTransform;
            titleRt.anchorMin = titleRt.anchorMax = new Vector2(0.5f, 1f);
            titleRt.pivot = new Vector2(0.5f, 1f);
            titleRt.anchoredPosition = new Vector2(0f, -12f);
            titleRt.sizeDelta = new Vector2(panelW, 40f);

            // 進捗テキスト
            var progress = MakeLabel(panelRt, "Progress", "", 16, new Color(0.6f, 0.65f, 0.75f),
                FontStyle.Normal, TextAnchor.MiddleCenter);
            var pRt = progress.rectTransform;
            pRt.anchorMin = pRt.anchorMax = new Vector2(0.5f, 1f);
            pRt.pivot = new Vector2(0.5f, 1f);
            pRt.anchoredPosition = new Vector2(0f, -52f);
            pRt.sizeDelta = new Vector2(panelW, 22f);

            // スクロール用コンテンツ領域
            // ScrollRectは複雑なので、固定高さのリストを直接配置
            var contentGo = new GameObject("Content");
            contentGo.transform.SetParent(panelRt, false);
            _listContent = contentGo.AddComponent<RectTransform>();
            _listContent.anchorMin = new Vector2(0f, 0f);
            _listContent.anchorMax = new Vector2(1f, 1f);
            _listContent.offsetMin = new Vector2(10f, 50f);
            _listContent.offsetMax = new Vector2(-10f, -78f);

            // 閉じるボタン
            var closeGo = MakePanel(panelRt, "CloseBtn", 140f, 38f, new Color(0.35f, 0.38f, 0.48f));
            var closeRt = closeGo.GetComponent<RectTransform>();
            closeRt.anchorMin = closeRt.anchorMax = new Vector2(0.5f, 0f);
            closeRt.pivot = new Vector2(0.5f, 0f);
            closeRt.anchoredPosition = new Vector2(0f, 8f);
            var closeImg = closeGo.GetComponent<Image>();
            closeImg.raycastTarget = true;
            var closeBtn = closeGo.AddComponent<Button>();
            closeBtn.targetGraphic = closeImg;
            var cc = closeBtn.colors;
            cc.highlightedColor = new Color(0.45f, 0.48f, 0.58f);
            cc.pressedColor = new Color(0.25f, 0.28f, 0.38f);
            closeBtn.colors = cc;
            closeBtn.onClick.AddListener(HideAchievementList);
            var closeLabel = MakeLabel(closeRt, "Label", "CLOSE", 18, Color.white,
                FontStyle.Bold, TextAnchor.MiddleCenter);
            StretchFill(closeLabel.rectTransform);

            _listPanel.SetActive(false);
        }

        /// <summary>実績一覧パネルを表示する</summary>
        public void ShowAchievementList()
        {
            if (_listPanel == null) return;

            // 進捗テキスト更新
            var progressText = _listPanel.GetComponentInChildren<RectTransform>()
                ?.parent?.Find("Progress")?.GetComponent<Text>();
            if (progressText != null)
                progressText.text = $"{_unlocked.Count} / {_definitions.Count} Unlocked";

            // アイテム再構築
            foreach (var item in _listItems)
                Destroy(item);
            _listItems.Clear();

            float itemH = 52f;
            float gap = 4f;
            float y = 0f;

            // カテゴリ順にソート表示
            var categories = new[] {
                AchievementCategory.Visitor, AchievementCategory.Economy,
                AchievementCategory.Park, AchievementCategory.Staff,
                AchievementCategory.Special
            };
            var catNames = new[] { "来場者", "経済", "パーク建設", "スタッフ", "特殊" };

            for (int ci = 0; ci < categories.Length; ci++)
            {
                var cat = categories[ci];
                var defs = _definitions.FindAll(d => d.Category == cat);
                if (defs.Count == 0) continue;

                // カテゴリヘッダー
                var headerGo = new GameObject($"CatHeader_{cat}");
                headerGo.transform.SetParent(_listContent, false);
                var headerRt = headerGo.AddComponent<RectTransform>();
                headerRt.anchorMin = headerRt.anchorMax = new Vector2(0f, 1f);
                headerRt.pivot = new Vector2(0f, 1f);
                headerRt.anchoredPosition = new Vector2(0f, -y);
                headerRt.sizeDelta = new Vector2(540f, 24f);
                var headerText = headerGo.AddComponent<Text>();
                headerText.text = $"--- {catNames[ci]} ---";
                headerText.font = CachedFont();
                headerText.fontSize = 14;
                headerText.fontStyle = FontStyle.Bold;
                headerText.color = Gold;
                headerText.alignment = TextAnchor.MiddleLeft;
                headerText.raycastTarget = false;
                _listItems.Add(headerGo);
                y += 28f;

                foreach (var def in defs)
                {
                    bool unlocked = _unlocked.Contains(def.Id);
                    var itemGo = CreateAchievementItem(def, unlocked, y);
                    _listItems.Add(itemGo);
                    y += itemH + gap;
                }
            }

            _listPanel.SetActive(true);

            // Dimを再表示
            var dim = _listPanel.transform.parent.Find("Dim");
            if (dim != null) dim.gameObject.SetActive(true);
        }

        /// <summary>実績一覧パネルを非表示にする</summary>
        public void HideAchievementList()
        {
            if (_listPanel != null) _listPanel.SetActive(false);
            var dim = _listPanel?.transform.parent?.Find("Dim");
            if (dim != null) dim.gameObject.SetActive(false);
        }

        private GameObject CreateAchievementItem(AchievementDef def, bool unlocked, float y)
        {
            float itemW = 530f;
            float itemH = 52f;

            var go = new GameObject($"Item_{def.Id}");
            go.transform.SetParent(_listContent, false);
            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0f, 1f);
            rt.anchoredPosition = new Vector2(0f, -y);
            rt.sizeDelta = new Vector2(itemW, itemH);

            var bgImg = go.AddComponent<Image>();
            bgImg.color = unlocked ? new Color(0.1f, 0.14f, 0.22f, 0.9f) : new Color(0.08f, 0.09f, 0.12f, 0.7f);
            bgImg.raycastTarget = false;

            // アイコン
            var icon = MakeLabel(rt, "Icon", unlocked ? def.Icon : "[?]", 22,
                unlocked ? Gold : Locked,
                FontStyle.Bold, TextAnchor.MiddleCenter);
            var iRt = icon.rectTransform;
            iRt.anchorMin = iRt.anchorMax = new Vector2(0f, 0.5f);
            iRt.pivot = new Vector2(0f, 0.5f);
            iRt.anchoredPosition = new Vector2(8f, 0f);
            iRt.sizeDelta = new Vector2(40f, 40f);

            // タイトル
            var title = MakeLabel(rt, "Title", unlocked ? def.Title : "???", 17,
                unlocked ? Color.white : Muted,
                FontStyle.Bold, TextAnchor.MiddleLeft);
            var tRt = title.rectTransform;
            tRt.anchorMin = tRt.anchorMax = new Vector2(0f, 1f);
            tRt.pivot = new Vector2(0f, 1f);
            tRt.anchoredPosition = new Vector2(56f, -4f);
            tRt.sizeDelta = new Vector2(itemW - 100f, 24f);

            // 説明
            var desc = MakeLabel(rt, "Desc", unlocked ? def.Description : "---", 13,
                unlocked ? new Color(0.65f, 0.68f, 0.75f) : Locked,
                FontStyle.Normal, TextAnchor.MiddleLeft);
            var dRt = desc.rectTransform;
            dRt.anchorMin = dRt.anchorMax = new Vector2(0f, 0f);
            dRt.pivot = new Vector2(0f, 0f);
            dRt.anchoredPosition = new Vector2(56f, 4f);
            dRt.sizeDelta = new Vector2(itemW - 100f, 20f);

            // ステータスマーク
            var status = MakeLabel(rt, "Status", unlocked ? "DONE" : "", 12,
                unlocked ? Green : Locked,
                FontStyle.Bold, TextAnchor.MiddleRight);
            var sRt = status.rectTransform;
            sRt.anchorMin = sRt.anchorMax = new Vector2(1f, 0.5f);
            sRt.pivot = new Vector2(1f, 0.5f);
            sRt.anchoredPosition = new Vector2(-10f, 0f);
            sRt.sizeDelta = new Vector2(50f, 20f);

            return go;
        }

        // ================================================================
        // 公開: ポーズ画面からの呼び出し
        // ================================================================

        public bool IsListVisible => _listPanel != null && _listPanel.activeSelf;

        // ================================================================
        // UIヘルパー
        // ================================================================

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

        private static void StretchFill(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }
    }
}
