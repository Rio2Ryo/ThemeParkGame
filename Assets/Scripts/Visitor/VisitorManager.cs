// ============================================================
// ThemeParkGame - VisitorManager
// 全来場者の生成・管理・統計を統括するマネージャー
// オブジェクトプールによる効率的なインスタンス管理
// ============================================================

using System;
using System.Collections.Generic;
using UnityEngine;
using ThemeParkGame.Core;

namespace ThemeParkGame.Visitor
{
    /// <summary>
    /// 全来場者の生成・管理・統計追跡を担当するマネージャー。
    ///
    /// 【ゲームデザイン】
    /// 来場者数はパークの「集客力」を直接反映する。
    /// パークの知名度（Fame）、評価（Rating）、天候、入場料が来場者数に影響する。
    ///
    /// スポーン量の計算式:
    ///   基本スポーンレート * 知名度倍率 * 天候倍率 * 価格倍率
    ///
    /// VIPは特別な条件で出現する（知名度一定以上、ゴールデンチケット使用時等）。
    /// VIPは高い期待値と特殊リクエストを持ち、満足させるとパーク評価が大幅上昇する。
    /// </summary>
    public class VisitorManager : MonoBehaviour
    {
        // ---- シリアライズフィールド ----

        [Header("スポーン設定")]
        [SerializeField] private GameObject visitorPrefab;
        [SerializeField] private Transform spawnPoint;
        [SerializeField] private Transform exitPoint;
        [SerializeField] private float baseSpawnInterval = 5f;
        [SerializeField] private int maxVisitors = 500;
        [SerializeField] private int initialPoolSize = 100;

        [Header("VIP設定")]
        [SerializeField] private float vipSpawnChance = 0.02f;
        [SerializeField] private float vipFameThreshold = 50f;

        [Header("統計表示")]
        [SerializeField] private bool enableDebugStats = true;

        // ---- 内部状態 ----

        // アクティブな来場者
        private List<VisitorAI> activeVisitors = new List<VisitorAI>();

        // オブジェクトプール
        private Queue<VisitorAI> visitorPool = new Queue<VisitorAI>();
        private Transform poolParent;

        // ID管理
        private int nextVisitorId = 1;

        // スポーン制御
        private float spawnTimer;
        private bool isSpawningEnabled;

        // 統計
        private int totalVisitorsToday;
        private int totalVisitorsEverLeft;
        private float happinessSum;
        private float peakVisitorCount;

        // ---- プロパティ ----

        /// <summary>現在パーク内にいる来場者数</summary>
        public int ActiveVisitorCount => activeVisitors.Count;

        /// <summary>本日の総来場者数</summary>
        public int TotalVisitorsToday => totalVisitorsToday;

        /// <summary>最大来場者数上限</summary>
        public int MaxVisitors => maxVisitors;

        /// <summary>来場者の平均幸福度（0-100）</summary>
        public float AverageHappiness
        {
            get
            {
                if (activeVisitors.Count == 0) return 0f;

                float sum = 0f;
                for (int i = 0; i < activeVisitors.Count; i++)
                {
                    sum += activeVisitors[i].Happiness;
                }
                return sum / activeVisitors.Count;
            }
        }

        /// <summary>ピーク来場者数</summary>
        public float PeakVisitorCount => peakVisitorCount;

        /// <summary>出口位置（来場者の退園先）</summary>
        public Vector3 ExitPosition => exitPoint != null ? exitPoint.position : Vector3.zero;

        // ---- Unity ライフサイクル ----

        private void Awake()
        {
            // プール用の親オブジェクトを作成
            var poolObj = new GameObject("VisitorPool");
            poolObj.transform.SetParent(transform);
            poolParent = poolObj.transform;
        }

        private void OnEnable()
        {
            SubscribeEvents();
        }

        private void OnDisable()
        {
            UnsubscribeEvents();
        }

