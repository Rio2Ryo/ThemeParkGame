// ============================================================
// ThemeParkGame - RuntimeGameSetup
// ゲーム開始後にランタイムでゲームワールドを構築する
// プレハブ/シーン配置なしで動作するWebGL対応セットアップ
// ============================================================

using UnityEngine;
using UnityEngine.AI;
using ThemeParkGame.Attraction;
using ThemeParkGame.Park;
using ThemeParkGame.Visitor;
using ThemeParkGame.Staff;

namespace ThemeParkGame.Core
{
    /// <summary>
    /// ゲーム開始後にランタイムでアトラクション・来場者・施設を生成する。
    /// Unityエディタでのプレハブ設定なしでWebGLビルドが動作するようにする。
    /// </summary>
    public class RuntimeGameSetup : MonoBehaviour
    {
        private bool _isSetUp;

        private void OnEnable()
        {
            GameEvents.OnParkClosed += HandleParkClosed;
        }

        private void OnDisable()
        {
            GameEvents.OnParkClosed -= HandleParkClosed;
        }

        /// <summary>パーク閉園時にワールドをリセットして再構築可能にする</summary>
        private void HandleParkClosed()
        {
            if (!_isSetUp) return;

            WebGLOptimizer.LogVerbose("[RuntimeGameSetup] パーク閉園 → ワールドクリーンアップ");
            CleanupGameWorld();
            _isSetUp = false;
        }

        /// <summary>既存のゲームワールドオブジェクトを破棄する</summary>
        private void CleanupGameWorld()
        {
            // 名前ベースで親オブジェクトを検索して破棄
            string[] rootNames = {
                "--- Attractions ---", "--- Shops ---", "--- Facilities ---",
                "SpawnPoint", "ExitPoint", "PathwaySystem",
                "WeatherEffectController"
            };
            foreach (var name in rootNames)
            {
                var go = GameObject.Find(name);
                if (go != null) Destroy(go);
            }

            // RuntimeHUDは残す（StartScreenで再利用される可能性があるため）
            // NavMeshSurfaceはGroundに付いているのでGroundは破棄しない

            // AlertMonitorのアラートをクリア
            if (AlertMonitor.Instance != null)
                AlertMonitor.Instance.ClearAllAlerts();
        }

        private void Update()
        {
            if (_isSetUp) return;
            if (GameManager.Instance == null) return;
            if (GameManager.Instance.CurrentState != GameState.Playing) return;

            _isSetUp = true;
            SetupGameWorld();
        }

        private void SetupGameWorld()
        {
            WebGLOptimizer.LogVerbose("[RuntimeGameSetup] === ゲームワールド構築開始 ===");

            // スポーン/出口ポイント作成
            Transform spawnPoint = CreateMarker("SpawnPoint", new Vector3(0f, 0f, -5f));
            Transform exitPoint = CreateMarker("ExitPoint", new Vector3(0f, 0f, -8f), "ParkExit");

            // セーブデータから建物を復元するか、サンプルを新規生成するか
            if (SaveSystem.PendingBuildingsToRestore != null &&
                SaveSystem.PendingBuildingsToRestore.Count > 0)
            {
                RestoreSavedBuildings(SaveSystem.PendingBuildingsToRestore);
                SaveSystem.PendingBuildingsToRestore = null; // 一度復元したらクリア
            }
            else
            {
                // サンプルアトラクション生成（5基）
                CreateSampleAttractions();

                // サンプルショップ生成
                CreateSampleShops();

                // サンプルトイレ・ベンチ・スタッフルーム
                CreateSampleFacilities();
            }

            // 通路ネットワーク生成（NavMesh Bake前に配置）
            CreatePathwayNetwork(spawnPoint.position);

            // NavMeshをランタイムで構築（全施設配置後、スタッフ/Visitor配置前にBake）
            BakeNavMesh();

            // スタッフ配置（NavMesh上に配置するため、Bake後に実行）
            SetupStaff();

            // VisitorManagerの設定
            SetupVisitorManager(spawnPoint, exitPoint);

            // RuntimeHUDの追加
            if (FindObjectOfType<RuntimeHUD>() == null)
            {
                gameObject.AddComponent<RuntimeHUD>();
            }

            // 天候エフェクトコントローラーの追加
            if (FindObjectOfType<WeatherEffectController>() == null)
            {
                var weatherFx = new GameObject("WeatherEffectController");
                weatherFx.AddComponent<WeatherEffectController>();
                WebGLOptimizer.LogVerbose("[RuntimeGameSetup] WeatherEffectController を生成");
            }

            WebGLOptimizer.LogVerbose("[RuntimeGameSetup] === ゲームワールド構築完了 ===");
        }

