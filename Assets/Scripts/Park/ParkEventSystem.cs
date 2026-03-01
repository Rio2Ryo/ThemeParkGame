// ============================================================
// ThemeParkGame - ParkEventSystem
// パーク内イベントシステム: 季節イベント・特別ショー・臨時イベント
// ============================================================

using System;
using System.Collections.Generic;
using UnityEngine;
using ThemeParkGame.Core;

namespace ThemeParkGame.Park
{
    // ================================================================
    // データ定義
    // ================================================================

    /// <summary>イベント種別</summary>
    public enum ParkEventType
    {
        Seasonal,       // 季節イベント（自動発生）
        SpecialShow,    // 特別ショー（毎日スケジュール）
        LimitedTime,    // 期間限定イベント（ランダム発生）
        Anniversary,    // 周年記念（年単位）
        Disaster        // 災害イベント（ランダム発生、パークに深刻な影響）
    }

    /// <summary>イベントの状態</summary>
    public enum ParkEventState
    {
        Scheduled,  // 予定
        Active,     // 開催中
        Ended       // 終了
    }

    /// <summary>
    /// パークイベントの定義データ。
    /// 季節イベント・特別ショー等の共通構造。
    /// </summary>
    [Serializable]
    public class ParkEventData
    {
        public string Id;
        public string DisplayName;
        public string Description;
        public ParkEventType Type;

        /// <summary>対応する季節（Seasonal時のみ。nullは全季節）</summary>
        public Season? TargetSeason;

        /// <summary>開催月（1-12、0=毎月）</summary>
        public int TargetMonth;

        /// <summary>開始時刻（ゲーム時間）</summary>
        public float StartHour;

        /// <summary>終了時刻（ゲーム時間）</summary>
        public float EndHour;

        /// <summary>開催日数（Seasonal: 季節全体、Show: 1日）</summary>
        public int DurationDays;

        // ---- 効果 ----

        /// <summary>来場者幸福度ボーナス</summary>
        public float HappinessBonus;

        /// <summary>来場者スポーン率倍率（1.0=通常）</summary>
        public float SpawnRateMultiplier = 1f;

        /// <summary>収益ボーナス倍率（1.0=通常）</summary>
        public float RevenueMultiplier = 1f;

        /// <summary>パーク評価ボーナス</summary>
        public float RatingBonus;

        /// <summary>来場者満足度ボーナス</summary>
        public float SatisfactionBonus;

        /// <summary>特定のVisitorTypeへの吸引力（nullは全タイプ）</summary>
        public VisitorType? PreferredVisitorType;
    }

    /// <summary>
    /// 実行中のイベントインスタンス。
    /// </summary>
    public class ActiveEvent
    {
        public ParkEventData Data;
        public ParkEventState State;
        public float ElapsedTime;
        public int StartDay;
        public int StartMonth;
        public int StartYear;

        /// <summary>ショーの繰り返し回数（累計）</summary>
        public int RepeatCount;

        /// <summary>参加来場者数（累計）</summary>
        public int TotalAttendees;
    }

    // ================================================================
    // ParkEventSystem 本体
    // ================================================================

    /// <summary>
    /// パーク内のイベントを管理するシステム。
    ///
    /// 【ゲームデザイン】
    /// イベントはパークの魅力を高め、来場者の満足度と収益を増加させる。
    ///
    /// ■ 季節イベント（自動発生）:
    ///   - 春: フラワーフェスティバル（幸福度UP・ファミリー集客）
    ///   - 夏: サマーナイト花火大会（夕方～夜の興奮度UP・カップル集客）
    ///   - 秋: ハロウィンホラーナイト（スリル系アトラクション人気UP）
    ///   - 冬: クリスマスイルミネーション（全体的な幸福度UP・VIP集客）
    ///
    /// ■ 特別ショー（毎日スケジュール）:
    ///   - パレード（12:00-13:00）: 全来場者の幸福度UP
    ///   - ナイトショー（20:00-21:00）: 退園を遅らせ、収益UP
    ///
    /// ■ 期間限定イベント（ランダム発生）:
    ///   - フードフェスティバル: ショップ収益2倍
    ///   - スリルチャレンジ: アトラクション満足度UP
    ///   - VIPガラ: VIP出現率UP
    /// </summary>
    public class ParkEventSystem : MonoBehaviour
    {
        // ---- イベントデータベース ----
        private readonly List<ParkEventData> _eventDatabase = new List<ParkEventData>();

