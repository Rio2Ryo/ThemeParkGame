// ParkTransportSystem.cs
// パーク内交通システム - モノレール、パークトレイン、シャトルの運行管理

using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using ThemeParkGame.Core;

namespace ThemeParkGame.Park
{
    [Serializable]
    public class TransportRoute
    {
        public int RouteId;
        public string DisplayName;
        public TransportType Type;
        public List<ThemeZone> Stops;
        public int Capacity;
        public float TripDuration;
        public float Frequency;
        public float TicketPrice;
        public float BuildCost;
        public float DailyMaintenanceCost;
        public bool IsOperating;
        public float CurrentPosition;
        public int CurrentPassengers;
        public int TotalPassengersToday;
        public bool IsBuilt;
        public float TimeSinceLastDeparture;
        public int CurrentStopIndex;

        public TransportRoute()
        {
            Stops = new List<ThemeZone>();
            IsOperating = false;
            IsBuilt = false;
            CurrentPosition = 0f;
            CurrentPassengers = 0;
            TotalPassengersToday = 0;
            TimeSinceLastDeparture = 0f;
            CurrentStopIndex = 0;
        }

        public bool ServesZone(ThemeZone zone) => Stops.Contains(zone);

        public int GetSegmentCount(ThemeZone from, ThemeZone to)
        {
            int fi = Stops.IndexOf(from);
            int ti = Stops.IndexOf(to);
            if (fi < 0 || ti < 0) return -1;
            return ti > fi ? ti - fi : (Stops.Count - fi) + ti;
        }
    }

    [Serializable]
    public class TransportStation
    {
        public int StationId;
        public ThemeZone Zone;
        public int WaitingPassengers;
        public float AverageWaitTime;
        private float _totalWaitTime;
        private int _totalServedPassengers;

        public TransportStation(int stationId, ThemeZone zone)
        {
            StationId = stationId;
            Zone = zone;
            WaitingPassengers = 0;
            AverageWaitTime = 0f;
            _totalWaitTime = 0f;
            _totalServedPassengers = 0;
        }

        public void AddWaitingPassengers(int count) => WaitingPassengers += count;

        public void RecordBoarding(int boardedCount, float waitTime)
        {
            WaitingPassengers = Mathf.Max(0, WaitingPassengers - boardedCount);
            _totalWaitTime += waitTime * boardedCount;
            _totalServedPassengers += boardedCount;
            if (_totalServedPassengers > 0)
                AverageWaitTime = _totalWaitTime / _totalServedPassengers;
        }

        public void ResetDaily()
        {
            _totalWaitTime = 0f;
            _totalServedPassengers = 0;
            AverageWaitTime = 0f;
        }
    }

    /// <summary>
    /// パーク内交通システム
    /// モノレール、パークトレイン、シャトルの運行を一元管理するシングルトン
    /// </summary>
    public class ParkTransportSystem : MonoBehaviour
    {
        public static ParkTransportSystem Instance { get; private set; }

        // イベント
        public Action<int, ThemeZone> OnTransportDeparted;
        public Action<int, ThemeZone> OnTransportArrived;
        public Action<int> OnRouteBuilt;

        // 内部データ
        [SerializeField] private List<TransportRoute> _routes = new List<TransportRoute>();
        private Dictionary<ThemeZone, TransportStation> _stations = new Dictionary<ThemeZone, TransportStation>();
        public float TotalRevenueToday { get; private set; }

        private float _passengerGenerationTimer;
        private const float PASSENGER_GEN_INTERVAL = 10f;
        private const int BASE_PASSENGERS_PER_ZONE = 2;
        private const float MAINTENANCE_COST_RATIO = 0.02f;

        // ゾーン一覧（初期化・ステーション設置用）
        private static readonly ThemeZone[] AllZones = {
            ThemeZone.LostKingdom, ThemeZone.HalloweenWorld,
            ThemeZone.Wonderland, ThemeZone.SpaceZone, ThemeZone.FutureCity
        };

        // ===================
        // ライフサイクル
        // ===================

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            WebGLOptimizer.LogVerbose("[交通システム] ParkTransportSystem を初期化しました");
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
            GameEvents.OnDayChanged -= HandleDayChanged;
            WebGLOptimizer.LogVerbose("[交通システム] ParkTransportSystem を破棄しました");
        }

