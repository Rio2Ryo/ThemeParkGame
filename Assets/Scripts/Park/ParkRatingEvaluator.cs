// ============================================================
// ThemeParkGame - ParkRatingEvaluator
// パーク評価を定期的に計算し、星レーティング・イベント通知を行う
// ============================================================

using UnityEngine;
using ThemeParkGame.Core;

namespace ThemeParkGame.Park
{
    /// <summary>
    /// ParkRatingの5カテゴリ評価を定期的に駆動するエンジン。
    /// 各サブシステムからデータを収集し、ParkManager.RecalculateAllRatingsに渡す。
    /// 評価変動イベントの発火、星レーティング変換、レーティング履歴を管理する。
    ///
    /// 【評価タイミング】
    /// - 短期更新: EVAL_INTERVAL秒ごとに全カテゴリを再計算
    /// - 月末処理: TimeManager.OnMonthChangedで認定証チェック
    /// - 年末処理: TimeManager.OnYearChangedで年度集計
    /// </summary>
    public class ParkRatingEvaluator : MonoBehaviour
    {
        // ---- 定数 ----
        private const float EVAL_INTERVAL = 5f; // 評価更新間隔（秒）
        private const float SIGNIFICANT_CHANGE = 2f; // イベント発火する最小変動量

        // ---- 状態 ----
        private float _evalTimer;
        private float _lastOverallRating;
        private int _lastStarRating;
        private bool _initialized;

        // ---- 履歴（直近12ヶ月分） ----
        private float[] _monthlyHistory = new float[12];
        private int _historyIndex;

        // ---- プロパティ ----

        /// <summary>現在の総合評価（0-100）</summary>
        public float OverallRating
        {
            get
            {
                var gm = GameManager.Instance;
                return gm != null && gm.ParkManager != null ? gm.ParkManager.Rating.OverallRating : 0f;
            }
        }

        /// <summary>星レーティング（0-5、0.5刻み）</summary>
        public float StarRating => ScoreToStars(OverallRating);

        /// <summary>星レーティング（整数部分、0-5）</summary>
        public int StarRatingInt => Mathf.FloorToInt(StarRating);

        /// <summary>直近12ヶ月の評価履歴</summary>
        public float[] MonthlyHistory => _monthlyHistory;

        /// <summary>評価トレンド（正:上昇、負:下降）</summary>
        public float Trend
        {
            get
            {
                int prev = (_historyIndex + 11) % 12;
                int prevPrev = (_historyIndex + 10) % 12;
                if (_monthlyHistory[prev] <= 0f && _monthlyHistory[prevPrev] <= 0f) return 0f;
                return _monthlyHistory[prev] - _monthlyHistory[prevPrev];
            }
        }

        // ---- 初期化 ----

        private void OnEnable()
        {
            if (GameManager.Instance != null && GameManager.Instance.TimeManager != null)
            {
                GameManager.Instance.TimeManager.OnMonthChanged += OnMonthEnd;
                GameManager.Instance.TimeManager.OnYearChanged += OnYearEnd;
            }
            GameEvents.OnParkOpened += OnParkOpened;
        }

        private void OnDisable()
        {
            if (GameManager.Instance != null && GameManager.Instance.TimeManager != null)
            {
                GameManager.Instance.TimeManager.OnMonthChanged -= OnMonthEnd;
                GameManager.Instance.TimeManager.OnYearChanged -= OnYearEnd;
            }
            GameEvents.OnParkOpened -= OnParkOpened;
        }

        private void OnParkOpened()
        {
            _initialized = true;
            _evalTimer = 0f;
            _lastOverallRating = 0f;
            _lastStarRating = 0;
            _monthlyHistory = new float[12];
            _historyIndex = 0;

            // 初回評価
            EvaluateAll();
        }

        // ---- Update ----

        private void Update()
        {
            if (!_initialized) return;
            var gm = GameManager.Instance;
            if (gm == null || gm.CurrentState != GameState.Playing) return;

            _evalTimer -= Time.deltaTime;
            if (_evalTimer > 0f) return;
            _evalTimer = EVAL_INTERVAL;

            EvaluateAll();
        }

        // ---- 評価実行 ----

