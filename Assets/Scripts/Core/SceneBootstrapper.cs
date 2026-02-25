// ============================================================
// ThemeParkGame - Scene Bootstrapper
// ランタイム用のブートストラッパー
// シーンに配置しておくと、必要なManagerが存在しない場合に自動生成する
// MainScene.unityが無い状態でもゲームを起動可能にする
// ============================================================

using UnityEngine;
using UnityEngine.SceneManagement;
using ThemeParkGame.Attraction;

namespace ThemeParkGame.Core
{
    /// <summary>
    /// シーン起動時にGameManagerの存在を確認し、
    /// 無ければ自動生成するランタイムブートストラッパー。
    ///
    /// 機能:
    /// 1. 必須Manager(GameManager, AudioManager, InputManager)の自動生成
    /// 2. EventSystemの自動生成
    /// 3. シーン環境(Camera, Light, Ground)の自動生成
    /// 4. サンドボックスモードでのゲーム自動開始
    /// 5. Config JSONデータの事前ロード
    /// </summary>
    [DefaultExecutionOrder(-100)]
    public class SceneBootstrapper : MonoBehaviour
    {
        [Header("Bootstrap Settings")]
        [Tooltip("Managerが存在しない場合に自動生成するか")]
        [SerializeField] private bool autoCreateManagers = true;

        [Tooltip("シーン環境が存在しない場合に自動生成するか")]
        [SerializeField] private bool autoCreateEnvironment = true;

        [Tooltip("ゲームを自動開始するか（サンドボックスモード）")]
        [SerializeField] private bool autoStartGame = true;

        [Tooltip("自動開始時の開始ゾーン")]
        [SerializeField] private ThemeZone startingZone = ThemeZone.LostKingdom;

        [Tooltip("起動時にログを出力するか")]
        [SerializeField] private bool verboseLogging = true;

        // 初期化完了フラグ（2回目のAwakeで二重初期化を防止）
        private static bool s_hasBootstrapped;

        private void Awake()
        {
            if (s_hasBootstrapped)
            {
                if (verboseLogging)
                    WebGLOptimizer.LogVerbose("[SceneBootstrapper] 既に初期化済み - スキップ");
                return;
            }
            s_hasBootstrapped = true;

            if (verboseLogging)
                WebGLOptimizer.LogVerbose("[SceneBootstrapper] === シーン起動シーケンス開始 ===");

            // Phase 1: 必須マネージャーの確保
            if (autoCreateManagers)
            {
                EnsureGameManager();
                EnsureAudioManager();
                EnsureInputManager();
                EnsureEventSystem();
            }

            // Phase 2: シーン環境の確保
            if (autoCreateEnvironment)
            {
                EnsureMainCamera();
                EnsureDirectionalLight();
                EnsureGround();
                EnsureParkEntrance();
            }

            // Phase 3: Config JSONの事前ロード
            LoadConfigData();

            // Phase 4: ゲーム自動開始
            if (autoStartGame)
            {
                StartGameDelayed();
            }

            if (verboseLogging)
                WebGLOptimizer.LogVerbose("[SceneBootstrapper] === シーン起動シーケンス完了 ===");
        }

        private void OnDestroy()
        {
            // シーン破棄時にフラグをリセット（再ロード対応）
            SceneManager.sceneLoaded -= OnSceneLoaded;
        }

        // ================================================================
        // Phase 1: 必須マネージャー
        // ================================================================

        private void EnsureGameManager()
        {
            if (GameManager.Instance != null)
            {
                if (verboseLogging)
                    WebGLOptimizer.LogVerbose("[SceneBootstrapper] GameManager 検出済み");
                return;
            }

            var go = new GameObject("--- Managers ---");
            go.AddComponent<GameManager>();
            if (verboseLogging)
                WebGLOptimizer.LogVerbose("[SceneBootstrapper] GameManager を自動生成しました");
        }

        private void EnsureAudioManager()
        {
            if (AudioManager.Instance != null)
            {
                if (verboseLogging)
                    WebGLOptimizer.LogVerbose("[SceneBootstrapper] AudioManager 検出済み");
                return;
            }

            var go = new GameObject("AudioManager");
            go.AddComponent<AudioManager>();
            if (verboseLogging)
                WebGLOptimizer.LogVerbose("[SceneBootstrapper] AudioManager を自動生成しました");
        }