        // ---- アクティブイベント ----
        private readonly List<ActiveEvent> _activeEvents = new List<ActiveEvent>();

        // ---- スケジュール済みショー ----
        private readonly List<ActiveEvent> _scheduledShows = new List<ActiveEvent>();

        // ---- 履歴 ----
        private readonly List<string> _eventHistory = new List<string>();

        // ---- タイマー ----
        private float _checkTimer;
        private const float CheckInterval = 2f;
        private float _limitedEventTimer;
        private const float LimitedEventCheckInterval = 30f;
        private Season _lastSeason;
        private int _lastMonth;

        // ---- 外部公開プロパティ ----

        /// <summary>現在開催中のイベント一覧</summary>
        public IReadOnlyList<ActiveEvent> CurrentEvents => _activeEvents;

        /// <summary>開催中のイベント数</summary>
        public int ActiveEventCount => _activeEvents.Count;

        /// <summary>現在の来場者スポーン率倍率（全イベント合計）</summary>
        public float CurrentSpawnMultiplier { get; private set; } = 1f;

        /// <summary>現在の収益倍率（全イベント合計）</summary>
        public float CurrentRevenueMultiplier { get; private set; } = 1f;

        /// <summary>現在の幸福度ボーナス合計</summary>
        public float CurrentHappinessBonus { get; private set; }

        /// <summary>現在の満足度ボーナス合計</summary>
        public float CurrentSatisfactionBonus { get; private set; }

        /// <summary>イベント履歴</summary>
        public IReadOnlyList<string> EventHistory => _eventHistory;

        // ================================================================
        // 初期化
        // ================================================================

        private void Awake()
        {
            BuildEventDatabase();
        }

        private void OnEnable()
        {
            if (GameManager.Instance != null && GameManager.Instance.TimeManager != null)
            {
                GameManager.Instance.TimeManager.OnDayChanged += OnDayChanged;
                GameManager.Instance.TimeManager.OnMonthChanged += OnMonthChanged;
            }
        }

        private void OnDisable()
        {
            if (GameManager.Instance != null && GameManager.Instance.TimeManager != null)
            {
                GameManager.Instance.TimeManager.OnDayChanged -= OnDayChanged;
                GameManager.Instance.TimeManager.OnMonthChanged -= OnMonthChanged;
            }
        }

