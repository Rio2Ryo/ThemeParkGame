// ============================================================
// ThemeParkGame - SaleCampaignSystem
// 期間限定割引キャンペーン・大セール機能
// ============================================================

using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using ThemeParkGame.Core;

namespace ThemeParkGame.Economy
{
    /// <summary>セール種別</summary>
    public enum SaleType
    {
        EntranceFee,       // 入場料割引
        AttractionTicket,  // アトラクション料金割引
        FoodDrink,         // 飲食割引
        Souvenir,          // お土産割引
        AllInclusive       // 全施設割引
    }

    /// <summary>アクティブなセールキャンペーン</summary>
    [Serializable]
    public class ActiveSale
    {
        public int Id;
        public string Name;
        public SaleType Type;
        public float DiscountRate;      // 0.1 = 10%割引
        public float DurationDays;      // ゲーム内日数
        public float RemainingTime;     // 残り秒数
        public float SpawnBonus;        // 来場者増加倍率
        public float CostToRun;         // 開催コスト
        public bool IsActive;
    }

    /// <summary>
    /// 期間限定セールキャンペーンを管理するシステム。
    ///
    /// 【ゲームデザイン】
    /// ・プレイヤーがセールを開催すると来場者数が増加
    /// ・割引により1人あたりの収益は減少するがボリュームで補う
    /// ・季節外れ（閑散期）に開催すると効果が高い
    /// ・同時に複数セールを開催可能（コストは加算）
    /// ・セール終了後も口コミ効果が一定期間持続
    /// </summary>
    public class SaleCampaignSystem : MonoBehaviour
    {
        private readonly List<ActiveSale> _activeSales = new List<ActiveSale>();
        private int _nextId = 1;

        // 定義済みセールプラン
        public static readonly SalePlan[] Plans = {
            new SalePlan {
                Name = "入場料半額セール",
                Type = SaleType.EntranceFee,
                DiscountRate = 0.5f,
                DurationDays = 3f,
                SpawnBonus = 1.5f,
                Cost = 5000
            },
            new SalePlan {
                Name = "アトラクション30%OFF",
                Type = SaleType.AttractionTicket,
                DiscountRate = 0.3f,
                DurationDays = 2f,
                SpawnBonus = 1.3f,
                Cost = 8000
            },
            new SalePlan {
                Name = "グルメフェスタ割引",
                Type = SaleType.FoodDrink,
                DiscountRate = 0.4f,
                DurationDays = 5f,
                SpawnBonus = 1.2f,
                Cost = 3000
            },
            new SalePlan {
                Name = "お土産大セール",
                Type = SaleType.Souvenir,
                DiscountRate = 0.5f,
                DurationDays = 3f,
                SpawnBonus = 1.1f,
                Cost = 4000
            },
            new SalePlan {
                Name = "全施設スーパーセール",
                Type = SaleType.AllInclusive,
                DiscountRate = 0.25f,
                DurationDays = 1f,
                SpawnBonus = 2.0f,
                Cost = 15000
            },
            new SalePlan {
                Name = "オフシーズン大特価",
                Type = SaleType.AllInclusive,
                DiscountRate = 0.6f,
                DurationDays = 7f,
                SpawnBonus = 1.8f,
                Cost = 10000
            }
        };

        // UI
        private GameObject _uiPanel;
        private Text _statusText;
        private bool _visible;

        public static SaleCampaignSystem Instance { get; private set; }

        public IReadOnlyList<ActiveSale> ActiveSales => _activeSales;

        /// <summary>現在の割引率（施設種別ごと）</summary>
        public float GetDiscountRate(SaleType type)
        {
            float maxDiscount = 0f;
            foreach (var sale in _activeSales)
            {
                if (sale.IsActive && (sale.Type == type || sale.Type == SaleType.AllInclusive))
                    maxDiscount = Mathf.Max(maxDiscount, sale.DiscountRate);
            }
            return maxDiscount;
        }

