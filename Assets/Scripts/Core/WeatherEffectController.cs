// ============================================================
// ThemeParkGame - WeatherEffectController
// 天候に応じた視覚エフェクト（パーティクル・ライティング・フォグ・UIオーバーレイ）
// GameEvents.OnWeatherChanged に連動し自動で演出を切り替える
// ============================================================

using UnityEngine;
using UnityEngine.UI;

namespace ThemeParkGame.Core
{
    /// <summary>
    /// 天候の視覚エフェクトを管理する。
    /// 雨・雪のパーティクルシステム、ライティング変更、フォグ、
    /// UIオーバーレイ（画面全体の色調補正）を統合制御する。
    /// すべてコードから生成するため、プレハブ不要。
    /// </summary>
    public class WeatherEffectController : MonoBehaviour
    {
        // ---- パーティクルシステム ----
        private ParticleSystem _rainPS;
        private ParticleSystem _snowPS;
        private ParticleSystem _sunDustPS;

        // ---- ライティング ----
        private Light _directionalLight;
        private Color _defaultLightColor;
        private float _defaultLightIntensity;
        private Color _defaultAmbientColor;

        // ---- UIオーバーレイ（画面全体の色調） ----
        private Canvas _overlayCanvas;
        private Image _overlayImage;
        private CanvasGroup _overlayCanvasGroup;

        // ---- WebGLプラットフォーム判定 ----
        private static readonly bool IsWebGL = Application.platform == RuntimePlatform.WebGLPlayer;

        // ---- パーティクル品質段階 ----
        // QualitySettings.GetQualityLevel() + プラットフォームに基づく4段階品質
        private enum ParticleQuality { Low = 0, Medium = 1, High = 2, Ultra = 3 }
        private ParticleQuality _currentQuality;

        // 品質別パーティクル上限テーブル [Low, Medium, High, Ultra]
        private static readonly int[] RainMaxParticlesTable    = { 400,  800, 2000, 3000 };
        private static readonly float[] RainEmissionRateTable  = { 250f, 500f, 1000f, 1500f };
        private static readonly int[] SnowMaxParticlesTable    = { 250,  500, 1500, 2000 };
        private static readonly float[] SnowEmissionRateTable  = { 80f,  150f, 300f, 400f };
        private static readonly int[] DustMaxParticlesTable     = { 50,   100,  150,  200 };
        private static readonly float[] DustEmissionNormalTable = { 10f,  20f,  30f,  30f };
        private static readonly float[] DustEmissionHotTable    = { 15f,  20f,  40f,  60f };

        // 品質別パーティクル形状スケール
        private static readonly Vector3[] RainSnowShapeScaleTable = {
            new Vector3(30f, 0.1f, 30f),  // Low
            new Vector3(40f, 0.1f, 40f),  // Medium
            new Vector3(50f, 0.1f, 50f),  // High
            new Vector3(60f, 0.1f, 60f),  // Ultra
        };
        private static readonly Vector3[] DustShapeScaleTable = {
            new Vector3(20f, 5f, 20f),    // Low
            new Vector3(25f, 6f, 25f),    // Medium
            new Vector3(35f, 7f, 35f),    // High
            new Vector3(40f, 8f, 40f),    // Ultra
        };

        // 品質テーブル参照ヘルパー
        private int QI => (int)_currentQuality; // Quality Index
        private int RainMaxParticles => RainMaxParticlesTable[QI];
        private float RainEmissionRate => RainEmissionRateTable[QI];
        private int SnowMaxParticles => SnowMaxParticlesTable[QI];
        private float SnowEmissionRate => SnowEmissionRateTable[QI];
        private int SunDustMaxParticles => DustMaxParticlesTable[QI];
        private float SunDustEmissionRateNormal => DustEmissionNormalTable[QI];
        private float SunDustEmissionRateHot => DustEmissionHotTable[QI];
        private Vector3 RainSnowShapeScale => RainSnowShapeScaleTable[QI];
        private Vector3 SunDustShapeScale => DustShapeScaleTable[QI];

        // ---- FPSベース自動LOD降格 ----
        private int _particleLOD = 0; // 0=なし, 1=1段階降格, 2=2段階降格
        private float _fpsAccumulator;
        private int _fpsFrameCount;
        private float _fpsCheckInterval = 1f;
        private float _fpsCheckTimer;
        private float _lowFpsDuration; // 30fps以下が続いた秒数
        private int _lastQualityLevel = -1; // QualitySettings変更検出用