        /// <summary>イベントデータベースを構築する</summary>
        private void BuildEventDatabase()
        {
            _eventDatabase.Clear();

            // ==== 季節イベント ====

            _eventDatabase.Add(new ParkEventData
            {
                Id = "spring_flower",
                DisplayName = "フラワーフェスティバル",
                Description = "色とりどりの花が咲き誇る春の祭典。パーク全体が華やかに。",
                Type = ParkEventType.Seasonal,
                TargetSeason = Season.Spring,
                TargetMonth = 0,
                StartHour = 8f,
                EndHour = 22f,
                DurationDays = 30,
                HappinessBonus = 8f,
                SpawnRateMultiplier = 1.3f,
                RevenueMultiplier = 1.15f,
                RatingBonus = 5f,
                SatisfactionBonus = 3f,
                PreferredVisitorType = VisitorType.Family
            });

            _eventDatabase.Add(new ParkEventData
            {
                Id = "summer_fireworks",
                DisplayName = "サマーナイト花火大会",
                Description = "夏の夜空を彩る壮大な花火ショー。ロマンチックな夜を演出。",
                Type = ParkEventType.Seasonal,
                TargetSeason = Season.Summer,
                TargetMonth = 0,
                StartHour = 19f,
                EndHour = 22f,
                DurationDays = 30,
                HappinessBonus = 12f,
                SpawnRateMultiplier = 1.5f,
                RevenueMultiplier = 1.3f,
                RatingBonus = 8f,
                SatisfactionBonus = 5f,
                PreferredVisitorType = VisitorType.Couple
            });

            _eventDatabase.Add(new ParkEventData
            {
                Id = "autumn_halloween",
                DisplayName = "ハロウィンホラーナイト",
                Description = "パーク全体がホラーテーマに変身。スリル満点の特別演出。",
                Type = ParkEventType.Seasonal,
                TargetSeason = Season.Autumn,
                TargetMonth = 0,
                StartHour = 16f,
                EndHour = 22f,
                DurationDays = 30,
                HappinessBonus = 6f,
                SpawnRateMultiplier = 1.4f,
                RevenueMultiplier = 1.25f,
                RatingBonus = 7f,
                SatisfactionBonus = 4f,
                PreferredVisitorType = VisitorType.Young
            });

            _eventDatabase.Add(new ParkEventData
            {
                Id = "winter_christmas",
                DisplayName = "クリスマスイルミネーション",
                Description = "パーク全体が輝くイルミネーション。特別な冬のひととき。",
                Type = ParkEventType.Seasonal,
                TargetSeason = Season.Winter,
                TargetMonth = 0,
                StartHour = 16f,
                EndHour = 22f,
                DurationDays = 30,
                HappinessBonus = 15f,
                SpawnRateMultiplier = 1.6f,
                RevenueMultiplier = 1.4f,
                RatingBonus = 10f,
                SatisfactionBonus = 6f,
                PreferredVisitorType = VisitorType.VIP
            });

            // ==== 特別ショー（毎日） ====

            _eventDatabase.Add(new ParkEventData
            {
                Id = "daily_parade",
                DisplayName = "デイリーパレード",
                Description = "華やかなキャラクターパレードがパーク内を練り歩く。",
                Type = ParkEventType.SpecialShow,
                TargetMonth = 0,
                StartHour = 12f,
                EndHour = 13f,
                DurationDays = 1,
                HappinessBonus = 10f,
                SpawnRateMultiplier = 1.1f,
                RevenueMultiplier = 1.1f,
                RatingBonus = 2f,
                SatisfactionBonus = 4f
            });

            _eventDatabase.Add(new ParkEventData
            {
                Id = "night_show",
                DisplayName = "ナイトスペクタクル",
                Description = "噴水と光の壮大なナイトショー。パークの1日を締めくくる。",
                Type = ParkEventType.SpecialShow,
                TargetMonth = 0,
                StartHour = 20f,
                EndHour = 21f,
                DurationDays = 1,
                HappinessBonus = 12f,
                SpawnRateMultiplier = 1.0f,
                RevenueMultiplier = 1.2f,
                RatingBonus = 3f,
                SatisfactionBonus = 5f
            });

            // ナイトパレード（ParadeSystemが有効な場合に連動）
            _eventDatabase.Add(new ParkEventData
            {
                Id = "night_parade",
                DisplayName = "ナイトパレード",
                Description = "きらびやかなフロートが夜のパークを彩る。パレード鑑賞で幸福度が大幅UP。",
                Type = ParkEventType.SpecialShow,
                TargetMonth = 0,
                StartHour = 18f,
                EndHour = 22f,
                DurationDays = 1,
                HappinessBonus = 15f,
                SpawnRateMultiplier = 1.2f,
                RevenueMultiplier = 1.3f,
                RatingBonus = 5f,
                SatisfactionBonus = 8f
            });

            // ==== 期間限定イベント ====

            _eventDatabase.Add(new ParkEventData
            {
                Id = "food_festival",
                DisplayName = "グルメフェスティバル",
                Description = "世界各国の美食が楽しめる特別フードイベント。",
                Type = ParkEventType.LimitedTime,
                TargetMonth = 0,
                StartHour = 10f,
                EndHour = 21f,
                DurationDays = 5,
                HappinessBonus = 5f,
                SpawnRateMultiplier = 1.2f,
                RevenueMultiplier = 2.0f,
                RatingBonus = 4f,
                SatisfactionBonus = 3f
            });

            _eventDatabase.Add(new ParkEventData
            {
                Id = "thrill_challenge",
                DisplayName = "スリルチャレンジ",
                Description = "アトラクション制覇チャレンジ！全アトラクションの満足度UP。",
                Type = ParkEventType.LimitedTime,
                TargetMonth = 0,
                StartHour = 8f,
                EndHour = 22f,
                DurationDays = 3,
                HappinessBonus = 3f,
                SpawnRateMultiplier = 1.3f,
                RevenueMultiplier = 1.15f,
                RatingBonus = 5f,
                SatisfactionBonus = 8f,
                PreferredVisitorType = VisitorType.Young
            });

            _eventDatabase.Add(new ParkEventData
            {
                Id = "vip_gala",
                DisplayName = "VIPガラパーティ",
                Description = "セレブが集う特別夜会。VIP来場率が大幅UP。",
                Type = ParkEventType.LimitedTime,
                TargetMonth = 0,
                StartHour = 17f,
                EndHour = 22f,
                DurationDays = 2,
                HappinessBonus = 8f,
                SpawnRateMultiplier = 1.4f,
                RevenueMultiplier = 1.5f,
                RatingBonus = 6f,
                SatisfactionBonus = 5f,
                PreferredVisitorType = VisitorType.VIP
            });

            _eventDatabase.Add(new ParkEventData
            {
                Id = "family_day",
                DisplayName = "ファミリーデー",
                Description = "家族連れ向けの特別割引デー。キッズ・ファミリーが大集合。",
                Type = ParkEventType.LimitedTime,
                TargetMonth = 0,
                StartHour = 8f,
                EndHour = 20f,
                DurationDays = 3,
                HappinessBonus = 6f,
                SpawnRateMultiplier = 1.5f,
                RevenueMultiplier = 0.85f, // 割引なので収益は若干減
                RatingBonus = 3f,
                SatisfactionBonus = 4f,
                PreferredVisitorType = VisitorType.Kids
            });

            // ==== 周年記念 ====

            _eventDatabase.Add(new ParkEventData
            {
                Id = "anniversary",
                DisplayName = "パーク周年記念祭",
                Description = "開園記念を祝う大規模フェスティバル。全ボーナスが発動！",
                Type = ParkEventType.Anniversary,
                TargetMonth = 1,
                StartHour = 8f,
                EndHour = 22f,
                DurationDays = 7,
                HappinessBonus = 15f,
                SpawnRateMultiplier = 1.8f,
                RevenueMultiplier = 1.5f,
                RatingBonus = 12f,
                SatisfactionBonus = 8f
            });

            WebGLOptimizer.LogVerbose($"[ParkEventSystem] イベントDB構築完了: {_eventDatabase.Count}件");
        }

