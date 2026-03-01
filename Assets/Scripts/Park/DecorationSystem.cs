// ============================================================
// ThemeParkGame - DecorationSystem
// 季節デコレーションシステム - ゾーン別の装飾管理とシーズン演出
// ============================================================

using System;
using System.Collections.Generic;
using UnityEngine;
using ThemeParkGame.Core;

namespace ThemeParkGame.Park
{
    // ================================================================
    // 列挙型
    // ================================================================

    /// <summary>
    /// 季節テーマ。基本4季節に加え、ハロウィン・クリスマスの特別シーズンを含む。
    ///
    /// 【ゲームデザイン: 季節テーマの役割】
    /// 季節に合わせた装飾を配置することで来場者のムードが上昇し、
    /// SNS投稿頻度にもボーナスが付与される。ハロウィンとクリスマスは
    /// 通常季節より大きなボーナスが期待できる特別シーズン。
    /// </summary>
    public enum SeasonalTheme
    {
        Spring,
        Summer,
        Autumn,
        Winter,
        Halloween,
        Christmas
    }

    /// <summary>
    /// 装飾レベル。投資額に応じて段階的にアップグレード可能。
    ///
    /// 【ゲームデザイン: 装飾投資の費用対効果】
    /// None → Basic: 低コストで最低限の装飾。効果は控えめ。
    /// Basic → Enhanced: 中程度の投資で目に見える効果。コスパが最も良い。
    /// Enhanced → Premium: 高額投資だがムード・SNS両方に大きなボーナス。
    /// </summary>
    public enum DecorationLevel
    {
        None,
        Basic,
        Enhanced,
        Premium
    }

    // ================================================================
    // ゾーン装飾データ
    // ================================================================

    /// <summary>
    /// 各ゾーンの装飾状態を管理するデータクラス。
    /// ゾーンごとにテーマ・レベル・各種ボーナスを保持する。
    /// </summary>
    [Serializable]
    public class ZoneDecoration
    {
        /// <summary>対象ゾーン</summary>
        public ThemeZone Zone;

        /// <summary>現在適用されている季節テーマ</summary>
        public SeasonalTheme CurrentTheme;

        /// <summary>装飾レベル</summary>
        public DecorationLevel Level;

        /// <summary>
        /// ガーデナーシナジー倍率。園芸師がゾーン内で作業するとボーナスが付与される。
        /// デフォルト1.0、園芸師作業中は1.0+GardenerSynergyBonusになる。
        /// </summary>
        public float GardenerSynergyMultiplier = 1.0f;

        /// <summary>
        /// 装飾レベルに応じたムードボーナスを返す。
        /// None=0, Basic=2, Enhanced=5, Premium=10
        /// </summary>
        public float MoodBonus
        {
            get
            {
                return Level switch
                {
                    DecorationLevel.None => 0f,
                    DecorationLevel.Basic => 2f,
                    DecorationLevel.Enhanced => 5f,
                    DecorationLevel.Premium => 10f,
                    _ => 0f
                };
            }
        }

        /// <summary>
        /// 装飾レベルに応じたSNS投稿ボーナスを返す。
        /// None=0, Basic=1, Enhanced=3, Premium=5
        /// </summary>
        public float SNSBonus
        {
            get
            {
                return Level switch
                {
                    DecorationLevel.None => 0f,
                    DecorationLevel.Basic => 1f,
                    DecorationLevel.Enhanced => 3f,
                    DecorationLevel.Premium => 5f,
                    _ => 0f
                };
            }
        }

        /// <summary>
        /// 装飾レベルに応じた投資コストを返す。
        /// Basic=500, Enhanced=1500, Premium=3000
        /// </summary>
        public int InvestmentCost
        {
            get
            {
                return Level switch
                {
                    DecorationLevel.Basic => 500,
                    DecorationLevel.Enhanced => 1500,
                    DecorationLevel.Premium => 3000,
                    _ => 0
                };
            }
        }
    }

    // ================================================================
    // デコレーションシステム本体
    // ================================================================

    /// <summary>
    /// 季節デコレーションシステム。ゾーンごとの装飾管理と季節自動検出を行う。
    ///
    /// 【ゲームデザイン: 装飾システムの全体像】
    /// ・TimeManagerの月情報から自動的に季節を判定し、テーマを切り替える
    /// ・各ゾーンに対して段階的な装飾投資が可能（None→Basic→Enhanced→Premium）
    /// ・装飾レベルに応じてムード評価とSNS投稿頻度にボーナスが付与される
    /// ・園芸師（Gardener）がゾーン内で作業するとシナジーボーナスが発生
    /// ・10月はハロウィン、12月はクリスマスの特別テーマが自動適用される
    /// </summary>
    public class DecorationSystem : MonoBehaviour
    {
        // ============================================================
        // シングルトン
        // ============================================================