        // ================================================================
        // セーブデータからの建物復元
        // ================================================================

        private void RestoreSavedBuildings(System.Collections.Generic.List<SavedBuilding> buildings)
        {
            var attrParent = new GameObject("--- Attractions ---").transform;
            var shopParent = new GameObject("--- Shops ---").transform;
            var facParent  = new GameObject("--- Facilities ---").transform;

            int attrCount = 0, shopCount = 0, facCount = 0;

            foreach (var b in buildings)
            {
                Vector3 pos = new Vector3(b.PosX, b.PosY, b.PosZ);
                ThemeZone zone = ThemeZone.LostKingdom;
                if (!string.IsNullOrEmpty(b.Zone))
                    System.Enum.TryParse(b.Zone, out zone);

                switch (b.Type)
                {
                    case "Attraction":
                        AttractionCategory cat = AttractionCategory.RideAttraction;
                        if (!string.IsNullOrEmpty(b.Category))
                            System.Enum.TryParse(b.Category, out cat);
                        CreateAttraction(attrParent, b.DataId, cat,
                            b.Excitement, b.NauseaFactor, b.Capacity, b.RideDuration,
                            b.BuildCost, b.TicketPrice, pos);
                        // アップグレード復元
                        if (b.UpgradeLevel > 0)
                        {
                            var attrs = attrParent.GetComponentsInChildren<Attraction.Attraction>();
                            foreach (var a in attrs)
                            {
                                if (a.DisplayName == b.DataId)
                                {
                                    for (int i = 0; i < b.UpgradeLevel; i++)
                                        a.TryUpgrade();
                                }
                            }
                        }
                        attrCount++;
                        break;

                    case "FoodShop":
                        CreateShop(shopParent, b.DataId, FacilityType.FoodShop, pos, "FoodShop");
                        shopCount++;
                        break;
                    case "DrinkShop":
                        CreateShop(shopParent, b.DataId, FacilityType.DrinkShop, pos, "DrinkShop");
                        shopCount++;
                        break;
                    case "SouvenirShop":
                        CreateShop(shopParent, b.DataId, FacilityType.SouvenirShop, pos, "SouvenirShop");
                        shopCount++;
                        break;

                    case "Toilet":
                        CreateSimpleFacility(facParent, b.DataId, "Toilet", pos, new Color(1f, 1f, 1f));
                        facCount++;
                        break;
                    case "Bench":
                        CreateSimpleFacility(facParent, b.DataId, "Bench", pos, new Color(0.6f, 0.4f, 0.2f));
                        facCount++;
                        break;
                    default:
                        CreateSimpleFacility(facParent, b.DataId, "Untagged", pos, new Color(0.5f, 0.5f, 0.5f));
                        facCount++;
                        break;
                }
            }

            // スタッフルームが無い場合は追加
            if (facCount == 0 || !System.Array.Exists(
                buildings.ToArray(), x => x.DataId.Contains("スタッフ")))
            {
                var staffRoom = CreateSimpleFacility(facParent, "スタッフルーム", "Untagged",
                    new Vector3(-25f, 0f, -20f), new Color(0.4f, 0.6f, 0.4f));
                var sm = GameManager.Instance?.StaffManager;
                if (sm != null && staffRoom != null)
                    sm.RegisterStaffRoom(staffRoom.transform);
            }

            WebGLOptimizer.LogVerbose($"[RuntimeGameSetup] セーブデータから復元: アトラクション{attrCount}, ショップ{shopCount}, 施設{facCount}");
        }

        // ================================================================
        // 通路ネットワーク
        // ================================================================