        // ================================================================
        // 更新ループ
        // ================================================================

        private void Update()
        {
            var gm = GameManager.Instance;
            if (gm == null || gm.CurrentState != GameState.Playing) return;
            if (gm.TimeManager == null) return;

            float dt = Time.deltaTime;

            _checkTimer += dt;
            if (_checkTimer >= CheckInterval)
            {
                _checkTimer = 0f;
                CheckSeasonalEvents();
                CheckDailyShows();
                UpdateActiveEvents();
                RecalculateBonuses();
            }

            _limitedEventTimer += dt;
            if (_limitedEventTimer >= LimitedEventCheckInterval)
            {
                _limitedEventTimer = 0f;
                TryTriggerLimitedEvent();
            }

            // アクティブイベントの経過時間を更新
            for (int i = 0; i < _activeEvents.Count; i++)
            {
                _activeEvents[i].ElapsedTime += dt;
            }
        }

        // ================================================================
        // 季節イベントチェック
        // ================================================================

        private void CheckSeasonalEvents()
        {
            var ws = GameManager.Instance.WeatherSystem;
            if (ws == null) return;

            Season currentSeason = ws.CurrentSeason;

            // 季節が変わった場合
            if (currentSeason != _lastSeason)
            {
                // 旧季節のイベントを終了
                EndEventsOfSeason(_lastSeason);

                // 新季節のイベントを開始
                StartSeasonalEvent(currentSeason);

                _lastSeason = currentSeason;
            }
        }