        private void Update()
        {
            if (!isSpawningEnabled) return;
            if (GameManager.Instance != null && GameManager.Instance.IsPaused) return;

            // スポーンタイマー
            spawnTimer -= Time.deltaTime;
            if (spawnTimer <= 0f)
            {
                spawnTimer = CalculateSpawnInterval();
                TrySpawnVisitor();
            }

            // 退園済み来場者のクリーンアップ
            CleanupInactiveVisitors();

            // ピーク更新
            if (activeVisitors.Count > peakVisitorCount)
            {
                peakVisitorCount = activeVisitors.Count;
            }
        }

        // ---- 初期化 ----

        /// <summary>
        /// VisitorManagerを初期化する。GameManager.StartNewGame()から呼ばれる。
        /// </summary>
        public void Initialize()
        {
            // 既存の来場者をすべてクリア
            ClearAllVisitors();

            // オブジェクトプールを初期化
            InitializePool();

            // 統計リセット
            totalVisitorsToday = 0;
            totalVisitorsEverLeft = 0;
            happinessSum = 0f;
            peakVisitorCount = 0;
            nextVisitorId = 1;

            isSpawningEnabled = true;
            spawnTimer = baseSpawnInterval;

            Debug.Log($"[VisitorManager] Initialized. Pool size: {visitorPool.Count}, Max visitors: {maxVisitors}");
        }

        // ---- オブジェクトプール ----

        /// <summary>プールを初期化し、初期数のビジターオブジェクトを事前生成する</summary>
        private void InitializePool()
        {
            // 既存プールをクリア
            while (visitorPool.Count > 0)
            {
                var v = visitorPool.Dequeue();
                if (v != null) Destroy(v.gameObject);
            }

            if (visitorPrefab == null)
            {
                Debug.LogWarning("[VisitorManager] Visitor prefab is not assigned. Pool initialization skipped.");
                return;
            }

            for (int i = 0; i < initialPoolSize; i++)
            {
                VisitorAI visitor = CreateVisitorObject();
                ReturnToPool(visitor);
            }
        }

        /// <summary>新しい来場者オブジェクトを生成する</summary>
        private VisitorAI CreateVisitorObject()
        {
            GameObject obj = Instantiate(visitorPrefab, poolParent);
            obj.SetActive(false);

            VisitorAI visitor = obj.GetComponent<VisitorAI>();
            if (visitor == null)
            {
                visitor = obj.AddComponent<VisitorAI>();
            }

            return visitor;
        }

        /// <summary>プールから来場者オブジェクトを取得する</summary>
        private VisitorAI GetFromPool()
        {
            VisitorAI visitor;

            if (visitorPool.Count > 0)
            {
                visitor = visitorPool.Dequeue();
            }
            else
            {
                // プールが空の場合は新規作成
                if (visitorPrefab == null)
                {
                    Debug.LogError("[VisitorManager] Cannot create visitor: prefab is null.");
                    return null;
                }
                visitor = CreateVisitorObject();
            }

            if (visitor != null)
            {
                visitor.gameObject.SetActive(true);
                visitor.transform.SetParent(transform);
            }

            return visitor;
        }

        /// <summary>来場者オブジェクトをプールに返却する</summary>
        private void ReturnToPool(VisitorAI visitor)
        {
            if (visitor == null) return;

            visitor.ResetVisitor();
            visitor.gameObject.SetActive(false);
            visitor.transform.SetParent(poolParent);
            visitorPool.Enqueue(visitor);
        }

        // ---- スポーン制御 ----

        /// <summary>
        /// 来場者のスポーンを試みる。
        /// 最大人数に達している場合や、スポーンポイントが未設定の場合はスキップ。
        /// </summary>
        private void TrySpawnVisitor()
        {
            if (activeVisitors.Count >= maxVisitors) return;

            VisitorType type = DetermineVisitorType();
            SpawnVisitor(type);
        }

