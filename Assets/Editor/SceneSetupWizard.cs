// ============================================================
// ThemeParkGame - Scene Setup Wizard
// Unityエディタ拡張: メニューからワンクリックでシーン階層を自動構築
// メニュー: ThemeParkGame > Setup Scene
// ============================================================

#if UNITY_EDITOR
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEditor;
using UnityEditor.SceneManagement;
using TMPro;
using ThemeParkGame.Core;
using ThemeParkGame.UI;
using ThemeParkGame.AI;

namespace ThemeParkGame.Editor
{
    public static class SceneSetupWizard
    {
        private const string MenuPath = "ThemeParkGame/Setup Scene";

        [MenuItem(MenuPath)]
        public static void SetupScene()
        {
            if (!EditorUtility.DisplayDialog(
                "Scene Setup",
                "現在のシーンにThemeParkGameの階層構造を構築します。\n" +
                "既存のオブジェクトは削除されません。\n\n続行しますか？",
                "構築する", "キャンセル"))
            {
                return;
            }

            // タグ・レイヤーが未設定なら先に実行
            TagLayerSetup.SetupTagsAndLayers();

            CreateManagers();
            CreateEnvironment();
            CreateParkContentContainers();
            CreateEntityContainers();
            CreateUIHierarchy();

            EditorSceneManager.MarkSceneDirty(
                EditorSceneManager.GetActiveScene()
            );

            Debug.Log("[SceneSetupWizard] シーン構築完了");
            EditorUtility.DisplayDialog("Scene Setup", "シーン構築が完了しました。", "OK");
        }

        // ================================================================
        // Managers
        // ================================================================

        private static void CreateManagers()
        {
            var managersRoot = FindOrCreateGameObject("--- Managers ---");

            // GameManager (all subsystems auto-added via GetOrAddComponent)
            var gmObj = FindOrCreateChild(managersRoot, "GameManager");
            AddComponentIfMissing<GameManager>(gmObj);
            AddComponentIfMissing<SceneBootstrapper>(gmObj);

            // AudioManager (separate for DontDestroyOnLoad)
            var audioObj = FindOrCreateChild(managersRoot, "AudioManager");
            AddComponentIfMissing<AudioManager>(audioObj);

            // InputManager (separate for DontDestroyOnLoad)
            var inputObj = FindOrCreateChild(managersRoot, "InputManager");
            AddComponentIfMissing<InputManager>(inputObj);

            Debug.Log("[SceneSetupWizard] Managers 作成完了");
        }

        // ================================================================
        // Environment
        // ================================================================

        private static void CreateEnvironment()
        {
            var envRoot = FindOrCreateGameObject("--- Environment ---");

            // Main Camera
            var camObj = FindOrCreateChild(envRoot, "MainCamera");
            camObj.tag = "MainCamera";
            var cam = AddComponentIfMissing<Camera>(camObj);
            cam.clearFlags = CameraClearFlags.Skybox;
            cam.fieldOfView = 45f;
            AddComponentIfMissing<AudioListener>(camObj);
            camObj.transform.position = new Vector3(0f, 30f, -20f);
            camObj.transform.eulerAngles = new Vector3(60f, 0f, 0f);

            // Remove any existing AudioListener on other objects
            var existingListeners = Object.FindObjectsOfType<AudioListener>();
            foreach (var listener in existingListeners)
            {
                if (listener.gameObject != camObj)
                    Object.DestroyImmediate(listener);
            }

            // Directional Light
            var lightObj = FindOrCreateChild(envRoot, "DirectionalLight");
            var light = AddComponentIfMissing<Light>(lightObj);
            light.type = LightType.Directional;
            light.color = new Color(1f, 0.96f, 0.84f, 1f);
            light.intensity = 1.2f;
            light.shadows = LightShadows.Soft;
            lightObj.transform.eulerAngles = new Vector3(50f, -30f, 0f);

            // Ground plane
            var groundObj = FindOrCreateChild(envRoot, "Ground");
            SetLayerByName(groundObj, "Ground");
            var meshFilter = AddComponentIfMissing<MeshFilter>(groundObj);
            meshFilter.sharedMesh = CreateGroundMesh();
            var meshRenderer = AddComponentIfMissing<MeshRenderer>(groundObj);
            meshRenderer.sharedMaterial = CreateDefaultGroundMaterial();
            var boxCol = AddComponentIfMissing<BoxCollider>(groundObj);
            boxCol.size = new Vector3(200f, 0.1f, 200f);
            boxCol.center = new Vector3(0f, -0.05f, 0f);
            groundObj.transform.position = Vector3.zero;

            // Park Entrance
            var entranceObj = FindOrCreateChild(envRoot, "ParkEntrance");
            entranceObj.tag = "ParkExit";
            var entranceCol = AddComponentIfMissing<BoxCollider>(entranceObj);
            entranceCol.size = new Vector3(3f, 3f, 1f);
            entranceCol.isTrigger = true;
            entranceObj.transform.position = Vector3.zero;

            Debug.Log("[SceneSetupWizard] Environment 作成完了");
        }