        private void StartSeasonalEvent(Season season)
        {
            foreach (var data in _eventDatabase)
            {
                if (data.Type != ParkEventType.Seasonal) continue;
                if (data.TargetSeason != season) continue;
                if (IsEventActive(data.Id)) continue;

                var ae = CreateActiveEvent(data);
                _activeEvents.Add(ae);

                // 通知
                NotifyEventStart(data);
                GameEvents.FireParkEventStarted(data.Id, data.DisplayName);

                WebGLOptimizer.LogVerbose($"[ParkEventSystem] 季節イベント開始: {data.DisplayName}");
            }
        }

        private void EndEventsOfSeason(Season season)
        {
            for (int i = _activeEvents.Count - 1; i >= 0; i--)
            {
                var ae = _activeEvents[i];
                if (ae.Data.Type == ParkEventType.Seasonal &&
                    ae.Data.TargetSeason == season)
                {
                    EndEvent(i);
                }
            }
        }

        // ================================================================
        // 特別ショー（毎日スケジュール）
        // ================================================================

        private void CheckDailyShows()
        {
            var tm = GameManager.Instance.TimeManager;
            float hour = tm.CurrentHour;

            foreach (var data in _eventDatabase)
            {
                if (data.Type != ParkEventType.SpecialShow) continue;

                bool inTimeWindow = hour >= data.StartHour && hour < data.EndHour;
                bool alreadyActive = IsEventActive(data.Id);

                if (inTimeWindow && !alreadyActive)
                {
                    var ae = CreateActiveEvent(data);
                    _activeEvents.Add(ae);

                    NotifyEventStart(data);
                    GameEvents.FireParkEventStarted(data.Id, data.DisplayName);
                    WebGLOptimizer.LogVerbose($"[ParkEventSystem] ショー開始: {data.DisplayName} ({data.StartHour:F0}:00～)");
                }
                else if (!inTimeWindow && alreadyActive)
                {
                    // 時間外になったら終了
                    EndEventById(data.Id);
                }
            }
        }

        // ================================================================
        // 期間限定イベント（ランダム発生）
        // ================================================================

        private void TryTriggerLimitedEvent()
        {
            // 既に期間限定イベントが2つ以上開催中なら新規は発生しない
            int limitedCount = 0;
            foreach (var ae in _activeEvents)
            {
                if (ae.Data.Type == ParkEventType.LimitedTime) limitedCount++;
            }
            if (limitedCount >= 2) return;

            // 発生確率: 基本1%/チェック、パーク評価が高いほどUP
            float baseChance = 0.01f;
            var pm = GameManager.Instance?.ParkManager;
            if (pm != null)
            {
                float rating = pm.GetOverallRating();
                baseChance += rating / 1000f; // 評価100で+0.1
            }

            if (UnityEngine.Random.value > baseChance) return;

            // ランダムに期間限定イベントを選択
            var candidates = new List<ParkEventData>();
            foreach (var data in _eventDatabase)
            {
                if (data.Type != ParkEventType.LimitedTime) continue;
                if (IsEventActive(data.Id)) continue;
                candidates.Add(data);
            }

            if (candidates.Count == 0) return;

            var selected = candidates[UnityEngine.Random.Range(0, candidates.Count)];
            var ae2 = CreateActiveEvent(selected);
            _activeEvents.Add(ae2);

            NotifyEventStart(selected);
            GameEvents.FireParkEventStarted(selected.Id, selected.DisplayName);

            WebGLOptimizer.LogVerbose($"[ParkEventSystem] 期間限定イベント発生: {selected.DisplayName} " +
                      $"({selected.DurationDays}日間)");
        }

        // ================================================================
        // 周年記念
        // ================================================================

        private void OnYearChanged(int newYear)
        {
            foreach (var data in _eventDatabase)
            {
                if (data.Type != ParkEventType.Anniversary) continue;
                if (IsEventActive(data.Id)) continue;

                var ae = CreateActiveEvent(data);
                _activeEvents.Add(ae);

                NotifyEventStart(data);
                GameEvents.FireParkEventStarted(data.Id,
                    $"{data.DisplayName} (Year {newYear})");

                WebGLOptimizer.LogVerbose($"[ParkEventSystem] 周年記念イベント開始: Year {newYear}");
            }
        }