        private void CreatePathwayNetwork(Vector3 entrancePos)
        {
            var pathGo = new GameObject("PathwaySystem");
            var pathSystem = pathGo.AddComponent<PathwaySystem>();

            // 施設の位置を収集
            var attractions = FindObjectsOfType<Attraction.Attraction>();
            var shops = FindObjectsOfType<Shop>();

            // メインストリート: 入口からパーク中央へ
            Vector3 hub = new Vector3(0f, 0f, 5f);
            pathSystem.CreateSegment(entrancePos, hub, 4f);

            // 中央ハブから各アトラクションへ放射状に通路を生成
            foreach (var attr in attractions)
            {
                Vector3 dest = attr.transform.position;
                // 通路の終点をアトラクション手前に設定
                Vector3 dir = (dest - hub).normalized;
                Vector3 pathEnd = dest - dir * 3f;
                pathEnd.y = 0f;

                float dx = Mathf.Abs(pathEnd.x - hub.x);
                float dz = Mathf.Abs(pathEnd.z - hub.z);

                if (dx < 2f || dz < 2f)
                {
                    pathSystem.CreateSegment(hub, pathEnd, 3f);
                }
                else
                {
                    pathSystem.CreateLShapedPath(hub, pathEnd, 3f);
                }
            }

            // ショップへの通路（中央ハブから）
            foreach (var shop in shops)
            {
                Vector3 dest = shop.transform.position;
                Vector3 dir = (dest - hub).normalized;
                Vector3 pathEnd = dest - dir * 2f;
                pathEnd.y = 0f;

                pathSystem.CreateSegment(hub, pathEnd, 2.5f);
            }

            // 外周回遊通路（環状）
            float radius = 20f;
            int sides = 8;
            Vector3[] ring = new Vector3[sides];
            for (int i = 0; i < sides; i++)
            {
                float angle = (float)i / sides * Mathf.PI * 2f;
                ring[i] = new Vector3(
                    Mathf.Cos(angle) * radius,
                    0f,
                    Mathf.Sin(angle) * radius
                );
            }
            for (int i = 0; i < sides; i++)
            {
                int next = (i + 1) % sides;
                pathSystem.CreateSegment(ring[i], ring[next], 2f);
            }

            WebGLOptimizer.LogVerbose($"[RuntimeGameSetup] 通路ネットワーク生成: {pathSystem.Segments.Count}セグメント, 総延長{pathSystem.TotalPathLength:F0}m");
        }

        // ================================================================
        // NavMesh構築
        // ================================================================

        private void BakeNavMesh()
        {
            // GroundオブジェクトにNavMeshSurfaceをアタッチしてBake
            var ground = GameObject.Find("Ground");
            if (ground == null)
            {
                Debug.LogWarning("[RuntimeGameSetup] Groundが見つかりません。NavMesh構築スキップ。");
                return;
            }

            // NavMeshSurfaceが利用可能か試行
            // com.unity.ai.navigation パッケージのNavMeshSurfaceを使用
            try
            {
                var surface = ground.GetComponent<Unity.AI.Navigation.NavMeshSurface>();
                if (surface == null)
                    surface = ground.AddComponent<Unity.AI.Navigation.NavMeshSurface>();

                surface.collectObjects = Unity.AI.Navigation.CollectObjects.All;
                surface.useGeometry = UnityEngine.AI.NavMeshCollectGeometry.PhysicsColliders;
                surface.BuildNavMesh();
                WebGLOptimizer.LogVerbose("[RuntimeGameSetup] NavMesh構築完了");
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"[RuntimeGameSetup] NavMesh構築失敗: {e.Message}");
                // フォールバック: NavMeshなしでも動くようにする
            }
        }

        // ================================================================
        // サンプルアトラクション
        // ================================================================

        private void CreateSampleAttractions()
        {
            var parent = new GameObject("--- Attractions ---").transform;

            CreateAttraction(parent, "ドラゴンコースター",
                AttractionCategory.GForce, 7.5f, 0.3f, 20, 8f,
                8000, 50, new Vector3(15f, 0f, 15f));

            CreateAttraction(parent, "マジカル観覧車",
                AttractionCategory.Observation, 4.0f, 0.05f, 30, 12f,
                6000, 30, new Vector3(-15f, 0f, 15f));

            CreateAttraction(parent, "スピンカップ",
                AttractionCategory.HorizontalRotation, 5.5f, 0.2f, 16, 6f,
                4000, 35, new Vector3(15f, 0f, -15f));

            CreateAttraction(parent, "お化け屋敷ダーク",
                AttractionCategory.ShowAttraction, 6.0f, 0.1f, 12, 10f,
                5500, 40, new Vector3(-15f, 0f, -15f));

            CreateAttraction(parent, "メリーゴーランド",
                AttractionCategory.RideAttraction, 3.5f, 0.02f, 24, 8f,
                3000, 25, new Vector3(0f, 0f, 25f));

            WebGLOptimizer.LogVerbose("[RuntimeGameSetup] サンプルアトラクション5基を生成");
        }