        public static DecorationSystem Instance { get; private set; }

        // ============================================================
        // 定数
        // ============================================================

        /// <summary>
        /// 園芸師シナジーボーナス（30%）。
        /// 園芸師がゾーン内で作業中、そのゾーンのムードボーナスに乗算される。
        /// </summary>
        public const float GardenerSynergyBonus = 0.3f;

        /// <summary>季節チェック間隔（リアルタイム秒）</summary>
        private const float SeasonCheckInterval = 10f;

        // ============================================================
        // フィールド
        // ============================================================

        /// <summary>ゾーン別装飾データ</summary>
        [Header("Zone Decorations")]
        [Tooltip("各テーマゾーンの装飾状態。ゾーンごとにレベルとテーマを管理する。")]
        [SerializeField] private List<ZoneDecoration> zoneDecorationList = new List<ZoneDecoration>();

        /// <summary>ゾーン別装飾データの辞書（高速アクセス用）</summary>
        private readonly Dictionary<ThemeZone, ZoneDecoration> zoneDecorations
            = new Dictionary<ThemeZone, ZoneDecoration>();

        /// <summary>現在の季節テーマ</summary>
        [Header("Season Info")]
        [Tooltip("TimeManagerの月情報から自動検出される現在の季節テーマ。")]
        [SerializeField] private SeasonalTheme currentSeason = SeasonalTheme.Spring;

        /// <summary>季節チェックタイマー（リアルタイム）</summary>
        private float seasonCheckTimer;

        /// <summary>装飾投資の累計支出額</summary>
        [Header("Statistics")]
        [Tooltip("装飾アップグレードに費やした累計金額。")]
        [SerializeField] private int totalDecorationInvestment;

        // ============================================================
        // プロパティ
        // ============================================================

        /// <summary>現在の季節テーマ</summary>
        public SeasonalTheme CurrentSeason => currentSeason;

        /// <summary>装飾投資の累計支出額</summary>
        public int TotalDecorationInvestment => totalDecorationInvestment;

        /// <summary>
        /// 全ゾーンのムードボーナス合計（ガーデナーシナジー適用済み）。
        /// パーク全体のムード評価に加算される。
        /// </summary>
        public float TotalMoodBonus
        {
            get
            {
                float total = 0f;
                foreach (var kvp in zoneDecorations)
                {
                    total += kvp.Value.MoodBonus * kvp.Value.GardenerSynergyMultiplier;
                }
                return total;
            }
        }

        /// <summary>
        /// 全ゾーンのSNSボーナス合計。
        /// SNS投稿頻度・拡散力に加算される。
        /// </summary>
        public float TotalSNSBonus
        {
            get
            {
                float total = 0f;
                foreach (var kvp in zoneDecorations)
                {
                    total += kvp.Value.SNSBonus;
                }
                return total;
            }
        }

        // ============================================================
        // イベント
        // ============================================================

        /// <summary>季節テーマが変更された際に発火するイベント</summary>
        public event Action<SeasonalTheme> OnSeasonChanged;

        // ============================================================
        // ライフサイクル
        // ============================================================

        private void Awake()
        {
            // シングルトンパターン
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            InitializeZoneDecorations();
        }

        private void Update()
        {
            // 季節チェックはリアルタイム10秒間隔で実行（パフォーマンス配慮）
            seasonCheckTimer += Time.unscaledDeltaTime;
            if (seasonCheckTimer >= SeasonCheckInterval)
            {
                seasonCheckTimer = 0f;

                SeasonalTheme detectedSeason = DetectCurrentSeason();
                if (detectedSeason != currentSeason)
                {
                    SeasonalTheme previousSeason = currentSeason;
                    currentSeason = detectedSeason;

                    // 全ゾーンのテーマを更新
                    foreach (var kvp in zoneDecorations)
                    {
                        kvp.Value.CurrentTheme = currentSeason;
                    }

                    WebGLOptimizer.LogVerbose(
                        $"[DecorationSystem] 季節変更: {GetSeasonDisplayName(previousSeason)} → {GetSeasonDisplayName(currentSeason)}");

                    OnSeasonChanged?.Invoke(currentSeason);
                }
            }
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }
        }

        // ============================================================
        // 初期化
        // ============================================================

