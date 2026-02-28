// ============================================================
// ThemeParkGame - WeatherSystem
// 天候管理システム（ランダム変化・季節パターン・予報）
// ============================================================

using System;
using System.Collections.Generic;
using UnityEngine;
using ThemeParkGame.Core;

namespace ThemeParkGame.Park
{
    /// <summary>
    /// 季節を定義する。天候の確率テーブルに使用。
    /// </summary>
    public enum Season
    {
        Spring, // 3～5月
        Summer, // 6～8月
        Autumn, // 9～11月
        Winter  // 12～2月
    }

    /// <summary>
    /// 天候が各パラメータに与える効果。
    ///
    /// 【ゲームデザイン: 天候の影響】
    /// - Sunny (晴れ): デフォルト。特別な効果なし。来場者数は標準。
    /// - Hot (猛暑): 喉の渇きが2倍速で増加。ドリンクショップの売上UP。
    ///   長時間屋外にいると来場者の快適度が低下。
    /// - Rainy (雨): 来場者数が30%減少。屋内施設の人気UP。
    ///   来場者の幸福度が徐々に低下。
    /// - Cloudy (曇り): 軽微な来場者数減少（10%）。他は標準。
    /// - Snowy (雪): 来場者数が50%減少。メンテナンスコスト増加。
    ///   一部アトラクションが運休。ただし「雪景色ボーナス」で評価UP。
    /// </summary>
    [Serializable]
    public class WeatherEffect
    {
        /// <summary>対象天候</summary>
        public Weather WeatherType;

        /// <summary>来場者数への乗数（1.0 = 変化なし）</summary>
        public float VisitorCountMultiplier;

        /// <summary>喉の渇き増加速度への乗数</summary>
        public float ThirstRateMultiplier;

        /// <summary>来場者幸福度への増減値（毎時）</summary>
        public float HappinessModifierPerHour;

        /// <summary>メンテナンスコストへの乗数</summary>
        public float MaintenanceCostMultiplier;

        /// <summary>パーク評価への一時ボーナス</summary>
        public float ParkRatingBonus;
    }

    /// <summary>
    /// 天候予報データ。数時間先の天候を予測する。
    /// </summary>
    [Serializable]
    public class WeatherForecast
    {
        /// <summary>予報対象時刻（ゲーム内時間）</summary>
        public float Hour;

        /// <summary>予報天候</summary>
        public Weather PredictedWeather;

        /// <summary>予報の確信度（0～1）。先の時間ほど低くなる。</summary>
        public float Confidence;
    }

    /// <summary>
    /// 天候管理システム。ランダムな天候変化と季節パターンを管理する。
    /// パーク経営に影響を与える環境要因として機能する。
    /// </summary>
    public class WeatherSystem : MonoBehaviour
    {
        [Header("Weather Settings")]
        [SerializeField] private float weatherChangeIntervalHours = 4f;  // 天候変化の間隔（ゲーム内時間）
        [SerializeField] private int forecastHours = 12;                 // 予報の先読み時間数

        /// <summary>現在の天候</summary>
        public Weather CurrentWeather { get; private set; } = Weather.Sunny;

        /// <summary>現在の季節</summary>
        public Season CurrentSeason { get; private set; } = Season.Spring;

        /// <summary>天候効果テーブル</summary>
        private readonly Dictionary<Weather, WeatherEffect> _weatherEffects
            = new Dictionary<Weather, WeatherEffect>();

        /// <summary>季節別の天候確率テーブル [季節][天候] → 確率(0～1)</summary>
        private readonly Dictionary<Season, Dictionary<Weather, float>> _seasonalProbabilities
            = new Dictionary<Season, Dictionary<Weather, float>>();

        /// <summary>天候予報リスト</summary>
        private readonly List<WeatherForecast> _forecasts = new List<WeatherForecast>();

        /// <summary>次の天候変化までの残り時間（ゲーム内時間）</summary>
        private float _hoursUntilWeatherChange;

        /// <summary>TimeManagerの前回チェック時刻</summary>
        private float _lastCheckedHour;

        // ================================================================
        // 初期化
        // ================================================================

        /// <summary>天候システムを初期化する</summary>
        public void Initialize()
        {
            InitializeWeatherEffects();
            InitializeSeasonalProbabilities();

            CurrentWeather = Weather.Sunny;
            _hoursUntilWeatherChange = weatherChangeIntervalHours;
            _lastCheckedHour = -1f;

            UpdateSeason();
            GenerateForecasts();

            WebGLOptimizer.LogVerbose("[WeatherSystem] 初期化完了");
        }

