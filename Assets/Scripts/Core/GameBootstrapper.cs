// ============================================================
// ThemeParkGame - GameBootstrapper (Static)
// RuntimeInitializeOnLoadMethod によるシーン配置不要のブートストラッパー
// WebGLビルドでGUIDミスマッチ等によりMonoBehaviourが
// ロードされない場合でも確実にゲームを起動する
// ============================================================

using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using ThemeParkGame.AI;
using ThemeParkGame.UI;
using ThemeParkGame.Visitor;
using ThemeParkGame.Park;
using ThemeParkGame.Economy;

namespace ThemeParkGame.Core
{
    public static class GameBootstrapper
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            WebGLOptimizer.LogVerbose("[GameBootstrapper] === 初期化開始 ===");

            EnsureGameManager();
            EnsureAudioManager();
            EnsureInputManager();
            EnsureEventSystem();

            EnsureMainCamera();
            EnsureDirectionalLight();
            EnsureGround();

            // オンラインサービス
            EnsureLeaderboardManager();
            EnsureCloudSaveManager();

            // VIPシステム
            EnsureVIPVisitorSystem();

            // チュートリアル
            EnsureTutorialSystem();

            // Phase 8 システム
            EnsureChallengeSystem();
            EnsureParkExpansionSystem();
            EnsureLoanInvestmentUI();
            EnsureCoopManager();

            // Phase 9 システム
            EnsureWeatherForecastUI();
            EnsureSpecialEventUI();
            EnsureSocialShareSystem();
            EnsureAccessibilitySystem();

            // Phase 10 システム
            EnsureAccidentEventSystem();
            EnsureWordOfMouthSystem();
            EnsureRivalParkSystem();
            EnsureSaleCampaignSystem();
            EnsureNPCDialogueSystem();
            EnsureHooliganManager();

            // ランタイムゲームセットアップ（ゲーム開始後にワールドを構築）
            EnsureRuntimeGameSetup();

            CreateStartScreen();

