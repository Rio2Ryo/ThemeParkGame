// ============================================================
// ThemeParkGame - RuntimeGameSetup
// ゲーム開始後にランタイムでゲームワールドを構築する
// プレハブ/シーン配置なしで動作するWebGL対応セットアップ
// ============================================================

using UnityEngine;
using UnityEngine.AI;
using ThemeParkGame.Attraction;
using ThemeParkGame.Visitor;

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

            // NavMeshをランタイムで構築
            BakeNavMesh();

            // スポーン/出口ポイント作成
            Transform spawnPoint = CreateMarker("SpawnPoint", new Vector3(0f, 0f, -5f));
            Transform exitPoint = CreateMarker("ExitPoint", new Vector3(0f, 0f, -8f));

            // サンプルアトラクション生成
            CreateSampleAttractions();

            // サンプルショップ生成
            CreateSampleShops();

            // サンプルトイレ・ベンチ
            CreateSampleFacilities();

            // VisitorManagerの設定
            SetupVisitorManager(spawnPoint, exitPoint);

            // RuntimeHUDの追加
            if (FindObjectOfType<RuntimeHUD>() == null)
            {
                gameObject.AddComponent<RuntimeHUD>();
            }

            Debug.Log("[RuntimeGameSetup] === ゲームワールド構築完了 ===");
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
                AttractionCategory.GForce, 7.5f, 0.3f, 20, 30f,
                8000, 500, new Vector3(15f, 0f, 15f));

            CreateAttraction(parent, "マジカル観覧車",
                AttractionCategory.Observation, 4.0f, 0.05f, 30, 45f,
                6000, 300, new Vector3(-15f, 0f, 15f));

            CreateAttraction(parent, "スピンカップ",
                AttractionCategory.HorizontalRotation, 5.5f, 0.2f, 16, 20f,
                4000, 350, new Vector3(15f, 0f, -15f));

            CreateAttraction(parent, "お化け屋敷ダーク",
                AttractionCategory.ShowAttraction, 6.0f, 0.1f, 12, 25f,
                5500, 400, new Vector3(-15f, 0f, -15f));

            Debug.Log("[RuntimeGameSetup] サンプルアトラクション4基を生成");
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
            attraction.MaxQueueLength = capacity * 2;

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
                new Vector3(0f, 0f, 20f), "SouvenirShop");

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

            Debug.Log("[RuntimeGameSetup] サンプル施設（トイレ2、ベンチ6）を生成");
        }

        private void CreateSimpleFacility(Transform parent, string facilityName,
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
            vm.ConfigureRuntime(prefab, spawnPoint, exitPoint,
                maxVis: 50, poolSize: 10, spawnInterval: 3f);

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

            var renderer = visual.GetComponent<Renderer>();
            if (renderer != null)
            {
                var shader = Shader.Find("Standard");
                if (shader == null) shader = Shader.Find("UI/Default");
                if (shader != null)
                    renderer.material = new Material(shader) { color = new Color(0.3f, 0.6f, 1.0f) };
            }

            // DontDestroyOnLoad対象にして破棄を防ぐ
            DontDestroyOnLoad(prefab);

            return prefab;
        }

        // ================================================================
        // ユーティリティ
        // ================================================================

        private Transform CreateMarker(string markerName, Vector3 position)
        {
            var go = new GameObject(markerName);
            go.transform.position = position;
            return go.transform;
        }

    }
}
