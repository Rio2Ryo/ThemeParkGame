// ============================================================
// ThemeParkGame - AlertMonitor
// パーク状態を定期監視し、問題をアラートとして通知するシステム
// ============================================================

using System;
using System.Collections.Generic;
using UnityEngine;
using ThemeParkGame.Attraction;

namespace ThemeParkGame.Core
{
    /// <summary>アラートの種類</summary>
    public enum AlertType
    {
        LowFunds,            // 資金不足
        BrokenAttractions,   // 故障アトラクション放置
        NoMechanic,          // メカニック不在で故障中
        StaffShortage,       // スタッフ不足
        StaffStrike,         // スタッフストライキ
        LowHappiness,        // 来場者満足度低下
        RatingDrop,          // パーク評価下落
        Overcrowding,        // 過密状態
        NoCleaner,           // 清掃スタッフ不在
        BudgetDeficit        // 収支赤字
    }

    /// <summary>アクティブなアラート情報</summary>
    public class ActiveAlert
    {
        public AlertType Type;
        public NotifLevel Level;
        public string Message;
        public float DetectedTime;     // Time.unscaledTime
        public float LastNotifiedTime; // 最後に通知ポップアップを出した時刻
    }

    /// <summary>
    /// パーク状態を定期的にスキャンし、問題を検知してNotificationSystemに通知する。
    /// アクティブなアラートのリストを保持し、HUDのアラートバーに表示する。
    /// 問題が解消されればアラートは自動的に消える。
    /// </summary>
    public class AlertMonitor : MonoBehaviour
    {
        public static AlertMonitor Instance { get; private set; }

        private const float SCAN_INTERVAL = 8f;         // スキャン間隔（秒）
        private const float REPEAT_NOTIFY_INTERVAL = 60f; // 同一アラートの再通知間隔
        private const float LOW_FUNDS_THRESHOLD = 2000f;
        private const float LOW_HAPPINESS_THRESHOLD = 35f;
        private const float DEFICIT_CHECK_WINDOW = 3f;   // 収支チェック（直近の維持費/収入比較）

        private float _scanTimer;
        private float _lastRating;
        private bool _initialized;

        // アクティブアラート
        private readonly Dictionary<AlertType, ActiveAlert> _activeAlerts
            = new Dictionary<AlertType, ActiveAlert>();

        /// <summary>現在アクティブなアラート一覧</summary>
        public IReadOnlyDictionary<AlertType, ActiveAlert> ActiveAlerts => _activeAlerts;

        /// <summary>Danger以上のアラート数</summary>
        public int DangerCount
        {
            get
            {
                int c = 0;
                foreach (var kv in _activeAlerts)
                    if (kv.Value.Level == NotifLevel.Danger) c++;
                return c;
            }
        }

        /// <summary>Warning以上のアラート数</summary>
        public int WarningCount
        {
            get
            {
                int c = 0;
                foreach (var kv in _activeAlerts)
                    if (kv.Value.Level >= NotifLevel.Warning) c++;
                return c;
            }
        }

        /// <summary>全アラート数</summary>
        public int TotalAlertCount => _activeAlerts.Count;

        /// <summary>最も深刻なアラートのレベル</summary>
        public NotifLevel HighestLevel
        {
            get
            {
                NotifLevel max = NotifLevel.Info;
                foreach (var kv in _activeAlerts)
                    if (kv.Value.Level > max) max = kv.Value.Level;
                return max;
            }
        }

        /// <summary>アラートサマリテキスト（HUDアラートバー用）</summary>
        public string GetSummaryText()
        {
            if (_activeAlerts.Count == 0) return "";

            // 最も深刻なものを1件表示
            ActiveAlert worst = null;
            foreach (var kv in _activeAlerts)
            {
                if (worst == null || kv.Value.Level > worst.Level)
                    worst = kv.Value;
            }

            if (_activeAlerts.Count == 1)
                return worst != null ? worst.Message : "";

            return worst != null
                ? $"{worst.Message}  (+{_activeAlerts.Count - 1}件)"
                : "";
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(this);
                return;
            }
            Instance = this;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        private void Update()
        {
            var gm = GameManager.Instance;
            if (gm == null || gm.CurrentState != GameState.Playing) return;

            if (!_initialized)
            {
                // 初期評価値を記録
                if (gm.ParkManager?.Rating != null)
                    _lastRating = gm.ParkManager.Rating.OverallRating;
                _initialized = true;
            }

            _scanTimer -= Time.unscaledDeltaTime;
            if (_scanTimer > 0f) return;
            _scanTimer = SCAN_INTERVAL;

            ScanParkConditions();
        }