        private void CreateAttraction(Transform parent, string nameJP,
            AttractionCategory category, float excitement, float nausea,
            int capacity, float rideDuration, int buildCost, int ticketPrice,
            Vector3 position)
        {
            // AttractionData (ScriptableObject) をランタイム作成
            var data = ScriptableObject.CreateInstance<AttractionData>();
            data.AttractionId = nameJP.Replace(" ", "_");
            data.NameJP = nameJP;
            data.NameEN = nameJP;
            data.Category = category;
            data.ExcitementRating = excitement;
            data.NauseaFactor = nausea;
            data.Capacity = capacity;
            data.RideDuration = rideDuration;
            data.BuildCost = buildCost;
            data.SuggestedTicketPrice = ticketPrice;
            data.MaintenanceCost = buildCost / 20;
            data.BaseBreakdownRate = 0.01f;
            data.TimeToMaxBreakdownRate = 600f;
            data.TimeToAccidentAfterBreakdown = 180f;
            data.Size = new Vector2Int(3, 3);

            // GameObjectの作成
            var go = new GameObject(nameJP);
            go.transform.SetParent(parent);
            go.transform.position = position;
            go.tag = "Attraction";
            go.layer = 9; // Facility

            // BoxColliderの追加
            var col = go.AddComponent<BoxCollider>();
            col.size = new Vector3(6f, 4f, 6f);
            col.center = new Vector3(0f, 2f, 0f);

            // ビジュアル: シンプルなキューブ
            var visual = GameObject.CreatePrimitive(PrimitiveType.Cube);
            visual.name = "Visual";
            visual.transform.SetParent(go.transform);
            visual.transform.localPosition = new Vector3(0f, 2f, 0f);
            visual.transform.localScale = new Vector3(5f, 4f, 5f);
            // Colliderは親のBoxColliderを使う
            Object.Destroy(visual.GetComponent<Collider>());

            var renderer = visual.GetComponent<Renderer>();
            if (renderer != null)
            {
                Color color;
                switch (category)
                {
                    case AttractionCategory.GForce:
                        color = new Color(0.9f, 0.2f, 0.2f); break;
                    case AttractionCategory.Observation:
                        color = new Color(0.2f, 0.5f, 0.9f); break;
                    case AttractionCategory.HorizontalRotation:
                        color = new Color(0.9f, 0.7f, 0.2f); break;
                    case AttractionCategory.RideAttraction:
                        color = new Color(0.3f, 0.9f, 0.5f); break;
                    default:
                        color = new Color(0.5f, 0.2f, 0.8f); break;
                }
                var shader = Shader.Find("Standard");
                if (shader == null) shader = Shader.Find("UI/Default");
                if (shader != null)
                    renderer.material = new Material(shader) { color = color };
            }

            // FacilityDirtの追加
            go.AddComponent<FacilityDirt>();

            // Attractionコンポーネント追加とデータ設定
            var attraction = go.AddComponent<Attraction.Attraction>();
            attraction.SetAttractionData(data);
            attraction.TicketPrice = ticketPrice;
            // 待ち行列上限: 定員と同数、最大15人に制限
            attraction.MaxQueueLength = Mathf.Min(capacity, 15);

            // 施設を配置済みとしてマーク（Placeで正のgrid座標を使いつつ、world座標を上書き）
            attraction.FacilityId = go.GetInstanceID();
            int gx = Mathf.Max(0, (int)(position.x + 50f));
            int gz = Mathf.Max(0, (int)(position.z + 50f));
            attraction.Place(new Vector2Int(gx, gz), ThemeZone.LostKingdom);
            go.transform.position = position;

            // 浮遊ラベル
            CreateFloatingLabel(go, nameJP, 5f, Color.white);
        }

        // ================================================================
        // サンプルショップ
        // ================================================================

        private void CreateSampleShops()
        {
            var parent = new GameObject("--- Shops ---").transform;

            CreateShop(parent, "ドラゴンバーガー", FacilityType.FoodShop,
                new Vector3(8f, 0f, 0f), "FoodShop");
            CreateShop(parent, "マジカルジュース", FacilityType.DrinkShop,
                new Vector3(-8f, 0f, 0f), "DrinkShop");
            CreateShop(parent, "おみやげ城", FacilityType.SouvenirShop,
                new Vector3(0f, 0f, -2f), "SouvenirShop");

            WebGLOptimizer.LogVerbose("[RuntimeGameSetup] サンプルショップ3店を生成");
        }