        /// <summary>
        /// 指定タイプの来場者をスポーンさせる。
        /// </summary>
        /// <param name="type">来場者タイプ</param>
        /// <returns>スポーンされた来場者。失敗時はnull。</returns>
        public VisitorAI SpawnVisitor(VisitorType type)
        {
            if (activeVisitors.Count >= maxVisitors)
            {
                Debug.LogWarning($"[VisitorManager] Cannot spawn visitor: max capacity ({maxVisitors}) reached.");
                return null;
            }

            VisitorAI visitor = GetFromPool();
            if (visitor == null) return null;

            Vector3 spawnPos = GetSpawnPosition();
            int id = nextVisitorId++;

            visitor.Initialize(id, type, spawnPos);
            activeVisitors.Add(visitor);
            totalVisitorsToday++;

            if (type == VisitorType.VIP)
            {
                GameEvents.FireVIPArrived(id);
                Debug.Log($"[VisitorManager] VIP visitor {id} has arrived!");
            }

            return visitor;
        }

        /// <summary>
        /// VIP来場者を強制スポーンさせる（ゴールデンチケット使用時等）。
        /// 【ゲームデザイン】VIPは特殊リクエストを持ち、
        /// 満足させるとパーク評価が大幅に上昇する。失望させると評価が下がる。
        /// </summary>
        /// <returns>スポーンされたVIP来場者</returns>
        public VisitorAI SpawnVIPVisitor()
        {
            return SpawnVisitor(VisitorType.VIP);
        }

        /// <summary>
        /// スポーンする来場者のタイプを決定する。
        /// 【ゲームデザイン】タイプの出現比率はパークの特性によって変動する。
        /// スリル系アトラクションが多い → Young率UP
        /// ショー系が充実 → Family率UP
        /// 展望系が多い → Senior/Couple率UP
        /// </summary>
        private VisitorType DetermineVisitorType()
        {
            // VIP判定（知名度条件を満たす場合のみ）
            float fame = GetParkFame();
            if (fame >= vipFameThreshold && UnityEngine.Random.value < vipSpawnChance)
            {
                return VisitorType.VIP;
            }

            // 通常タイプの抽選（基本比率）
            // 【ゲームデザイン】基本比率: Family 30%, Young 25%, Kids 15%, Couple 15%, Senior 15%
            float roll = UnityEngine.Random.value;

            if (roll < 0.30f) return VisitorType.Family;
            if (roll < 0.55f) return VisitorType.Young;
            if (roll < 0.70f) return VisitorType.Kids;
            if (roll < 0.85f) return VisitorType.Couple;
            return VisitorType.Senior;
        }

        /// <summary>
        /// スポーン間隔を計算する。
        /// パークの知名度・天候・入場料によってスポーン速度が変動する。
        ///
        /// 【ゲームデザイン】
        /// - 知名度が高い → スポーン間隔短縮（人気パーク）
        /// - 天候が悪い → スポーン間隔延長（客足が遠のく）
        /// - 入場料が高い → スポーン間隔延長（コスパが悪い）
        /// - 平均幸福度が高い → スポーン間隔短縮（口コミ効果）
        /// </summary>
        private float CalculateSpawnInterval()
        {
            float interval = baseSpawnInterval;

            // 知名度倍率（高いほど短く）
            float fame = GetParkFame();
            float fameMultiplier = Mathf.Lerp(2.0f, 0.3f, fame / 100f);
            interval *= fameMultiplier;

            // 天候倍率
            float weatherMultiplier = GetWeatherSpawnMultiplier();
            interval *= weatherMultiplier;

            // 平均幸福度ボーナス（口コミ効果）
            float avgHappiness = AverageHappiness;
            if (avgHappiness > 70f)
            {
                float happinessBonus = (avgHappiness - 70f) / 30f; // 0-1
                interval *= Mathf.Lerp(1.0f, 0.7f, happinessBonus);
            }

            // 混雑抑制（上限に近いほどスポーン間隔が延びる）
            float capacityRatio = (float)activeVisitors.Count / maxVisitors;
            if (capacityRatio > 0.8f)
            {
                interval *= Mathf.Lerp(1.0f, 3.0f, (capacityRatio - 0.8f) / 0.2f);
            }

            // 最小/最大間隔の制限
            return Mathf.Clamp(interval, 1f, 30f);
        }