        // ================================================================
        // Park Content Containers
        // ================================================================

        private static void CreateParkContentContainers()
        {
            var parkRoot = FindOrCreateGameObject("--- Park Content ---");
            FindOrCreateChild(parkRoot, "Attractions");
            FindOrCreateChild(parkRoot, "Shops");
            FindOrCreateChild(parkRoot, "Facilities");
            FindOrCreateChild(parkRoot, "Pathways");
            FindOrCreateChild(parkRoot, "Decorations");

            Debug.Log("[SceneSetupWizard] Park Content containers 作成完了");
        }

        // ================================================================
        // Entity Containers
        // ================================================================

        private static void CreateEntityContainers()
        {
            var entityRoot = FindOrCreateGameObject("--- Entities ---");
            FindOrCreateChild(entityRoot, "Visitors");
            FindOrCreateChild(entityRoot, "Staff");

            Debug.Log("[SceneSetupWizard] Entity containers 作成完了");
        }

        // ================================================================
        // UI Hierarchy
        // ================================================================

        private static void CreateUIHierarchy()
        {
            var uiRoot = FindOrCreateGameObject("--- UI ---");

            // Canvas
            var canvasObj = FindOrCreateChild(uiRoot, "Canvas");
            var canvas = AddComponentIfMissing<Canvas>(canvasObj);
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 0;

            var scaler = AddComponentIfMissing<CanvasScaler>(canvasObj);
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;

            AddComponentIfMissing<GraphicRaycaster>(canvasObj);

            // HUD
            var hudObj = FindOrCreateChild(canvasObj, "HUD");
            AddComponentIfMissing<HUDController>(hudObj);
            SetRectTransformStretch(hudObj);
            CreateHUDStructure(hudObj);

            // Build Panel (initially hidden)
            var buildPanelObj = FindOrCreateChild(canvasObj, "BuildPanel");
            AddComponentIfMissing<BuildPanelUI>(buildPanelObj);
            SetRectTransformStretch(buildPanelObj);
            buildPanelObj.SetActive(false);

            // Staff Panel (initially hidden)
            var staffPanelObj = FindOrCreateChild(canvasObj, "StaffPanel");
            AddComponentIfMissing<StaffPanelUI>(staffPanelObj);
            SetRectTransformStretch(staffPanelObj);
            staffPanelObj.SetActive(false);

            // Visitor Info Panel (initially hidden)
            var visitorInfoObj = FindOrCreateChild(canvasObj, "VisitorInfoPanel");
            AddComponentIfMissing<VisitorInfoPanel>(visitorInfoObj);
            SetRectTransformStretch(visitorInfoObj);
            visitorInfoObj.SetActive(false);

            // Conversation UI (initially hidden)
            var convoObj = FindOrCreateChild(canvasObj, "ConversationUI");
            AddComponentIfMissing<ConversationUI>(convoObj);
            SetRectTransformStretch(convoObj);
            convoObj.SetActive(false);

            // Tutorial Panel (initially hidden)
            var tutorialObj = FindOrCreateChild(canvasObj, "TutorialPanel");
            AddComponentIfMissing<TutorialSystem>(tutorialObj);
            SetRectTransformStretch(tutorialObj);
            tutorialObj.SetActive(false);

            // EventSystem
            var esObj = FindOrCreateChild(uiRoot, "EventSystem");
            AddComponentIfMissing<EventSystem>(esObj);
            AddComponentIfMissing<StandaloneInputModule>(esObj);

            Debug.Log("[SceneSetupWizard] UI hierarchy 作成完了");
        }