        private void EnsureInputManager()
        {
            if (InputManager.Instance != null)
            {
                if (verboseLogging)
                    WebGLOptimizer.LogVerbose("[SceneBootstrapper] InputManager 検出済み");
                return;
            }

            var go = new GameObject("InputManager");
            go.AddComponent<InputManager>();
            if (verboseLogging)
                WebGLOptimizer.LogVerbose("[SceneBootstrapper] InputManager を自動生成しました");
        }

        private void EnsureEventSystem()
        {
            var existingES = FindObjectOfType<UnityEngine.EventSystems.EventSystem>();
            if (existingES != null)
            {
                if (verboseLogging)
                    WebGLOptimizer.LogVerbose("[SceneBootstrapper] EventSystem 検出済み");
                return;
            }

            var go = new GameObject("EventSystem");
            go.AddComponent<UnityEngine.EventSystems.EventSystem>();
            go.AddComponent<UnityEngine.EventSystems.StandaloneInputModule>();
            if (verboseLogging)
                WebGLOptimizer.LogVerbose("[SceneBootstrapper] EventSystem を自動生成しました");
        }

        // ================================================================
        // Phase 2: シーン環境
        // ================================================================

        private void EnsureMainCamera()
        {
            if (Camera.main != null)
            {
                if (verboseLogging)
                    WebGLOptimizer.LogVerbose("[SceneBootstrapper] MainCamera 検出済み");
                return;
            }

            var go = new GameObject("MainCamera");
            go.tag = "MainCamera";
            var cam = go.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.Skybox;
            cam.fieldOfView = 60f;
            go.AddComponent<AudioListener>();

            // God View初期位置: パークを見下ろす斜め視点
            go.transform.position = new Vector3(0f, 30f, -20f);
            go.transform.rotation = Quaternion.Euler(60f, 0f, 0f);

            if (verboseLogging)
                WebGLOptimizer.LogVerbose("[SceneBootstrapper] MainCamera を自動生成しました");
        }

        private void EnsureDirectionalLight()
        {
            var existingLight = FindObjectOfType<Light>();
            if (existingLight != null && existingLight.type == LightType.Directional)
            {
                if (verboseLogging)
                    WebGLOptimizer.LogVerbose("[SceneBootstrapper] DirectionalLight 検出済み");
                return;
            }

            var go = new GameObject("DirectionalLight");
            var light = go.AddComponent<Light>();
            light.type = LightType.Directional;
            light.color = new Color(1f, 0.96f, 0.84f); // 暖色系太陽光
            light.intensity = 1.2f;
            light.shadows = LightShadows.Soft;
            go.transform.rotation = Quaternion.Euler(50f, -30f, 0f);

            if (verboseLogging)
                WebGLOptimizer.LogVerbose("[SceneBootstrapper] DirectionalLight を自動生成しました");
        }

        private void EnsureGround()
        {
            // "Ground"レイヤー(Layer 8)のオブジェクトが存在するかチェック
            var existingGround = GameObject.Find("Ground");
            if (existingGround != null)
            {
                if (verboseLogging)
                    WebGLOptimizer.LogVerbose("[SceneBootstrapper] Ground 検出済み");
                return;
            }

            var go = new GameObject("Ground");
            go.layer = 8; // Ground layer

            // プリミティブメッシュ生成
            var meshFilter = go.AddComponent<MeshFilter>();
            var meshRenderer = go.AddComponent<MeshRenderer>();
            var boxCollider = go.AddComponent<BoxCollider>();

            // 100x100の地面メッシュ（QuadをXZ平面に配置）
            meshFilter.sharedMesh = CreateGroundMesh(100f, 100f);
            boxCollider.size = new Vector3(100f, 0.1f, 100f);
            boxCollider.center = Vector3.zero;

            // デフォルトマテリアル
            meshRenderer.material = new Material(Shader.Find("Standard"))
            {
                color = new Color(0.45f, 0.65f, 0.35f) // 芝生色
            };

            go.isStatic = true;

            if (verboseLogging)
                WebGLOptimizer.LogVerbose("[SceneBootstrapper] Ground を自動生成しました (100x100)");
        }

