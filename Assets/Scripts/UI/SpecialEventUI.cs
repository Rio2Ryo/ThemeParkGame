// ============================================================
// ThemeParkGame - SpecialEventUI
// 特別イベント管理UI - 花火大会・コンサート・季節祭りの表示＆手動開催
// ============================================================

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using ThemeParkGame.Core;
using ThemeParkGame.Park;
using ThemeParkGame.Economy;

namespace ThemeParkGame.UI
{
    /// <summary>
    /// 特別イベントの一覧表示と手動開催UIを提供する。
    ///
    /// 【ゲームデザイン】
    /// ・自動発生イベント（季節・デイリーショー）のステータス表示
    /// ・プレイヤーが手動で開催できる有料特別イベント
    ///   - 花火大会: 来場者急増（+60%）, 幸福度大幅UP
    ///   - ライブコンサート: 収益UP（+50%）, 若者集客
    ///   - フードフェスタ: ショップ収益2倍
    ///   - VIPナイト: VIP出現率UP, 高収益
    /// ・イベント開催にはコストが掛かるが、成功すれば大きなリターン
    /// </summary>
    public class SpecialEventUI : MonoBehaviour
    {
        // ============================================================
        // 手動開催イベント定義
        // ============================================================

        private struct ManualEvent
        {
            public string Id;
            public string Name;
            public string Description;
            public float Cost;
            public float SpawnBonus;
            public float RevenueBonus;
            public float HappinessBonus;
            public float RatingBonus;
            public int DurationDays;
        }

        private static readonly ManualEvent[] ManualEvents = new ManualEvent[]
        {
            new ManualEvent
            {
                Id = "manual_fireworks",
                Name = "花火大会",
                Description = "壮大な花火ショーで来場者を魅了！来園率60%UP",
                Cost = 8000f,
                SpawnBonus = 1.6f,
                RevenueBonus = 1.3f,
                HappinessBonus = 15f,
                RatingBonus = 8f,
                DurationDays = 2
            },
            new ManualEvent
            {
                Id = "manual_concert",
                Name = "ライブコンサート",
                Description = "人気アーティストによるライブ！若者が殺到",
                Cost = 12000f,
                SpawnBonus = 1.5f,
                RevenueBonus = 1.5f,
                HappinessBonus = 12f,
                RatingBonus = 6f,
                DurationDays = 1
            },
            new ManualEvent
            {
                Id = "manual_foodfesta",
                Name = "グルメフェスタ",
                Description = "世界の食が集結！ショップ売上が2倍に",
                Cost = 6000f,
                SpawnBonus = 1.3f,
                RevenueBonus = 2.0f,
                HappinessBonus = 8f,
                RatingBonus = 4f,
                DurationDays = 3
            },
            new ManualEvent
            {
                Id = "manual_vipnight",
                Name = "VIPナイト",
                Description = "セレブ限定の特別夜会。高額チケットで高収益",
                Cost = 15000f,
                SpawnBonus = 1.4f,
                RevenueBonus = 1.8f,
                HappinessBonus = 10f,
                RatingBonus = 10f,
                DurationDays = 1
            }
        };

        // ============================================================
        // フィールド
        // ============================================================

        private GameObject _panel;
        private Text _activeEventsText;
        private GameObject _manualListContent;
        private bool _visible;
        private float _refreshTimer;

        // 手動イベントのアクティブ状態
        private readonly Dictionary<string, float> _activeManualEvents = new Dictionary<string, float>();

        // キャッシュ
        private Canvas _cachedCanvas;
        private ParkEventSystem _cachedParkEventSystem;

        public static SpecialEventUI Instance { get; private set; }

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
        }