        private void InitializeWeatherEffects()
        {
            _weatherEffects.Clear();

            _weatherEffects[Weather.Sunny] = new WeatherEffect
            {
                WeatherType = Weather.Sunny,
                VisitorCountMultiplier = 1.0f,
                ThirstRateMultiplier = 1.0f,
                HappinessModifierPerHour = 0f,
                MaintenanceCostMultiplier = 1.0f,
                ParkRatingBonus = 0f
            };

            _weatherEffects[Weather.Cloudy] = new WeatherEffect
            {
                WeatherType = Weather.Cloudy,
                VisitorCountMultiplier = 0.9f,
                ThirstRateMultiplier = 0.8f,
                HappinessModifierPerHour = -0.5f,
                MaintenanceCostMultiplier = 1.0f,
                ParkRatingBonus = 0f
            };

            _weatherEffects[Weather.Rainy] = new WeatherEffect
            {
                WeatherType = Weather.Rainy,
                VisitorCountMultiplier = 0.7f,   // 来場者30%減
                ThirstRateMultiplier = 0.5f,
                HappinessModifierPerHour = -2.0f, // 幸福度低下
                MaintenanceCostMultiplier = 1.2f,
                ParkRatingBonus = -5f
            };

            _weatherEffects[Weather.Hot] = new WeatherEffect
            {
                WeatherType = Weather.Hot,
                VisitorCountMultiplier = 0.95f,
                ThirstRateMultiplier = 2.0f,     // 喉の渇き2倍速
                HappinessModifierPerHour = -1.0f,
                MaintenanceCostMultiplier = 1.1f,
                ParkRatingBonus = 0f
            };

            _weatherEffects[Weather.Snowy] = new WeatherEffect
            {
                WeatherType = Weather.Snowy,
                VisitorCountMultiplier = 0.5f,    // 来場者50%減
                ThirstRateMultiplier = 0.3f,
                HappinessModifierPerHour = -1.5f,
                MaintenanceCostMultiplier = 1.5f,  // メンテナンスコスト増
                ParkRatingBonus = 3f               // 雪景色ボーナス
            };

            _weatherEffects[Weather.Typhoon] = new WeatherEffect
            {
                WeatherType = Weather.Typhoon,
                VisitorCountMultiplier = 0.1f,     // 来場者90%減（ほぼ来場停止）
                ThirstRateMultiplier = 0.2f,
                HappinessModifierPerHour = -5.0f,  // 幸福度大幅低下
                MaintenanceCostMultiplier = 2.5f,   // 維持費2.5倍
                ParkRatingBonus = -10f              // 評価大幅ダウン
            };

            _weatherEffects[Weather.Thunderstorm] = new WeatherEffect
            {
                WeatherType = Weather.Thunderstorm,
                VisitorCountMultiplier = 0.3f,     // 来場者70%減
                ThirstRateMultiplier = 0.4f,
                HappinessModifierPerHour = -3.0f,  // 幸福度低下
                MaintenanceCostMultiplier = 2.0f,   // 維持費2倍
                ParkRatingBonus = -5f               // 評価ダウン
            };
        }

        /// <summary>
        /// 季節ごとの天候出現確率を定義する。
        /// 各季節の全天候の確率合計は1.0になる。
        /// </summary>
        private void InitializeSeasonalProbabilities()
        {
            _seasonalProbabilities.Clear();

            // 春: 晴れ多め、たまに曇り・雨。台風稀、雷雨少し
            _seasonalProbabilities[Season.Spring] = new Dictionary<Weather, float>
            {
                { Weather.Sunny, 0.40f },
                { Weather.Cloudy, 0.23f },
                { Weather.Rainy, 0.18f },
                { Weather.Hot, 0.05f },
                { Weather.Snowy, 0.05f },
                { Weather.Typhoon, 0.02f },
                { Weather.Thunderstorm, 0.07f }
            };

            // 夏: 晴れと猛暑が多い。台風・雷雨が発生しやすい
            _seasonalProbabilities[Season.Summer] = new Dictionary<Weather, float>
            {
                { Weather.Sunny, 0.25f },
                { Weather.Cloudy, 0.08f },
                { Weather.Rainy, 0.12f },
                { Weather.Hot, 0.40f },
                { Weather.Snowy, 0.00f },
                { Weather.Typhoon, 0.05f },
                { Weather.Thunderstorm, 0.10f }
            };

            // 秋: 曇りと雨が増える。台風シーズン
            _seasonalProbabilities[Season.Autumn] = new Dictionary<Weather, float>
            {
                { Weather.Sunny, 0.23f },
                { Weather.Cloudy, 0.25f },
                { Weather.Rainy, 0.25f },
                { Weather.Hot, 0.05f },
                { Weather.Snowy, 0.02f },
                { Weather.Typhoon, 0.10f },
                { Weather.Thunderstorm, 0.10f }
            };

            // 冬: 雪と曇りが多い。台風なし、雷雨稀
            _seasonalProbabilities[Season.Winter] = new Dictionary<Weather, float>
            {
                { Weather.Sunny, 0.15f },
                { Weather.Cloudy, 0.28f },
                { Weather.Rainy, 0.15f },
                { Weather.Hot, 0.00f },
                { Weather.Snowy, 0.40f },
                { Weather.Typhoon, 0.00f },
                { Weather.Thunderstorm, 0.02f }
            };
        }