        // ================================================================
        // パーク状態スキャン
        // ================================================================

        private void ScanParkConditions()
        {
            var gm = GameManager.Instance;
            if (gm == null) return;

            // 1. 資金チェック
            CheckLowFunds(gm);

            // 2. 故障アトラクションチェック
            CheckBrokenAttractions(gm);

            // 3. メカニック不在チェック
            CheckNoMechanic(gm);

            // 4. 清掃員不在チェック
            CheckNoCleaner(gm);

            // 5. スタッフストライキ
            CheckStaffStrike(gm);

            // 6. 来場者満足度チェック
            CheckLowHappiness(gm);

            // 7. 評価下落チェック
            CheckRatingDrop(gm);

            // 8. 過密チェック
            CheckOvercrowding(gm);

            // 9. 収支赤字チェック
            CheckBudgetDeficit(gm);
        }

        // ---- 個別チェック ----

        private void CheckLowFunds(GameManager gm)
        {
            if (gm.EconomyManager == null) return;
            float balance = gm.EconomyManager.CurrentBalance;

            if (balance < 0)
                SetAlert(AlertType.LowFunds, NotifLevel.Danger,
                    $"資金がマイナスです！ (${balance:N0})");
            else if (balance < LOW_FUNDS_THRESHOLD)
                SetAlert(AlertType.LowFunds, NotifLevel.Warning,
                    $"資金が残り少なくなっています (${balance:N0})");
            else
                ClearAlert(AlertType.LowFunds);
        }

        private void CheckBrokenAttractions(GameManager gm)
        {
            if (gm.AttractionManager == null) return;
            int broken = gm.AttractionManager.BrokenDownCount;

            if (broken >= 3)
                SetAlert(AlertType.BrokenAttractions, NotifLevel.Danger,
                    $"{broken}基のアトラクションが故障中！");
            else if (broken >= 1)
                SetAlert(AlertType.BrokenAttractions, NotifLevel.Warning,
                    $"{broken}基のアトラクションが故障中");
            else
                ClearAlert(AlertType.BrokenAttractions);
        }

        private void CheckNoMechanic(GameManager gm)
        {
            if (gm.StaffManager == null || gm.AttractionManager == null) return;
            int mechanics = gm.StaffManager.GetStaffCount(StaffType.Mechanic);
            int broken = gm.AttractionManager.BrokenDownCount;

            if (broken > 0 && mechanics == 0)
                SetAlert(AlertType.NoMechanic, NotifLevel.Danger,
                    "メカニック不在！故障修理ができません");
            else
                ClearAlert(AlertType.NoMechanic);
        }

        private void CheckNoCleaner(GameManager gm)
        {
            if (gm.StaffManager == null) return;
            int cleaners = gm.StaffManager.GetStaffCount(StaffType.Cleaner);
            int totalAttr = gm.AttractionManager != null ? gm.AttractionManager.TotalCount : 0;

            if (totalAttr >= 3 && cleaners == 0)
                SetAlert(AlertType.NoCleaner, NotifLevel.Warning,
                    "清掃スタッフ不在！パークが汚れます");
            else
                ClearAlert(AlertType.NoCleaner);
        }

        private void CheckStaffStrike(GameManager gm)
        {
            if (gm.StaffManager == null) return;
            int striking = gm.StaffManager.StrikingStaffCount;

            if (striking >= 3)
                SetAlert(AlertType.StaffStrike, NotifLevel.Danger,
                    $"{striking}名のスタッフがストライキ中！");
            else if (striking >= 1)
                SetAlert(AlertType.StaffStrike, NotifLevel.Warning,
                    $"{striking}名のスタッフがストライキ中");
            else
                ClearAlert(AlertType.StaffStrike);
        }