        // ---- 雷雨フラッシュ制御 ----
        private float _thunderFlashTimer;
        private float _thunderFlashDuration;
        private float _nextThunderFlashInterval;
        private bool _isThunderFlashing;

        // ---- 現在の天候 ----
        private Weather _currentWeather = Weather.Sunny;

        // ---- 遷移制御 ----
        private Weather _targetWeather = Weather.Sunny;
        private float _transitionTimer;
        private const float TransitionDuration = 2f;
        private bool _isTransitioning;

        // ---- ライティング目標値 ----
        private Color _targetLightColor;
        private float _targetLightIntensity;
        private Color _targetAmbientColor;
        private Color _targetOverlayColor;
        private float _targetFogDensity;
        private Color _targetFogColor;
        private bool _targetFogEnabled;

        // ================================================================
        // 初期化
        // ================================================================

        private void Awake()
        {
            _currentQuality = DetermineParticleQuality();
            _lastQualityLevel = QualitySettings.GetQualityLevel();
            WebGLOptimizer.LogVerbose($"[WeatherEffect] ParticleQuality={_currentQuality} (QualityLevel={_lastQualityLevel}, WebGL={IsWebGL})");

            FindDirectionalLight();
            SaveDefaultLighting();
            CreateParticleSystems();
            CreateOverlayCanvas();

            GameEvents.OnWeatherChanged += OnWeatherChanged;
        }

        private void Start()
        {
            // WeatherSystemがすでに初期化済みの場合、現在の天候を即座に適用
            if (GameManager.Instance != null && GameManager.Instance.WeatherSystem != null)
            {
                ApplyImmediate(GameManager.Instance.WeatherSystem.CurrentWeather);
                WebGLOptimizer.LogVerbose($"[WeatherEffect] Initial weather: {GameManager.Instance.WeatherSystem.CurrentWeather}");
            }
        }

        private void OnDestroy()
        {
            GameEvents.OnWeatherChanged -= OnWeatherChanged;
            RestoreDefaultLighting();
        }

        // ================================================================
        // DirectionalLight 検出
        // ================================================================

        private void FindDirectionalLight()
        {
            var lights = FindObjectsOfType<Light>();
            foreach (var l in lights)
            {
                if (l.type == LightType.Directional)
                {
                    _directionalLight = l;
                    break;
                }
            }

            if (_directionalLight == null)
            {
                var go = new GameObject("WeatherLight");
                go.transform.SetParent(transform);
                _directionalLight = go.AddComponent<Light>();
                _directionalLight.type = LightType.Directional;
                _directionalLight.transform.eulerAngles = new Vector3(50f, -30f, 0f);
            }
        }

        private void SaveDefaultLighting()
        {
            _defaultLightColor = _directionalLight.color;
            _defaultLightIntensity = _directionalLight.intensity;
            _defaultAmbientColor = RenderSettings.ambientLight;
        }

        private void RestoreDefaultLighting()
        {
            if (_directionalLight != null)
            {
                _directionalLight.color = _defaultLightColor;
                _directionalLight.intensity = _defaultLightIntensity;
            }
            RenderSettings.ambientLight = _defaultAmbientColor;
            RenderSettings.fog = false;
        }

        // ================================================================
        // パーティクルシステム生成
        // ================================================================

        private void CreateParticleSystems()
        {
            _rainPS = CreateRainParticleSystem();
            _snowPS = CreateSnowParticleSystem();
            _sunDustPS = CreateSunDustParticleSystem();

            _rainPS.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            _snowPS.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            _sunDustPS.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        }

