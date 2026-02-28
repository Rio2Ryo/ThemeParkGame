// ============================================================
// ThemeParkGame - AccidentEventSystem
// ランダムアクシデント発生・対応システム
// ============================================================

using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace ThemeParkGame.Core
{
    /// <summary>アクシデント種別</summary>
    public enum AccidentType
    {
        CoasterBreakdown,   // ジェットコースター故障（乗客避難必要）
        LostChild,          // 迷子（スタッフが捜索→見つけると満足度UP）
        VisitorComplaint,   // クレーム（対応しないと口コミ悪化）
        PowerOutage,        // 停電（全アトラクション一時停止）
        FoodPoisoning,      // 食中毒（ショップ一時閉鎖、医療対応）
        StaffConflict,      // スタッフ間トラブル（士気低下）
        VandalismIncident,  // 器物損壊（ガードが対応）
        AnimalEscape,       // 動物逃走（パニック発生）
        WaterLeakage,       // 水漏れ（メカニック対応）
        FireAlarm,          // 火災報知器誤作動（全員避難→時間損失）
        PickpocketReport,   // スリ被害報告（ガード必要）
        BrokenBench,        // ベンチ故障（メカニック対応）
        TrashOverflow,      // ゴミ溢れ（清掃スタッフ対応）
        NoisyVisitor,       // 騒音トラブル客（ガード対応）
        QueueCutting        // 割り込みトラブル（ガード対応）
    }

    /// <summary>アクシデントの深刻度</summary>
    public enum AccidentSeverity
    {
        Minor,      // 軽微（自然回復可能）
        Moderate,   // 中程度（スタッフ対応推奨）
        Major,      // 重大（即時対応必要）
        Critical    // 致命的（対応しないとゲーム影響大）
    }

    /// <summary>アクティブなアクシデント情報</summary>
    public class ActiveAccident
    {
        public int Id;
        public AccidentType Type;
        public AccidentSeverity Severity;
        public string Title;
        public string Description;
        public float TimeRemaining;     // 対応猶予時間
        public float MaxTime;
        public bool IsResolved;
        public float HappinessPenalty;  // 毎秒
        public float RatingPenalty;     // 未対応時
        public float RevenueLoss;       // 毎秒の収益損失
        public StaffType RequiredStaff; // 対応に必要なスタッフ
    }

    /// <summary>
    /// ランダムアクシデント発生・管理システム。
    ///
    /// 【ゲームデザイン】
    /// ・一定間隔でランダムなアクシデントが発生
    /// ・各アクシデントには対応猶予時間があり、適切なスタッフで対応可能
    /// ・未対応のまま時間切れになると満足度・評価にペナルティ
    /// ・対応成功でボーナス（評価UP・ゴールデンチケットチャンス）
    /// ・難易度によってアクシデント頻度が変化
    /// </summary>
    public class AccidentEventSystem : MonoBehaviour
    {
        private const float BASE_INTERVAL_MIN = 60f;
        private const float BASE_INTERVAL_MAX = 180f;
        private const float MAX_ACTIVE_ACCIDENTS = 3;

        private float _nextAccidentTimer;
        private int _nextId = 1;
        private readonly List<ActiveAccident> _activeAccidents = new List<ActiveAccident>();
        private readonly List<ActiveAccident> _recentResolved = new List<ActiveAccident>();

        // UI
        private GameObject _uiPanel;
        private RectTransform _listContent;
        private readonly List<GameObject> _uiItems = new List<GameObject>();
        private Text _titleLabel;
        private bool _visible;

        // 統計
        public int TotalAccidents { get; private set; }
        public int ResolvedAccidents { get; private set; }
        public int FailedAccidents { get; private set; }
        public bool HasResolvedCritical { get; private set; }
        public float ResolutionRate => TotalAccidents > 0 ? (float)ResolvedAccidents / TotalAccidents : 1f;

        public static AccidentEventSystem Instance { get; private set; }

        public IReadOnlyList<ActiveAccident> ActiveAccidents => _activeAccidents;

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(this); return; }
            Instance = this;
            ResetTimer();
        }

        private void Update()
        {
            if (GameManager.Instance == null) return;
            if (GameManager.Instance.CurrentState != GameState.Playing) return;

            float dt = Time.deltaTime;

            // アクシデント発生タイマー
            _nextAccidentTimer -= dt;
            if (_nextAccidentTimer <= 0f && _activeAccidents.Count < MAX_ACTIVE_ACCIDENTS)
            {
                SpawnRandomAccident();
                ResetTimer();
            }

            // アクティブアクシデントの更新
            for (int i = _activeAccidents.Count - 1; i >= 0; i--)
            {
                var acc = _activeAccidents[i];
                if (acc.IsResolved) continue;

                acc.TimeRemaining -= dt;

                // ペナルティ適用
                if (GameManager.Instance.VisitorManager != null)
                    GameManager.Instance.VisitorManager.ApplyGlobalHappinessModifier(-acc.HappinessPenalty * dt);

                // 時間切れ
                if (acc.TimeRemaining <= 0f)
                {
                    FailAccident(acc);
                    _activeAccidents.RemoveAt(i);
                }
            }

            // UIリフレッシュ
            if (_visible) RefreshUI();
        }

        private void ResetTimer()
        {
            float diffMul = 1f;
            if (GameManager.Instance != null)
            {
                diffMul = GameManager.Instance.CurrentDifficulty switch
                {
                    GameDifficulty.Easy => 1.5f,
                    GameDifficulty.Hard => 0.6f,
                    _ => 1f
                };
            }
            _nextAccidentTimer = UnityEngine.Random.Range(BASE_INTERVAL_MIN, BASE_INTERVAL_MAX) * diffMul;
        }

        // ================================================================
        // アクシデント生成
        // ================================================================

        private void SpawnRandomAccident()
        {
            var types = (AccidentType[])Enum.GetValues(typeof(AccidentType));
            var type = types[UnityEngine.Random.Range(0, types.Length)];
            var acc = CreateAccident(type);

            _activeAccidents.Add(acc);
            TotalAccidents++;

            GameManager.Instance?.ShowNotification(
                $"[事故] {acc.Title}", NotifLevel.Danger);
            GameEvents.FireAccidentOccurred(acc.Id, acc.Type);

            WebGLOptimizer.LogVerbose($"[AccidentEvent] Accident spawned: {acc.Title} ({acc.Severity})");
        }

        private ActiveAccident CreateAccident(AccidentType type)
        {
            var acc = new ActiveAccident { Id = _nextId++, Type = type };

            switch (type)
            {
                case AccidentType.CoasterBreakdown:
                    acc.Title = "ジェットコースター緊急停止";
                    acc.Description = "コースターが走行中に停止。乗客の救出が必要です。";
                    acc.Severity = AccidentSeverity.Critical;
                    acc.MaxTime = 120f; acc.HappinessPenalty = 0.5f; acc.RatingPenalty = 5f;
                    acc.RevenueLoss = 10f; acc.RequiredStaff = StaffType.Mechanic;
                    break;
                case AccidentType.LostChild:
                    acc.Title = "迷子の子供を発見";
                    acc.Description = "泣いている子供が保護者とはぐれています。";
                    acc.Severity = AccidentSeverity.Moderate;
                    acc.MaxTime = 90f; acc.HappinessPenalty = 0.2f; acc.RatingPenalty = 3f;
                    acc.RevenueLoss = 0f; acc.RequiredStaff = StaffType.Guard;
                    break;
                case AccidentType.VisitorComplaint:
                    acc.Title = "来場者からのクレーム";
                    acc.Description = "不満を持つ来場者が受付で抗議しています。";
                    acc.Severity = AccidentSeverity.Minor;
                    acc.MaxTime = 60f; acc.HappinessPenalty = 0.1f; acc.RatingPenalty = 2f;
                    acc.RevenueLoss = 0f; acc.RequiredStaff = StaffType.Entertainer;
                    break;
                case AccidentType.PowerOutage:
                    acc.Title = "一時的な停電";
                    acc.Description = "パークの一部区域で停電が発生。アトラクションが停止中。";
                    acc.Severity = AccidentSeverity.Major;
                    acc.MaxTime = 90f; acc.HappinessPenalty = 0.4f; acc.RatingPenalty = 4f;
                    acc.RevenueLoss = 20f; acc.RequiredStaff = StaffType.Mechanic;
                    break;
                case AccidentType.FoodPoisoning:
                    acc.Title = "食中毒の疑い";
                    acc.Description = "複数の来場者が体調不良を訴えています。";
                    acc.Severity = AccidentSeverity.Critical;
                    acc.MaxTime = 60f; acc.HappinessPenalty = 0.6f; acc.RatingPenalty = 8f;
                    acc.RevenueLoss = 15f; acc.RequiredStaff = StaffType.Doctor;
                    break;
                case AccidentType.StaffConflict:
                    acc.Title = "スタッフ間トラブル";
                    acc.Description = "スタッフ同士の口論が発生し、業務に支障が出ています。";
                    acc.Severity = AccidentSeverity.Minor;
                    acc.MaxTime = 120f; acc.HappinessPenalty = 0.05f; acc.RatingPenalty = 1f;
                    acc.RevenueLoss = 0f; acc.RequiredStaff = StaffType.Guard;
                    break;
                case AccidentType.VandalismIncident:
                    acc.Title = "器物損壊が発生";
                    acc.Description = "パーク内の施設が故意に破損されました。";
                    acc.Severity = AccidentSeverity.Moderate;
                    acc.MaxTime = 90f; acc.HappinessPenalty = 0.15f; acc.RatingPenalty = 3f;
                    acc.RevenueLoss = 5f; acc.RequiredStaff = StaffType.Guard;
                    break;
                case AccidentType.AnimalEscape:
                    acc.Title = "動物が逃走！";
                    acc.Description = "展示動物が囲いから逃げ出し、来場者がパニック中。";
                    acc.Severity = AccidentSeverity.Major;
                    acc.MaxTime = 60f; acc.HappinessPenalty = 0.5f; acc.RatingPenalty = 5f;
                    acc.RevenueLoss = 10f; acc.RequiredStaff = StaffType.Guard;
                    break;
                case AccidentType.WaterLeakage:
                    acc.Title = "水漏れ発生";
                    acc.Description = "配管から水が漏れ、通路が水浸しになっています。";
                    acc.Severity = AccidentSeverity.Moderate;
                    acc.MaxTime = 120f; acc.HappinessPenalty = 0.1f; acc.RatingPenalty = 2f;
                    acc.RevenueLoss = 3f; acc.RequiredStaff = StaffType.Mechanic;
                    break;
                case AccidentType.FireAlarm:
                    acc.Title = "火災報知器が作動";
                    acc.Description = "誤作動の可能性がありますが、安全確認が必要です。";
                    acc.Severity = AccidentSeverity.Major;
                    acc.MaxTime = 45f; acc.HappinessPenalty = 0.3f; acc.RatingPenalty = 4f;
                    acc.RevenueLoss = 25f; acc.RequiredStaff = StaffType.Guard;
                    break;
                case AccidentType.PickpocketReport:
                    acc.Title = "スリ被害報告";
                    acc.Description = "来場者から財布を盗まれたとの報告がありました。";
                    acc.Severity = AccidentSeverity.Moderate;
                    acc.MaxTime = 90f; acc.HappinessPenalty = 0.2f; acc.RatingPenalty = 4f;
                    acc.RevenueLoss = 0f; acc.RequiredStaff = StaffType.Guard;
                    break;
                case AccidentType.BrokenBench:
                    acc.Title = "ベンチ破損";
                    acc.Description = "ベンチが壊れて使用不能になっています。";
                    acc.Severity = AccidentSeverity.Minor;
                    acc.MaxTime = 180f; acc.HappinessPenalty = 0.02f; acc.RatingPenalty = 1f;
                    acc.RevenueLoss = 0f; acc.RequiredStaff = StaffType.Mechanic;
                    break;
                case AccidentType.TrashOverflow:
                    acc.Title = "ゴミ箱溢れ";
                    acc.Description = "ゴミ箱が満杯で周囲にゴミが散乱しています。";
                    acc.Severity = AccidentSeverity.Minor;
                    acc.MaxTime = 180f; acc.HappinessPenalty = 0.05f; acc.RatingPenalty = 1f;
                    acc.RevenueLoss = 0f; acc.RequiredStaff = StaffType.Cleaner;
                    break;
                case AccidentType.NoisyVisitor:
                    acc.Title = "騒音トラブル客";
                    acc.Description = "騒がしい来場者グループが他の客に迷惑をかけています。";
                    acc.Severity = AccidentSeverity.Minor;
                    acc.MaxTime = 60f; acc.HappinessPenalty = 0.1f; acc.RatingPenalty = 1f;
                    acc.RevenueLoss = 0f; acc.RequiredStaff = StaffType.Guard;
                    break;
                default: // QueueCutting
                    acc.Title = "行列割り込みトラブル";
                    acc.Description = "割り込みをめぐって来場者同士が口論しています。";
                    acc.Severity = AccidentSeverity.Minor;
                    acc.MaxTime = 45f; acc.HappinessPenalty = 0.15f; acc.RatingPenalty = 1f;
                    acc.RevenueLoss = 0f; acc.RequiredStaff = StaffType.Guard;
                    break;
            }

            acc.TimeRemaining = acc.MaxTime;
            return acc;
        }

        // ================================================================
        // アクシデント解決
        // ================================================================

        /// <summary>指定IDのアクシデントを解決する</summary>
        public bool ResolveAccident(int accidentId)
        {
            var acc = _activeAccidents.Find(a => a.Id == accidentId);
            if (acc == null || acc.IsResolved) return false;

            // 必要スタッフの確認
            if (GameManager.Instance?.StaffManager != null)
            {
                bool hasStaff = GameManager.Instance.StaffManager.HasStaffOfType(acc.RequiredStaff);
                if (!hasStaff)
                {
                    GameManager.Instance?.ShowNotification(
                        $"{acc.RequiredStaff} が不在のため対応できません", NotifLevel.Warning);
                    return false;
                }
            }

            acc.IsResolved = true;
            ResolvedAccidents++;
            if (acc.Severity == AccidentSeverity.Critical) HasResolvedCritical = true;
            _activeAccidents.Remove(acc);
            _recentResolved.Add(acc);
            if (_recentResolved.Count > 10) _recentResolved.RemoveAt(0);

            // 解決ボーナス
            float timeRatio = acc.TimeRemaining / acc.MaxTime;
            float ratingBonus = timeRatio * 2f;
            if (GameManager.Instance?.ParkManager?.Rating != null)
                GameManager.Instance.ParkManager.Rating.ApplyExternalBonus(CertificateCategory.Safety, ratingBonus);

            GameManager.Instance?.ShowNotification(
                $"[解決] {acc.Title} (評価+{ratingBonus:F1})", NotifLevel.Success);
            GameEvents.FireAccidentResolved(accidentId);

            WebGLOptimizer.LogVerbose($"[AccidentEvent] Resolved: {acc.Title} (time ratio: {timeRatio:F2})");
            return true;
        }

        private void FailAccident(ActiveAccident acc)
        {
            FailedAccidents++;

            // ペナルティ
            if (GameManager.Instance?.ParkManager?.Rating != null)
            {
                GameManager.Instance.ParkManager.Rating.ApplyExternalBonus(
                    CertificateCategory.Safety, -acc.RatingPenalty);
            }

            GameManager.Instance?.ShowNotification(
                $"[未対応] {acc.Title} → 評価-{acc.RatingPenalty:F0}", NotifLevel.Danger);

            WebGLOptimizer.LogVerbose($"[AccidentEvent] Failed: {acc.Title}");
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

            _uiPanel = new GameObject("AccidentPanel");
            _uiPanel.transform.SetParent(canvas.transform, false);
            var rt = _uiPanel.AddComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.15f, 0.1f);
            rt.anchorMax = new Vector2(0.85f, 0.9f);
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;

            var bg = _uiPanel.AddComponent<Image>();
            bg.color = new Color(0.06f, 0.08f, 0.14f, 0.96f);

            _titleLabel = MakeText(_uiPanel.transform, "Title",
                new Vector2(0.02f, 0.92f), new Vector2(0.85f, 1f),
                "事故レポート", 22, FontStyle.Bold, Color.white);

            // 統計
            MakeText(_uiPanel.transform, "Stats",
                new Vector2(0.02f, 0.85f), new Vector2(0.98f, 0.92f),
                "", 14, FontStyle.Normal, new Color(0.6f, 0.7f, 0.8f));

            // 閉じるボタン
            MakeBtn(_uiPanel.transform, "CloseBtn",
                new Vector2(0.9f, 0.93f), new Vector2(0.98f, 0.99f),
                "X", new Color(0.7f, 0.15f, 0.15f), HideUI);

            // リストエリア
            var scrollArea = new GameObject("ScrollArea");
            scrollArea.transform.SetParent(_uiPanel.transform, false);
            var scrollRt = scrollArea.AddComponent<RectTransform>();
            scrollRt.anchorMin = new Vector2(0.02f, 0.02f);
            scrollRt.anchorMax = new Vector2(0.98f, 0.84f);
            scrollRt.offsetMin = Vector2.zero;
            scrollRt.offsetMax = Vector2.zero;

            _listContent = scrollArea.AddComponent<RectTransform>();

            _uiPanel.SetActive(false);
        }

        private void RefreshUI()
        {
            if (_uiPanel == null) return;

            // 統計更新
            var statsText = _uiPanel.transform.Find("Stats")?.GetComponent<Text>();
            if (statsText != null)
            {
                statsText.text = $"合計: {TotalAccidents} | 解決: {ResolvedAccidents} | " +
                    $"未対応: {FailedAccidents} | 解決率: {ResolutionRate * 100f:F0}%";
            }

            // 既存アイテムクリア
            foreach (var item in _uiItems)
                if (item != null) Destroy(item);
            _uiItems.Clear();

            // アクティブアクシデント表示
            float y = 0.82f;
            foreach (var acc in _activeAccidents)
            {
                if (y < 0.02f) break;

                var item = new GameObject($"Accident_{acc.Id}");
                item.transform.SetParent(_uiPanel.transform, false);
                var itemRt = item.AddComponent<RectTransform>();
                itemRt.anchorMin = new Vector2(0.02f, y - 0.09f);
                itemRt.anchorMax = new Vector2(0.98f, y);
                itemRt.offsetMin = Vector2.zero;
                itemRt.offsetMax = Vector2.zero;

                var itemBg = item.AddComponent<Image>();
                Color sevColor = acc.Severity switch
                {
                    AccidentSeverity.Critical => new Color(0.5f, 0.1f, 0.1f, 0.8f),
                    AccidentSeverity.Major => new Color(0.5f, 0.3f, 0.1f, 0.8f),
                    AccidentSeverity.Moderate => new Color(0.4f, 0.4f, 0.15f, 0.8f),
                    _ => new Color(0.2f, 0.3f, 0.4f, 0.8f)
                };
                itemBg.color = sevColor;

                // タイトル
                MakeText(item.transform, "AccTitle",
                    new Vector2(0.02f, 0.5f), new Vector2(0.55f, 1f),
                    $"[{acc.Severity}] {acc.Title}", 14, FontStyle.Bold, Color.white);

                // 残り時間
                MakeText(item.transform, "Timer",
                    new Vector2(0.02f, 0f), new Vector2(0.55f, 0.5f),
                    $"残り {acc.TimeRemaining:F0}秒 | 必要: {acc.RequiredStaff}", 12,
                    FontStyle.Normal, new Color(0.8f, 0.8f, 0.9f));

                // 解決ボタン
                MakeBtn(item.transform, "ResolveBtn",
                    new Vector2(0.6f, 0.15f), new Vector2(0.98f, 0.85f),
                    "対応する", new Color(0.2f, 0.5f, 0.3f), () => ResolveAccident(acc.Id));

                _uiItems.Add(item);
                y -= 0.1f;
            }

            if (_activeAccidents.Count == 0)
            {
                var noItems = MakeText(_uiPanel.transform, "NoAccidents",
                    new Vector2(0.1f, 0.4f), new Vector2(0.9f, 0.5f),
                    "現在発生中のアクシデントはありません", 16, FontStyle.Normal,
                    new Color(0.5f, 0.6f, 0.5f));
                // Store as UI item so it gets cleaned up
                _uiItems.Add(noItems.gameObject);
            }
        }

        // ================================================================
        // UIヘルパー
        // ================================================================

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

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }
    }
}