        // ================================================================
        // イベント更新・終了
        // ================================================================

        private void UpdateActiveEvents()
        {
            var tm = GameManager.Instance.TimeManager;

            for (int i = _activeEvents.Count - 1; i >= 0; i--)
            {
                var ae = _activeEvents[i];

                // 期間限定イベントの日数チェック
                if (ae.Data.Type == ParkEventType.LimitedTime ||
                    ae.Data.Type == ParkEventType.Anniversary)
                {
                    int elapsedDays = CalculateElapsedDays(ae, tm);
                    if (elapsedDays >= ae.Data.DurationDays)
                    {
                        EndEvent(i);
                    }
                }
            }
        }

        private int CalculateElapsedDays(ActiveEvent ae, TimeManager tm)
        {
            int days = 0;

            // 年の差分
            days += (tm.CurrentYear - ae.StartYear) * 360; // 12月 * 30日
            // 月の差分
            days += (tm.CurrentMonth - ae.StartMonth) * 30;
            // 日の差分
            days += (tm.CurrentDay - ae.StartDay);

            return Mathf.Max(0, days);
        }

        private void EndEvent(int index)
        {
            var ae = _activeEvents[index];
            ae.State = ParkEventState.Ended;

            string historyEntry = $"[Y{ae.StartYear}M{ae.StartMonth}] {ae.Data.DisplayName} (参加者:{ae.TotalAttendees})";
            _eventHistory.Add(historyEntry);

            GameEvents.FireParkEventEnded(ae.Data.Id, ae.Data.DisplayName);

            // 通知
            if (NotificationSystem.Instance != null)
            {
                NotificationSystem.Instance.Notify(
                    $"イベント終了: {ae.Data.DisplayName}", NotifLevel.Info);
            }

            WebGLOptimizer.LogVerbose($"[ParkEventSystem] イベント終了: {ae.Data.DisplayName}");

            _activeEvents.RemoveAt(index);
        }

        private void EndEventById(string eventId)
        {
            for (int i = _activeEvents.Count - 1; i >= 0; i--)
            {
                if (_activeEvents[i].Data.Id == eventId)
                {
                    EndEvent(i);
                    return;
                }
            }
        }

        // ================================================================
        // ボーナス計算
        // ================================================================

        /// <summary>全アクティブイベントのボーナスを集計する</summary>
        private void RecalculateBonuses()
        {
            float spawnMul = 1f;
            float revMul = 1f;
            float happyBonus = 0f;
            float satBonus = 0f;

            var tm = GameManager.Instance?.TimeManager;
            float currentHour = tm != null ? tm.CurrentHour : 12f;

            foreach (var ae in _activeEvents)
            {
                var d = ae.Data;

                // 時間帯チェック（ショー・季節イベントは時間帯制限あり）
                bool inTimeWindow = currentHour >= d.StartHour && currentHour < d.EndHour;

                if (d.Type == ParkEventType.SpecialShow && !inTimeWindow)
                    continue;

                // 季節イベントの時間帯ボーナスは、時間外でも半減で適用
                float timeFactor = inTimeWindow ? 1f : 0.5f;
                if (d.Type == ParkEventType.Seasonal)
                {
                    timeFactor = inTimeWindow ? 1f : 0.3f;
                }

                spawnMul *= Mathf.Lerp(1f, d.SpawnRateMultiplier, timeFactor);
                revMul *= Mathf.Lerp(1f, d.RevenueMultiplier, timeFactor);
                happyBonus += d.HappinessBonus * timeFactor;
                satBonus += d.SatisfactionBonus * timeFactor;
            }

            CurrentSpawnMultiplier = spawnMul;
            CurrentRevenueMultiplier = revMul;
            CurrentHappinessBonus = happyBonus;
            CurrentSatisfactionBonus = satBonus;
        }

        // ================================================================
        // TimeManager イベントハンドラ
        // ================================================================