        /// <summary>スポーン位置を取得する（少しランダムにずらす）</summary>
        private Vector3 GetSpawnPosition()
        {
            Vector3 basePos = spawnPoint != null ? spawnPoint.position : transform.position;
            Vector3 offset = UnityEngine.Random.insideUnitSphere * 3f;
            offset.y = 0f;
            return basePos + offset;
        }

        // ---- 退園済みクリーンアップ ----

        /// <summary>退園済み・非アクティブな来場者をプールに返却する</summary>
        private void CleanupInactiveVisitors()
        {
            for (int i = activeVisitors.Count - 1; i >= 0; i--)
            {
                VisitorAI visitor = activeVisitors[i];
                if (visitor == null)
                {
                    activeVisitors.RemoveAt(i);
                    continue;
                }

                if (!visitor.IsActive)
                {
                    // 退園統計を記録
                    happinessSum += visitor.Happiness;
                    totalVisitorsEverLeft++;

                    activeVisitors.RemoveAt(i);
                    ReturnToPool(visitor);
                }
            }
        }

        /// <summary>全来場者を強制退園させてプールに返却する</summary>
        public void ClearAllVisitors()
        {
            for (int i = activeVisitors.Count - 1; i >= 0; i--)
            {
                if (activeVisitors[i] != null)
                {
                    ReturnToPool(activeVisitors[i]);
                }
            }
            activeVisitors.Clear();
        }

        // ---- 来場者検索 ----

        /// <summary>IDで来場者を検索する</summary>
        public VisitorAI FindVisitorById(int visitorId)
        {
            for (int i = 0; i < activeVisitors.Count; i++)
            {
                if (activeVisitors[i].VisitorId == visitorId)
                    return activeVisitors[i];
            }
            return null;
        }

        /// <summary>指定位置に最も近い来場者を取得する</summary>
        public VisitorAI FindNearestVisitor(Vector3 position, float maxDistance = float.MaxValue)
        {
            VisitorAI nearest = null;
            float nearestDist = maxDistance;

            for (int i = 0; i < activeVisitors.Count; i++)
            {
                float dist = Vector3.Distance(position, activeVisitors[i].transform.position);
                if (dist < nearestDist)
                {
                    nearestDist = dist;
                    nearest = activeVisitors[i];
                }
            }

            return nearest;
        }

        /// <summary>指定タイプの来場者リストを取得する</summary>
        public List<VisitorAI> GetVisitorsByType(VisitorType type)
        {
            var result = new List<VisitorAI>();
            for (int i = 0; i < activeVisitors.Count; i++)
            {
                if (activeVisitors[i].Type == type)
                    result.Add(activeVisitors[i]);
            }
            return result;
        }

        /// <summary>全アクティブ来場者の読み取り専用リスト</summary>
        public IReadOnlyList<VisitorAI> GetAllActiveVisitors()
        {
            return activeVisitors.AsReadOnly();
        }

        // ---- 統計 ----

        /// <summary>
        /// 退園した全来場者の平均幸福度。パーク評価の重要指標。
        /// </summary>
        public float AverageExitHappiness
        {
            get
            {
                if (totalVisitorsEverLeft == 0) return 0f;
                return happinessSum / totalVisitorsEverLeft;
            }
        }

        /// <summary>タイプ別の来場者数を取得する</summary>
        public Dictionary<VisitorType, int> GetVisitorCountByType()
        {
            var counts = new Dictionary<VisitorType, int>();
            foreach (VisitorType type in Enum.GetValues(typeof(VisitorType)))
            {
                counts[type] = 0;
            }

            for (int i = 0; i < activeVisitors.Count; i++)
            {
                counts[activeVisitors[i].Type]++;
            }

            return counts;
        }

        /// <summary>行動状態別の来場者数を取得する</summary>
        public Dictionary<VisitorBehaviorState, int> GetVisitorCountByState()
        {
            var counts = new Dictionary<VisitorBehaviorState, int>();
            foreach (VisitorBehaviorState state in Enum.GetValues(typeof(VisitorBehaviorState)))
            {
                counts[state] = 0;
            }

            for (int i = 0; i < activeVisitors.Count; i++)
            {
                counts[activeVisitors[i].CurrentState]++;
            }

            return counts;
        }