        /// <summary>雨パーティクルシステムを生成する</summary>
        private ParticleSystem CreateRainParticleSystem()
        {
            var go = new GameObject("RainEffect");
            go.transform.SetParent(transform);
            go.transform.localPosition = new Vector3(0f, 30f, 0f);

            var ps = go.AddComponent<ParticleSystem>();
            var main = ps.main;
            main.maxParticles = RainMaxParticles;
            main.startLifetime = 1.2f;
            main.startSpeed = 25f;
            main.startSize = 0.08f;
            main.startColor = new Color(0.6f, 0.7f, 0.85f, 0.7f);
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.gravityModifier = 1.5f;
            main.loop = true;
            main.playOnAwake = false;

            var emission = ps.emission;
            emission.rateOverTime = RainEmissionRate;

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = RainSnowShapeScale;

            WebGLOptimizer.LogVerbose($"[WeatherEffect] Rain PS: maxParticles={RainMaxParticles}, emission={RainEmissionRate}, shape={RainSnowShapeScale}");

            // 描画設定
            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Stretch;
            renderer.lengthScale = 8f;
            renderer.velocityScale = 0.1f;
            renderer.material = CreateParticleMaterial(new Color(0.6f, 0.7f, 0.85f, 0.5f));

            // 風で少し斜めに
            var vel = ps.velocityOverLifetime;
            vel.enabled = true;
            vel.x = new ParticleSystem.MinMaxCurve(-2f, 2f);
            vel.z = new ParticleSystem.MinMaxCurve(-1f, 1f);

            return ps;
        }

        /// <summary>雪パーティクルシステムを生成する</summary>
        private ParticleSystem CreateSnowParticleSystem()
        {
            var go = new GameObject("SnowEffect");
            go.transform.SetParent(transform);
            go.transform.localPosition = new Vector3(0f, 30f, 0f);

            var ps = go.AddComponent<ParticleSystem>();
            var main = ps.main;
            main.maxParticles = SnowMaxParticles;
            main.startLifetime = 6f;
            main.startSpeed = 2f;
            main.startSize = new ParticleSystem.MinMaxCurve(0.15f, 0.35f);
            main.startColor = new Color(0.95f, 0.95f, 1f, 0.85f);
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.gravityModifier = 0.15f;
            main.loop = true;
            main.playOnAwake = false;

            // ゆっくり回転
            main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);

            var emission = ps.emission;
            emission.rateOverTime = SnowEmissionRate;

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = RainSnowShapeScale;

            WebGLOptimizer.LogVerbose($"[WeatherEffect] Snow PS: maxParticles={SnowMaxParticles}, emission={SnowEmissionRate}, shape={RainSnowShapeScale}");

            // 描画設定
            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.material = CreateParticleMaterial(new Color(0.95f, 0.95f, 1f, 0.8f));

            // ふわふわ揺れる
            var vel = ps.velocityOverLifetime;
            vel.enabled = true;
            vel.x = new ParticleSystem.MinMaxCurve(-1.5f, 1.5f);
            vel.z = new ParticleSystem.MinMaxCurve(-1.5f, 1.5f);

            // サイズ変化
            var sizeOverLifetime = ps.sizeOverLifetime;
            sizeOverLifetime.enabled = true;
            sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f,
                new AnimationCurve(
                    new Keyframe(0f, 0.5f),
                    new Keyframe(0.3f, 1f),
                    new Keyframe(1f, 0.3f)
                ));

            // 回転
            var rotOverLifetime = ps.rotationOverLifetime;
            rotOverLifetime.enabled = true;
            rotOverLifetime.z = new ParticleSystem.MinMaxCurve(-90f * Mathf.Deg2Rad, 90f * Mathf.Deg2Rad);