        private void OnEnable() => GameEvents.OnDayChanged += HandleDayChanged;
        private void OnDisable() => GameEvents.OnDayChanged -= HandleDayChanged;

        // ===================
        // 初期化
        // ===================

        public void Initialize()
        {
            WebGLOptimizer.LogVerbose("[交通システム] 初期化開始");
            _routes.Clear();
            _stations.Clear();
            TotalRevenueToday = 0f;
            _passengerGenerationTimer = 0f;

            SetupDefaultRoutes();
            SetupStations();

            WebGLOptimizer.LogVerbose("[交通システム] デフォルトルート3件、ステーション5件を構成しました");
            NotificationSystem.Instance?.Notify(
                "交通システムが初期化されました。ルートを建設してパーク内の移動を便利にしましょう！",
                NotifLevel.Info);
        }

        private void SetupDefaultRoutes()
        {
            // ルート1: パークモノレール（全5ゾーン周回）
            _routes.Add(new TransportRoute
            {
                RouteId = 1,
                DisplayName = "パークモノレール",
                Type = TransportType.Monorail,
                Stops = new List<ThemeZone>(AllZones),
                Capacity = 30,
                TripDuration = 120f,
                Frequency = 60f,
                TicketPrice = 15f,
                BuildCost = 20000f,
                DailyMaintenanceCost = 20000f * MAINTENANCE_COST_RATIO
            });

            // ルート2: おさんぽトレイン（LostKingdom→Wonderland→HalloweenWorld→LostKingdom）
            _routes.Add(new TransportRoute
            {
                RouteId = 2,
                DisplayName = "おさんぽトレイン",
                Type = TransportType.ParkTrain,
                Stops = new List<ThemeZone> {
                    ThemeZone.LostKingdom, ThemeZone.Wonderland, ThemeZone.HalloweenWorld
                },
                Capacity = 40,
                TripDuration = 180f,
                Frequency = 90f,
                TicketPrice = 8f,
                BuildCost = 10000f,
                DailyMaintenanceCost = 10000f * MAINTENANCE_COST_RATIO
            });

            // ルート3: フューチャーシャトル（FutureCity↔SpaceZone）
            _routes.Add(new TransportRoute
            {
                RouteId = 3,
                DisplayName = "フューチャーシャトル",
                Type = TransportType.Shuttle,
                Stops = new List<ThemeZone> {
                    ThemeZone.FutureCity, ThemeZone.SpaceZone
                },
                Capacity = 20,
                TripDuration = 60f,
                Frequency = 45f,
                TicketPrice = 10f,
                BuildCost = 8000f,
                DailyMaintenanceCost = 8000f * MAINTENANCE_COST_RATIO
            });
        }

        private void SetupStations()
        {
            for (int i = 0; i < AllZones.Length; i++)
                _stations[AllZones[i]] = new TransportStation(i + 1, AllZones[i]);
        }

        // ===================
        // ルート建設・撤去・運行操作
        // ===================

        public bool BuildRoute(int routeId)
        {
            TransportRoute route = GetRouteById(routeId);
            if (route == null)
            {
                WebGLOptimizer.LogVerbose($"[交通システム] ルートID {routeId} が見つかりません");
                return false;
            }
            if (route.IsBuilt)
            {
                NotificationSystem.Instance?.Notify(
                    $"{route.DisplayName}は既に建設済みです。", NotifLevel.Warning);
                return false;
            }

            var econ = GameManager.Instance?.EconomyManager;
            if (econ == null)
            {
                WebGLOptimizer.LogVerbose("[交通システム] EconomyManager が利用できません");
                return false;
            }
            if (!econ.CanAfford(route.BuildCost))
            {
                NotificationSystem.Instance?.Notify(
                    $"{route.DisplayName}の建設資金が不足しています。必要額: ${route.BuildCost:N0}",
                    NotifLevel.Warning);
                return false;
            }

            econ.PayExpense(route.BuildCost, Economy.ExpenseCategory.Other);
            route.IsBuilt = true;
            route.IsOperating = true;
            route.CurrentPosition = 0f;
            route.CurrentPassengers = 0;
            route.TimeSinceLastDeparture = 0f;
            route.CurrentStopIndex = 0;

            OnRouteBuilt?.Invoke(routeId);

            string zoneNames = string.Join("→", route.Stops.Select(z => GetZoneDisplayName(z)));
            NotificationSystem.Instance?.Notify(
                $"{route.DisplayName}が開通しました！ ルート: {zoneNames}", NotifLevel.Info);
            WebGLOptimizer.LogVerbose(
                $"[交通システム] ルート '{route.DisplayName}' (ID:{routeId}) を建設。費用: ${route.BuildCost:N0}");
            return true;
        }