        /// <summary>現在のスポーンボーナス</summary>
        public float GetSpawnMultiplier()
        {
            float mul = 1f;
            foreach (var sale in _activeSales)
            {
                if (sale.IsActive)
                    mul = Mathf.Max(mul, sale.SpawnBonus);
            }
            return mul;
        }

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(this); return; }
            Instance = this;
        }

        private void Update()
        {
            if (GameManager.Instance == null) return;
            if (GameManager.Instance.CurrentState != GameState.Playing) return;

            float dt = Time.deltaTime;

            for (int i = _activeSales.Count - 1; i >= 0; i--)
            {
                var sale = _activeSales[i];
                if (!sale.IsActive) continue;

                sale.RemainingTime -= dt;
                if (sale.RemainingTime <= 0f)
                {
                    sale.IsActive = false;
                    _activeSales.RemoveAt(i);

                    GameManager.Instance?.ShowNotification(
                        $"セール終了: {sale.Name}", NotifLevel.Info);
                    WebGLOptimizer.LogVerbose($"[SaleCampaign] Sale ended: {sale.Name}");
                }
            }
        }

        // ================================================================
        // セール開催
        // ================================================================

        /// <summary>指定プランのセールを開催する</summary>
        public bool StartSale(int planIndex)
        {
            if (planIndex < 0 || planIndex >= Plans.Length) return false;

            var plan = Plans[planIndex];
            var econ = GameManager.Instance?.EconomyManager;
            if (econ == null) return false;

            if (!econ.CanAfford(plan.Cost))
            {
                GameManager.Instance?.ShowNotification("資金が不足しています", NotifLevel.Warning);
                return false;
            }

            // 同じタイプのセールが既に開催中かチェック
            foreach (var active in _activeSales)
            {
                if (active.IsActive && active.Type == plan.Type)
                {
                    GameManager.Instance?.ShowNotification(
                        "同種のセールが開催中です", NotifLevel.Warning);
                    return false;
                }
            }

            econ.PayExpense(plan.Cost, ExpenseCategory.Other);

            // 閑散期ボーナス（冬・秋は効果UP）
            float seasonBonus = 1f;
            var tm = GameManager.Instance?.TimeManager;
            if (tm != null)
            {
                int month = tm.CurrentMonth;
                if (month >= 11 || month <= 2) seasonBonus = 1.4f;  // 冬
                else if (month >= 9 && month <= 10) seasonBonus = 1.2f; // 秋
            }

            float durationSeconds = plan.DurationDays * 24f * 5f; // 5秒/ゲーム内1時間

            var sale = new ActiveSale
            {
                Id = _nextId++,
                Name = plan.Name,
                Type = plan.Type,
                DiscountRate = plan.DiscountRate,
                DurationDays = plan.DurationDays,
                RemainingTime = durationSeconds,
                SpawnBonus = plan.SpawnBonus * seasonBonus,
                CostToRun = plan.Cost,
                IsActive = true
            };

            _activeSales.Add(sale);

            GameManager.Instance?.ShowNotification(
                $"セール開始！ {plan.Name} ({plan.DiscountRate * 100f:F0}%OFF, {plan.DurationDays}日間)" +
                (seasonBonus > 1f ? $" 閑散期ボーナス x{seasonBonus:F1}" : ""),
                NotifLevel.Success);

            WebGLOptimizer.LogVerbose($"[SaleCampaign] Sale started: {plan.Name} (season bonus: {seasonBonus:F1})");
            return true;
        }

        // ================================================================
        // UI
        // ================================================================

        public void ToggleUI()
        {
            if (_visible) HideUI(); else ShowUI();
        }

        public void ShowUI()
        {
            if (_uiPanel == null) BuildUI();
            _uiPanel.SetActive(true);
            _visible = true;
            RefreshUI();
        }

        public void HideUI()
        {
            if (_uiPanel != null) _uiPanel.SetActive(false);
            _visible = false;
        }

        private void BuildUI()
        {
            var canvas = FindObjectOfType<Canvas>();
            if (canvas == null) return;

            _uiPanel = new GameObject("SaleCampaignPanel");
            _uiPanel.transform.SetParent(canvas.transform, false);
            var rt = _uiPanel.AddComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.15f, 0.05f);
            rt.anchorMax = new Vector2(0.85f, 0.95f);
            rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;

            var bg = _uiPanel.AddComponent<Image>();
            bg.color = new Color(0.06f, 0.08f, 0.14f, 0.96f);

            MakeText(_uiPanel.transform, "Title",
                new Vector2(0.02f, 0.93f), new Vector2(0.8f, 1f),
                "セールキャンペーン", 20, FontStyle.Bold, Color.white);

            MakeBtn(_uiPanel.transform, "Close",
                new Vector2(0.9f, 0.94f), new Vector2(0.98f, 0.99f),
                "X", new Color(0.7f, 0.15f, 0.15f), HideUI);

            // ステータス
            _statusText = MakeText(_uiPanel.transform, "Status",
                new Vector2(0.02f, 0.86f), new Vector2(0.98f, 0.93f),
                "", 14, FontStyle.Bold, new Color(0.9f, 0.85f, 0.5f));

            // セールプランボタン
            float y = 0.78f;
            for (int i = 0; i < Plans.Length; i++)
            {
                var plan = Plans[i];
                int idx = i;

                // プラン背景
                var planObj = new GameObject($"Plan_{i}");
                planObj.transform.SetParent(_uiPanel.transform, false);
                var planRt = planObj.AddComponent<RectTransform>();
                planRt.anchorMin = new Vector2(0.02f, y - 0.1f);
                planRt.anchorMax = new Vector2(0.98f, y);
                planRt.offsetMin = Vector2.zero; planRt.offsetMax = Vector2.zero;
                var planBg = planObj.AddComponent<Image>();
                planBg.color = new Color(0.12f, 0.14f, 0.22f, 0.9f);

                // プラン名
                MakeText(planObj.transform, "Name",
                    new Vector2(0.02f, 0.5f), new Vector2(0.5f, 1f),
                    plan.Name, 14, FontStyle.Bold, Color.white);

                // プラン詳細
                MakeText(planObj.transform, "Detail",
                    new Vector2(0.02f, 0f), new Vector2(0.5f, 0.5f),
                    $"{plan.DiscountRate * 100f:F0}%OFF | {plan.DurationDays}日 | 来場者x{plan.SpawnBonus:F1}",
                    12, FontStyle.Normal, new Color(0.6f, 0.7f, 0.8f));

                // コスト
                MakeText(planObj.transform, "Cost",
                    new Vector2(0.52f, 0f), new Vector2(0.72f, 1f),
                    $"${plan.Cost:N0}", 15, FontStyle.Bold, new Color(0.95f, 0.88f, 0.45f));

                // 開催ボタン
                MakeBtn(planObj.transform, "StartBtn",
                    new Vector2(0.75f, 0.15f), new Vector2(0.98f, 0.85f),
                    "開催", new Color(0.2f, 0.5f, 0.3f), () => { StartSale(idx); RefreshUI(); });

                y -= 0.115f;
            }

            // アクティブセール
            MakeText(_uiPanel.transform, "ActiveTitle",
                new Vector2(0.02f, 0.06f), new Vector2(0.98f, 0.12f),
                "開催中のセール:", 14, FontStyle.Bold, new Color(0.5f, 0.8f, 0.5f));

            _uiPanel.SetActive(false);
        }

        private void RefreshUI()
        {
            if (_statusText != null)
            {
                float spawnMul = GetSpawnMultiplier();
                string seasonInfo = "";
                var tm = GameManager.Instance?.TimeManager;
                if (tm != null)
                {
                    int month = tm.CurrentMonth;
                    if (month >= 11 || month <= 2) seasonInfo = " [冬季ボーナス期間]";
                    else if (month >= 9 && month <= 10) seasonInfo = " [秋季ボーナス期間]";
                }

                string activeList = "";
                foreach (var sale in _activeSales)
                {
                    if (sale.IsActive)
                    {
                        float remainDays = sale.RemainingTime / (24f * 5f);
                        activeList += $"\n  {sale.Name}: 残り{remainDays:F1}日 (x{sale.SpawnBonus:F1})";
                    }
                }

                _statusText.text = $"集客倍率: x{spawnMul:F2}{seasonInfo}" +
                    (activeList.Length > 0 ? activeList : "\n  開催中のセールなし");
            }
        }

        // UIヘルパー
        private Text MakeText(Transform parent, string name,
            Vector2 aMin, Vector2 aMax, string content, int size, FontStyle style, Color color)
        {
            var obj = new GameObject(name);
            obj.transform.SetParent(parent, false);
            var r = obj.AddComponent<RectTransform>();
            r.anchorMin = aMin; r.anchorMax = aMax;
            r.offsetMin = new Vector2(5f, 0f); r.offsetMax = new Vector2(-5f, 0f);
            var t = obj.AddComponent<Text>();
            t.text = content; t.font = FontManager.Regular;
            t.fontSize = size; t.fontStyle = style; t.color = color;
            t.alignment = TextAnchor.MiddleLeft;
            return t;
        }

        private void MakeBtn(Transform parent, string name,
            Vector2 aMin, Vector2 aMax, string text, Color bg,
            UnityEngine.Events.UnityAction onClick)
        {
            var obj = new GameObject(name);
            obj.transform.SetParent(parent, false);
            var r = obj.AddComponent<RectTransform>();
            r.anchorMin = aMin; r.anchorMax = aMax;
            r.offsetMin = Vector2.zero; r.offsetMax = Vector2.zero;
            var img = obj.AddComponent<Image>(); img.color = bg;
            var btn = obj.AddComponent<Button>(); btn.targetGraphic = img;
            btn.onClick.AddListener(onClick);
            var txt = new GameObject("Text").AddComponent<Text>();
            txt.transform.SetParent(obj.transform, false);
            var tr = txt.GetComponent<RectTransform>();
            tr.anchorMin = Vector2.zero; tr.anchorMax = Vector2.one;
            tr.offsetMin = Vector2.zero; tr.offsetMax = Vector2.zero;
            txt.font = FontManager.Regular;
            txt.fontSize = 14; txt.color = Color.white;
            txt.alignment = TextAnchor.MiddleCenter; txt.text = text;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }
    }

    /// <summary>セールプラン定義</summary>
    public class SalePlan
    {
        public string Name;
        public SaleType Type;
        public float DiscountRate;
        public float DurationDays;
        public float SpawnBonus;
        public float Cost;
    }
}