        private static void CreateHUDStructure(GameObject hudObj)
        {
            // Top Bar
            var topBar = FindOrCreateChild(hudObj, "TopBar");
            SetRectTransformAnchored(topBar, new Vector2(0, 1), new Vector2(1, 1),
                new Vector2(0, -40), new Vector2(0, 80));
            AddComponentIfMissing<HorizontalLayoutGroup>(topBar);

            var moneyGroup = FindOrCreateChild(topBar, "MoneyGroup");
            CreateTMPText(moneyGroup, "MoneyText", "$50,000", 24);
            CreateTMPText(moneyGroup, "MoneyDeltaText", "", 16);

            var dateGroup = FindOrCreateChild(topBar, "DateGroup");
            CreateTMPText(dateGroup, "DateTimeText", "Year 1 / Apr / Day 1 10:00", 20);

            var ratingGroup = FindOrCreateChild(topBar, "RatingGroup");
            for (int i = 0; i < 5; i++)
            {
                var star = FindOrCreateChild(ratingGroup, $"Star_{i}");
                AddComponentIfMissing<Image>(star);
            }

            // Speed Controls
            var speedBar = FindOrCreateChild(hudObj, "SpeedControls");
            SetRectTransformAnchored(speedBar, new Vector2(0.5f, 1), new Vector2(0.5f, 1),
                new Vector2(0, -100), new Vector2(200, 40));

            string[] speedLabels = { "||", ">", ">>", ">>>" };
            for (int i = 0; i < speedLabels.Length; i++)
            {
                var btnObj = FindOrCreateChild(speedBar, $"Speed{i}Button");
                AddComponentIfMissing<Image>(btnObj);
                var btn = AddComponentIfMissing<Button>(btnObj);
                CreateTMPText(btnObj, "Label", speedLabels[i], 14);
            }

            // Info Bar
            var infoBar = FindOrCreateChild(hudObj, "InfoBar");
            SetRectTransformAnchored(infoBar, new Vector2(0, 1), new Vector2(1, 1),
                new Vector2(0, -120), new Vector2(0, 40));

            var visitorCount = FindOrCreateChild(infoBar, "VisitorCount");
            CreateTMPText(visitorCount, "VisitorCountText", "Visitors: 0", 18);

            var weatherInfo = FindOrCreateChild(infoBar, "WeatherInfo");
            var weatherIconObj = FindOrCreateChild(weatherInfo, "WeatherIcon");
            AddComponentIfMissing<Image>(weatherIconObj);
            CreateTMPText(weatherInfo, "WeatherText", "Sunny", 18);

            var ticketInfo = FindOrCreateChild(infoBar, "GoldenTicket");
            var ticketIconObj = FindOrCreateChild(ticketInfo, "TicketIcon");
            AddComponentIfMissing<Image>(ticketIconObj);
            CreateTMPText(ticketInfo, "TicketCountText", "0", 18);

            // Bottom Bar
            var bottomBar = FindOrCreateChild(hudObj, "BottomBar");
            SetRectTransformAnchored(bottomBar, new Vector2(0, 0), new Vector2(1, 0),
                new Vector2(0, 40), new Vector2(0, 80));

            var buildBtn = FindOrCreateChild(bottomBar, "BuildModeButton");
            AddComponentIfMissing<Image>(buildBtn);
            AddComponentIfMissing<Button>(buildBtn);
            CreateTMPText(buildBtn, "Label", "Build", 16);

            var viewBtn = FindOrCreateChild(bottomBar, "ViewModeButton");
            AddComponentIfMissing<Image>(viewBtn);
            AddComponentIfMissing<Button>(viewBtn);
            CreateTMPText(viewBtn, "Label", "View", 16);

            var notifBtn = FindOrCreateChild(bottomBar, "NotificationBell");
            AddComponentIfMissing<Image>(notifBtn);
            AddComponentIfMissing<Button>(notifBtn);
            var badge = FindOrCreateChild(notifBtn, "UnreadBadge");
            AddComponentIfMissing<Image>(badge);
            CreateTMPText(badge, "UnreadCount", "0", 12);
        }