        private void Update()
        {
            if (!_visible && _activeManualEvents.Count == 0) return;

            // 手動イベントの期間管理
            var expired = new List<string>();
            var keys = new List<string>(_activeManualEvents.Keys);
            foreach (var key in keys)
            {
                float remaining = _activeManualEvents[key] - Time.deltaTime;
                if (remaining <= 0f)
                {
                    expired.Add(key);
                }
                else
                {
                    _activeManualEvents[key] = remaining;
                }
            }
            foreach (var key in expired)
            {
                _activeManualEvents.Remove(key);
                GameManager.Instance?.ShowNotification($"イベント終了", NotifLevel.Info);
            }

            if (_visible)
            {
                _refreshTimer += Time.unscaledDeltaTime;
                if (_refreshTimer >= 2f)
                {
                    _refreshTimer = 0f;
                    RefreshUI();
                }
            }
        }

        // ============================================================
        // UI表示
        // ============================================================

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

        /// <summary>手動イベントの総合スポーン倍率</summary>
        public float GetManualSpawnMultiplier()
        {
            float mul = 1f;
            foreach (var kvp in _activeManualEvents)
            {
                foreach (var me in ManualEvents)
                {
                    if (me.Id == kvp.Key) { mul *= me.SpawnBonus; break; }
                }
            }
            return mul;
        }

        /// <summary>手動イベントの総合収益倍率</summary>
        public float GetManualRevenueMultiplier()
        {
            float mul = 1f;
            foreach (var kvp in _activeManualEvents)
            {
                foreach (var me in ManualEvents)
                {
                    if (me.Id == kvp.Key) { mul *= me.RevenueBonus; break; }
                }
            }
            return mul;
        }

        // ============================================================
        // UI構築
        // ============================================================

        private void BuildUI()
        {
            if (_cachedCanvas == null)
                _cachedCanvas = FindObjectOfType<Canvas>();
            var canvas = _cachedCanvas;
            if (canvas == null) return;

            _panel = new GameObject("SpecialEventPanel");
            _panel.transform.SetParent(canvas.transform, false);
            var rt = _panel.AddComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.15f, 0.1f);
            rt.anchorMax = new Vector2(0.85f, 0.9f);
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;

            var bg = _panel.AddComponent<Image>();
            bg.color = new Color(0.06f, 0.08f, 0.16f, 0.96f);

            // タイトル
            MakeText(_panel.transform, "Title", new Vector2(0.02f, 0.92f), new Vector2(0.8f, 1f),
                "特別イベント管理", 22, FontStyle.Bold, Color.white);

            // 閉じる
            MakeButton(_panel.transform, "CloseBtn",
                new Vector2(0.9f, 0.93f), new Vector2(0.98f, 0.99f),
                "X", new Color(0.7f, 0.15f, 0.15f), HideUI);

            // 現在のイベント表示
            MakeText(_panel.transform, "ActiveLabel",
                new Vector2(0.02f, 0.84f), new Vector2(0.98f, 0.92f),
                "--- 開催中のイベント ---", 14, FontStyle.Bold, new Color(1f, 0.9f, 0.5f));

            var aeObj = MakeText(_panel.transform, "ActiveEvents",
                new Vector2(0.02f, 0.68f), new Vector2(0.98f, 0.84f),
                "", 13, FontStyle.Normal, Color.white);
            _activeEventsText = aeObj.GetComponent<Text>();

            // 手動開催セクション
            MakeText(_panel.transform, "ManualLabel",
                new Vector2(0.02f, 0.6f), new Vector2(0.98f, 0.68f),
                "--- イベント開催 (有料) ---", 14, FontStyle.Bold, new Color(0.5f, 1f, 0.7f));

            // スクロールリスト
            var scrollObj = new GameObject("Scroll");
            scrollObj.transform.SetParent(_panel.transform, false);
            var scrollRt = scrollObj.AddComponent<RectTransform>();
            scrollRt.anchorMin = new Vector2(0.02f, 0.02f);
            scrollRt.anchorMax = new Vector2(0.98f, 0.6f);
            scrollRt.offsetMin = Vector2.zero;
            scrollRt.offsetMax = Vector2.zero;
            var sv = scrollObj.AddComponent<ScrollRect>();
            var svImg = scrollObj.AddComponent<Image>();
            svImg.color = new Color(0.05f, 0.05f, 0.1f, 0.6f);
            scrollObj.AddComponent<Mask>().showMaskGraphic = true;

