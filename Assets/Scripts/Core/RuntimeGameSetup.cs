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
            Debug.Log("[RuntimeGameSetup] === ゲームワールド構築開始 ===");

            // スポーン/出口ポイント作成
            Transform spawnPoint = CreateMarker("SpawnPoint", new Vector3(0f, 0f, -5f));
            Transform exitPoint = CreateMarker("ExitPoint", new Vector3(0f, 0f, -8f), "ParkExit");

            // サンプルアトラクション生成（5基）
            CreateSampleAttractions();

            // サンプルショップ生成
            CreateSampleShops();

            // サンプルトイレ・ベンチ・スタッフルーム
            CreateSampleFacilities();

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
                Debug.Log("[RuntimeGameSetup] WeatherEffectController を生成");
            }

            Debug.Log("[RuntimeGameSetup] === ゲームワールド構築完了 ===");
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

            Debug.Log($"[RuntimeGameSetup] 通路ネットワーク生成: {pathSystem.Segments.Count}セグメント, 総延長{pathSystem.TotalPathLength:F0}m");
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
                Debug.Log("[RuntimeGameSetup] NavMesh構築完了");
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

            Debug.Log("[RuntimeGameSetup] サンプルアトラクション5基を生成");
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

            Debug.Log("[RuntimeGameSetup] サンプルショップ3店を生成");
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

            Debug.Log("[RuntimeGameSetup] サンプル施設（トイレ2、ベンチ6、スタッフルーム1）を生成");
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

            // アトラクション位置情報を取得してスタッフを近くに配置
            var attractions = FindObjectsOfType<Attraction.Attraction>();

            // メカニック2名: アトラクション近辺に配置
            if (attractions.Length >= 2)
            {
                sm.HireStaff(StaffType.Mechanic,
                    attractions[0].transform.position + new Vector3(3f, 0f, 0f), "メカニック太郎");
                sm.HireStaff(StaffType.Mechanic,
                    attractions[2 % attractions.Length].transform.position + new Vector3(3f, 0f, 0f), "メカニック次郎");
            }

            // クリーナー2名: パーク中央付近
            sm.HireStaff(StaffType.Cleaner, new Vector3(5f, 0f, 5f), "クリーナーA");
            sm.HireStaff(StaffType.Cleaner, new Vector3(-5f, 0f, -5f), "クリーナーB");

            // エンターテイナー1名: 入口付近
            sm.HireStaff(StaffType.Entertainer, new Vector3(0f, 0f, -3f), "パフォーマー花子");

            // ガード1名: パーク中央
            sm.HireStaff(StaffType.Guard, new Vector3(0f, 0f, 10f), "ガードマン一号");

            // スタッフにパトロールエリアを自動割り当て
            // パーク全体 (-30,-30) ~ (30,30) の範囲
            Bounds parkBounds = new Bounds(Vector3.zero, new Vector3(60f, 10f, 60f));
            foreach (var staff in sm.GetAllStaff())
            {
                if (!staff.HasPatrolArea)
                {
                    staff.SetPatrolArea(parkBounds);
                }
            }

            Debug.Log($"[RuntimeGameSetup] スタッフ{sm.TotalStaffCount}名を配置");
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

            Debug.Log("[RuntimeGameSetup] VisitorManager設定完了");
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

    }
}