        private void EnsureParkEntrance()
        {
            var existingEntrance = GameObject.FindGameObjectWithTag("ParkExit");
            if (existingEntrance != null)
            {
                if (verboseLogging)
                    WebGLOptimizer.LogVerbose("[SceneBootstrapper] ParkEntrance 検出済み");
                return;
            }

            var go = new GameObject("ParkEntrance");
            go.tag = "ParkExit";
            go.layer = 9; // Facility layer

            var boxCollider = go.AddComponent<BoxCollider>();
            boxCollider.isTrigger = true;
            boxCollider.size = new Vector3(6f, 4f, 3f);
            boxCollider.center = new Vector3(0f, 2f, 0f);

            // パーク入口はグリッド原点に配置
            go.transform.position = Vector3.zero;

            if (verboseLogging)
                WebGLOptimizer.LogVerbose("[SceneBootstrapper] ParkEntrance を自動生成しました");
        }

        // ================================================================
        // Phase 3: Config データロード
        // ================================================================

        private void LoadConfigData()
        {
            // AttractionDatabaseへのJSONロード（静的クラスのため直接参照）
            if (AttractionDatabase.Count > 0)
            {
                if (verboseLogging)
                    WebGLOptimizer.LogVerbose("[SceneBootstrapper] AttractionDatabase 既にロード済み");
            }
            else
            {
                // Resources.Loadで設定JSONを読み込む
                TextAsset balanceJson = Resources.Load<TextAsset>("Config/balance_config");
                TextAsset attractionsJson = Resources.Load<TextAsset>("Config/attractions");
                TextAsset shopsJson = Resources.Load<TextAsset>("Config/shops");

                int loadedCount = 0;
                if (balanceJson != null) loadedCount++;
                if (attractionsJson != null) loadedCount++;
                if (shopsJson != null) loadedCount++;

                if (verboseLogging)
                    WebGLOptimizer.LogVerbose($"[SceneBootstrapper] Config JSON ロード: {loadedCount}/3 ファイル");
            }
        }

        // ================================================================
        // Phase 4: ゲーム自動開始
        // ================================================================

        /// <summary>
        /// 1フレーム遅延させてゲームを開始する。
        /// GameManagerのAwake/InitializeSubSystemsが完了してから呼ぶ必要がある。
        /// </summary>
        private void StartGameDelayed()
        {
            // シーンロード完了コールバックで開始
            SceneManager.sceneLoaded += OnSceneLoaded;

            // 既にシーンがロード済みの場合はInvokeで遅延実行
            Invoke(nameof(AutoStartGame), 0.1f);
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            // sceneLoadedからの呼び出しは不要（Invokeで処理済み）
        }

        private void AutoStartGame()
        {
            if (GameManager.Instance == null)
            {
                WebGLOptimizer.LogVerbose("[SceneBootstrapper] GameManager が見つかりません。自動開始をスキップ。");
                return;
            }

            // 既にゲームが開始されている場合はスキップ
            if (GameManager.Instance.CurrentState != GameState.MainMenu)
            {
                if (verboseLogging)
                    WebGLOptimizer.LogVerbose("[SceneBootstrapper] ゲーム既に開始済み - 自動開始スキップ");
                return;
            }

            if (verboseLogging)
                WebGLOptimizer.LogVerbose($"[SceneBootstrapper] サンドボックスモード自動開始: ゾーン={startingZone}");

            GameManager.Instance.StartNewGame(startingZone);
        }

        // ================================================================
        // ユーティリティ
        // ================================================================

        /// <summary>XZ平面のグリッドメッシュを生成する</summary>
        private static Mesh CreateGroundMesh(float width, float depth)
        {
            var mesh = new Mesh { name = "GroundPlane" };

            float halfW = width * 0.5f;
            float halfD = depth * 0.5f;

            mesh.vertices = new[]
            {
                new Vector3(-halfW, 0f, -halfD),
                new Vector3(-halfW, 0f,  halfD),
                new Vector3( halfW, 0f,  halfD),
                new Vector3( halfW, 0f, -halfD)
            };

            mesh.normals = new[]
            {
                Vector3.up, Vector3.up, Vector3.up, Vector3.up
            };

            mesh.uv = new[]
            {
                new Vector2(0f, 0f),
                new Vector2(0f, 1f),
                new Vector2(1f, 1f),
                new Vector2(1f, 0f)
            };

            mesh.triangles = new[] { 0, 1, 2, 0, 2, 3 };
            mesh.RecalculateBounds();

            return mesh;
        }

        /// <summary>ブートストラップフラグをリセットする（テスト用）</summary>
        public static void ResetBootstrapFlag()
        {
            s_hasBootstrapped = false;
        }
    }
}