        /// <summary>全カテゴリの評価を再計算する</summary>
        private void EvaluateAll()
        {
            var gm = GameManager.Instance;
            if (gm == null || gm.ParkManager == null) return;

            // データ収集
            float mechanicCoverage = CalculateStaffCoverage(StaffType.Mechanic, 3);
            float entertainerCoverage = CalculateStaffCoverage(StaffType.Entertainer, 10);
            float cleanliness = CalculateCleanliness();
            float toiletCoverage = CalculateFacilityCoverage(FacilityType.Toilet, 10);
            float benchCoverage = CalculateFacilityCoverage(FacilityType.Bench, 8);
            float foodDrinkAvail = CalculateFoodDrinkAvailability();
            int uniqueCategories = CalculateUniqueAttractionCategories();
            float avgQuality = CalculateAverageAttractionQuality();
            int recentAccidents = GetRecentAccidentCount();

            // ParkManagerに渡して再計算
            gm.ParkManager.RecalculateAllRatings(
                mechanicCoverage,
                toiletCoverage,
                benchCoverage,
                foodDrinkAvail,
                uniqueCategories,
                avgQuality,
                entertainerCoverage,
                recentAccidents);

            float newRating = gm.ParkManager.Rating.OverallRating;

            // 大きな変動があればイベント発火
            if (Mathf.Abs(newRating - _lastOverallRating) >= SIGNIFICANT_CHANGE)
            {
                GameEvents.FireParkRatingChanged(newRating, _lastOverallRating);
            }

            // 星レーティングが変化したら追加イベント
            int newStars = Mathf.FloorToInt(ScoreToStars(newRating));
            if (newStars != _lastStarRating && _lastStarRating > 0)
            {
                bool improved = newStars > _lastStarRating;
                Debug.Log($"[ParkRatingEvaluator] Star rating: {_lastStarRating} -> {newStars} " +
                          $"({(improved ? "UP" : "DOWN")})");
            }

            _lastOverallRating = newRating;
            _lastStarRating = newStars;

            // パーク統計も更新
            int visitors = gm.VisitorManager != null ? gm.VisitorManager.ActiveVisitorCount : 0;
            float avgHappy = gm.VisitorManager != null ? gm.VisitorManager.AverageHappiness / 100f : 0f;
            int staffCount = gm.StaffManager != null ? gm.StaffManager.TotalStaffCount : 0;
            gm.ParkManager.UpdateStats(visitors, avgHappy, cleanliness, staffCount);
        }

        // ---- イベントハンドラ ----

        private void OnMonthEnd()
        {
            // 月末時点の評価を履歴に記録
            _monthlyHistory[_historyIndex] = OverallRating;
            _historyIndex = (_historyIndex + 1) % 12;
        }

        private void OnYearEnd(int newYear)
        {
            var gm = GameManager.Instance;
            if (gm != null && gm.ParkManager != null)
            {
                Debug.Log($"[ParkRatingEvaluator] Year {newYear} - Rating: {OverallRating:F1} " +
                          $"Stars: {StarRating:F1}");
            }
        }

        // ---- データ収集ヘルパー ----

        /// <summary>スタッフ充足率（スタッフ数 / (アトラクション数 * perAttraction)）</summary>
        private float CalculateStaffCoverage(StaffType type, int perAttraction)
        {
            var gm = GameManager.Instance;
            if (gm == null || gm.StaffManager == null) return 0f;

            int staffCount = gm.StaffManager.GetStaffCount(type);
            int attrCount = gm.ParkManager != null ? gm.ParkManager.Stats.TotalAttractions : 0;
            int needed = Mathf.Max(1, attrCount) * perAttraction > 0 ? Mathf.Max(1, attrCount / perAttraction + 1) : 1;
            return Mathf.Clamp01((float)staffCount / needed);
        }

        /// <summary>清潔度（クリーナー充足度ベース、0-1）</summary>
        private float CalculateCleanliness()
        {
            var gm = GameManager.Instance;
            if (gm == null || gm.StaffManager == null || gm.VisitorManager == null) return 0.5f;

            int cleaners = gm.StaffManager.GetStaffCount(StaffType.Cleaner);
            int visitors = gm.VisitorManager.ActiveVisitorCount;
            if (visitors <= 0) return 1f;

            // クリーナー1人で来場者10人分の清掃能力
            float coverage = (float)cleaners * 10f / visitors;
            return Mathf.Clamp01(coverage);
        }

        /// <summary>施設充足率（ParkManagerのグリッドベース）</summary>
        private float CalculateFacilityCoverage(FacilityType type, int capacityPerFacility)
        {
            var gm = GameManager.Instance;
            if (gm == null || gm.ParkManager == null) return 0f;
            return gm.ParkManager.CalculateFacilityCoverage(type, capacityPerFacility);
        }