        /// <summary>
        /// 全テーマゾーンの装飾データを初期化する。
        /// 各ゾーンをLevel=Noneで作成し、辞書に登録する。
        /// </summary>
        private void InitializeZoneDecorations()
        {
            zoneDecorations.Clear();
            zoneDecorationList.Clear();

            // ThemeZone列挙型の全値に対して装飾データを生成
            foreach (ThemeZone zone in Enum.GetValues(typeof(ThemeZone)))
            {
                var decoration = new ZoneDecoration
                {
                    Zone = zone,
                    CurrentTheme = DetectCurrentSeason(),
                    Level = DecorationLevel.None,
                    GardenerSynergyMultiplier = 1.0f
                };

                zoneDecorations[zone] = decoration;
                zoneDecorationList.Add(decoration);
            }

            currentSeason = DetectCurrentSeason();

            WebGLOptimizer.LogVerbose(
                $"[DecorationSystem] 初期化完了。{zoneDecorations.Count}ゾーンの装飾データを作成。" +
                $"現在の季節: {GetSeasonDisplayName(currentSeason)}");
        }

        // ============================================================
        // 装飾アップグレード
        // ============================================================

        /// <summary>
        /// 指定ゾーンの装飾レベルをアップグレードする。
        /// None→Basic→Enhanced→Premiumの順に段階的にアップグレードされる。
        /// EconomyManagerから費用が自動的に差し引かれる。
        ///
        /// 【ゲームデザイン: アップグレードコスト】
        /// Basic: 500（低コストでまず装飾を始める動機づけ）
        /// Enhanced: 1500（中盤の投資。コスパが最も良い）
        /// Premium: 3000（終盤の贅沢投資。SNSボーナスが大きい）
        /// </summary>
        /// <param name="zone">アップグレード対象のゾーン</param>
        /// <returns>アップグレードが成功したかどうか</returns>
        public bool UpgradeZoneDecoration(ThemeZone zone)
        {
            if (!zoneDecorations.TryGetValue(zone, out ZoneDecoration decoration))
            {
                WebGLOptimizer.LogWarning($"[DecorationSystem] ゾーン {zone} の装飾データが見つかりません");
                return false;
            }

            // 最大レベルチェック
            if (decoration.Level == DecorationLevel.Premium)
            {
                WebGLOptimizer.LogVerbose($"[DecorationSystem] ゾーン {zone} は既に最大レベルです");
                return false;
            }

            // 次のレベルとコストを決定
            DecorationLevel nextLevel = decoration.Level switch
            {
                DecorationLevel.None => DecorationLevel.Basic,
                DecorationLevel.Basic => DecorationLevel.Enhanced,
                DecorationLevel.Enhanced => DecorationLevel.Premium,
                _ => DecorationLevel.Premium
            };

            int cost = GetDecorationLevelCost(nextLevel);

            // EconomyManagerから費用を差し引く
            var economyManager = GameManager.Instance?.EconomyManager;
            if (economyManager == null)
            {
                WebGLOptimizer.LogWarning("[DecorationSystem] EconomyManagerが利用できません");
                return false;
            }

            if (!economyManager.CanAfford(cost))
            {
                WebGLOptimizer.LogVerbose(
                    $"[DecorationSystem] 資金不足。必要額: {cost}, 所持金: {economyManager.CurrentBalance}");
                return false;
            }

            // 費用を支払い、レベルをアップグレード
            economyManager.SpendMoney(cost);
            decoration.Level = nextLevel;
            totalDecorationInvestment += cost;

            WebGLOptimizer.LogVerbose(
                $"[DecorationSystem] ゾーン {zone} の装飾を {nextLevel} にアップグレード " +
                $"(費用: {cost}, ムード+{decoration.MoodBonus}, SNS+{decoration.SNSBonus}, " +
                $"累計投資: {totalDecorationInvestment})");

            return true;
        }

        // ============================================================
        // 装飾データ参照
        // ============================================================

        /// <summary>指定ゾーンの装飾データを取得する</summary>
        /// <param name="zone">対象ゾーン</param>
        /// <returns>ゾーン装飾データ。見つからない場合はnull。</returns>
        public ZoneDecoration GetZoneDecoration(ThemeZone zone)
        {
            if (zoneDecorations.TryGetValue(zone, out ZoneDecoration decoration))
            {
                return decoration;
            }
            return null;
        }

        /// <summary>
        /// 指定ゾーンのムードボーナスを取得する（ガーデナーシナジー適用済み）。
        /// </summary>
        /// <param name="zone">対象ゾーン</param>
        /// <returns>シナジー倍率を乗じたムードボーナス値</returns>
        public float GetZoneMoodBonus(ThemeZone zone)
        {
            if (zoneDecorations.TryGetValue(zone, out ZoneDecoration decoration))
            {
                return decoration.MoodBonus * decoration.GardenerSynergyMultiplier;
            }
            return 0f;
        }

