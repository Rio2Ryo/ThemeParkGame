// ============================================================
// ThemeParkGame - TimeManager
// ゲーム内時間管理（日付、開園/閉園、年度）
// ============================================================

using System;
using UnityEngine;

namespace ThemeParkGame.Core
{
    /// <summary>
    /// ゲーム内時間を管理する。1ゲーム年 = リアル約20分を想定。
    /// </summary>
    public class TimeManager : MonoBehaviour
    {
        [Header("Time Settings")]
        [SerializeField] private float secondsPerGameHour = 5f; // リアル5秒=ゲーム内1時間

        public int CurrentYear { get; private set; } = 1;
        public int CurrentMonth { get; private set; } = 1;
        public int CurrentDay { get; private set; } = 1;
        public float CurrentHour { get; private set; } = 8f; // 8:00 AM start
        public bool IsParkOpen { get; private set; }

        public float OpenHour => 8f;
        public float CloseHour => 22f;

        public event Action OnDayChanged;
        public event Action OnMonthChanged;
        public event Action<int> OnYearChanged;
        public event Action OnParkOpenTimeReached;
        public event Action OnParkCloseTimeReached;

        private float _hourAccumulator;

        public void Initialize()
        {
            CurrentYear = 1;
            CurrentMonth = 1;
            CurrentDay = 1;
            CurrentHour = OpenHour;
            IsParkOpen = true;
            _hourAccumulator = 0f;
        }

        /// <summary>セーブデータから時間を復元する</summary>
        public void RestoreTime(int year, int month, int day, float hour)
        {
            CurrentYear = Mathf.Max(1, year);
            CurrentMonth = Mathf.Clamp(month, 1, 12);
            CurrentDay = Mathf.Clamp(day, 1, 30);
            CurrentHour = Mathf.Clamp(hour, 0f, 23.99f);
            IsParkOpen = CurrentHour >= OpenHour && CurrentHour < CloseHour;
            _hourAccumulator = 0f;
        }

        private void Update()
        {
            if (GameManager.Instance == null || GameManager.Instance.IsPaused) return;

            _hourAccumulator += Time.deltaTime;

            if (_hourAccumulator >= secondsPerGameHour)
            {
                _hourAccumulator -= secondsPerGameHour;
                AdvanceHour();
            }
        }

        private void AdvanceHour()
        {
            CurrentHour += 1f;

            // 開園チェック
            if (!IsParkOpen && CurrentHour >= OpenHour && CurrentHour < CloseHour)
            {
                IsParkOpen = true;
                OnParkOpenTimeReached?.Invoke();
                GameEvents.FireParkOpened();
            }

            // 閉園チェック
            if (IsParkOpen && CurrentHour >= CloseHour)
            {
                IsParkOpen = false;
                OnParkCloseTimeReached?.Invoke();
                GameEvents.FireParkClosed();
            }

            // 日付更新
            if (CurrentHour >= 24f)
            {
                CurrentHour = 0f;
                AdvanceDay();
            }
        }

        private void AdvanceDay()
        {
            CurrentDay++;
            OnDayChanged?.Invoke();

            if (CurrentDay > 30) // 簡略化: 全月30日
            {
                CurrentDay = 1;
                AdvanceMonth();
            }
        }

        private void AdvanceMonth()
        {
            CurrentMonth++;
            OnMonthChanged?.Invoke();

            if (CurrentMonth > 12)
            {
                CurrentMonth = 1;
                AdvanceYear();
            }
        }

        private void AdvanceYear()
        {
            CurrentYear++;
            OnYearChanged?.Invoke(CurrentYear);
            GameEvents.FireParkYearPassed(CurrentYear);
        }

        /// <summary>現在時刻を文字列で返す (例: "Year 1 - Apr 15 14:00")</summary>
        public string GetFormattedDateTime()
        {
            string[] months = { "Jan", "Feb", "Mar", "Apr", "May", "Jun",
                               "Jul", "Aug", "Sep", "Oct", "Nov", "Dec" };
            string monthStr = months[Mathf.Clamp(CurrentMonth - 1, 0, 11)];
            int hour = Mathf.FloorToInt(CurrentHour);
            return $"Year {CurrentYear} - {monthStr} {CurrentDay} {hour:00}:00";
        }

        /// <summary>現在の時間帯を取得</summary>
        public DayPeriod GetCurrentPeriod()
        {
            if (CurrentHour < 6f) return DayPeriod.Night;
            if (CurrentHour < 12f) return DayPeriod.Morning;
            if (CurrentHour < 18f) return DayPeriod.Afternoon;
            if (CurrentHour < 22f) return DayPeriod.Evening;
            return DayPeriod.Night;
        }
    }

    public enum DayPeriod
    {
        Morning,
        Afternoon,
        Evening,
        Night
    }
}