        private void CreateShop(Transform parent, string shopName, FacilityType type,
            Vector3 position, string tag)
        {
            var go = new GameObject(shopName);
            go.transform.SetParent(parent);
            go.transform.position = position;
            go.tag = tag;
            go.layer = 9; // Facility

            var col = go.AddComponent<BoxCollider>();
            col.size = new Vector3(4f, 3f, 4f);
            col.center = new Vector3(0f, 1.5f, 0f);

            // ビジュアル: シンプルなキューブ
            var visual = GameObject.CreatePrimitive(PrimitiveType.Cube);
            visual.name = "Visual";
            visual.transform.SetParent(go.transform);
            visual.transform.localPosition = new Vector3(0f, 1.5f, 0f);
            visual.transform.localScale = new Vector3(3.5f, 3f, 3.5f);
            Object.Destroy(visual.GetComponent<Collider>());

            var renderer = visual.GetComponent<Renderer>();
            if (renderer != null)
            {
                Color color;
                switch (type)
                {
                    case FacilityType.FoodShop:
                        color = new Color(1.0f, 0.6f, 0.2f); break;
                    case FacilityType.DrinkShop:
                        color = new Color(0.2f, 0.8f, 1.0f); break;
                    default:
                        color = new Color(1.0f, 0.4f, 0.8f); break;
                }
                var shader = Shader.Find("Standard");
                if (shader == null) shader = Shader.Find("UI/Default");
                if (shader != null)
                    renderer.material = new Material(shader) { color = color };
            }

            go.AddComponent<FacilityDirt>();

            var shop = go.AddComponent<Shop>();
            shop.FacilityId = go.GetInstanceID();

            // ShopTypeの変換
            ShopType sType;
            switch (type)
            {
                case FacilityType.DrinkShop: sType = ShopType.DrinkShop; break;
                case FacilityType.SouvenirShop: sType = ShopType.SouvenirShop; break;
                default: sType = ShopType.FoodShop; break;
            }

            shop.ConfigureRuntime(
                shopName,
                sType,
                type == FacilityType.FoodShop ? 5 : (type == FacilityType.DrinkShop ? 3 : 8),
                type == FacilityType.FoodShop ? 10 : (type == FacilityType.DrinkShop ? 6 : 20),
                100
            );

            int gx = Mathf.Max(0, (int)(position.x + 50f));
            int gz = Mathf.Max(0, (int)(position.z + 50f));
            shop.Place(new Vector2Int(gx, gz), ThemeZone.LostKingdom);
            go.transform.position = position;

            // 浮遊ラベル
            CreateFloatingLabel(go, shopName, 4f, new Color(1f, 0.9f, 0.5f));
        }

        // ================================================================
        // サンプル施設（トイレ・ベンチ）
        // ================================================================

        private void CreateSampleFacilities()
        {
            var parent = new GameObject("--- Facilities ---").transform;

            // トイレ
            CreateSimpleFacility(parent, "トイレA", "Toilet",
                new Vector3(20f, 0f, 0f), new Color(1f, 1f, 1f));
            CreateSimpleFacility(parent, "トイレB", "Toilet",
                new Vector3(-20f, 0f, 0f), new Color(1f, 1f, 1f));

            // ベンチ
            for (int i = 0; i < 6; i++)
            {
                float angle = i * 60f * Mathf.Deg2Rad;
                Vector3 pos = new Vector3(Mathf.Cos(angle) * 12f, 0f, Mathf.Sin(angle) * 12f);
                CreateSimpleFacility(parent, $"ベンチ{i + 1}", "Bench", pos, new Color(0.6f, 0.4f, 0.2f));
            }

            // スタッフルーム
            var staffRoom = CreateSimpleFacility(parent, "スタッフルーム", "Untagged",
                new Vector3(-25f, 0f, -20f), new Color(0.4f, 0.6f, 0.4f));
            // StaffManagerにスタッフルームを登録
            var sm = GameManager.Instance?.StaffManager;
            if (sm != null && staffRoom != null)
            {
                sm.RegisterStaffRoom(staffRoom.transform);
            }

            WebGLOptimizer.LogVerbose("[RuntimeGameSetup] サンプル施設（トイレ2、ベンチ6、スタッフルーム1）を生成");
        }