        /// <summary>指定ゾーンのSNSボーナスを取得する</summary>
        /// <param name="zone">対象ゾーン</param>
        /// <returns>SNSボーナス値</returns>
        public float GetZoneSNSBonus(ThemeZone zone)
        {
            if (zoneDecorations.TryGetValue(zone, out ZoneDecoration decoration))
            {
                return decoration.SNSBonus;
            }
            return 0f;
        }

        // ============================================================
        // ガーデナーシナジー
        // ============================================================

        /// <summary>
        /// 園芸師がゾーン内で作業を開始した際に呼ばれる。
        /// シナジー倍率を1.0+GardenerSynergyBonus（1.3）に設定する。
        /// </summary>
        /// <param name="zone">園芸師が作業中のゾーン</param>
        public void ApplyGardenerSynergy(ThemeZone zone)
        {
            if (zoneDecorations.TryGetValue(zone, out ZoneDecoration decoration))
            {
                decoration.GardenerSynergyMultiplier = 1.0f + GardenerSynergyBonus;
                WebGLOptimizer.LogVerbose(
                    $"[DecorationSystem] ゾーン {zone} にガーデナーシナジー適用 " +
                    $"(倍率: {decoration.GardenerSynergyMultiplier:F1}x)");
            }
        }

        /// <summary>
        /// 園芸師がゾーンから離れた際に呼ばれる。
        /// シナジー倍率を1.0にリセットする。
        /// </summary>
        /// <param name="zone">園芸師が離れたゾーン</param>
        public void ResetGardenerSynergy(ThemeZone zone)
        {
            if (zoneDecorations.TryGetValue(zone, out ZoneDecoration decoration))
            {
                decoration.GardenerSynergyMultiplier = 1.0f;
                WebGLOptimizer.LogVerbose(
                    $"[DecorationSystem] ゾーン {zone} のガーデナーシナジーをリセット");
            }
        }

        // ============================================================
        // 季節検出
        // ============================================================

        /// <summary>
        /// TimeManagerの現在月から季節テーマを判定する。
        ///
        /// 【ゲームデザイン: 季節判定ルール】
        /// ・3～5月: 春（Spring）- 桜や花の装飾
        /// ・6～8月: 夏（Summer）- 夏祭り・花火の装飾
        /// ・9～11月: 秋（Autumn）- 紅葉の装飾。10月はハロウィン優先
        /// ・12～2月: 冬（Winter）- イルミネーション。12月はクリスマス優先
        ///
        /// ハロウィン（10月）とクリスマス（12月）は特別テーマとして
        /// 通常の季節テーマより優先される。
        /// </summary>
        /// <returns>検出された季節テーマ</returns>
        private SeasonalTheme DetectCurrentSeason()
        {
            int month = 1;

            if (GameManager.Instance != null && GameManager.Instance.TimeManager != null)
            {
                month = GameManager.Instance.TimeManager.CurrentMonth;
            }

            // 特別シーズン判定（通常季節より優先）
            if (month == 10) return SeasonalTheme.Halloween;
            if (month == 12) return SeasonalTheme.Christmas;

            // 通常季節判定
            switch (month)
            {
                case 3: case 4: case 5:
                    return SeasonalTheme.Spring;
                case 6: case 7: case 8:
                    return SeasonalTheme.Summer;
                case 9: case 11:
                    return SeasonalTheme.Autumn;
                default: // 1, 2
                    return SeasonalTheme.Winter;
            }
        }

        // ============================================================
        // 表示名・コスト参照
        // ============================================================

        /// <summary>
        /// 季節テーマの日本語表示名を返す。
        /// UI表示やログ出力に使用する。
        /// </summary>
        /// <param name="theme">季節テーマ</param>
        /// <returns>日本語の季節テーマ名</returns>
        public static string GetSeasonDisplayName(SeasonalTheme theme)
        {
            return theme switch
            {
                SeasonalTheme.Spring => "春の花",
                SeasonalTheme.Summer => "夏祭り",
                SeasonalTheme.Autumn => "秋の紅葉",
                SeasonalTheme.Winter => "冬のイルミネーション",
                SeasonalTheme.Halloween => "ハロウィン",
                SeasonalTheme.Christmas => "クリスマス",
                _ => "不明"
            };
        }

        /// <summary>
        /// 装飾レベルに応じたアップグレードコストを返す。
        /// UpgradeZoneDecoration()で次レベルの費用算出に使用する。
        /// </summary>
        /// <param name="level">装飾レベル</param>
        /// <returns>アップグレードに必要な費用</returns>
        public static int GetDecorationLevelCost(DecorationLevel level)
        {
            return level switch
            {
                DecorationLevel.Basic => 500,
                DecorationLevel.Enhanced => 1500,
                DecorationLevel.Premium => 3000,
                _ => 0
            };
        }
    }
}