            var content = new GameObject("Content");
            content.transform.SetParent(scrollObj.transform, false);
            var cRt = content.AddComponent<RectTransform>();
            cRt.anchorMin = new Vector2(0f, 1f);
            cRt.anchorMax = new Vector2(1f, 1f);
            cRt.pivot = new Vector2(0.5f, 1f);
            cRt.offsetMin = Vector2.zero;
            cRt.offsetMax = Vector2.zero;
            var layout = content.AddComponent<VerticalLayoutGroup>();
            layout.spacing = 5f;
            layout.padding = new RectOffset(5, 5, 5, 5);
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            var fitter = content.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            sv.content = cRt;
            sv.vertical = true;
            sv.horizontal = false;

            _manualListContent = content;
            _panel.SetActive(false);
        }

        private void RefreshUI()
        {
            // 開催中イベント表示
            if (_activeEventsText != null)
            {
                if (_cachedParkEventSystem == null)
                    _cachedParkEventSystem = FindObjectOfType<ParkEventSystem>();
                var pes = _cachedParkEventSystem;
                string auto = pes != null ? pes.GetActiveEventsSummary() : "---";

                string manual = "";
                foreach (var kvp in _activeManualEvents)
                {
                    foreach (var me in ManualEvents)
                    {
                        if (me.Id == kvp.Key)
                        {
                            float mins = kvp.Value / 60f;
                            manual += $" | {me.Name} (残{mins:F1}分)";
                            break;
                        }
                    }
                }

                _activeEventsText.text = $"自動: {auto}\n手動: {(manual.Length > 0 ? manual.Substring(3) : "なし")}";
            }

            // 手動イベントリスト再構築
            if (_manualListContent != null)
            {
                for (int i = _manualListContent.transform.childCount - 1; i >= 0; i--)
                    Destroy(_manualListContent.transform.GetChild(i).gameObject);

                float money = GameManager.Instance?.EconomyManager?.CurrentMoney ?? 0f;

                for (int i = 0; i < ManualEvents.Length; i++)
                {
                    var me = ManualEvents[i];
                    bool active = _activeManualEvents.ContainsKey(me.Id);
                    bool canAfford = money >= me.Cost && !active;

                    var item = new GameObject($"Event_{i}");
                    item.transform.SetParent(_manualListContent.transform, false);
                    item.AddComponent<LayoutElement>().preferredHeight = 75f;

                    var itemBg = item.AddComponent<Image>();
                    itemBg.color = active
                        ? new Color(0.15f, 0.3f, 0.15f, 0.8f)
                        : new Color(0.1f, 0.15f, 0.25f, 0.8f);

                    // テキスト
                    var txtObj = MakeText(item.transform, "Info",
                        new Vector2(0f, 0f), new Vector2(0.72f, 1f),
                        $"{me.Name}  (${me.Cost:N0} / {me.DurationDays}日間)\n" +
                        $"{me.Description}\n" +
                        $"来園+{(me.SpawnBonus - 1f) * 100f:F0}%  収益x{me.RevenueBonus:F1}  " +
                        $"幸福+{me.HappinessBonus:F0}  評価+{me.RatingBonus:F0}",
                        12, FontStyle.Normal, Color.white);

                    // ボタン
                    string status = active ? "開催中" : (canAfford ? "開催する" : "資金不足");
                    Color btnColor = active
                        ? new Color(0.3f, 0.5f, 0.3f)
                        : canAfford ? new Color(0.2f, 0.45f, 0.2f) : new Color(0.35f, 0.35f, 0.35f);

                    string eventId = me.Id;
                    int idx = i;
                    var btn = MakeButton(item.transform, "LaunchBtn",
                        new Vector2(0.74f, 0.15f), new Vector2(0.98f, 0.85f),
                        status, btnColor,
                        () => OnLaunchEvent(idx));

                    btn.GetComponent<Button>().interactable = canAfford;
                }
            }
        }

        private void OnLaunchEvent(int index)
        {
            if (index < 0 || index >= ManualEvents.Length) return;
            var me = ManualEvents[index];

            if (_activeManualEvents.ContainsKey(me.Id)) return;

            var econ = GameManager.Instance?.EconomyManager;
            if (econ == null || !econ.PayExpense(me.Cost, ExpenseCategory.Other))
            {
                GameManager.Instance?.ShowNotification("資金が不足しています", NotifLevel.Warning);
                return;
            }

            // イベント開始
            // 実時間でDurationDaysをゲーム内日数に変換（1ゲーム日≈24ゲーム時間×5秒≈120秒）
            float durationSeconds = me.DurationDays * 120f;
            _activeManualEvents[me.Id] = durationSeconds;

            // パーク評価ボーナス
            var pm = GameManager.Instance?.ParkManager;
            if (pm?.Rating != null)
            {
                pm.Rating.ApplyExternalBonus(CertificateCategory.Fame, me.RatingBonus);
                pm.Rating.ApplyExternalBonus(CertificateCategory.Excitement, me.RatingBonus * 0.5f);
            }

            GameEvents.FireParkEventStarted(me.Id, me.Name);
            GameManager.Instance?.ShowNotification(
                $"{me.Name} を開催しました！ ({me.DurationDays}日間)", NotifLevel.Success);

            RefreshUI();
        }

        // ============================================================
        // UIヘルパー
        // ============================================================

        private GameObject MakeText(Transform parent, string name,
            Vector2 anchorMin, Vector2 anchorMax,
            string content, int fontSize, FontStyle style, Color color)
        {
            var obj = new GameObject(name);
            obj.transform.SetParent(parent, false);
            var r = obj.AddComponent<RectTransform>();
            r.anchorMin = anchorMin;
            r.anchorMax = anchorMax;
            r.offsetMin = new Vector2(8f, 2f);
            r.offsetMax = new Vector2(-8f, -2f);
            var t = obj.AddComponent<Text>();
            t.text = content;
            t.font = FontManager.Regular;
            t.fontSize = fontSize;
            t.fontStyle = style;
            t.color = color;
            t.alignment = TextAnchor.MiddleLeft;
            return obj;
        }

        private GameObject MakeButton(Transform parent, string name,
            Vector2 anchorMin, Vector2 anchorMax,
            string text, Color bgColor, UnityEngine.Events.UnityAction onClick)
        {
            var obj = new GameObject(name);
            obj.transform.SetParent(parent, false);
            var r = obj.AddComponent<RectTransform>();
            r.anchorMin = anchorMin;
            r.anchorMax = anchorMax;
            r.offsetMin = Vector2.zero;
            r.offsetMax = Vector2.zero;
            var img = obj.AddComponent<Image>();
            img.color = bgColor;
            var btn = obj.AddComponent<Button>();
            btn.targetGraphic = img;
            btn.onClick.AddListener(onClick);
            var txtObj = new GameObject("Text").AddComponent<Text>();
            txtObj.transform.SetParent(obj.transform, false);
            var tr = txtObj.GetComponent<RectTransform>();
            tr.anchorMin = Vector2.zero;
            tr.anchorMax = Vector2.one;
            tr.offsetMin = Vector2.zero;
            tr.offsetMax = Vector2.zero;
            txtObj.font = FontManager.Regular;
            txtObj.fontSize = 12;
            txtObj.color = Color.white;
            txtObj.alignment = TextAnchor.MiddleCenter;
            txtObj.text = text;
            return obj;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }
    }
}