        // ================================================================
        // 更新ループ
        // ================================================================

        private void Update()
        {
            if (GameManager.Instance == null || GameManager.Instance.IsPaused) return;
            if (GameManager.Instance.TimeManager == null) return;

            float currentHour = GameManager.Instance.TimeManager.CurrentHour;

            // 時刻が進んだかチェック（日付の巻き戻しも考慮）
            if (_lastCheckedHour < 0f)
            {
                _lastCheckedHour = currentHour;
                return;
            }

            float elapsedHours = currentHour - _lastCheckedHour;
            if (elapsedHours < 0f)
            {
                // 日付が変わった（24:00 → 0:00）
                elapsedHours = (24f - _lastCheckedHour) + currentHour;
            }

            _lastCheckedHour = currentHour;

            if (elapsedHours <= 0f) return;

            _hoursUntilWeatherChange -= elapsedHours;

            if (_hoursUntilWeatherChange <= 0f)
            {
                ChangeWeather();
                _hoursUntilWeatherChange = weatherChangeIntervalHours;
            }

            // 月が変わったら季節を更新
            UpdateSeason();
        }

        // ================================================================
        // 天候変化
        // ================================================================

        /// <summary>天候をランダムに変更する</summary>
        private void ChangeWeather()
        {
            Weather previousWeather = CurrentWeather;
            CurrentWeather = RollWeather(CurrentSeason);

            if (CurrentWeather != previousWeather)
            {
                GameEvents.FireWeatherChanged(CurrentWeather);
                WebGLOptimizer.LogVerbose($"[WeatherSystem] 天候変化: {previousWeather} → {CurrentWeather}");
            }

            // 予報を再生成
            GenerateForecasts();
        }

        /// <summary>
        /// 季節の確率テーブルに基づいて天候を抽選する。
        /// </summary>
        private Weather RollWeather(Season season)
        {
            if (!_seasonalProbabilities.TryGetValue(season, out Dictionary<Weather, float> probabilities))
            {
                return Weather.Sunny;
            }

            float roll = UnityEngine.Random.Range(0f, 1f);
            float cumulative = 0f;

            foreach (var kvp in probabilities)
            {
                cumulative += kvp.Value;
                if (roll <= cumulative)
                {
                    return kvp.Key;
                }
            }

            return Weather.Sunny;
        }

        /// <summary>現在の月から季節を更新する</summary>
        private void UpdateSeason()
        {
            if (GameManager.Instance == null || GameManager.Instance.TimeManager == null) return;

            int month = GameManager.Instance.TimeManager.CurrentMonth;
            Season newSeason = GetSeasonFromMonth(month);

            if (newSeason != CurrentSeason)
            {
                CurrentSeason = newSeason;
                WebGLOptimizer.LogVerbose($"[WeatherSystem] 季節変化: {CurrentSeason}");
            }
        }

        /// <summary>月から季節を判定する</summary>
        private Season GetSeasonFromMonth(int month)
        {
            switch (month)
            {
                case 3: case 4: case 5:
                    return Season.Spring;
                case 6: case 7: case 8:
                    return Season.Summer;
                case 9: case 10: case 11:
                    return Season.Autumn;
                default: // 12, 1, 2
                    return Season.Winter;
            }
        }

        // ================================================================
        // 天候予報
        // ================================================================