        public bool DemolishRoute(int routeId)
        {
            TransportRoute route = GetRouteById(routeId);
            if (route == null)
            {
                WebGLOptimizer.LogVerbose($"[交通システム] ルートID {routeId} が見つかりません");
                return false;
            }
            if (!route.IsBuilt)
            {
                NotificationSystem.Instance?.Notify(
                    $"{route.DisplayName}は建設されていません。", NotifLevel.Warning);
                return false;
            }

            route.IsBuilt = false;
            route.IsOperating = false;
            route.CurrentPosition = 0f;
            route.CurrentPassengers = 0;
            route.CurrentStopIndex = 0;
            route.TimeSinceLastDeparture = 0f;

            NotificationSystem.Instance?.Notify(
                $"{route.DisplayName}を撤去しました。", NotifLevel.Info);
            WebGLOptimizer.LogVerbose(
                $"[交通システム] ルート '{route.DisplayName}' (ID:{routeId}) を撤去しました");
            return true;
        }

        public void ToggleOperation(int routeId)
        {
            TransportRoute route = GetRouteById(routeId);
            if (route == null)
            {
                WebGLOptimizer.LogVerbose($"[交通システム] ルートID {routeId} が見つかりません");
                return;
            }
            if (!route.IsBuilt)
            {
                NotificationSystem.Instance?.Notify(
                    $"{route.DisplayName}は未建設のため運行切替できません。", NotifLevel.Warning);
                return;
            }

            route.IsOperating = !route.IsOperating;
            string status = route.IsOperating ? "運行開始" : "運行停止";
            NotificationSystem.Instance?.Notify($"{route.DisplayName}: {status}", NotifLevel.Info);
            WebGLOptimizer.LogVerbose(
                $"[交通システム] ルート '{route.DisplayName}' (ID:{routeId}) {status}");
        }

        // ===================
        // メインループ
        // ===================

        private void Update()
        {
            float dt = Time.deltaTime;
            UpdatePassengerGeneration(dt);

            for (int i = 0; i < _routes.Count; i++)
            {
                TransportRoute route = _routes[i];
                if (route.IsBuilt && route.IsOperating)
                    UpdateRouteProgress(route, dt);
            }
        }

        private void UpdatePassengerGeneration(float dt)
        {
            _passengerGenerationTimer += dt;
            if (_passengerGenerationTimer < PASSENGER_GEN_INTERVAL) return;
            _passengerGenerationTimer = 0f;

            foreach (var kvp in _stations)
            {
                bool hasActive = _routes.Any(r => r.IsBuilt && r.IsOperating && r.ServesZone(kvp.Key));
                if (!hasActive) continue;
                int count = UnityEngine.Random.Range(1, BASE_PASSENGERS_PER_ZONE + 1);
                kvp.Value.AddWaitingPassengers(count);
            }
        }

        private void UpdateRouteProgress(TransportRoute route, float dt)
        {
            float prev = route.CurrentPosition;
            route.CurrentPosition += dt / route.TripDuration;
            route.TimeSinceLastDeparture += dt;

            int stopCount = route.Stops.Count;
            if (stopCount < 2) return;

            float seg = 1f / stopCount;
            for (int i = 0; i < stopCount; i++)
            {
                float stopPos = seg * (i + 1);
                if (prev < stopPos && route.CurrentPosition >= stopPos)
                {
                    ThemeZone zone = route.Stops[i];
                    ProcessArrival(route, zone);

                    if (route.TimeSinceLastDeparture >= route.Frequency)
                    {
                        ProcessDeparture(route, zone);
                        route.TimeSinceLastDeparture = 0f;
                    }
                    route.CurrentStopIndex = (i + 1) % stopCount;
                }
            }

            if (route.CurrentPosition >= 1f)
            {
                route.CurrentPosition -= 1f;
                WebGLOptimizer.LogVerbose(
                    $"[交通システム] ルート '{route.DisplayName}' が1周完了しました");
            }
        }