        /// <summary>飲食施設充足率（FoodShop + DrinkShopの合算）</summary>
        private float CalculateFoodDrinkAvailability()
        {
            float food = CalculateFacilityCoverage(FacilityType.FoodShop, 12);
            float drink = CalculateFacilityCoverage(FacilityType.DrinkShop, 12);
            return (food + drink) * 0.5f;
        }

        /// <summary>異なるアトラクションカテゴリ数</summary>
        private int CalculateUniqueAttractionCategories()
        {
            var gm = GameManager.Instance;
            if (gm == null || gm.AttractionManager == null) return 0;
            return gm.AttractionManager.GetUniqueCategories();
        }

        /// <summary>アトラクション平均品質（0-1）</summary>
        private float CalculateAverageAttractionQuality()
        {
            var gm = GameManager.Instance;
            if (gm == null || gm.AttractionManager == null) return 0f;
            return gm.AttractionManager.GetAverageQuality();
        }

        /// <summary>直近1年の事故件数</summary>
        private int GetRecentAccidentCount()
        {
            var gm = GameManager.Instance;
            if (gm == null || gm.ParkManager == null) return 0;

            int year = gm.TimeManager != null ? gm.TimeManager.CurrentYear : 1;
            return gm.ParkManager.Rating.GetAccidentCount(year);
        }

        // ---- ユーティリティ ----

        /// <summary>スコア(0-100)を星レーティング(0-5)に変換する</summary>
        public static float ScoreToStars(float score)
        {
            // 0-100 → 0-5（非線形: 低スコアでも最低1星、高スコアはシビア）
            if (score <= 0f) return 0f;
            if (score < 10f) return 0.5f;
            if (score < 25f) return 1f;
            if (score < 40f) return 1.5f;
            if (score < 50f) return 2f;
            if (score < 60f) return 2.5f;
            if (score < 70f) return 3f;
            if (score < 78f) return 3.5f;
            if (score < 85f) return 4f;
            if (score < 93f) return 4.5f;
            return 5f;
        }

        /// <summary>星レーティングをテキスト表示する</summary>
        public static string StarsToText(float stars)
        {
            int full = Mathf.FloorToInt(stars);
            bool half = (stars - full) >= 0.4f;
            string result = "";
            for (int i = 0; i < full; i++) result += "*";
            if (half) result += "+";
            int empty = 5 - full - (half ? 1 : 0);
            for (int i = 0; i < empty; i++) result += "-";
            return result;
        }

        /// <summary>評価のラベルを返す</summary>
        public static string GetRatingLabel(float score)
        {
            if (score >= 90f) return "伝説のパーク";
            if (score >= 80f) return "素晴らしいパーク";
            if (score >= 70f) return "優良パーク";
            if (score >= 55f) return "普通のパーク";
            if (score >= 40f) return "改善が必要";
            if (score >= 20f) return "評判が悪い";
            return "崩壊寸前";
        }

        /// <summary>カテゴリ名の日本語ラベル</summary>
        public static string GetCategoryLabel(CertificateCategory cat)
        {
            switch (cat)
            {
                case CertificateCategory.Fame:       return "知名度";
                case CertificateCategory.Safety:     return "安全性";
                case CertificateCategory.Comfort:    return "快適性";
                case CertificateCategory.Excitement: return "興奮度";
                case CertificateCategory.Mood:       return "ムード";
                default: return cat.ToString();
            }
        }

        /// <summary>トレンド矢印を返す</summary>
        public static string GetTrendArrow(float trend)
        {
            if (trend > 3f) return "^^";
            if (trend > 0.5f) return "^";
            if (trend < -3f) return "vv";
            if (trend < -0.5f) return "v";
            return "-";
        }

        // ---- セーブ/ロード ----

        /// <summary>セーブ用にカテゴリスコアを配列で返す</summary>
        public float[] GetCategoryScores()
        {
            var gm = GameManager.Instance;
            if (gm == null || gm.ParkManager == null || gm.ParkManager.Rating == null)
                return new float[5];

            var r = gm.ParkManager.Rating;
            return new float[]
            {
                r.GetCategoryScore(CertificateCategory.Fame),
                r.GetCategoryScore(CertificateCategory.Safety),
                r.GetCategoryScore(CertificateCategory.Comfort),
                r.GetCategoryScore(CertificateCategory.Excitement),
                r.GetCategoryScore(CertificateCategory.Mood)
            };
        }
    }
}
