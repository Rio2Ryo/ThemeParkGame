// ============================================================
// ThemeParkGame - FastPassSystem
// ファストパス/フリーパス チケット管理システム
// ============================================================

using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using ThemeParkGame.Core;

namespace ThemeParkGame.Economy
{
    /// <summary>チケットタイプ</summary>
    public enum TicketType
    {
        /// <summary>通常チケット（個別購入、通常キュー）</summary>
        Normal,
        /// <summary>ファストパス（特定アトラクション優先搭乗、1回限り）</summary>
        FastPass,
        /// <summary>フリーパス（全アトラクション乗り放題、当日有効）</summary>
        FreePass,
        /// <summary>ゾーンパス（特定ゾーン内アトラクション乗り放題、当日有効）</summary>
        ZonePass
    }

    /// <summary>
    /// 来場者が保有するチケット情報。
    /// </summary>
    [Serializable]
    public class VisitorTicket
    {
        public TicketType Type;
        /// <summary>ファストパス: 対象アトラクションID。フリーパス/ゾーンパスでは-1</summary>
        public int TargetAttractionId = -1;
        /// <summary>ゾーンパス: 対象テーマゾーン</summary>
        public ThemeZone TargetZone;
        /// <summary>残り利用回数（ファストパス=1、フリーパス/ゾーンパス=int.MaxValue）</summary>
        public int UsesRemaining;
        /// <summary>購入価格</summary>
        public float PurchasePrice;
    }

    /// <summary>
    /// ファストパス/フリーパス/ゾーンパスの販売・管理を行うシステム。
    ///
    /// 【ゲームデザイン】
    /// ・ファストパス: 特定アトラクションの優先搭乗権。1回限り。人気アトラクションの行列問題を解決する。
    /// ・フリーパス: 全アトラクション乗り放題の1日券。高額だが、たくさん乗る来場者にお得。
    /// ・ゾーンパス: 特定テーマゾーン内アトラクション乗り放題。中間価格帯。
    /// ・プレイヤーは各チケットの販売ON/OFF と価格設定が可能。
    /// ・VIPゲストはフリーパスを自動購入する傾向がある。
    /// </summary>
    public class FastPassSystem : MonoBehaviour
    {
        public static FastPassSystem Instance { get; private set; }

        // ---- 価格設定 ----

        /// <summary>ファストパス販売が有効か</summary>
        public bool FastPassEnabled { get; set; } = true;

        /// <summary>フリーパス販売が有効か</summary>
        public bool FreePassEnabled { get; set; } = true;

        /// <summary>ゾーンパス販売が有効か</summary>
        public bool ZonePassEnabled { get; set; } = true;

        /// <summary>ファストパスの基本価格（アトラクションチケット価格に対する倍率で計算）</summary>
        [Header("価格設定")]
        [SerializeField] private float fastPassPriceMultiplier = 2.0f;
        public float FastPassPriceMultiplier
        {
            get => fastPassPriceMultiplier;
            set => fastPassPriceMultiplier = Mathf.Clamp(value, 1f, 5f);
        }

        /// <summary>フリーパス価格</summary>
        [SerializeField] private float freePassPrice = 2000f;
        public float FreePassPrice
        {
            get => freePassPrice;
            set => freePassPrice = Mathf.Max(0f, value);
        }

        /// <summary>ゾーンパス価格</summary>
        [SerializeField] private float zonePassPrice = 800f;
        public float ZonePassPrice
        {
            get => zonePassPrice;
            set => zonePassPrice = Mathf.Max(0f, value);
        }

        // ---- 販売統計 ----

        /// <summary>本日のファストパス販売数</summary>
        public int TodayFastPassSold { get; private set; }

        /// <summary>本日のフリーパス販売数</summary>
        public int TodayFreePassSold { get; private set; }

        /// <summary>本日のゾーンパス販売数</summary>
        public int TodayZonePassSold { get; private set; }

        /// <summary>本日のチケット総売上</summary>
        public float TodayTicketRevenue { get; private set; }

        /// <summary>累計チケット売上</summary>
        public float TotalTicketRevenue { get; private set; }

        // ---- 来場者チケット管理 ----

        /// <summary>来場者ID → 保有チケットリスト</summary>
        private readonly Dictionary<int, List<VisitorTicket>> _visitorTickets
            = new Dictionary<int, List<VisitorTicket>>();

        // ---- UI ----
        private GameObject _uiPanel;
        private bool _visible;

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(this); return; }
            Instance = this;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        // ================================================================
        // チケット購入
        // ================================================================