        // ===================
        // 乗降処理
        // ===================

        public void ProcessDeparture(TransportRoute route, ThemeZone fromZone)
        {
            if (!_stations.ContainsKey(fromZone)) return;
            TransportStation station = _stations[fromZone];
            int seats = route.Capacity - route.CurrentPassengers;

            if (seats <= 0 || station.WaitingPassengers <= 0)
            {
                OnTransportDeparted?.Invoke(route.RouteId, fromZone);
                return;
            }

            int boarding = Mathf.Min(seats, station.WaitingPassengers);
            float wait = station.AverageWaitTime > 0f ? station.AverageWaitTime : route.Frequency * 0.5f;
            station.RecordBoarding(boarding, wait);

            route.CurrentPassengers += boarding;
            route.TotalPassengersToday += boarding;

            float fare = boarding * route.TicketPrice;
            TotalRevenueToday += fare;

            var econ = GameManager.Instance?.EconomyManager;
            econ?.AddRevenue(fare, Economy.RevenueCategory.Other);

            OnTransportDeparted?.Invoke(route.RouteId, fromZone);
            WebGLOptimizer.LogVerbose(
                $"[交通システム] {route.DisplayName} が {GetZoneDisplayName(fromZone)} を出発。" +
                $"乗車: {boarding}名, 運賃合計: ${fare:N0}");
        }

        public void ProcessArrival(TransportRoute route, ThemeZone atZone)
        {
            if (route.CurrentPassengers <= 0)
            {
                OnTransportArrived?.Invoke(route.RouteId, atZone);
                return;
            }

            float ratio = route.Stops.Count <= 2 ? 0.5f : (1f / route.Stops.Count + 0.1f);
            int disembark = Mathf.Clamp(
                Mathf.FloorToInt(route.CurrentPassengers * ratio), 1, route.CurrentPassengers);
            route.CurrentPassengers -= disembark;

            float bonus = GetSatisfactionBonus(route.Type);
            if (bonus > 0f && disembark > 0)
            {
                GameEvents.OnSatisfactionBonusApplied?.Invoke(atZone, bonus * disembark * 0.1f);
                WebGLOptimizer.LogVerbose(
                    $"[交通システム] {GetZoneDisplayName(atZone)} に満足度ボーナス +{bonus:F1} 適用 ({disembark}名降車)");
            }

            OnTransportArrived?.Invoke(route.RouteId, atZone);
            WebGLOptimizer.LogVerbose(
                $"[交通システム] {route.DisplayName} が {GetZoneDisplayName(atZone)} に到着。降車: {disembark}名");
        }

        // ===================
        // 照会メソッド
        // ===================

        public float GetWaitTime(ThemeZone zone)
        {
            if (!_stations.ContainsKey(zone)) return float.MaxValue;

            float minFreq = float.MaxValue;
            bool hasActive = false;
            foreach (var route in _routes)
            {
                if (!route.IsBuilt || !route.IsOperating || !route.ServesZone(zone)) continue;
                hasActive = true;
                if (route.Frequency < minFreq) minFreq = route.Frequency;
            }
            if (!hasActive) return float.MaxValue;

            float est = minFreq * 0.5f;
            TransportStation station = _stations[zone];
            if (station.AverageWaitTime > 0f)
                est = (est + station.AverageWaitTime) * 0.5f;
            return est;
        }

        public bool CanRideTransport(ThemeZone fromZone)
        {
            foreach (var route in _routes)
                if (route.IsBuilt && route.IsOperating && route.ServesZone(fromZone))
                    return true;
            return false;
        }

        public TransportRoute GetBestRoute(ThemeZone from, ThemeZone to)
        {
            if (from == to) return null;
            TransportRoute best = null;
            int bestSeg = int.MaxValue;

            foreach (var route in _routes)
            {
                if (!route.IsBuilt || !route.IsOperating) continue;
                if (!route.ServesZone(from) || !route.ServesZone(to)) continue;
                int seg = route.GetSegmentCount(from, to);
                if (seg > 0 && seg < bestSeg) { bestSeg = seg; best = route; }
            }
            return best;
        }