        /// <summary>
        /// 数時間先の天候予報を生成する。
        /// 近い未来ほど高精度、遠い未来ほど不確実。
        /// </summary>
        private void GenerateForecasts()
        {
            _forecasts.Clear();

            float currentHour = 0f;
            if (GameManager.Instance != null && GameManager.Instance.TimeManager != null)
            {
                currentHour = GameManager.Instance.TimeManager.CurrentHour;
            }

            Weather lastWeather = CurrentWeather;

            for (int i = 1; i <= forecastHours / (int)weatherChangeIntervalHours + 1; i++)
            {
                float forecastHour = currentHour + i * weatherChangeIntervalHours;
                // 24時間制に正規化
                while (forecastHour >= 24f) forecastHour -= 24f;

                // 時間が離れるほど確信度が低下
                float confidence = Mathf.Clamp01(1.0f - (i - 1) * 0.2f);

                // 確信度が高ければ現在天候を維持、低ければランダム
                Weather predictedWeather;
                if (UnityEngine.Random.Range(0f, 1f) < confidence * 0.6f)
                {
                    // 天候が持続する確率
                    predictedWeather = lastWeather;
                }
                else
                {
                    predictedWeather = RollWeather(CurrentSeason);
                }

                _forecasts.Add(new WeatherForecast
                {
                    Hour = forecastHour,
                    PredictedWeather = predictedWeather,
                    Confidence = confidence
                });

                lastWeather = predictedWeather;
            }
        }

        // ================================================================
        // 天候効果参照
        // ================================================================

        /// <summary>現在の天候の効果データを取得する</summary>
        public WeatherEffect GetCurrentWeatherEffect()
        {
            if (_weatherEffects.TryGetValue(CurrentWeather, out WeatherEffect effect))
            {
                return effect;
            }
            return _weatherEffects[Weather.Sunny];
        }

        /// <summary>指定天候の効果データを取得する</summary>
        public WeatherEffect GetWeatherEffect(Weather weather)
        {
            if (_weatherEffects.TryGetValue(weather, out WeatherEffect effect))
            {
                return effect;
            }
            return _weatherEffects[Weather.Sunny];
        }

        /// <summary>天候予報を取得する</summary>
        public IReadOnlyList<WeatherForecast> GetForecast()
        {
            return _forecasts.AsReadOnly();
        }

        /// <summary>現在の来場者数乗数を取得する</summary>
        public float GetVisitorCountMultiplier()
        {
            return GetCurrentWeatherEffect().VisitorCountMultiplier;
        }

        /// <summary>現在の喉の渇き速度乗数を取得する</summary>
        public float GetThirstRateMultiplier()
        {
            return GetCurrentWeatherEffect().ThirstRateMultiplier;
        }

        /// <summary>現在の幸福度変化値を取得する（毎時）</summary>
        public float GetHappinessModifierPerHour()
        {
            return GetCurrentWeatherEffect().HappinessModifierPerHour;
        }

        /// <summary>現在のメンテナンスコスト乗数を取得する</summary>
        public float GetMaintenanceCostMultiplier()
        {
            return GetCurrentWeatherEffect().MaintenanceCostMultiplier;
        }

        /// <summary>
        /// 天候を強制的に変更する（デバッグ/イベント用）。
        /// </summary>
        public void ForceWeather(Weather weather)
        {
            Weather previous = CurrentWeather;
            CurrentWeather = weather;
            if (CurrentWeather != previous)
            {
                GameEvents.FireWeatherChanged(CurrentWeather);
                GenerateForecasts();
                WebGLOptimizer.LogVerbose($"[WeatherSystem] 天候を強制変更: {previous} → {CurrentWeather}");
            }
        }

        /// <summary>
        /// 天候の日本語表示名を取得する。
        /// </summary>
        public static string GetWeatherDisplayName(Weather weather)
        {
            switch (weather)
            {
                case Weather.Sunny: return "晴れ";
                case Weather.Cloudy: return "曇り";
                case Weather.Rainy: return "雨";
                case Weather.Snowy: return "雪";
                case Weather.Hot: return "猛暑";
                case Weather.Typhoon: return "台風";
                case Weather.Thunderstorm: return "雷雨";
                default: return "不明";
            }
        }

        /// <summary>
        /// 季節の日本語表示名を取得する。
        /// </summary>
        public static string GetSeasonDisplayName(Season season)
        {
            switch (season)
            {
                case Season.Spring: return "春";
                case Season.Summer: return "夏";
                case Season.Autumn: return "秋";
                case Season.Winter: return "冬";
                default: return "不明";
            }
        }
    }
}