            WebGLOptimizer.LogVerbose("[GameBootstrapper] === 初期化完了 ===");
        }

        // ================================================================
        // マネージャー生成
        // ================================================================

        private static void EnsureGameManager()
        {
            if (GameManager.Instance != null)
            {
                WebGLOptimizer.LogVerbose("[GameBootstrapper] GameManager 検出済み");
                return;
            }
            var go = new GameObject("--- Managers ---");
            go.AddComponent<GameManager>();
            WebGLOptimizer.LogVerbose("[GameBootstrapper] GameManager を生成");
        }

        private static void EnsureAudioManager()
        {
            if (AudioManager.Instance != null) return;
            var go = new GameObject("AudioManager");
            go.AddComponent<AudioManager>();
            WebGLOptimizer.LogVerbose("[GameBootstrapper] AudioManager を生成");
        }

        private static void EnsureInputManager()
        {
            if (InputManager.Instance != null) return;
            var go = new GameObject("InputManager");
            go.AddComponent<InputManager>();
            WebGLOptimizer.LogVerbose("[GameBootstrapper] InputManager を生成");
        }

        private static void EnsureEventSystem()
        {
            if (Object.FindObjectOfType<EventSystem>() != null) return;
            var go = new GameObject("EventSystem");
            go.AddComponent<EventSystem>();
            go.AddComponent<StandaloneInputModule>();
            WebGLOptimizer.LogVerbose("[GameBootstrapper] EventSystem を生成");
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
            go.AddComponent<AudioListener>();
            go.AddComponent<FirstPersonCamera>();
            go.AddComponent<GameCameraController>();
            WebGLOptimizer.LogVerbose("[GameBootstrapper] MainCamera を生成（アイソメトリックカメラ）");
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
            WebGLOptimizer.LogVerbose("[GameBootstrapper] DirectionalLight を生成");
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

            // プロシージャル芝生テクスチャを適用
            mr.material = ProceduralTextureGenerator.CreateGroundMaterial();

            go.isStatic = true;
            WebGLOptimizer.LogVerbose("[GameBootstrapper] Ground を生成（芝生テクスチャ適用）");
        }

        // ================================================================
        // オンラインサービス・追加システム
        // ================================================================

        private static void EnsureLeaderboardManager()
        {
            if (LeaderboardManager.Instance != null) return;
            var go = new GameObject("LeaderboardManager");
            go.AddComponent<LeaderboardManager>();
            WebGLOptimizer.LogVerbose("[GameBootstrapper] LeaderboardManager を生成");
        }

        private static void EnsureCloudSaveManager()
        {
            if (CloudSaveManager.Instance != null) return;
            var go = new GameObject("CloudSaveManager");
            go.AddComponent<CloudSaveManager>();
            WebGLOptimizer.LogVerbose("[GameBootstrapper] CloudSaveManager を生成");
        }

        private static void EnsureVIPVisitorSystem()
        {
            if (VIPVisitorSystem.Instance != null) return;
            var go = new GameObject("VIPVisitorSystem");
            go.AddComponent<VIPVisitorSystem>();
            WebGLOptimizer.LogVerbose("[GameBootstrapper] VIPVisitorSystem を生成");
        }

        private static void EnsureTutorialSystem()
        {
            if (TutorialSystem.Instance != null) return;
            var go = new GameObject("TutorialSystem");
            go.AddComponent<TutorialSystem>();
            WebGLOptimizer.LogVerbose("[GameBootstrapper] TutorialSystem を生成");
        }

        private static void EnsureChallengeSystem()
        {
            if (ChallengeSystem.Instance != null) return;
            var go = new GameObject("ChallengeSystem");
            go.AddComponent<ChallengeSystem>();
            WebGLOptimizer.LogVerbose("[GameBootstrapper] ChallengeSystem を生成");
        }

        private static void EnsureParkExpansionSystem()
        {
            if (ParkExpansionSystem.Instance != null) return;
            var go = new GameObject("ParkExpansionSystem");
            var sys = go.AddComponent<ParkExpansionSystem>();
            sys.Initialize();
            WebGLOptimizer.LogVerbose("[GameBootstrapper] ParkExpansionSystem を生成");
        }

        private static void EnsureLoanInvestmentUI()
        {
            if (LoanInvestmentUI.Instance != null) return;
            var go = new GameObject("LoanInvestmentUI");
            go.AddComponent<LoanInvestmentUI>();
            WebGLOptimizer.LogVerbose("[GameBootstrapper] LoanInvestmentUI を生成");
        }

        private static void EnsureCoopManager()
        {
            if (CoopManager.Instance != null) return;
            var go = new GameObject("CoopManager");
            go.AddComponent<CoopManager>();
            WebGLOptimizer.LogVerbose("[GameBootstrapper] CoopManager を生成");
        }

        // ================================================================
        // Phase 9 システム
        // ================================================================

        private static void EnsureWeatherForecastUI()
        {
            if (UI.WeatherForecastUI.Instance != null) return;
            var go = new GameObject("WeatherForecastUI");
            go.AddComponent<UI.WeatherForecastUI>();
            WebGLOptimizer.LogVerbose("[GameBootstrapper] WeatherForecastUI を生成");
        }

        private static void EnsureSpecialEventUI()
        {
            if (UI.SpecialEventUI.Instance != null) return;
            var go = new GameObject("SpecialEventUI");
            go.AddComponent<UI.SpecialEventUI>();
            WebGLOptimizer.LogVerbose("[GameBootstrapper] SpecialEventUI を生成");
        }

        private static void EnsureSocialShareSystem()
        {
            if (SocialShareSystem.Instance != null) return;
            var go = new GameObject("SocialShareSystem");
            go.AddComponent<SocialShareSystem>();
            WebGLOptimizer.LogVerbose("[GameBootstrapper] SocialShareSystem を生成");
        }

        private static void EnsureAccessibilitySystem()
        {
            if (AccessibilitySystem.Instance != null) return;
            var go = new GameObject("AccessibilitySystem");
            go.AddComponent<AccessibilitySystem>();
            WebGLOptimizer.LogVerbose("[GameBootstrapper] AccessibilitySystem を生成");
        }

        // ================================================================
        // Phase 10 システム
        // ================================================================

        private static void EnsureAccidentEventSystem()
        {
            if (AccidentEventSystem.Instance != null) return;
            var go = new GameObject("AccidentEventSystem");
            go.AddComponent<AccidentEventSystem>();
            WebGLOptimizer.LogVerbose("[GameBootstrapper] AccidentEventSystem を生成");
        }

        private static void EnsureWordOfMouthSystem()
        {
            if (WordOfMouthSystem.Instance != null) return;
            var go = new GameObject("WordOfMouthSystem");
            go.AddComponent<WordOfMouthSystem>();
            WebGLOptimizer.LogVerbose("[GameBootstrapper] WordOfMouthSystem を生成");
        }

        private static void EnsureRivalParkSystem()
        {
            if (RivalParkSystem.Instance != null) return;
            var go = new GameObject("RivalParkSystem");
            go.AddComponent<RivalParkSystem>();
            WebGLOptimizer.LogVerbose("[GameBootstrapper] RivalParkSystem を生成");
        }

        private static void EnsureSaleCampaignSystem()
        {
            if (SaleCampaignSystem.Instance != null) return;
            var go = new GameObject("SaleCampaignSystem");
            go.AddComponent<SaleCampaignSystem>();
            WebGLOptimizer.LogVerbose("[GameBootstrapper] SaleCampaignSystem を生成");
        }

        private static void EnsureNPCDialogueSystem()
        {
            if (NPCDialogueSystem.Instance != null) return;
            var go = new GameObject("NPCDialogueSystem");
            go.AddComponent<NPCDialogueSystem>();
            WebGLOptimizer.LogVerbose("[GameBootstrapper] NPCDialogueSystem を生成");
        }

        private static void EnsureHooliganManager()
        {
            if (HooliganManager.Instance != null) return;
            var go = new GameObject("HooliganManager");
            go.AddComponent<HooliganManager>();
            WebGLOptimizer.LogVerbose("[GameBootstrapper] HooliganManager を生成");
        }

        // ================================================================
        // ランタイムゲームセットアップ
        // ================================================================

        private static void EnsureRuntimeGameSetup()
        {
            if (Object.FindObjectOfType<RuntimeGameSetup>() != null) return;
            var go = new GameObject("RuntimeGameSetup");
            go.AddComponent<RuntimeGameSetup>();
            Object.DontDestroyOnLoad(go);
            WebGLOptimizer.LogVerbose("[GameBootstrapper] RuntimeGameSetup を生成");
        }

        // ================================================================
        // スタート画面（タイトル画面）
        // ================================================================

        /// <summary>メインメニュー復帰時にスタート画面を再生成する</summary>
        public static void RecreateStartScreen()
        {
            var existing = Object.FindObjectOfType<StartScreenController>();
            if (existing != null) Object.Destroy(existing.gameObject);
            CreateStartScreen();
        }

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

            var ctrl = canvasGo.AddComponent<StartScreenController>();

            // ---- 背景（グラデーション風 2レイヤー） ----
            var bg = CreateUIElement("Background", canvasGo.transform);
            var bgImg = bg.AddComponent<Image>();
            bgImg.color = new Color(0.04f, 0.06f, 0.14f, 1f);
            StretchFull(bg.GetComponent<RectTransform>());

            // 上部グラデーション帯
            var topBand = CreateUIElement("TopBand", canvasGo.transform);
            var topBandImg = topBand.AddComponent<Image>();
            topBandImg.color = new Color(0.08f, 0.18f, 0.35f, 0.6f);
            var topBandRt = topBand.GetComponent<RectTransform>();
            topBandRt.anchorMin = new Vector2(0f, 0.5f);
            topBandRt.anchorMax = new Vector2(1f, 1f);
            topBandRt.offsetMin = Vector2.zero;
            topBandRt.offsetMax = Vector2.zero;

            // ---- 装飾アイコン行 ----
            var icons = CreateUIElement("Icons", canvasGo.transform);
            var iconsText = icons.AddComponent<Text>();
            iconsText.text = "[ ジェットコースター ]   [ 観覧車 ]   [ フードコート ]   [ お化け屋敷 ]";
            iconsText.font = GetBuiltinFont();
            iconsText.fontSize = 18;
            iconsText.alignment = TextAnchor.MiddleCenter;
            iconsText.color = new Color(0.4f, 0.55f, 0.7f, 0.7f);
            SetAnchored(icons.GetComponent<RectTransform>(), 0, 230, 900, 30);

            // ---- メインタイトル ----
            var title = CreateUIElement("TitleText", canvasGo.transform);
            var titleText = title.AddComponent<Text>();
            titleText.text = LocalizationData.GameTitle;
            titleText.font = GetBuiltinFont();
            titleText.fontSize = 80;
            titleText.fontStyle = FontStyle.Bold;
            titleText.alignment = TextAnchor.MiddleCenter;
            titleText.color = new Color(1f, 0.95f, 0.7f);
            SetAnchored(title.GetComponent<RectTransform>(), 0, 160, 1000, 100);

            // ---- サブタイトル ----
            var sub = CreateUIElement("SubTitle", canvasGo.transform);
            var subText = sub.AddComponent<Text>();
            subText.text = LocalizationData.GameSubtitle;
            subText.font = GetBuiltinFont();
            subText.fontSize = 28;
            subText.alignment = TextAnchor.MiddleCenter;
            subText.color = new Color(0.7f, 0.8f, 0.9f);
            SetAnchored(sub.GetComponent<RectTransform>(), 0, 90, 700, 40);

            // ---- 説明テキスト ----
            var desc = CreateUIElement("Description", canvasGo.transform);
            var descText = desc.AddComponent<Text>();
            descText.text =
                "アトラクション / ショップ / スタッフ管理\n" +
                "来場者AI / 天候変化 / 経済システム";
            descText.font = GetBuiltinFont();
            descText.fontSize = 20;
            descText.alignment = TextAnchor.MiddleCenter;
            descText.color = new Color(0.5f, 0.6f, 0.7f);
            descText.lineSpacing = 1.4f;
            SetAnchored(desc.GetComponent<RectTransform>(), 0, 20, 700, 70);

            // ---- 難易度選択ラベル ----
            var diffLabel = CreateUIElement("DiffLabel", canvasGo.transform);
            var diffLabelText = diffLabel.AddComponent<Text>();
            diffLabelText.text = "難易度を選択してください";
            diffLabelText.font = GetBuiltinFont();
            diffLabelText.fontSize = 22;
            diffLabelText.fontStyle = FontStyle.Bold;
            diffLabelText.alignment = TextAnchor.MiddleCenter;
            diffLabelText.color = new Color(0.6f, 0.65f, 0.75f);
            SetAnchored(diffLabel.GetComponent<RectTransform>(), 0, -40, 600, 30);

            // ---- 難易度ボタン3つ（横並び） ----
            float btnW = 200f;
            float btnH = 70f;
            float gap = 16f;
            float totalW = btnW * 3 + gap * 2;
            float startX = -totalW / 2f + btnW / 2f;

            // Easy
            CreateDifficultyButton(canvasGo.transform, ctrl, "EasyBtn",
                "かんたん", "初期資金: ¥80,000\nスポーン: ゆっくり",
                new Color(0.2f, 0.6f, 0.35f), new Color(0.25f, 0.7f, 0.42f), new Color(0.15f, 0.48f, 0.28f),
                startX, -90f, btnW, btnH, GameDifficulty.Easy);

            // Normal
            CreateDifficultyButton(canvasGo.transform, ctrl, "NormalBtn",
                "ふつう", "初期資金: ¥50,000\nスポーン: 標準",
                new Color(0.25f, 0.45f, 0.65f), new Color(0.3f, 0.55f, 0.75f), new Color(0.18f, 0.35f, 0.52f),
                startX + btnW + gap, -90f, btnW, btnH, GameDifficulty.Normal);

            // Hard
            CreateDifficultyButton(canvasGo.transform, ctrl, "HardBtn",
                "むずかしい", "初期資金: ¥30,000\nスポーン: 高速",
                new Color(0.65f, 0.25f, 0.2f), new Color(0.75f, 0.35f, 0.3f), new Color(0.5f, 0.18f, 0.15f),
                startX + (btnW + gap) * 2, -90f, btnW, btnH, GameDifficulty.Hard);

            // ---- CONTINUEボタン（セーブデータがある場合のみ表示） ----
            if (SaveSystem.HasAnySaveData())
            {
                var contGo = CreateUIElement("ContinueBtn", canvasGo.transform);
                var contImg = contGo.AddComponent<Image>();
                contImg.color = new Color(0.2f, 0.5f, 0.65f);
                var contBtn = contGo.AddComponent<Button>();
                var contColors = contBtn.colors;
                contColors.highlightedColor = new Color(0.25f, 0.6f, 0.78f);
                contColors.pressedColor = new Color(0.15f, 0.38f, 0.52f);
                contBtn.colors = contColors;
                contBtn.targetGraphic = contImg;
                SetAnchored(contGo.GetComponent<RectTransform>(), 0, -180f, 300f, 50f);

                var contLabel = CreateUIElement("ContLabel", contGo.transform);
                var contLabelText = contLabel.AddComponent<Text>();
                contLabelText.text = LocalizationData.BtnContinue;
                contLabelText.font = GetBuiltinFont();
                contLabelText.fontSize = 24;
                contLabelText.fontStyle = FontStyle.Bold;
                contLabelText.alignment = TextAnchor.MiddleCenter;
                contLabelText.color = Color.white;
                StretchFull(contLabel.GetComponent<RectTransform>());

                // 最新スロット情報
                string slotInfo = "";
                for (int s = 0; s < SaveSystem.MaxSlots; s++)
                {
                    if (SaveSystem.HasSaveData(s))
                    {
                        slotInfo = SaveSystem.GetSlotSummary(s);
                        break;
                    }
                }

                var contDesc = CreateUIElement("ContDesc", canvasGo.transform);
                var contDescText = contDesc.AddComponent<Text>();
                contDescText.text = slotInfo;
                contDescText.font = GetBuiltinFont();
                contDescText.fontSize = 14;
                contDescText.alignment = TextAnchor.MiddleCenter;
                contDescText.color = new Color(0.5f, 0.6f, 0.7f);
                SetAnchored(contDesc.GetComponent<RectTransform>(), 0, -215f, 500f, 20f);

                contBtn.onClick.AddListener(() => ctrl.OnContinueClicked());
            }

            // ---- シナリオモードボタン ----
            var scenGo = CreateUIElement("ScenarioBtn", canvasGo.transform);
            var scenImg = scenGo.AddComponent<Image>();
            scenImg.color = new Color(0.5f, 0.3f, 0.6f);
            var scenBtn = scenGo.AddComponent<Button>();
            var scenColors = scenBtn.colors;
            scenColors.highlightedColor = new Color(0.6f, 0.38f, 0.72f);
            scenColors.pressedColor = new Color(0.38f, 0.2f, 0.48f);
            scenBtn.colors = scenColors;
            scenBtn.targetGraphic = scenImg;
            SetAnchored(scenGo.GetComponent<RectTransform>(), 0, -245f, 300f, 50f);

            var scenLabel = CreateUIElement("ScenLabel", scenGo.transform);
            var scenLabelText = scenLabel.AddComponent<Text>();
            scenLabelText.text = "シナリオモード";
            scenLabelText.font = GetBuiltinFont();
            scenLabelText.fontSize = 24;
            scenLabelText.fontStyle = FontStyle.Bold;
            scenLabelText.alignment = TextAnchor.MiddleCenter;
            scenLabelText.color = Color.white;
            StretchFull(scenLabel.GetComponent<RectTransform>());

            scenBtn.onClick.AddListener(() => ctrl.OnScenarioModeClicked());

            // ---- 操作ヒント ----
            var hint = CreateUIElement("Hint", canvasGo.transform);
            var hintText = hint.AddComponent<Text>();
            hintText.text = "来場者をクリックで詳細  |  メニューで一時停止  |  速度: ⏸  ½  ▶  ▶▶  ▶▶▶";
            hintText.font = GetBuiltinFont();
            hintText.fontSize = 16;
            hintText.alignment = TextAnchor.MiddleCenter;
            hintText.color = new Color(0.4f, 0.45f, 0.55f);
            SetAnchored(hint.GetComponent<RectTransform>(), 0, -310, 900, 30);

            // ---- バージョン表示 ----
            var ver = CreateUIElement("Version", canvasGo.transform);
            var verText = ver.AddComponent<Text>();
            verText.text = "v3.0 - テーマパークワールド";
            verText.font = GetBuiltinFont();
            verText.fontSize = 18;
            verText.alignment = TextAnchor.LowerRight;
            verText.color = new Color(0.35f, 0.4f, 0.5f);
            var verRect = ver.GetComponent<RectTransform>();
            verRect.anchorMin = new Vector2(1, 0);
            verRect.anchorMax = new Vector2(1, 0);
            verRect.pivot = new Vector2(1, 0);
            verRect.anchoredPosition = new Vector2(-20, 10);
            verRect.sizeDelta = new Vector2(300, 30);

            // ---- コピーライト ----
            var cr = CreateUIElement("Copyright", canvasGo.transform);
            var crText = cr.AddComponent<Text>();
            crText.text = "テーマパークゲーム プロジェクト";
            crText.font = GetBuiltinFont();
            crText.fontSize = 16;
            crText.alignment = TextAnchor.LowerLeft;
            crText.color = new Color(0.35f, 0.4f, 0.5f);
            var crRect = cr.GetComponent<RectTransform>();
            crRect.anchorMin = new Vector2(0, 0);
            crRect.anchorMax = new Vector2(0, 0);
            crRect.pivot = new Vector2(0, 0);
            crRect.anchoredPosition = new Vector2(20, 10);
            crRect.sizeDelta = new Vector2(400, 30);

            WebGLOptimizer.LogVerbose("[GameBootstrapper] タイトル画面を生成");
        }

        // ================================================================
        // 難易度ボタン生成ヘルパー
        // ================================================================

        private static void CreateDifficultyButton(Transform parent, StartScreenController ctrl,
            string name, string label, string description,
            Color normal, Color highlight, Color pressed,
            float x, float y, float w, float h, GameDifficulty difficulty)
        {
            var btnGo = CreateUIElement(name, parent);
            var btnImg = btnGo.AddComponent<Image>();
            btnImg.color = normal;
            var btn = btnGo.AddComponent<Button>();
            var c = btn.colors;
            c.highlightedColor = highlight;
            c.pressedColor = pressed;
            btn.colors = c;
            btn.targetGraphic = btnImg;
            SetAnchored(btnGo.GetComponent<RectTransform>(), x, y, w, h);

            // ラベル（上部）
            var labelGo = CreateUIElement("Label", btnGo.transform);
            var labelText = labelGo.AddComponent<Text>();
            labelText.text = label;
            labelText.font = GetBuiltinFont();
            labelText.fontSize = 28;
            labelText.fontStyle = FontStyle.Bold;
            labelText.alignment = TextAnchor.MiddleCenter;
            labelText.color = Color.white;
            var labelRt = labelGo.GetComponent<RectTransform>();
            labelRt.anchorMin = new Vector2(0f, 0.45f);
            labelRt.anchorMax = new Vector2(1f, 1f);
            labelRt.offsetMin = Vector2.zero;
            labelRt.offsetMax = Vector2.zero;

            // 説明（下部）
            var descGo = CreateUIElement("Desc", btnGo.transform);
            var descText = descGo.AddComponent<Text>();
            descText.text = description;
            descText.font = GetBuiltinFont();
            descText.fontSize = 12;
            descText.alignment = TextAnchor.MiddleCenter;
            descText.color = new Color(0.85f, 0.88f, 0.92f, 0.8f);
            descText.lineSpacing = 1.1f;
            var descRt = descGo.GetComponent<RectTransform>();
            descRt.anchorMin = new Vector2(0f, 0f);
            descRt.anchorMax = new Vector2(1f, 0.45f);
            descRt.offsetMin = Vector2.zero;
            descRt.offsetMax = Vector2.zero;

            var diff = difficulty; // closure capture
            btn.onClick.AddListener(() => ctrl.OnStartWithDifficulty(diff));
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
            return FontManager.Regular;
        }
    }

    /// <summary>
    /// スタート画面の状態監視とボタンハンドラ。
    /// </summary>
    internal class StartScreenController : MonoBehaviour
    {
        private void Update()
        {
            if (GameManager.Instance != null &&
                GameManager.Instance.CurrentState != GameState.MainMenu)
            {
                Destroy(gameObject);
            }
        }

        public void OnStartWithDifficulty(GameDifficulty difficulty)
        {
            if (GameManager.Instance == null)
            {
                WebGLOptimizer.LogError("[GameBootstrapper] GameManager が見つかりません");
                return;
            }
            GameManager.Instance.StartNewGame(ThemeZone.LostKingdom, difficulty);
            WebGLOptimizer.LogVerbose($"[GameBootstrapper] ゲーム開始! 難易度: {difficulty}");
            Destroy(gameObject);
        }

        public void OnContinueClicked()
        {
            // 最初に見つかったセーブスロットをロード
            for (int i = 0; i < SaveSystem.MaxSlots; i++)
            {
                if (SaveSystem.HasSaveData(i))
                {
                    if (SaveSystem.Load(i))
                    {
                        WebGLOptimizer.LogVerbose($"[StartScreen] Loaded save slot {i}");
                        Destroy(gameObject);
                    }
                    else
                    {
                        WebGLOptimizer.LogError($"[StartScreen] Failed to load slot {i}");
                    }
                    return;
                }
            }
            WebGLOptimizer.LogWarning("[StartScreen] No save data found for continue");
        }

        public void OnScenarioModeClicked()
        {
            // タイトル画面のCanvasにステージ選択UIを表示
            var canvasRt = GetComponent<RectTransform>();
            StageSelectUI.Show(canvasRt);

            // タイトル画面の子要素を非表示（StageSelectUI以外）
            for (int i = 0; i < transform.childCount - 1; i++)
            {
                transform.GetChild(i).gameObject.SetActive(false);
            }
        }
    }
}
