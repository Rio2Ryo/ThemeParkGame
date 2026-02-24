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
                Debug.Log($"[WeatherEffect] Initial weather: {GameManager.Instance.WeatherSystem.CurrentWeather}");
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
            main.maxParticles = 3000;
            main.startLifetime = 1.2f;
            main.startSpeed = 25f;
            main.startSize = 0.08f;
            main.startColor = new Color(0.6f, 0.7f, 0.85f, 0.7f);
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.gravityModifier = 1.5f;
            main.loop = true;
            main.playOnAwake = false;

            var emission = ps.emission;
            emission.rateOverTime = 1500f;

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(60f, 0.1f, 60f);

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
            main.maxParticles = 2000;
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
            emission.rateOverTime = 400f;

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(60f, 0.1f, 60f);

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
            main.maxParticles = 200;
            main.startLifetime = 8f;
            main.startSpeed = 0.3f;
            main.startSize = new ParticleSystem.MinMaxCurve(0.05f, 0.15f);
            main.startColor = new Color(1f, 0.95f, 0.8f, 0.3f);
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.gravityModifier = -0.02f;
            main.loop = true;
            main.playOnAwake = false;

            var emission = ps.emission;
            emission.rateOverTime = 30f;

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(40f, 8f, 40f);

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

            Debug.Log($"[WeatherEffect] Transitioning to: {weather}");
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
            }
        }

        // ================================================================
        // 更新ループ
        // ================================================================

        private void Update()
        {
            if (GameManager.Instance == null) return;
            if (GameManager.Instance.CurrentState == GameState.MainMenu) return;

            FollowCamera();

            if (_isTransitioning)
            {
                UpdateTransition();
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

            if (t >= 1f)
            {
                _isTransitioning = false;
                _currentWeather = _targetWeather;
            }
        }

        /// <summary>天候に応じてパーティクルシステムのON/OFFを制御する</summary>
        private void UpdateParticleSystems(float t)
        {
            // 雨
            if (_rainPS != null)
            {
                if (_targetWeather == Weather.Rainy)
                {
                    if (!_rainPS.isPlaying) _rainPS.Play();
                    // 徐々にパーティクル量を増やす
                    var emission = _rainPS.emission;
                    emission.rateOverTime = Mathf.Lerp(0f, 1500f, t);
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
                    emission.rateOverTime = Mathf.Lerp(0f, 400f, t);
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
                    float targetRate = _targetWeather == Weather.Hot ? 60f : 30f;
                    emission.rateOverTime = Mathf.Lerp(0f, targetRate, t);
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
                    if (_rainPS != null) _rainPS.Play();
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