            return ps;
        }

        /// <summary>晴れの陽光ダストパーティクルシステムを生成する</summary>
        private ParticleSystem CreateSunDustParticleSystem()
        {
            var go = new GameObject("SunDustEffect");
            go.transform.SetParent(transform);
            go.transform.localPosition = new Vector3(0f, 10f, 0f);

            var ps = go.AddComponent<ParticleSystem>();
            var main = ps.main;
            main.maxParticles = SunDustMaxParticles;
            main.startLifetime = 8f;
            main.startSpeed = 0.3f;
            main.startSize = new ParticleSystem.MinMaxCurve(0.05f, 0.15f);
            main.startColor = new Color(1f, 0.95f, 0.8f, 0.3f);
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.gravityModifier = -0.02f;
            main.loop = true;
            main.playOnAwake = false;

            var emission = ps.emission;
            emission.rateOverTime = SunDustEmissionRateNormal;

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = SunDustShapeScale;

            WebGLOptimizer.LogVerbose($"[WeatherEffect] SunDust PS: maxParticles={SunDustMaxParticles}, emission={SunDustEmissionRateNormal}, shape={SunDustShapeScale}");

            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.material = CreateParticleMaterial(new Color(1f, 0.95f, 0.8f, 0.25f));

            // フェードイン・アウト
            var colorOverLifetime = ps.colorOverLifetime;
            colorOverLifetime.enabled = true;
            Gradient grad = new Gradient();
            grad.SetKeys(
                new GradientColorKey[] {
                    new GradientColorKey(Color.white, 0f),
                    new GradientColorKey(Color.white, 1f)
                },
                new GradientAlphaKey[] {
                    new GradientAlphaKey(0f, 0f),
                    new GradientAlphaKey(0.3f, 0.2f),
                    new GradientAlphaKey(0.3f, 0.8f),
                    new GradientAlphaKey(0f, 1f)
                }
            );
            colorOverLifetime.color = grad;

            return ps;
        }

        /// <summary>パーティクル用のデフォルトマテリアルを生成する</summary>
        private Material CreateParticleMaterial(Color color)
        {
            // Particles/Standard Unlit がない場合のフォールバック
            Shader shader = Shader.Find("Particles/Standard Unlit");
            if (shader == null) shader = Shader.Find("Legacy Shaders/Particles/Alpha Blended");
            if (shader == null) shader = Shader.Find("Sprites/Default");
            if (shader == null) shader = Shader.Find("Unlit/Transparent");

            var mat = new Material(shader);
            mat.color = color;

            // Additive or Alpha blending
            if (mat.HasProperty("_Mode"))
            {
                mat.SetFloat("_Mode", 2); // Fade
            }

            return mat;
        }

        // ================================================================
        // UIオーバーレイ（画面全体の色調補正）
        // ================================================================

        private void CreateOverlayCanvas()
        {
            var go = new GameObject("WeatherOverlay");
            go.transform.SetParent(transform);

            _overlayCanvas = go.AddComponent<Canvas>();
            _overlayCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _overlayCanvas.sortingOrder = 45; // HUDより下

            var scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);

            _overlayCanvasGroup = go.AddComponent<CanvasGroup>();
            _overlayCanvasGroup.blocksRaycasts = false;
            _overlayCanvasGroup.interactable = false;

            var imgGo = new GameObject("OverlayTint");
            imgGo.transform.SetParent(go.transform, false);
            _overlayImage = imgGo.AddComponent<Image>();
            _overlayImage.color = new Color(0f, 0f, 0f, 0f);
            _overlayImage.raycastTarget = false;

            var rt = imgGo.GetComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }

        // ================================================================
        // 天候変更イベントハンドラ
        // ================================================================

        private void OnWeatherChanged(Weather weather)
        {
            _targetWeather = weather;
            _isTransitioning = true;
            _transitionTimer = 0f;

            SetTargetValues(weather);

            WebGLOptimizer.LogVerbose($"[WeatherEffect] Transitioning to: {weather}");
        }

        /// <summary>天候ごとの目標値を設定する</summary>
        private void SetTargetValues(Weather weather)
        {
            switch (weather)
            {
                case Weather.Sunny:
                    _targetLightColor = new Color(1f, 0.96f, 0.84f);
                    _targetLightIntensity = 1.3f;
                    _targetAmbientColor = new Color(0.45f, 0.5f, 0.55f);
                    _targetOverlayColor = new Color(0f, 0f, 0f, 0f);
                    _targetFogEnabled = false;
                    _targetFogDensity = 0f;
                    _targetFogColor = Color.white;
                    break;

                case Weather.Cloudy:
                    _targetLightColor = new Color(0.8f, 0.82f, 0.85f);
                    _targetLightIntensity = 0.8f;
                    _targetAmbientColor = new Color(0.4f, 0.42f, 0.45f);
                    _targetOverlayColor = new Color(0.3f, 0.35f, 0.4f, 0.08f);
                    _targetFogEnabled = true;
                    _targetFogDensity = 0.003f;
                    _targetFogColor = new Color(0.7f, 0.72f, 0.75f);
                    break;

                case Weather.Rainy:
                    _targetLightColor = new Color(0.6f, 0.65f, 0.75f);
                    _targetLightIntensity = 0.5f;
                    _targetAmbientColor = new Color(0.3f, 0.33f, 0.4f);
                    _targetOverlayColor = new Color(0.15f, 0.2f, 0.35f, 0.15f);
                    _targetFogEnabled = true;
                    _targetFogDensity = 0.008f;
                    _targetFogColor = new Color(0.5f, 0.55f, 0.65f);
                    break;

                case Weather.Snowy:
                    _targetLightColor = new Color(0.85f, 0.88f, 1f);
                    _targetLightIntensity = 0.7f;
                    _targetAmbientColor = new Color(0.5f, 0.55f, 0.65f);
                    _targetOverlayColor = new Color(0.7f, 0.75f, 0.85f, 0.1f);
                    _targetFogEnabled = true;
                    _targetFogDensity = 0.006f;
                    _targetFogColor = new Color(0.8f, 0.82f, 0.9f);
                    break;

                case Weather.Hot:
                    _targetLightColor = new Color(1f, 0.9f, 0.7f);
                    _targetLightIntensity = 1.5f;
                    _targetAmbientColor = new Color(0.55f, 0.5f, 0.4f);
                    _targetOverlayColor = new Color(1f, 0.85f, 0.4f, 0.06f);
                    _targetFogEnabled = true;
                    _targetFogDensity = 0.002f;
                    _targetFogColor = new Color(0.95f, 0.9f, 0.8f);
                    break;

                case Weather.Typhoon:
                    _targetLightColor = new Color(0.35f, 0.38f, 0.45f);
                    _targetLightIntensity = 0.3f;
                    _targetAmbientColor = new Color(0.2f, 0.22f, 0.3f);
                    _targetOverlayColor = new Color(0.05f, 0.08f, 0.15f, 0.4f); // 暗い画面オーバーレイ alpha 0.4
                    _targetFogEnabled = true;
                    _targetFogDensity = 0.015f;
                    _targetFogColor = new Color(0.35f, 0.38f, 0.45f);
                    break;

                case Weather.Thunderstorm:
                    _targetLightColor = new Color(0.5f, 0.52f, 0.6f);
                    _targetLightIntensity = 0.4f;
                    _targetAmbientColor = new Color(0.25f, 0.28f, 0.35f);
                    _targetOverlayColor = new Color(0.1f, 0.12f, 0.25f, 0.2f);
                    _targetFogEnabled = true;
                    _targetFogDensity = 0.01f;
                    _targetFogColor = new Color(0.4f, 0.42f, 0.5f);
                    _nextThunderFlashInterval = Random.Range(3f, 8f);
                    _thunderFlashTimer = 0f;
                    break;
            }
        }

        // ================================================================
        // 更新ループ
        // ================================================================

        private void Update()
        {
            if (GameManager.Instance == null) return;
            if (GameManager.Instance.CurrentState == GameState.MainMenu) return;

            UpdateParticleLOD();
            FollowCamera();

            if (_isTransitioning)
            {
                UpdateTransition();
            }
            else
            {
                // 遷移完了後も雷雨フラッシュは継続
                UpdateThunderFlash();
            }
        }

        /// <summary>
        /// QualitySettings.GetQualityLevel() とプラットフォームから品質段階を決定する。
        /// WebGLは最大Mediumに制限。デスクトップはQualityLevelに応じて4段階。
        /// </summary>
        private static ParticleQuality DetermineParticleQuality()
        {
            int level = QualitySettings.GetQualityLevel();

            if (IsWebGL)
            {
                // WebGL: Low (level 0-1) / Medium (level 2+)。High以上は許可しない。
                return level >= 2 ? ParticleQuality.Medium : ParticleQuality.Low;
            }

            // デスクトップ: 0-1=Low, 2-3=Medium, 4=High, 5+=Ultra
            if (level <= 1) return ParticleQuality.Low;
            if (level <= 3) return ParticleQuality.Medium;
            if (level <= 4) return ParticleQuality.High;
            return ParticleQuality.Ultra;
        }

        /// <summary>
        /// パーティクル品質を手動で設定する（設定メニュー等から呼び出し用）。
        /// FPS LOD降格もリセットされる。
        /// </summary>
        public void SetParticleQuality(int qualityIndex)
        {
            int maxQ = IsWebGL ? (int)ParticleQuality.Medium : (int)ParticleQuality.Ultra;
            qualityIndex = Mathf.Clamp(qualityIndex, 0, maxQ);
            _currentQuality = (ParticleQuality)qualityIndex;
            _particleLOD = 0;
            _lowFpsDuration = 0f;
            ApplyQualityToParticleSystems();
            WebGLOptimizer.LogVerbose($"[WeatherEffect] 品質手動設定: {_currentQuality}");
        }

        /// <summary>現在のパーティクル品質段階を返す（0=Low, 1=Medium, 2=High, 3=Ultra）</summary>
        public int CurrentParticleQuality => (int)_currentQuality;

        /// <summary>品質設定をパーティクルシステムに反映する</summary>
        private void ApplyQualityToParticleSystems()
        {
            if (_rainPS != null)
            {
                var main = _rainPS.main;
                main.maxParticles = RainMaxParticles;
                var shape = _rainPS.shape;
                shape.scale = RainSnowShapeScale;
            }
            if (_snowPS != null)
            {
                var main = _snowPS.main;
                main.maxParticles = SnowMaxParticles;
                var shape = _snowPS.shape;
                shape.scale = RainSnowShapeScale;
            }
            if (_sunDustPS != null)
            {
                var main = _sunDustPS.main;
                main.maxParticles = SunDustMaxParticles;
                var shape = _sunDustPS.shape;
                shape.scale = SunDustShapeScale;
            }
        }

        /// <summary>FPSを監視しパーティクルLODを自動調整。QualitySettings変更も検出。</summary>
        private void UpdateParticleLOD()
        {
            // QualitySettings変更を検出して品質を再決定
            int currentLevel = QualitySettings.GetQualityLevel();
            if (currentLevel != _lastQualityLevel)
            {
                _lastQualityLevel = currentLevel;
                _currentQuality = DetermineParticleQuality();
                _particleLOD = 0;
                _lowFpsDuration = 0f;
                ApplyQualityToParticleSystems();
                WebGLOptimizer.LogVerbose($"[WeatherEffect] QualitySettings変更検出: level={currentLevel} → {_currentQuality}");
            }

            // FPS計測
            _fpsAccumulator += Time.unscaledDeltaTime;
            _fpsFrameCount++;
            _fpsCheckTimer += Time.unscaledDeltaTime;

            if (_fpsCheckTimer < _fpsCheckInterval) return;

            float avgFps = _fpsFrameCount / _fpsAccumulator;
            _fpsAccumulator = 0f;
            _fpsFrameCount = 0;
            _fpsCheckTimer = 0f;

            // 30fps以下が続いた秒数を追跡
            if (avgFps < 30f)
            {
                _lowFpsDuration += _fpsCheckInterval;
            }
            else
            {
                _lowFpsDuration = 0f;
            }

            // 3秒以上30fps以下が続いたらLODを下げる（品質の範囲内で最大2段階降格）
            if (_lowFpsDuration >= 3f && _particleLOD < 2)
            {
                _particleLOD++;
                _lowFpsDuration = 0f;
                WebGLOptimizer.LogVerbose($"[WeatherEffect] FPS LOD降格: LOD={_particleLOD}, Quality={_currentQuality} (avgFps={avgFps:F1})");
            }
        }

        /// <summary>LODに応じたemissionRateスケールを返す（0=1.0, 1=0.5, 2=0.25）</summary>
        private float GetLODScale()
        {
            switch (_particleLOD)
            {
                case 1: return 0.5f;
                case 2: return 0.25f;
                default: return 1f;
            }
        }

        /// <summary>パーティクルシステムをカメラに追従させる</summary>
        private void FollowCamera()
        {
            Camera cam = Camera.main;
            if (cam == null) return;

            Vector3 camPos = cam.transform.position;
            // パーティクルエミッターをカメラ頭上に移動
            if (_rainPS != null)
                _rainPS.transform.position = new Vector3(camPos.x, 30f, camPos.z);
            if (_snowPS != null)
                _snowPS.transform.position = new Vector3(camPos.x, 30f, camPos.z);
            if (_sunDustPS != null)
                _sunDustPS.transform.position = new Vector3(camPos.x, 10f, camPos.z);
        }

        /// <summary>天候遷移アニメーション</summary>
        private void UpdateTransition()
        {
            _transitionTimer += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(_transitionTimer / TransitionDuration);
            float smooth = Mathf.SmoothStep(0f, 1f, t);

            // ライティング補間
            if (_directionalLight != null)
            {
                _directionalLight.color = Color.Lerp(_directionalLight.color, _targetLightColor, smooth);
                _directionalLight.intensity = Mathf.Lerp(_directionalLight.intensity, _targetLightIntensity, smooth);
            }

            // アンビエントライト補間
            RenderSettings.ambientLight = Color.Lerp(RenderSettings.ambientLight, _targetAmbientColor, smooth);

            // フォグ
            if (_targetFogEnabled)
            {
                RenderSettings.fog = true;
                RenderSettings.fogMode = FogMode.ExponentialSquared;
                RenderSettings.fogDensity = Mathf.Lerp(RenderSettings.fogDensity, _targetFogDensity, smooth);
                RenderSettings.fogColor = Color.Lerp(RenderSettings.fogColor, _targetFogColor, smooth);
            }
            else
            {
                RenderSettings.fogDensity = Mathf.Lerp(RenderSettings.fogDensity, 0f, smooth);
                if (RenderSettings.fogDensity < 0.0001f)
                    RenderSettings.fog = false;
            }

            // UIオーバーレイ
            if (_overlayImage != null)
            {
                _overlayImage.color = Color.Lerp(_overlayImage.color, _targetOverlayColor, smooth);
            }

            // パーティクルシステム制御
            UpdateParticleSystems(smooth);

            // 雷雨の画面フラッシュ
            UpdateThunderFlash();

            if (t >= 1f)
            {
                _isTransitioning = false;
                _currentWeather = _targetWeather;
            }
        }

        /// <summary>雷雨時の不定期ホワイトフラッシュを制御する</summary>
        private void UpdateThunderFlash()
        {
            // 雷雨中のみフラッシュを処理
            bool isThunderstorm = _targetWeather == Weather.Thunderstorm
                               || _currentWeather == Weather.Thunderstorm;
            if (!isThunderstorm)
            {
                _isThunderFlashing = false;
                return;
            }

            // アクセシビリティ: フラッシュ軽減モードの場合、ホワイトフラッシュを抑制
            if (AccessibilitySystem.Instance != null && AccessibilitySystem.Instance.ReduceFlashEnabled)
            {
                _isThunderFlashing = false;
                return;
            }

            if (_isThunderFlashing)
            {
                // フラッシュ中: オーバーレイを白くフラッシュ
                _thunderFlashDuration -= Time.unscaledDeltaTime;
                if (_thunderFlashDuration <= 0f)
                {
                    _isThunderFlashing = false;
                    // フラッシュ終了: 元のオーバーレイ色に戻す
                    if (_overlayImage != null)
                        _overlayImage.color = _targetOverlayColor;
                }
                else
                {
                    // フラッシュ中の白い点滅
                    float flashAlpha = Mathf.PingPong(_thunderFlashDuration * 15f, 1f) * 0.6f;
                    if (_overlayImage != null)
                        _overlayImage.color = new Color(1f, 1f, 1f, flashAlpha);
                }
            }
            else
            {
                // フラッシュ間隔カウントダウン
                _thunderFlashTimer += Time.unscaledDeltaTime;
                if (_thunderFlashTimer >= _nextThunderFlashInterval)
                {
                    _isThunderFlashing = true;
                    _thunderFlashDuration = Random.Range(0.1f, 0.4f);
                    _thunderFlashTimer = 0f;
                    _nextThunderFlashInterval = Random.Range(3f, 10f);
                }
            }
        }

        /// <summary>天候に応じてパーティクルシステムのON/OFFを制御する</summary>
        private void UpdateParticleSystems(float t)
        {
            float lodScale = GetLODScale();

            // 雨（通常雨・台風・雷雨で共有）
            bool needsRain = _targetWeather == Weather.Rainy
                          || _targetWeather == Weather.Typhoon
                          || _targetWeather == Weather.Thunderstorm;
            if (_rainPS != null)
            {
                if (needsRain)
                {
                    if (!_rainPS.isPlaying) _rainPS.Play();
                    var emission = _rainPS.emission;
                    // 台風は雨PSのemission rate 3倍
                    float rateMultiplier = _targetWeather == Weather.Typhoon ? 3f : 1f;
                    emission.rateOverTime = Mathf.Lerp(0f, RainEmissionRate * lodScale * rateMultiplier, t);

                    // 台風: 横方向velocityオーバーライド（強風）
                    var vel = _rainPS.velocityOverLifetime;
                    vel.enabled = true;
                    if (_targetWeather == Weather.Typhoon)
                    {
                        vel.x = new ParticleSystem.MinMaxCurve(8f, 15f);
                        vel.z = new ParticleSystem.MinMaxCurve(-3f, 5f);
                    }
                    else
                    {
                        // 通常の雨・雷雨は軽い風
                        vel.x = new ParticleSystem.MinMaxCurve(-2f, 2f);
                        vel.z = new ParticleSystem.MinMaxCurve(-1f, 1f);
                    }
                }
                else
                {
                    if (_rainPS.isPlaying)
                    {
                        var emission = _rainPS.emission;
                        emission.rateOverTime = Mathf.Lerp(emission.rateOverTime.constant, 0f, t);
                        if (emission.rateOverTime.constant < 1f)
                            _rainPS.Stop(true, ParticleSystemStopBehavior.StopEmitting);
                    }
                }
            }

            // 雪
            if (_snowPS != null)
            {
                if (_targetWeather == Weather.Snowy)
                {
                    if (!_snowPS.isPlaying) _snowPS.Play();
                    var emission = _snowPS.emission;
                    emission.rateOverTime = Mathf.Lerp(0f, SnowEmissionRate * lodScale, t);
                }
                else
                {
                    if (_snowPS.isPlaying)
                    {
                        var emission = _snowPS.emission;
                        emission.rateOverTime = Mathf.Lerp(emission.rateOverTime.constant, 0f, t);
                        if (emission.rateOverTime.constant < 1f)
                            _snowPS.Stop(true, ParticleSystemStopBehavior.StopEmitting);
                    }
                }
            }

            // 陽光ダスト（晴れ・猛暑）
            if (_sunDustPS != null)
            {
                if (_targetWeather == Weather.Sunny || _targetWeather == Weather.Hot)
                {
                    if (!_sunDustPS.isPlaying) _sunDustPS.Play();
                    var emission = _sunDustPS.emission;
                    float targetRate = _targetWeather == Weather.Hot
                        ? SunDustEmissionRateHot
                        : SunDustEmissionRateNormal;
                    emission.rateOverTime = Mathf.Lerp(0f, targetRate * lodScale, t);
                }
                else
                {
                    if (_sunDustPS.isPlaying)
                    {
                        var emission = _sunDustPS.emission;
                        emission.rateOverTime = Mathf.Lerp(emission.rateOverTime.constant, 0f, t);
                        if (emission.rateOverTime.constant < 1f)
                            _sunDustPS.Stop(true, ParticleSystemStopBehavior.StopEmitting);
                    }
                }
            }
        }

        // ================================================================
        // 外部API
        // ================================================================

        /// <summary>現在のエフェクト天候を返す</summary>
        public Weather CurrentEffectWeather => _currentWeather;

        /// <summary>天候エフェクトを即座に適用する（遷移アニメーションなし）</summary>
        public void ApplyImmediate(Weather weather)
        {
            _targetWeather = weather;
            _currentWeather = weather;
            _isTransitioning = false;

            SetTargetValues(weather);

            // ライティング即時適用
            if (_directionalLight != null)
            {
                _directionalLight.color = _targetLightColor;
                _directionalLight.intensity = _targetLightIntensity;
            }
            RenderSettings.ambientLight = _targetAmbientColor;

            // フォグ即時適用
            RenderSettings.fog = _targetFogEnabled;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogDensity = _targetFogDensity;
            RenderSettings.fogColor = _targetFogColor;

            // UIオーバーレイ
            if (_overlayImage != null)
                _overlayImage.color = _targetOverlayColor;

            // パーティクル
            StopAllParticles();
            switch (weather)
            {
                case Weather.Rainy:
                case Weather.Thunderstorm:
                    if (_rainPS != null) _rainPS.Play();
                    break;
                case Weather.Typhoon:
                    if (_rainPS != null)
                    {
                        _rainPS.Play();
                        // 台風は即座に3倍emission + 横風
                        var emission = _rainPS.emission;
                        emission.rateOverTime = RainEmissionRate * 3f;
                        var vel = _rainPS.velocityOverLifetime;
                        vel.enabled = true;
                        vel.x = new ParticleSystem.MinMaxCurve(8f, 15f);
                        vel.z = new ParticleSystem.MinMaxCurve(-3f, 5f);
                    }
                    break;
                case Weather.Snowy:
                    if (_snowPS != null) _snowPS.Play();
                    break;
                case Weather.Sunny:
                case Weather.Hot:
                    if (_sunDustPS != null) _sunDustPS.Play();
                    break;
            }
        }

        private void StopAllParticles()
        {
            if (_rainPS != null) _rainPS.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            if (_snowPS != null) _snowPS.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            if (_sunDustPS != null) _sunDustPS.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        }
    }
}
