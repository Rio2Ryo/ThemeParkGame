// ============================================================
// ThemeParkGame - GameBootstrapper (Static)
// RuntimeInitializeOnLoadMethod によるシーン配置不要のブートストラッパー
// WebGLビルドでGUIDミスマッチ等によりMonoBehaviourが
// ロードされない場合でも確実にゲームを起動する
// ============================================================

using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

namespace ThemeParkGame.Core
{
    public static class GameBootstrapper
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            Debug.Log("[GameBootstrapper] === 初期化開始 ===");

            EnsureGameManager();
            EnsureAudioManager();
            EnsureInputManager();
            EnsureEventSystem();

            EnsureMainCamera();
            EnsureDirectionalLight();
            EnsureGround();

            CreateStartScreen();

            Debug.Log("[GameBootstrapper] === 初期化完了 ===");
        }

        // ================================================================
        // マネージャー生成
        // ================================================================

        private static void EnsureGameManager()
        {
            if (GameManager.Instance != null)
            {
                Debug.Log("[GameBootstrapper] GameManager 検出済み");
                return;
            }
            var go = new GameObject("--- Managers ---");
            go.AddComponent<GameManager>();
            Debug.Log("[GameBootstrapper] GameManager を生成");
        }

        private static void EnsureAudioManager()
        {
            if (AudioManager.Instance != null) return;
            var go = new GameObject("AudioManager");
            go.AddComponent<AudioManager>();
            Debug.Log("[GameBootstrapper] AudioManager を生成");
        }

        private static void EnsureInputManager()
        {
            if (InputManager.Instance != null) return;
            var go = new GameObject("InputManager");
            go.AddComponent<InputManager>();
            Debug.Log("[GameBootstrapper] InputManager を生成");
        }

        private static void EnsureEventSystem()
        {
            if (Object.FindObjectOfType<EventSystem>() != null) return;
            var go = new GameObject("EventSystem");
            go.AddComponent<EventSystem>();
            go.AddComponent<StandaloneInputModule>();
            Debug.Log("[GameBootstrapper] EventSystem を生成");
        }

        // ================================================================
        // 環境生成
        // ================================================================

        private static void EnsureMainCamera()
        {
            if (Camera.main != null) return;
            var go = new GameObject("MainCamera");
            go.tag = "MainCamera";
            var cam = go.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.Skybox;
            cam.fieldOfView = 60f;
            go.AddComponent<AudioListener>();
            go.transform.position = new Vector3(0f, 30f, -20f);
            go.transform.rotation = Quaternion.Euler(60f, 0f, 0f);
            Debug.Log("[GameBootstrapper] MainCamera を生成");
        }

        private static void EnsureDirectionalLight()
        {
            foreach (var light in Object.FindObjectsOfType<Light>())
            {
                if (light.type == LightType.Directional) return;
            }
            var go = new GameObject("DirectionalLight");
            var newLight = go.AddComponent<Light>();
            newLight.type = LightType.Directional;
            newLight.color = new Color(1f, 0.96f, 0.84f);
            newLight.intensity = 1.2f;
            newLight.shadows = LightShadows.Soft;
            go.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
            Debug.Log("[GameBootstrapper] DirectionalLight を生成");
        }

        private static void EnsureGround()
        {
            if (GameObject.Find("Ground") != null) return;
            var go = new GameObject("Ground");
            go.layer = 8;
            var mf = go.AddComponent<MeshFilter>();
            var mr = go.AddComponent<MeshRenderer>();
            var col = go.AddComponent<BoxCollider>();
            col.size = new Vector3(100f, 0.1f, 100f);

            var mesh = new Mesh { name = "GroundPlane" };
            const float h = 50f;
            mesh.vertices = new[]
            {
                new Vector3(-h, 0f, -h), new Vector3(-h, 0f, h),
                new Vector3(h, 0f, h), new Vector3(h, 0f, -h)
            };
            mesh.normals = new[] { Vector3.up, Vector3.up, Vector3.up, Vector3.up };
            mesh.uv = new[]
            {
                new Vector2(0, 0), new Vector2(0, 1),
                new Vector2(1, 1), new Vector2(1, 0)
            };
            mesh.triangles = new[] { 0, 1, 2, 0, 2, 3 };
            mesh.RecalculateBounds();
            mf.sharedMesh = mesh;

            var shader = Shader.Find("Standard");
            if (shader == null) shader = Shader.Find("UI/Default");
            if (shader != null)
                mr.material = new Material(shader) { color = new Color(0.45f, 0.65f, 0.35f) };

            go.isStatic = true;
            Debug.Log("[GameBootstrapper] Ground を生成");
        }

        // ================================================================
        // スタート画面
        // ================================================================

        private static void CreateStartScreen()
        {
            var canvasGo = new GameObject("StartScreenCanvas");
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100;
            var scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            canvasGo.AddComponent<GraphicRaycaster>();

            // コントローラー（状態監視 + ボタンハンドラ）
            var ctrl = canvasGo.AddComponent<StartScreenController>();

            // 背景
            var bg = CreateUIElement("Background", canvasGo.transform);
            var bgImg = bg.AddComponent<Image>();
            bgImg.color = new Color(0.08f, 0.12f, 0.22f, 0.97f);
            StretchFull(bg.GetComponent<RectTransform>());

            // タイトル
            var title = CreateUIElement("TitleText", canvasGo.transform);
            var titleText = title.AddComponent<Text>();
            titleText.text = "Theme Park Game";
            titleText.font = GetBuiltinFont();
            titleText.fontSize = 72;
            titleText.fontStyle = FontStyle.Bold;
            titleText.alignment = TextAnchor.MiddleCenter;
            titleText.color = Color.white;
            SetAnchored(title.GetComponent<RectTransform>(), 0, 120, 900, 100);

            // サブタイトル
            var sub = CreateUIElement("SubTitle", canvasGo.transform);
            var subText = sub.AddComponent<Text>();
            subText.text = "- テーマパークゲーム -";
            subText.font = GetBuiltinFont();
            subText.fontSize = 36;
            subText.alignment = TextAnchor.MiddleCenter;
            subText.color = new Color(0.8f, 0.85f, 0.9f);
            SetAnchored(sub.GetComponent<RectTransform>(), 0, 40, 600, 50);

            // スタートボタン
            var btnGo = CreateUIElement("StartButton", canvasGo.transform);
            var btnImg = btnGo.AddComponent<Image>();
            btnImg.color = new Color(0.18f, 0.55f, 0.34f, 1f);
            var btn = btnGo.AddComponent<Button>();
            var btnColors = btn.colors;
            btnColors.highlightedColor = new Color(0.22f, 0.65f, 0.40f);
            btnColors.pressedColor = new Color(0.14f, 0.45f, 0.28f);
            btn.colors = btnColors;
            SetAnchored(btnGo.GetComponent<RectTransform>(), 0, -60, 320, 80);

            var btnLabel = CreateUIElement("Label", btnGo.transform);
            var btnText = btnLabel.AddComponent<Text>();
            btnText.text = "スタート";
            btnText.font = GetBuiltinFont();
            btnText.fontSize = 44;
            btnText.fontStyle = FontStyle.Bold;
            btnText.alignment = TextAnchor.MiddleCenter;
            btnText.color = Color.white;
            StretchFull(btnLabel.GetComponent<RectTransform>());

            btn.onClick.AddListener(ctrl.OnStartClicked);

            // バージョン表示
            var ver = CreateUIElement("Version", canvasGo.transform);
            var verText = ver.AddComponent<Text>();
            verText.text = "v0.1 - WebGL Build";
            verText.font = GetBuiltinFont();
            verText.fontSize = 20;
            verText.alignment = TextAnchor.LowerRight;
            verText.color = new Color(0.5f, 0.5f, 0.6f);
            var verRect = ver.GetComponent<RectTransform>();
            verRect.anchorMin = new Vector2(1, 0);
            verRect.anchorMax = new Vector2(1, 0);
            verRect.pivot = new Vector2(1, 0);
            verRect.anchoredPosition = new Vector2(-20, 10);
            verRect.sizeDelta = new Vector2(300, 30);

            Debug.Log("[GameBootstrapper] スタート画面を生成");
        }

        // ================================================================
        // UIヘルパー
        // ================================================================

        private static GameObject CreateUIElement(string name, Transform parent)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.AddComponent<RectTransform>();
            return go;
        }

        private static void StretchFull(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }

        private static void SetAnchored(RectTransform rt, float x, float y, float w, float h)
        {
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = new Vector2(x, y);
            rt.sizeDelta = new Vector2(w, h);
        }

        private static Font GetBuiltinFont()
        {
            var font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            if (font == null)
                font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            return font;
        }
    }

    /// <summary>
    /// スタート画面の状態監視とボタンハンドラ。
    /// SceneBootstrapperが先にゲームを開始した場合は自動的に画面を閉じる。
    /// </summary>
    internal class StartScreenController : MonoBehaviour
    {
        private void Update()
        {
            if (GameManager.Instance != null &&
                GameManager.Instance.CurrentState != GameState.MainMenu)
            {
                Debug.Log("[GameBootstrapper] ゲーム既に開始済み - スタート画面を閉じる");
                Destroy(gameObject);
            }
        }

        public void OnStartClicked()
        {
            if (GameManager.Instance == null)
            {
                Debug.LogError("[GameBootstrapper] GameManager が見つかりません");
                return;
            }
            GameManager.Instance.StartNewGame(ThemeZone.LostKingdom);
            Debug.Log("[GameBootstrapper] ゲーム開始!");
            Destroy(gameObject);
        }
    }
}