        /// <summary>
        /// ファストパスを購入する。
        /// </summary>
        /// <param name="visitorId">来場者ID</param>
        /// <param name="attractionId">対象アトラクションID</param>
        /// <param name="attractionTicketPrice">アトラクションの通常チケット価格</param>
        /// <returns>購入成功ならtrue</returns>
        public bool PurchaseFastPass(int visitorId, int attractionId, int attractionTicketPrice)
        {
            if (!FastPassEnabled) return false;

            float price = attractionTicketPrice * fastPassPriceMultiplier;
            if (!TryChargeVisitor(visitorId, price)) return false;

            var ticket = new VisitorTicket
            {
                Type = TicketType.FastPass,
                TargetAttractionId = attractionId,
                UsesRemaining = 1,
                PurchasePrice = price
            };

            AddTicket(visitorId, ticket);
            RecordSale(TicketType.FastPass, price);

            WebGLOptimizer.LogVerbose($"[FastPass] Visitor {visitorId} purchased FastPass for attraction {attractionId} (${price:F0})");
            return true;
        }

        /// <summary>
        /// フリーパスを購入する。
        /// </summary>
        public bool PurchaseFreePass(int visitorId)
        {
            if (!FreePassEnabled) return false;
            if (!TryChargeVisitor(visitorId, freePassPrice)) return false;

            var ticket = new VisitorTicket
            {
                Type = TicketType.FreePass,
                UsesRemaining = int.MaxValue,
                PurchasePrice = freePassPrice
            };

            AddTicket(visitorId, ticket);
            RecordSale(TicketType.FreePass, freePassPrice);

            WebGLOptimizer.LogVerbose($"[FastPass] Visitor {visitorId} purchased FreePass (${freePassPrice:F0})");
            return true;
        }

        /// <summary>
        /// ゾーンパスを購入する。
        /// </summary>
        public bool PurchaseZonePass(int visitorId, ThemeZone zone)
        {
            if (!ZonePassEnabled) return false;
            if (!TryChargeVisitor(visitorId, zonePassPrice)) return false;

            var ticket = new VisitorTicket
            {
                Type = TicketType.ZonePass,
                TargetZone = zone,
                UsesRemaining = int.MaxValue,
                PurchasePrice = zonePassPrice
            };

            AddTicket(visitorId, ticket);
            RecordSale(TicketType.ZonePass, zonePassPrice);

            WebGLOptimizer.LogVerbose($"[FastPass] Visitor {visitorId} purchased ZonePass for {zone} (${zonePassPrice:F0})");
            return true;
        }

        // ================================================================
        // チケット利用判定
        // ================================================================

        /// <summary>
        /// 来場者が特定アトラクションのファストパスを保有しているか。
        /// </summary>
        public bool HasFastPassFor(int visitorId, int attractionId)
        {
            if (!_visitorTickets.TryGetValue(visitorId, out var tickets)) return false;
            return tickets.Exists(t =>
                t.Type == TicketType.FastPass &&
                t.TargetAttractionId == attractionId &&
                t.UsesRemaining > 0);
        }

        /// <summary>
        /// 来場者がフリーパスを保有しているか。
        /// </summary>
        public bool HasFreePass(int visitorId)
        {
            if (!_visitorTickets.TryGetValue(visitorId, out var tickets)) return false;
            return tickets.Exists(t => t.Type == TicketType.FreePass && t.UsesRemaining > 0);
        }

        /// <summary>
        /// 来場者が特定ゾーンのゾーンパスを保有しているか。
        /// </summary>
        public bool HasZonePassFor(int visitorId, ThemeZone zone)
        {
            if (!_visitorTickets.TryGetValue(visitorId, out var tickets)) return false;
            return tickets.Exists(t =>
                t.Type == TicketType.ZonePass &&
                t.TargetZone == zone &&
                t.UsesRemaining > 0);
        }

        /// <summary>
        /// 来場者がアトラクションに対する優先搭乗権を持っているか（ファストパスまたはフリーパス）。
        /// </summary>
        /// <param name="visitorId">来場者ID</param>
        /// <param name="attractionId">アトラクションID</param>
        /// <param name="attractionZone">アトラクションのテーマゾーン</param>
        /// <returns>優先搭乗権があればtrue</returns>
        public bool HasPriorityAccess(int visitorId, int attractionId, ThemeZone attractionZone)
        {
            return HasFastPassFor(visitorId, attractionId)
                || HasFreePass(visitorId)
                || HasZonePassFor(visitorId, attractionZone);
        }

        /// <summary>
        /// ファストパスを1回消費する。
        /// </summary>
        public void ConsumeFastPass(int visitorId, int attractionId)
        {
            if (!_visitorTickets.TryGetValue(visitorId, out var tickets)) return;
            var ticket = tickets.Find(t =>
                t.Type == TicketType.FastPass &&
                t.TargetAttractionId == attractionId &&
                t.UsesRemaining > 0);
            if (ticket != null)
            {
                ticket.UsesRemaining--;
                if (ticket.UsesRemaining <= 0)
                    tickets.Remove(ticket);
            }
        }