        public float GetSatisfactionBonus(TransportType type)
        {
            switch (type)
            {
                case TransportType.Monorail:  return 8f;
                case TransportType.ParkTrain: return 5f;
                case TransportType.Shuttle:   return 3f;
                default:                      return 0f;
            }
        }

        public List<TransportRoute> GetBuiltRoutes()
        {
            return _routes.Where(r => r.IsBuilt).ToList();
        }

        public List<TransportRoute> GetAvailableRoutes()
        {
            return _routes.Where(r => !r.IsBuilt).ToList();
        }

        public string GetTransportDisplayName(TransportType type)
        {
            switch (type)
            {
                case TransportType.Monorail:  return "モノレール";
                case TransportType.ParkTrain: return "パークトレイン";
                case TransportType.Shuttle:   return "シャトル";
                default:                      return "不明な交通手段";
            }
        }

        public int GetTotalDailyPassengers()
        {
            int total = 0;
            foreach (var route in _routes)
                if (route.IsBuilt) total += route.TotalPassengersToday;
            return total;
        }

        public float GetTotalDailyRevenue() => TotalRevenueToday;

        // ===================
        // 日次処理
        // ===================

        public void ProcessDailyMaintenance()
        {
            float totalCost = 0f;
            int opCount = 0;
            foreach (var route in _routes)
            {
                if (!route.IsBuilt || !route.IsOperating) continue;
                totalCost += route.DailyMaintenanceCost;
                opCount++;
            }

            if (totalCost > 0f)
            {
                var econ = GameManager.Instance?.EconomyManager;
                econ?.PayExpense(totalCost, Economy.ExpenseCategory.Other);
                WebGLOptimizer.LogVerbose(
                    $"[交通システム] 日次メンテナンス費用: ${totalCost:N0} ({opCount}ルート運行中)");
            }

            int passengers = GetTotalDailyPassengers();
            float revenue = GetTotalDailyRevenue();
            if (passengers > 0 || opCount > 0)
            {
                NotificationSystem.Instance?.Notify(
                    $"交通システム日次報告: 乗客数 {passengers}名 / 収入 ${revenue:N0} / 維持費 ${totalCost:N0}",
                    NotifLevel.Info);
            }

            ResetDailyStatistics();
        }

        private void ResetDailyStatistics()
        {
            TotalRevenueToday = 0f;
            foreach (var route in _routes) route.TotalPassengersToday = 0;
            foreach (var kvp in _stations) kvp.Value.ResetDaily();
            WebGLOptimizer.LogVerbose("[交通システム] 日次統計をリセットしました");
        }

        private void HandleDayChanged() => ProcessDailyMaintenance();

        // ===================
        // ユーティリティ
        // ===================

        private TransportRoute GetRouteById(int routeId)
        {
            foreach (var route in _routes)
                if (route.RouteId == routeId) return route;
            return null;
        }

        /// <summary>運行中のルートが存在するかどうか</summary>
        public bool HasAvailableRoute()
        {
            foreach (var route in _routes)
                if (route.IsBuilt && route.IsOperating) return true;
            return false;
        }

        /// <summary>最安運賃を返す</summary>
        public float GetCheapestFare()
        {
            float cheapest = float.MaxValue;
            foreach (var route in _routes)
            {
                if (route.IsBuilt && route.IsOperating && route.TicketPrice < cheapest)
                    cheapest = route.TicketPrice;
            }
            return cheapest == float.MaxValue ? 0f : cheapest;
        }

        /// <summary>乗客到着時のコールバック（VisitorAIから呼ばれる）</summary>
        public void OnPassengerArrived(int visitorId)
        {
            WebGLOptimizer.LogVerbose($"[交通システム] 乗客 {visitorId} が目的地に到着");
        }

        private string GetZoneDisplayName(ThemeZone zone)
        {
            switch (zone)
            {
                case ThemeZone.LostKingdom:   return "ロストキングダム";
                case ThemeZone.HalloweenWorld: return "ハロウィンワールド";
                case ThemeZone.Wonderland:     return "ワンダーランド";
                case ThemeZone.SpaceZone:      return "スペースゾーン";
                case ThemeZone.FutureCity:     return "フューチャーシティ";
                default:                       return "不明なゾーン";
            }
        }
    }
}