        private GameObject CreateSimpleFacility(Transform parent, string facilityName,
            string tag, Vector3 position, Color color)
        {
            var go = new GameObject(facilityName);
            go.transform.SetParent(parent);
            go.transform.position = position;
            go.tag = tag;
            go.layer = 9; // Facility

            var col = go.AddComponent<BoxCollider>();
            col.size = tag == "Toilet"
                ? new Vector3(3f, 3f, 3f)
                : new Vector3(2f, 1f, 1f);
            col.center = new Vector3(0f, col.size.y * 0.5f, 0f);

            // ビジュアル
            var visual = GameObject.CreatePrimitive(
                tag == "Toilet" ? PrimitiveType.Cube : PrimitiveType.Cube);
            visual.name = "Visual";
            visual.transform.SetParent(go.transform);
            visual.transform.localPosition = col.center;
            visual.transform.localScale = col.size * 0.9f;
            Object.Destroy(visual.GetComponent<Collider>());

            var renderer = visual.GetComponent<Renderer>();
            if (renderer != null)
            {
                var shader = Shader.Find("Standard");
                if (shader == null) shader = Shader.Find("UI/Default");
                if (shader != null)
                    renderer.material = new Material(shader) { color = color };
            }

            // 浮遊ラベル
            float labelH = tag == "Toilet" ? 4f : 2f;
            CreateFloatingLabel(go, facilityName, labelH, new Color(0.8f, 0.9f, 1f));

            return go;
        }

        // ================================================================
        // スタッフ配置
        // ================================================================

        private void SetupStaff()
        {
            var sm = GameManager.Instance?.StaffManager;
            if (sm == null)
            {
                Debug.LogWarning("[RuntimeGameSetup] StaffManager未検出");
                return;
            }

            // ランタイムでスタッフプレハブを作成してStaffManagerに注入
            var mechanicPrefab = CreateStaffPrefab<MechanicStaff>("MechanicPrefab", new Color(1f, 0.5f, 0f));
            var cleanerPrefab = CreateStaffPrefab<CleanerStaff>("CleanerPrefab", new Color(0.2f, 0.9f, 0.2f));
            var entertainerPrefab = CreateStaffPrefab<EntertainerStaff>("EntertainerPrefab", new Color(0.9f, 0.2f, 0.9f));
            var guardPrefab = CreateStaffPrefab<GuardStaff>("GuardPrefab", new Color(0.2f, 0.2f, 0.8f));
            var scientistPrefab = CreateStaffPrefab<ScientistStaff>("ScientistPrefab", new Color(1f, 1f, 0.3f));

            sm.ConfigureRuntimePrefabs(mechanicPrefab, cleanerPrefab,
                entertainerPrefab, guardPrefab, scientistPrefab);

            // セーブデータからスタッフを復元するか、デフォルト配置するか
            if (SaveSystem.PendingStaffToRestore != null &&
                SaveSystem.PendingStaffToRestore.Count > 0)
            {
                RestoreSavedStaff(sm, SaveSystem.PendingStaffToRestore);
                SaveSystem.PendingStaffToRestore = null;
            }
            else
            {
                HireDefaultStaff(sm);
            }

            // パトロールエリア未割り当てスタッフにデフォルト範囲を設定
            Bounds parkBounds = new Bounds(Vector3.zero, new Vector3(60f, 10f, 60f));
            foreach (var staff in sm.GetAllStaff())
            {
                if (!staff.HasPatrolArea)
                {
                    staff.SetPatrolArea(parkBounds);
                }
            }

            WebGLOptimizer.LogVerbose($"[RuntimeGameSetup] スタッフ{sm.TotalStaffCount}名を配置");
        }

        /// <summary>デフォルトのスタッフ配置（新規ゲーム用）</summary>
        private void HireDefaultStaff(StaffManager sm)
        {
            var attractions = FindObjectsOfType<Attraction.Attraction>();

            if (attractions.Length >= 2)
            {
                sm.HireStaff(StaffType.Mechanic,
                    attractions[0].transform.position + new Vector3(3f, 0f, 0f), "メカニック太郎");
                sm.HireStaff(StaffType.Mechanic,
                    attractions[2 % attractions.Length].transform.position + new Vector3(3f, 0f, 0f), "メカニック次郎");
            }

            sm.HireStaff(StaffType.Cleaner, new Vector3(5f, 0f, 5f), "クリーナーA");
            sm.HireStaff(StaffType.Cleaner, new Vector3(-5f, 0f, -5f), "クリーナーB");
            sm.HireStaff(StaffType.Entertainer, new Vector3(0f, 0f, -3f), "パフォーマー花子");
            sm.HireStaff(StaffType.Guard, new Vector3(0f, 0f, 10f), "ガードマン一号");
        }