        /// <summary>
        /// 来場者がアトラクション搭乗時にチケットを消費する（アトラクションチケット料金免除判定）。
        /// </summary>
        /// <returns>チケット料金が免除されるならtrue</returns>
        public bool TryConsumeRideTicket(int visitorId, int attractionId, ThemeZone attractionZone)
        {
            if (HasFastPassFor(visitorId, attractionId))
            {
                ConsumeFastPass(visitorId, attractionId);
                return true;
            }

            if (HasFreePass(visitorId)) return true; // フリーパスは消費しない（乗り放題）
            if (HasZonePassFor(visitorId, attractionZone)) return true; // ゾーンパスも消費しない

            return false; // 通常チケット＝料金支払い必要
        }

        // ================================================================
        // 内部ヘルパー
        // ================================================================

        private void AddTicket(int visitorId, VisitorTicket ticket)
        {
            if (!_visitorTickets.ContainsKey(visitorId))
                _visitorTickets[visitorId] = new List<VisitorTicket>();
            _visitorTickets[visitorId].Add(ticket);
        }

        private void RecordSale(TicketType type, float price)
        {
            switch (type)
            {
                case TicketType.FastPass: TodayFastPassSold++; break;
                case TicketType.FreePass: TodayFreePassSold++; break;
                case TicketType.ZonePass: TodayZonePassSold++; break;
            }

            TodayTicketRevenue += price;
            TotalTicketRevenue += price;

            // EconomyManagerに売上を計上
            if (GameManager.Instance?.EconomyManager != null)
            {
                GameManager.Instance.EconomyManager.AddRevenue(
                    price, RevenueCategory.AttractionFee, -1);
            }
        }

        /// <summary>来場者から料金を徴収する（所持金チェック込み）</summary>
        private bool TryChargeVisitor(int visitorId, float price)
        {
            if (GameManager.Instance?.VisitorManager == null) return true;

            var visitor = GameManager.Instance.VisitorManager.FindVisitorById(visitorId);
            if (visitor == null) return false;

            if (visitor.Parameters.Money < price) return false;

            visitor.Parameters.SpendMoney(price);
            return true;
        }

        /// <summary>来場者退園時にチケット情報をクリアする</summary>
        public void OnVisitorLeft(int visitorId)
        {
            _visitorTickets.Remove(visitorId);
        }

        /// <summary>日次リセット</summary>
        public void ResetDailyStats()
        {
            TodayFastPassSold = 0;
            TodayFreePassSold = 0;
            TodayZonePassSold = 0;
            TodayTicketRevenue = 0f;
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
            var canvas = CanvasCache.Get();
            if (canvas == null) return;

            _uiPanel = new GameObject("FastPassPanel");
            _uiPanel.transform.SetParent(canvas.transform, false);
            var rt = _uiPanel.AddComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.2f, 0.15f);
            rt.anchorMax = new Vector2(0.8f, 0.85f);
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;

            var bg = _uiPanel.AddComponent<Image>();
            bg.color = new Color(0.06f, 0.08f, 0.14f, 0.96f);

            // タイトル
            MakeText(_uiPanel.transform, "Title",
                new Vector2(0.02f, 0.9f), new Vector2(0.7f, 1f),
                "チケットシステム管理", 22, FontStyle.Bold, Color.white);

            // 閉じるボタン
            MakeBtn(_uiPanel.transform, "CloseBtn",
                new Vector2(0.9f, 0.93f), new Vector2(0.98f, 0.99f),
                "X", new Color(0.7f, 0.15f, 0.15f), HideUI);

            _uiPanel.SetActive(false);
        }

        private void RefreshUI()
        {
            // 統計パネルの更新（簡易実装）
        }

        // UIヘルパー
        private Text MakeText(Transform parent, string name,
            Vector2 aMin, Vector2 aMax, string content, int size, FontStyle style, Color color)
        {
            var obj = new GameObject(name);
            obj.transform.SetParent(parent, false);
            var r = obj.AddComponent<RectTransform>();
            r.anchorMin = aMin; r.anchorMax = aMax;
            r.offsetMin = new Vector2(5f, 0f);
            r.offsetMax = new Vector2(-5f, 0f);
            var t = obj.AddComponent<Text>();
            t.text = content;
            t.font = FontManager.Regular;
            t.fontSize = size;
            t.fontStyle = style;
            t.color = color;
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
            var img = obj.AddComponent<Image>();
            img.color = bg;
            var btn = obj.AddComponent<Button>();
            btn.targetGraphic = img;
            btn.onClick.AddListener(onClick);
            var txt = new GameObject("Text").AddComponent<Text>();
            txt.transform.SetParent(obj.transform, false);
            var tr = txt.GetComponent<RectTransform>();
            tr.anchorMin = Vector2.zero; tr.anchorMax = Vector2.one;
            tr.offsetMin = Vector2.zero; tr.offsetMax = Vector2.zero;
            txt.font = FontManager.Regular;
            txt.fontSize = 14; txt.color = Color.white;
            txt.alignment = TextAnchor.MiddleCenter;
            txt.text = text;
        }
    }
}