        // ================================================================
        // Helper: Create TMP Text
        // ================================================================

        private static GameObject CreateTMPText(GameObject parent, string name, string defaultText, int fontSize)
        {
            var obj = FindOrCreateChild(parent, name);
            var tmp = AddComponentIfMissing<TextMeshProUGUI>(obj);
            tmp.text = defaultText;
            tmp.fontSize = fontSize;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.enableAutoSizing = false;
            return obj;
        }

        // ================================================================
        // Helper: Ground mesh / material
        // ================================================================

        private static Mesh CreateGroundMesh()
        {
            // Use a simple quad scaled up
            var mesh = new Mesh { name = "GroundPlane" };
            float half = 100f;
            mesh.vertices = new Vector3[]
            {
                new Vector3(-half, 0, -half),
                new Vector3(-half, 0, half),
                new Vector3(half, 0, half),
                new Vector3(half, 0, -half)
            };
            mesh.triangles = new int[] { 0, 1, 2, 0, 2, 3 };
            mesh.normals = new Vector3[]
            {
                Vector3.up, Vector3.up, Vector3.up, Vector3.up
            };
            mesh.uv = new Vector2[]
            {
                new Vector2(0, 0), new Vector2(0, 1),
                new Vector2(1, 1), new Vector2(1, 0)
            };
            mesh.RecalculateBounds();
            return mesh;
        }

        private static Material CreateDefaultGroundMaterial()
        {
            string matPath = "Assets/Materials/GroundMaterial.mat";
            var existing = AssetDatabase.LoadAssetAtPath<Material>(matPath);
            if (existing != null) return existing;

            // Ensure directory exists
            if (!AssetDatabase.IsValidFolder("Assets/Materials"))
                AssetDatabase.CreateFolder("Assets", "Materials");

            var mat = new Material(Shader.Find("Standard"));
            mat.color = new Color(0.35f, 0.65f, 0.25f, 1f); // Grass green
            AssetDatabase.CreateAsset(mat, matPath);
            AssetDatabase.SaveAssets();
            return mat;
        }

        // ================================================================
        // Helper: GameObject creation
        // ================================================================

        private static GameObject FindOrCreateGameObject(string name)
        {
            var existing = GameObject.Find(name);
            if (existing != null) return existing;
            var go = new GameObject(name);
            Undo.RegisterCreatedObjectUndo(go, $"Create {name}");
            return go;
        }

        private static GameObject FindOrCreateChild(GameObject parent, string name)
        {
            var t = parent.transform.Find(name);
            if (t != null) return t.gameObject;

            var go = new GameObject(name);
            go.transform.SetParent(parent.transform, false);
            Undo.RegisterCreatedObjectUndo(go, $"Create {name}");
            return go;
        }

        private static T AddComponentIfMissing<T>(GameObject go) where T : Component
        {
            var comp = go.GetComponent<T>();
            if (comp == null)
                comp = Undo.AddComponent<T>(go);
            return comp;
        }

        private static void SetLayerByName(GameObject go, string layerName)
        {
            int layer = LayerMask.NameToLayer(layerName);
            if (layer >= 0)
                go.layer = layer;
        }

        private static void SetRectTransformStretch(GameObject go)
        {
            var rt = go.GetComponent<RectTransform>();
            if (rt == null) rt = go.AddComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }

        private static void SetRectTransformAnchored(GameObject go,
            Vector2 anchorMin, Vector2 anchorMax, Vector2 anchoredPosition, Vector2 sizeDelta)
        {
            var rt = go.GetComponent<RectTransform>();
            if (rt == null) rt = go.AddComponent<RectTransform>();
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.anchoredPosition = anchoredPosition;
            rt.sizeDelta = sizeDelta;
        }
    }
}
#endif
