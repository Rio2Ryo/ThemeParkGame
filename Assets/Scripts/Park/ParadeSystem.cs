// ============================================================
// ThemeParkGame - ParadeSystem
// ナイトパレードシステム: 夜間パレードの運行・フロート管理・来場者体験
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

    /// <summary>
    /// パレードフロートの定義データ。
    /// 各フロートはテーマゾーンに紐づき、来場者の興奮度と幸福度を向上させる。
    /// </summary>
    [Serializable]
    public class ParadeFloatData
    {
        /// <summary>フロート名称</summary>
        public string FloatName;

        /// <summary>対応テーマゾーン</summary>
        public ThemeZone Theme;

        /// <summary>興奮度ボーナス（1～5）</summary>
        [Range(1f, 5f)]
        public float ExcitementBonus;

        /// <summary>幸福度ボーナス（2～8）</summary>
        [Range(2f, 8f)]
        public float HappinessBonus;

        /// <summary>購入コスト（2000～5000）</summary>
        [Range(2000, 5000)]
        public int BuildCost;

        /// <summary>フロートのプレハブ参照</summary>
        public GameObject FloatPrefab;
    }

    /// <summary>
    /// パレードルートの経由ノード。
    /// フロートは各ノードを順番に移動し、WaitDuration秒だけ停止してパフォーマンスを行う。
    /// </summary>
    [Serializable]
    public class ParadeRouteNode
    {
        /// <summary>ノードのワールド座標</summary>
        public Vector3 Position;

        /// <summary>このノードでの停止時間（0～10秒）</summary>
        [Range(0f, 10f)]
        public float WaitDuration;
    }

    /// <summary>パレードの進行状態</summary>
    public enum ParadeState
    {
        Idle,       // 待機中（パレード未開始）
        Preparing,  // 準備中（開始前の演出待ち）
        InProgress, // 運行中
        Finished    // 終了処理中
    }

    // ================================================================
    // ParadeSystem 本体
    // ================================================================

    /// <summary>
    /// ナイトパレードの運行を管理するシングルトンシステム。
    ///
    /// 【ゲームデザイン: ナイトパレード】
    /// パークの夜間体験を大幅に強化するコンテンツ。18:00～22:00に自動運行される。
    ///
    /// ■ フロート購入:
    ///   - 各テーマゾーンに対応したフロートを購入可能
    ///   - フロート数が多いほどパレード効果が増大（線形スケール）
    ///   - 初期投資は大きいが、来場者満足度と評判への影響は絶大
    ///
    /// ■ パレード効果:
    ///   - ParadeViewingRadius内の来場者に幸福度・興奮度ボーナスを付与
    ///   - ボーナスは全フロートの合計値（フロートが多いほど豪華）
    ///   - 閉園間際の来場者を引き留め、夜間のショップ売上にも貢献
    ///
    /// ■ ルート設計:
    ///   - ParadeRouteNodeのリストで経路を定義
    ///   - 各ノードでの停止演出により、パーク全体に来場者が分散
    /// </summary>
    public class ParadeSystem : MonoBehaviour
    {
        // ================================================================
        // Singleton
        // ================================================================

        public static ParadeSystem Instance { get; private set; }

        // ================================================================
        // Serialized Fields
        // ================================================================

        [Header("Parade Floats")]
        [Tooltip("購入済みのパレードフロート一覧")]
        [SerializeField] private List<ParadeFloatData> ownedFloats = new List<ParadeFloatData>();

        [Header("Parade Route")]
        [Tooltip("パレードの経路ノード。順番にフロートが移動する")]
        [SerializeField] private List<ParadeRouteNode> paradeRoute = new List<ParadeRouteNode>();

        [Header("Time Settings")]
        [Tooltip("パレード開始時刻（ゲーム内時間 0-24）")]
        [SerializeField] private float paradeStartHour = 18f;

        [Tooltip("パレード終了時刻（ゲーム内時間 0-24）")]
        [SerializeField] private float paradeEndHour = 22f;

        [Header("Movement Settings")]
        [Tooltip("フロートの移動速度（ユニット/秒）")]
        [SerializeField] private float paradeSpeed = 3f;

        [Header("General Settings")]
        [Tooltip("ナイトパレードの有効/無効切替")]
        [SerializeField] private bool isNightParadeEnabled = true;

        // ================================================================
        // Runtime State
        // ================================================================

        /// <summary>現在のパレード状態</summary>
        private ParadeState currentState = ParadeState.Idle;

        /// <summary>現在のルートノードインデックス</summary>
        private int currentRouteIndex;

        /// <summary>現在のノードでの待機残り時間</summary>
        private float routeWaitTimer;

        /// <summary>準備フェーズの経過時間（ゲーム内1分 = 準備完了）</summary>
        private float preparingTimer;

        /// <summary>準備に必要なゲーム内時間（秒）。TimeManagerの secondsPerGameHour / 60 = 1ゲーム分相当</summary>
        private const float PreparingDurationSeconds = 5f; // リアル5秒 ≒ ゲーム内1時間 / 60 = 1分

        // ---- Daily Stats ----
        // 日次パレード統計

        /// <summary>累計パレード開催回数</summary>
        private int totalParadesHeld;

        /// <summary>累計パレード鑑賞者数</summary>
        private int totalViewers;

        /// <summary>今回のパレードで既に開始処理を行ったかのフラグ（1日1回制御用）</summary>
        private bool hasStartedToday;

        // ================================================================
        // Purchasable Float Templates
        // ================================================================

        /// <summary>購入可能なフロートテンプレート一覧</summary>
        private readonly List<ParadeFloatData> availableFloatTemplates = new List<ParadeFloatData>();

        // ================================================================
        // Events
        // ================================================================

        /// <summary>パレード開始時に発火するイベント</summary>
        public event Action OnParadeStarted;

        /// <summary>パレード終了時に発火するイベント</summary>
        public event Action OnParadeEnded;

        // ================================================================
        // Properties
        // ================================================================

        /// <summary>パレードが現在運行中かどうか</summary>
        public bool IsParadeActive => currentState == ParadeState.InProgress;

        /// <summary>現在のパレード状態</summary>
        public ParadeState CurrentState => currentState;

        /// <summary>
        /// パレード観覧可能半径（ユニット）。
        /// 来場者がこの範囲内にいるとパレードを鑑賞できる。
        /// </summary>
        public float ParadeViewingRadius => 15f;

        /// <summary>
        /// 現在のパレードによる幸福度ボーナス合計。
        /// パレード運行中のみ有効。全フロートのHappinessBonusの合計を返す。
        /// </summary>
        public float ParadeHappinessBonus
        {
            get
            {
                if (!IsParadeActive) return 0f;
                float total = 0f;
                for (int i = 0; i < ownedFloats.Count; i++)
                {
                    total += ownedFloats[i].HappinessBonus;
                }
                return total;
            }
        }

        /// <summary>
        /// 現在のパレードによる興奮度ボーナス合計。
        /// パレード運行中のみ有効。全フロートのExcitementBonusの合計を返す。
        /// </summary>
        public float ParadeExcitementBonus
        {
            get
            {
                if (!IsParadeActive) return 0f;
                float total = 0f;
                for (int i = 0; i < ownedFloats.Count; i++)
                {
                    total += ownedFloats[i].ExcitementBonus;
                }
                return total;
            }
        }

        /// <summary>累計パレード開催回数</summary>
        public int TotalParadesHeld => totalParadesHeld;

        /// <summary>累計パレード鑑賞者数</summary>
        public int TotalViewers => totalViewers;

        // ================================================================
        // Unity Lifecycle
        // ================================================================

        private void Awake()
        {
            // Singleton setup
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            InitializeDefaultFloats();

            WebGLOptimizer.LogVerbose("[ParadeSystem] 初期化完了。ナイトパレードシステム起動");
        }

        private void Update()
        {
            if (GameManager.Instance == null || GameManager.Instance.IsPaused) return;

            var tm = GameManager.Instance.TimeManager;
            if (tm == null) return;

            float currentHour = tm.CurrentHour;

            switch (currentState)
            {
                case ParadeState.Idle:
                    // パレード開始時刻チェック: 18:00になったら自動開始
                    if (isNightParadeEnabled
                        && !hasStartedToday
                        && ownedFloats.Count > 0
                        && currentHour >= paradeStartHour
                        && currentHour < paradeEndHour)
                    {
                        StartParade();
                    }
                    // 日付リセット: 翌日の開始判定用フラグを戻す
                    if (currentHour < paradeStartHour)
                    {
                        hasStartedToday = false;
                    }
                    break;

                case ParadeState.Preparing:
                    // 準備フェーズ: ゲーム内1分（リアル約5秒）経過で運行開始
                    preparingTimer += Time.deltaTime;
                    if (preparingTimer >= PreparingDurationSeconds)
                    {
                        currentState = ParadeState.InProgress;
                        currentRouteIndex = 0;
                        routeWaitTimer = 0f;

                        OnParadeStarted?.Invoke();
                        GameEvents.FireParkEventStarted("night_parade", "ナイトパレード");

                        if (NotificationSystem.Instance != null)
                        {
                            NotificationSystem.Instance.Notify(
                                "ナイトパレード開始！", NotifLevel.Success);
                        }

                        WebGLOptimizer.LogVerbose("[ParadeSystem] パレード運行開始");
                    }
                    break;

                case ParadeState.InProgress:
                    // 終了時刻チェック: 22:00を過ぎたら終了
                    if (currentHour >= paradeEndHour || currentHour < paradeStartHour)
                    {
                        EndParade();
                        break;
                    }
                    // ルート進行を更新
                    UpdateParadeMovement();
                    break;

                case ParadeState.Finished:
                    // 終了処理後にIdleへ遷移（EndParade内で処理済み）
                    break;
            }
        }

        // ================================================================
        // Parade Control
        // ================================================================

        /// <summary>
        /// パレードを開始する。Preparing状態に遷移し、ゲーム内1分後にInProgressとなる。
        /// 【条件】フロートが1台以上所有されていること。
        /// </summary>
        public void StartParade()
        {
            if (currentState != ParadeState.Idle)
            {
                WebGLOptimizer.LogWarning("[ParadeSystem] StartParade: 既にパレードが進行中または準備中です");
                return;
            }

            if (ownedFloats.Count == 0)
            {
                WebGLOptimizer.LogWarning("[ParadeSystem] StartParade: フロートが1台もありません");
                return;
            }

            currentState = ParadeState.Preparing;
            preparingTimer = 0f;
            hasStartedToday = true;

            if (NotificationSystem.Instance != null)
            {
                NotificationSystem.Instance.Notify(
                    "ナイトパレード準備中...", NotifLevel.Info);
            }

            WebGLOptimizer.LogVerbose($"[ParadeSystem] パレード準備開始（フロート数: {ownedFloats.Count}）");
        }

        /// <summary>
        /// パレードを終了する。Finished状態を経てIdleに遷移する。
        /// </summary>
        public void EndParade()
        {
            if (currentState != ParadeState.InProgress && currentState != ParadeState.Preparing)
            {
                WebGLOptimizer.LogWarning("[ParadeSystem] EndParade: パレードが運行中ではありません");
                return;
            }

            currentState = ParadeState.Finished;
            totalParadesHeld++;

            OnParadeEnded?.Invoke();
            GameEvents.FireParkEventEnded("night_parade", "ナイトパレード");

            if (NotificationSystem.Instance != null)
            {
                NotificationSystem.Instance.Notify(
                    $"ナイトパレード終了（鑑賞者: {totalViewers}人）", NotifLevel.Info);
            }

            WebGLOptimizer.LogVerbose($"[ParadeSystem] パレード終了。累計開催: {totalParadesHeld}回, 累計鑑賞者: {totalViewers}人");

            // Finished -> Idle へ即時遷移
            currentState = ParadeState.Idle;
        }

        // ================================================================
        // Parade Movement
        // ================================================================

        /// <summary>パレードフロートのルート進行を更新する</summary>
        private void UpdateParadeMovement()
        {
            if (paradeRoute == null || paradeRoute.Count == 0) return;
            if (currentRouteIndex >= paradeRoute.Count) return;

            var currentNode = paradeRoute[currentRouteIndex];

            // ノードでの停止待機中
            if (routeWaitTimer > 0f)
            {
                routeWaitTimer -= Time.deltaTime;
                return;
            }

            // 次のノードへ移動判定（距離ベースで進行をシミュレート）
            // 実際のフロートGameObjectの移動はFloatPrefabのインスタンスで行う想定
            // ここではルートインデックスの進行管理のみ
            if (currentRouteIndex < paradeRoute.Count - 1)
            {
                var nextNode = paradeRoute[currentRouteIndex + 1];
                float distance = Vector3.Distance(currentNode.Position, nextNode.Position);
                float travelTime = distance / Mathf.Max(paradeSpeed, 0.1f);

                // 簡易的にdeltaTimeで到達判定（詳細な移動はフロートプレハブのスクリプトが担当）
                // ここではルート管理としてノードを進める
                currentRouteIndex++;
                routeWaitTimer = paradeRoute[currentRouteIndex].WaitDuration;

                WebGLOptimizer.LogVerbose($"[ParadeSystem] ルートノード {currentRouteIndex}/{paradeRoute.Count - 1} に到達");
            }
            else
            {
                // ルートの最終ノードに到達 → ループまたは終了
                currentRouteIndex = 0;
                routeWaitTimer = paradeRoute[0].WaitDuration;
                WebGLOptimizer.LogVerbose("[ParadeSystem] ルート1周完了。再ループ開始");
            }
        }

        // ================================================================
        // Float Purchase
        // ================================================================

        /// <summary>
        /// パレードフロートを購入する。
        /// EconomyManagerを通じてBuildCost分を支払い、所有フロートに追加する。
        ///
        /// 【ゲームデザイン: フロート投資】
        /// フロートは「長期投資」として位置づけ。初期コストは高いが、
        /// 毎晩のパレードで来場者満足度を継続的に向上させる。
        /// 5台揃えると「グランドパレード」としてボーナス効果が最大化。
        /// </summary>
        /// <param name="floatData">購入するフロートのデータ</param>
        /// <returns>購入成功時はtrue</returns>
        public bool PurchaseFloat(ParadeFloatData floatData)
        {
            if (floatData == null)
            {
                WebGLOptimizer.LogWarning("[ParadeSystem] PurchaseFloat: フロートデータがnullです");
                return false;
            }

            var econ = GameManager.Instance?.EconomyManager;
            if (econ == null)
            {
                WebGLOptimizer.LogWarning("[ParadeSystem] PurchaseFloat: EconomyManagerが見つかりません");
                return false;
            }

            if (!econ.CanAfford(floatData.BuildCost))
            {
                WebGLOptimizer.LogWarning($"[ParadeSystem] PurchaseFloat: 資金不足（必要: {floatData.BuildCost}, 所持: {econ.CurrentBalance}）");
                return false;
            }

            // 購入コストを支払い
            econ.SpendMoney(floatData.BuildCost);

            // フロートデータをコピーして所有リストに追加
            var purchased = new ParadeFloatData
            {
                FloatName = floatData.FloatName,
                Theme = floatData.Theme,
                ExcitementBonus = floatData.ExcitementBonus,
                HappinessBonus = floatData.HappinessBonus,
                BuildCost = floatData.BuildCost,
                FloatPrefab = floatData.FloatPrefab
            };
            ownedFloats.Add(purchased);

            if (NotificationSystem.Instance != null)
            {
                NotificationSystem.Instance.Notify(
                    $"フロート購入: {floatData.FloatName}", NotifLevel.Success);
            }

            WebGLOptimizer.LogVerbose($"[ParadeSystem] フロート購入完了: {floatData.FloatName} " +
                      $"(コスト: {floatData.BuildCost}, 所有数: {ownedFloats.Count})");

            return true;
        }

        // ================================================================
        // Position & Viewing
        // ================================================================

        /// <summary>
        /// 現在のパレード位置を取得する。
        /// 来場者がパレードを見に移動する際のターゲット座標として使用。
        /// パレード非運行時はVector3.zeroを返す。
        /// </summary>
        /// <returns>パレードフロートの現在位置</returns>
        public Vector3 GetParadePosition()
        {
            if (!IsParadeActive) return Vector3.zero;
            if (paradeRoute == null || paradeRoute.Count == 0) return Vector3.zero;

            int index = Mathf.Clamp(currentRouteIndex, 0, paradeRoute.Count - 1);
            return paradeRoute[index].Position;
        }

        /// <summary>
        /// 指定位置がパレード観覧範囲内にあるかを判定する。
        /// 来場者AIがパレードを鑑賞可能かどうかの判定に使用。
        /// </summary>
        /// <param name="position">判定対象の位置</param>
        /// <returns>観覧範囲内であればtrue</returns>
        public bool IsWithinViewingRange(Vector3 position)
        {
            if (!IsParadeActive) return false;

            Vector3 paradePos = GetParadePosition();
            float distance = Vector3.Distance(position, paradePos);
            return distance <= ParadeViewingRadius;
        }

        // ================================================================
        // Bonus Calculation
        // ================================================================

        /// <summary>
        /// ナイトパレードのボーナスを計算する。
        /// 幸福度・興奮度ボーナスをフロート数で乗算して返す。
        ///
        /// 【ゲームデザイン: スケーリング】
        /// フロート1台: 基礎ボーナス × 1
        /// フロート3台: 基礎ボーナス × 3 → 本格的なパレード
        /// フロート5台: 基礎ボーナス × 5 → グランドパレード級の効果
        /// </summary>
        /// <returns>(happinessBonus, excitementBonus) のタプル</returns>
        public (float happiness, float excitement) CalculateNightParadeBonus()
        {
            if (!IsParadeActive || ownedFloats.Count == 0)
            {
                return (0f, 0f);
            }

            float totalHappiness = 0f;
            float totalExcitement = 0f;

            for (int i = 0; i < ownedFloats.Count; i++)
            {
                totalHappiness += ownedFloats[i].HappinessBonus;
                totalExcitement += ownedFloats[i].ExcitementBonus;
            }

            // フロート数によるスケーリング（フロートが多いほど壮観なパレード）
            float floatCountMultiplier = ownedFloats.Count;
            return (totalHappiness * floatCountMultiplier, totalExcitement * floatCountMultiplier);
        }

        // ================================================================
        // Float Templates
        // ================================================================

        /// <summary>
        /// 購入可能なフロートテンプレートの一覧を返す。
        /// UIでのフロート購入画面に使用する。
        /// </summary>
        /// <returns>購入可能なフロートデータのリスト</returns>
        public List<ParadeFloatData> GetAvailableFloats()
        {
            return new List<ParadeFloatData>(availableFloatTemplates);
        }

        /// <summary>
        /// デフォルトの購入可能フロートを初期化する。
        ///
        /// 【ゲームデザイン: 5つのテーマフロート】
        /// 各テーマゾーンに1台ずつ対応するフロートを用意。
        /// プレイヤーはパークのテーマに合わせてフロートを選択・購入する。
        /// 全5台を揃えると全ゾーンのボーナスが発動し、最大効果を発揮。
        /// </summary>
        private void InitializeDefaultFloats()
        {
            availableFloatTemplates.Clear();

            // ロイヤルキャッスル号 - 中世ファンタジーテーマの豪華フロート
            availableFloatTemplates.Add(new ParadeFloatData
            {
                FloatName = "ロイヤルキャッスル号",
                Theme = ThemeZone.LostKingdom,
                ExcitementBonus = 3f,
                HappinessBonus = 5f,
                BuildCost = 3000,
                FloatPrefab = null
            });

            // ゴーストシップ号 - ハロウィンテーマのホラー演出フロート
            availableFloatTemplates.Add(new ParadeFloatData
            {
                FloatName = "ゴーストシップ号",
                Theme = ThemeZone.HalloweenWorld,
                ExcitementBonus = 4f,
                HappinessBonus = 4f,
                BuildCost = 3500,
                FloatPrefab = null
            });

            // マジカルドリーム号 - 夢と魔法のファンタジーフロート
            availableFloatTemplates.Add(new ParadeFloatData
            {
                FloatName = "マジカルドリーム号",
                Theme = ThemeZone.Wonderland,
                ExcitementBonus = 2f,
                HappinessBonus = 6f,
                BuildCost = 2500,
                FloatPrefab = null
            });

            // コスモライナー号 - 宇宙テーマの光と音のフロート
            availableFloatTemplates.Add(new ParadeFloatData
            {
                FloatName = "コスモライナー号",
                Theme = ThemeZone.SpaceZone,
                ExcitementBonus = 5f,
                HappinessBonus = 3f,
                BuildCost = 4000,
                FloatPrefab = null
            });

            // ネオンシティ号 - 未来都市テーマのサイバーパンクフロート
            availableFloatTemplates.Add(new ParadeFloatData
            {
                FloatName = "ネオンシティ号",
                Theme = ThemeZone.FutureCity,
                ExcitementBonus = 4f,
                HappinessBonus = 5f,
                BuildCost = 4500,
                FloatPrefab = null
            });

            WebGLOptimizer.LogVerbose($"[ParadeSystem] デフォルトフロート初期化完了: {availableFloatTemplates.Count}種類");
        }

        // ================================================================
        // Viewer Tracking
        // ================================================================

        /// <summary>
        /// パレード鑑賞者を記録する。
        /// 来場者AIがパレードを鑑賞した際に呼び出す。
        /// </summary>
        public void RecordViewer()
        {
            totalViewers++;
        }
    }
}