        /// <summary>セーブデータからスタッフを復元する</summary>
        private void RestoreSavedStaff(StaffManager sm, System.Collections.Generic.List<SavedStaff> staffList)
        {
            foreach (var ss in staffList)
            {
                if (!System.Enum.TryParse<StaffType>(ss.Type, out var staffType))
                    continue;

                Vector3 pos = new Vector3(ss.PosX, ss.PosY, ss.PosZ);
                var staff = sm.HireStaff(staffType, pos, ss.Name);
                if (staff == null) continue;

                // スキルレベル復元（訓練を繰り返す）
                for (int i = 1; i < ss.SkillLevel; i++)
                    staff.Train();

                // 給与復元
                staff.Salary = ss.Salary;

                // パトロールエリア復元
                if (ss.PatrolSizeX > 0f || ss.PatrolSizeZ > 0f)
                {
                    var center = new Vector3(ss.PatrolCenterX, ss.PatrolCenterY, ss.PatrolCenterZ);
                    var size = new Vector3(ss.PatrolSizeX, ss.PatrolSizeY, ss.PatrolSizeZ);
                    staff.SetPatrolArea(new Bounds(center, size));
                }
            }

            WebGLOptimizer.LogVerbose($"[RuntimeGameSetup] セーブデータからスタッフ{staffList.Count}名を復元");
        }

        /// <summary>スタッフプレハブをランタイムで生成する</summary>
        private GameObject CreateStaffPrefab<T>(string prefabName, Color bodyColor) where T : StaffMember
        {
            var prefab = new GameObject(prefabName);
            prefab.SetActive(false);

            // NavMeshAgent
            var agent = prefab.AddComponent<NavMeshAgent>();
            agent.speed = 3.0f;
            agent.angularSpeed = 120f;
            agent.acceleration = 8f;
            agent.stoppingDistance = 1.5f;
            agent.radius = 0.3f;
            agent.height = 1.8f;
            agent.enabled = false;

            // CapsuleCollider
            var col = prefab.AddComponent<CapsuleCollider>();
            col.radius = 0.3f;
            col.height = 1.8f;
            col.center = new Vector3(0f, 0.9f, 0f);

            // StaffMember派生コンポーネント
            prefab.AddComponent<T>();

            // ビジュアル: 少し太めのカプセルでVisitorと区別
            var visual = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            visual.name = "Body";
            visual.transform.SetParent(prefab.transform);
            visual.transform.localPosition = new Vector3(0f, 0.9f, 0f);
            visual.transform.localScale = new Vector3(0.6f, 0.5f, 0.6f);
            Object.Destroy(visual.GetComponent<Collider>());

            var bodyRenderer = visual.GetComponent<Renderer>();
            if (bodyRenderer != null)
            {
                var shader = Shader.Find("Standard");
                if (shader == null) shader = Shader.Find("UI/Default");
                if (shader != null)
                    bodyRenderer.material = new Material(shader) { color = bodyColor };
            }

            // 頭上マーカー: 小さな球で「スタッフ」と分かるようにする
            var marker = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            marker.name = "StaffMarker";
            marker.transform.SetParent(prefab.transform);
            marker.transform.localPosition = new Vector3(0f, 2.0f, 0f);
            marker.transform.localScale = new Vector3(0.25f, 0.25f, 0.25f);
            Object.Destroy(marker.GetComponent<Collider>());

            var markerRenderer = marker.GetComponent<Renderer>();
            if (markerRenderer != null)
            {
                var shader = Shader.Find("Standard");
                if (shader == null) shader = Shader.Find("UI/Default");
                if (shader != null)
                    markerRenderer.material = new Material(shader) { color = Color.white };
            }

            DontDestroyOnLoad(prefab);
            return prefab;
        }

        // ================================================================
        // VisitorManager設定
        // ================================================================

        private void SetupVisitorManager(Transform spawnPoint, Transform exitPoint)
        {
            var vm = GameManager.Instance?.VisitorManager;
            if (vm == null)
            {
                Debug.LogWarning("[RuntimeGameSetup] VisitorManager未検出");
                return;
            }

            // VisitorPrefabをランタイム生成
            GameObject prefab = CreateVisitorPrefab();

            // 公開メソッドで設定
            var difficulty = GameManager.Instance.CurrentDifficulty;
            float spawnInterval = GameManager.GetSpawnInterval(difficulty);
            int maxVis = GameManager.GetMaxVisitors(difficulty);

            vm.ConfigureRuntime(prefab, spawnPoint, exitPoint,
                maxVis: maxVis, poolSize: 10, spawnInterval: spawnInterval);

            // 再初期化
            vm.Initialize();

            WebGLOptimizer.LogVerbose("[RuntimeGameSetup] VisitorManager設定完了");
        }