        private void CheckLowHappiness(GameManager gm)
        {
            if (gm.VisitorManager == null) return;
            int active = gm.VisitorManager.ActiveVisitorCount;
            if (active < 5) { ClearAlert(AlertType.LowHappiness); return; }

            float happiness = gm.VisitorManager.AverageHappiness;

            if (happiness < 20f)
                SetAlert(AlertType.LowHappiness, NotifLevel.Danger,
                    $"来場者が非常に不満です！ (満足度{happiness:F0}%)");
            else if (happiness < LOW_HAPPINESS_THRESHOLD)
                SetAlert(AlertType.LowHappiness, NotifLevel.Warning,
                    $"来場者の満足度が低下しています ({happiness:F0}%)");
            else
                ClearAlert(AlertType.LowHappiness);
        }

        private void CheckRatingDrop(GameManager gm)
        {
            if (gm.ParkManager?.Rating == null) return;
            float current = gm.ParkManager.Rating.OverallRating;

            if (_lastRating > 0f && current < _lastRating - 10f)
                SetAlert(AlertType.RatingDrop, NotifLevel.Warning,
                    $"パーク評価が下落中 ({_lastRating:F0} -> {current:F0})");
            else
                ClearAlert(AlertType.RatingDrop);

            // 評価記録更新（緩やかに追従）
            _lastRating = Mathf.Lerp(_lastRating, current, 0.1f);
        }

        private void CheckOvercrowding(GameManager gm)
        {
            if (gm.VisitorManager == null) return;
            int active = gm.VisitorManager.ActiveVisitorCount;
            int max = GameManager.GetMaxVisitors(gm.CurrentDifficulty);

            if (active >= max)
                SetAlert(AlertType.Overcrowding, NotifLevel.Warning,
                    $"パークが満員です！ ({active}/{max}人)");
            else
                ClearAlert(AlertType.Overcrowding);
        }

        private void CheckBudgetDeficit(GameManager gm)
        {
            if (gm.EconomyManager == null) return;
            float revenue = gm.EconomyManager.TotalRevenueEarned;
            float expenses = gm.EconomyManager.TotalExpensesPaid;

            // 支出が収入を大きく上回っている
            if (revenue > 0f && expenses > revenue * 1.5f)
                SetAlert(AlertType.BudgetDeficit, NotifLevel.Warning,
                    $"支出が収入を大きく超過 (収入${revenue:N0} / 支出${expenses:N0})");
            else
                ClearAlert(AlertType.BudgetDeficit);
        }

        // ================================================================
        // アラート管理
        // ================================================================

        private void SetAlert(AlertType type, NotifLevel level, string message)
        {
            float now = Time.unscaledTime;

            if (_activeAlerts.TryGetValue(type, out var existing))
            {
                // 既にあるアラートを更新
                existing.Level = level;
                existing.Message = message;

                // 一定間隔で再通知
                if (now - existing.LastNotifiedTime >= REPEAT_NOTIFY_INTERVAL)
                {
                    existing.LastNotifiedTime = now;
                    NotifyAlert(message, level);
                }
            }
            else
            {
                // 新しいアラート
                var alert = new ActiveAlert
                {
                    Type = type,
                    Level = level,
                    Message = message,
                    DetectedTime = now,
                    LastNotifiedTime = now
                };
                _activeAlerts[type] = alert;
                NotifyAlert(message, level);
            }
        }

        private void ClearAlert(AlertType type)
        {
            if (_activeAlerts.ContainsKey(type))
            {
                _activeAlerts.Remove(type);
            }
        }

        private void NotifyAlert(string message, NotifLevel level)
        {
            if (NotificationSystem.Instance != null)
                NotificationSystem.Instance.Notify(message, level);
        }

        /// <summary>全アラートをクリアする（ゲームリセット時など）</summary>
        public void ClearAllAlerts()
        {
            _activeAlerts.Clear();
            _initialized = false;
        }
    }
}