        // ---- イベントハンドラ ----

        private void SubscribeEvents()
        {
            GameEvents.OnParkOpened += HandleParkOpened;
            GameEvents.OnParkClosed += HandleParkClosed;
            GameEvents.OnVisitorSelected += HandleVisitorSelected;
            GameEvents.OnWeatherChanged += HandleWeatherChanged;
        }

        private void UnsubscribeEvents()
        {
            GameEvents.OnParkOpened -= HandleParkOpened;
            GameEvents.OnParkClosed -= HandleParkClosed;
            GameEvents.OnVisitorSelected -= HandleVisitorSelected;
            GameEvents.OnWeatherChanged -= HandleWeatherChanged;
        }

        private void HandleParkOpened()
        {
            isSpawningEnabled = true;
            Debug.Log("[VisitorManager] Park opened. Visitor spawning enabled.");
        }

        private void HandleParkClosed()
        {
            isSpawningEnabled = false;
            Debug.Log("[VisitorManager] Park closed. Visitor spawning disabled.");
        }

        private void HandleVisitorSelected(int visitorId)
        {
            VisitorAI visitor = FindVisitorById(visitorId);
            if (visitor != null)
            {
                Debug.Log($"[VisitorManager] Visitor selected: {visitor.Profile.VisitorName} " +
                          $"(ID:{visitorId}, {visitor.Type}, {visitor.CurrentState}) {visitor.Parameters}");
            }
        }

        private void HandleWeatherChanged(Weather newWeather)
        {
            // 天候変化時のログ（スポーンレートは自動で反映される）
            Debug.Log($"[VisitorManager] Weather changed to {newWeather}. Spawn interval will be recalculated.");
        }

        // ---- ユーティリティ ----

        /// <summary>
        /// パークの知名度を取得する（0-100）。
        /// ParkManagerのAPIが実装されるまでのスタブ。
        /// </summary>
        private float GetParkFame()
        {
            if (GameManager.Instance != null && GameManager.Instance.ParkManager != null)
            {
                return GameManager.Instance.ParkManager.GetOverallRating();
            }
            return 50f;
        }

        /// <summary>
        /// 天候によるスポーン倍率。
        /// 【ゲームデザイン】雨天は客足が半減、晴天は1.2倍。
        /// </summary>
        private float GetWeatherSpawnMultiplier()
        {
            Weather weather = GetCurrentWeather();
            switch (weather)
            {
                case Weather.Sunny:  return 0.9f;   // 普通（やや多い）
                case Weather.Cloudy: return 1.0f;    // 基準
                case Weather.Hot:    return 1.1f;    // やや少ない（暑い）
                case Weather.Rainy:  return 2.0f;    // 客足半減（間隔倍増）
                case Weather.Snowy:  return 2.5f;    // 大幅減
                default: return 1.0f;
            }
        }

        /// <summary>現在の天候を取得する</summary>
        private Weather GetCurrentWeather()
        {
            if (GameManager.Instance != null && GameManager.Instance.WeatherSystem != null)
            {
                return GameManager.Instance.WeatherSystem.CurrentWeather;
            }
            return Weather.Sunny;
        }

        // ---- デバッグ ----

        private void OnGUI()
        {
            if (!enableDebugStats) return;
            if (!Application.isPlaying) return;

            GUILayout.BeginArea(new Rect(10, 10, 300, 200));
            GUILayout.Label($"=== Visitor Stats ===");
            GUILayout.Label($"Active: {ActiveVisitorCount} / {maxVisitors}");
            GUILayout.Label($"Today: {totalVisitorsToday}");
            GUILayout.Label($"Peak: {peakVisitorCount}");
            GUILayout.Label($"Avg Happiness: {AverageHappiness:F1}");
            GUILayout.Label($"Avg Exit Happiness: {AverageExitHappiness:F1}");
            GUILayout.Label($"Pool: {visitorPool.Count}");
            GUILayout.Label($"Spawn Interval: {CalculateSpawnInterval():F1}s");
            GUILayout.EndArea();
        }
    }
}