        /// <summary>来場者プレハブをコードで生成する</summary>
        private GameObject CreateVisitorPrefab()
        {
            var prefab = new GameObject("VisitorPrefab");
            prefab.SetActive(false); // プレハブとして扱う

            // NavMeshAgent
            var agent = prefab.AddComponent<NavMeshAgent>();
            agent.speed = 3.5f;
            agent.angularSpeed = 120f;
            agent.acceleration = 8f;
            agent.stoppingDistance = 1.5f;
            agent.radius = 0.3f;
            agent.height = 1.8f;
            agent.enabled = false; // プール時は無効

            // CapsuleCollider
            var col = prefab.AddComponent<CapsuleCollider>();
            col.radius = 0.3f;
            col.height = 1.8f;
            col.center = new Vector3(0f, 0.9f, 0f);

            // VisitorAI
            prefab.AddComponent<VisitorAI>();

            // ビジュアル: カプセル
            var visual = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            visual.name = "Body";
            visual.transform.SetParent(prefab.transform);
            visual.transform.localPosition = new Vector3(0f, 0.9f, 0f);
            visual.transform.localScale = new Vector3(0.5f, 0.5f, 0.5f);
            Object.Destroy(visual.GetComponent<Collider>());

            var bodyRenderer = visual.GetComponent<Renderer>();
            if (bodyRenderer != null)
            {
                var shader = Shader.Find("Standard");
                if (shader == null) shader = Shader.Find("UI/Default");
                if (shader != null)
                    bodyRenderer.material = new Material(shader) { color = new Color(0.3f, 0.6f, 1.0f) };
            }

            // VisitorVisualController: 状態別プロシージャルアニメ
            var visualCtrl = prefab.AddComponent<VisitorVisualController>();
            visualCtrl.Setup(bodyRenderer);

            // VisitorStateMachine: ライフサイクルFSM + 頭上フェーズマーカー
            var vsm = prefab.AddComponent<VisitorStateMachine>();
            var phaseMarker = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            phaseMarker.name = "PhaseMarker";
            phaseMarker.transform.SetParent(prefab.transform);
            phaseMarker.transform.localPosition = new Vector3(0f, 2.2f, 0f);
            phaseMarker.transform.localScale = new Vector3(0.2f, 0.2f, 0.2f);
            Object.Destroy(phaseMarker.GetComponent<Collider>());

            var pmRenderer = phaseMarker.GetComponent<Renderer>();
            if (pmRenderer != null)
            {
                var pmShader = Shader.Find("Standard");
                if (pmShader == null) pmShader = Shader.Find("UI/Default");
                if (pmShader != null)
                    pmRenderer.material = new Material(pmShader) { color = new Color(0.3f, 0.7f, 1.0f) };
            }
            vsm.SetupHeadMarker(pmRenderer);

            // DontDestroyOnLoad対象にして破棄を防ぐ
            DontDestroyOnLoad(prefab);

            return prefab;
        }

        // ================================================================
        // ユーティリティ
        // ================================================================

        private Transform CreateMarker(string markerName, Vector3 position, string tag = null)
        {
            var go = new GameObject(markerName);
            go.transform.position = position;
            if (!string.IsNullOrEmpty(tag))
            {
                try { go.tag = tag; }
                catch (System.Exception) { /* tag not registered */ }
            }
            return go.transform;
        }

        /// <summary>施設の上部に浮遊する名前ラベルを作成する</summary>
        private void CreateFloatingLabel(GameObject parent, string labelText, float height, Color color)
        {
            var labelGo = new GameObject("Label");
            labelGo.transform.SetParent(parent.transform);
            labelGo.transform.localPosition = new Vector3(0f, height, 0f);

            var tm = labelGo.AddComponent<TextMesh>();
            tm.text = labelText;
            tm.fontSize = 48;
            tm.characterSize = 0.12f;
            tm.anchor = TextAnchor.MiddleCenter;
            tm.alignment = TextAlignment.Center;
            tm.color = color;
            tm.fontStyle = FontStyle.Bold;

            // カメラに向くようにBillboard挙動を追加
            labelGo.AddComponent<FacingCamera>();
        }

    }

    /// <summary>カメラに常に正面を向けるBillboard挙動</summary>
    internal class FacingCamera : MonoBehaviour
    {
        private Transform camTransform;

        private void Start()
        {
            if (Camera.main != null)
                camTransform = Camera.main.transform;
        }

        private void LateUpdate()
        {
            if (camTransform == null)
            {
                if (Camera.main != null)
                    camTransform = Camera.main.transform;
                return;
            }
            transform.rotation = Quaternion.LookRotation(transform.position - camTransform.position);
        }
    }
}