        private void OnDayChanged()
        {
            // 来場者参加数を更新（移動中・搭乗中の来場者数をカウント）
            foreach (var ae in _activeEvents)
            {
                var vm = GameManager.Instance?.VisitorManager;
                if (vm != null)
                {
                    ae.TotalAttendees += vm.ActiveVisitorCount;
                }
            }
        }

        private void OnMonthChanged()
        {
            var tm = GameManager.Instance?.TimeManager;
            if (tm == null) return;

            int month = tm.CurrentMonth;

            // 周年チェック（1月に発動）
            if (month == 1 && tm.CurrentYear > 1)
            {
                OnYearChanged(tm.CurrentYear);
            }

            _lastMonth = month;
        }

        // ================================================================
        // ユーティリティ
        // ================================================================

        private ActiveEvent CreateActiveEvent(ParkEventData data)
        {
            var tm = GameManager.Instance.TimeManager;
            return new ActiveEvent
            {
                Data = data,
                State = ParkEventState.Active,
                ElapsedTime = 0f,
                StartDay = tm.CurrentDay,
                StartMonth = tm.CurrentMonth,
                StartYear = tm.CurrentYear,
                RepeatCount = 0,
                TotalAttendees = 0
            };
        }

        private bool IsEventActive(string eventId)
        {
            for (int i = 0; i < _activeEvents.Count; i++)
            {
                if (_activeEvents[i].Data.Id == eventId &&
                    _activeEvents[i].State == ParkEventState.Active)
                    return true;
            }
            return false;
        }

        private void NotifyEventStart(ParkEventData data)
        {
            if (NotificationSystem.Instance != null)
            {
                string prefix = data.Type switch
                {
                    ParkEventType.Seasonal => "季節イベント",
                    ParkEventType.SpecialShow => "ショー開演",
                    ParkEventType.LimitedTime => "期間限定",
                    ParkEventType.Anniversary => "周年記念",
                    _ => "イベント"
                };
                NotificationSystem.Instance.Notify(
                    $"{prefix}: {data.DisplayName}", NotifLevel.Success);
            }
        }

        // ================================================================
        // 外部API
        // ================================================================

        /// <summary>現在時間帯で最も効果の高いイベント名を取得する</summary>
        public string GetTopEventName()
        {
            if (_activeEvents.Count == 0) return null;

            ActiveEvent best = null;
            float bestScore = float.MinValue;

            foreach (var ae in _activeEvents)
            {
                float score = ae.Data.HappinessBonus + ae.Data.SatisfactionBonus +
                              ae.Data.RatingBonus;
                if (score > bestScore)
                {
                    bestScore = score;
                    best = ae;
                }
            }

            return best?.Data.DisplayName;
        }

        /// <summary>指定IDのイベントデータを取得する</summary>
        public ParkEventData GetEventData(string eventId)
        {
            foreach (var data in _eventDatabase)
            {
                if (data.Id == eventId) return data;
            }
            return null;
        }

        /// <summary>現在のイベント情報をHUD表示用の文字列で返す</summary>
        public string GetActiveEventsSummary()
        {
            if (_activeEvents.Count == 0) return "イベントなし";

            var sb = new System.Text.StringBuilder();
            for (int i = 0; i < _activeEvents.Count; i++)
            {
                if (i > 0) sb.Append(" | ");
                var ae = _activeEvents[i];
                string typeIcon = ae.Data.Type switch
                {
                    ParkEventType.Seasonal => "[季]",
                    ParkEventType.SpecialShow => "[演]",
                    ParkEventType.LimitedTime => "[限]",
                    ParkEventType.Anniversary => "[記]",
                    _ => ""
                };
                sb.Append($"{typeIcon}{ae.Data.DisplayName}");
            }
            return sb.ToString();
        }

        /// <summary>特定のVisitorTypeに対する集客ボーナスがあるか</summary>
        public bool HasAttractionBonusFor(VisitorType type)
        {
            foreach (var ae in _activeEvents)
            {
                if (ae.Data.PreferredVisitorType == type)
                    return true;
            }
            return false;
        }
    }
}
