// ============================================================
// ThemeParkGame - ParkEffectsManager
// パーク内のパーティクルエフェクト管理
// ライト・噴水・煙をParticleSystemで表現
// ============================================================

using System.Collections.Generic;
using UnityEngine;

namespace ThemeParkGame.Core
{
    /// <summary>
    /// パーク内のビジュアルエフェクトを管理する。
    /// ParticleSystemを使ってライト(きらめき)、噴水、煙エフェクトを生成する。
    /// RuntimeGameSetupから呼び出される。
    /// </summary>
    public class ParkEffectsManager : MonoBehaviour
    {
        public static ParkEffectsManager Instance { get; private set; }

        private readonly List<ParticleSystem> _activeEffects = new List<ParticleSystem>();

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        // ============================================================
        // パーク全体のエフェクト配置
        // ============================================================

        /// <summary>パーク全体にデフォルトのエフェクトを配置する</summary>
        public void SetupParkEffects()
        {
            // 中央噴水
            CreateFountain(new Vector3(0f, 0f, 5f), 2.5f);

            // エントランスのきらめきライト
            CreateSparkleLight(new Vector3(0f, 3f, -5f), 3f, new Color(1f, 0.9f, 0.5f));
            CreateSparkleLight(new Vector3(3f, 3f, -5f), 2f, new Color(1f, 0.5f, 0.5f));
            CreateSparkleLight(new Vector3(-3f, 3f, -5f), 2f, new Color(0.5f, 0.8f, 1f));

            // アトラクション周辺のきらめき
            var attractions = FindObjectsOfType<Attraction.Attraction>();
            foreach (var attr in attractions)
            {
                Vector3 pos = attr.transform.position + Vector3.up * 5f;
                Color c = Random.ColorHSV(0f, 1f, 0.7f, 1f, 0.8f, 1f);
                CreateSparkleLight(pos, 1.5f, c);
            }

            // パーク周辺のアンビエント煙/霧
            CreateAmbientSmoke(new Vector3(20f, 0f, 20f));
            CreateAmbientSmoke(new Vector3(-20f, 0f, 20f));

            WebGLOptimizer.LogVerbose($"[ParkEffectsManager] エフェクト配置完了: {_activeEffects.Count}個");
        }

        // ============================================================
        // きらめきライトエフェクト
        // ============================================================

        /// <summary>きらめくライトパーティクルを生成</summary>
        public ParticleSystem CreateSparkleLight(Vector3 position, float radius, Color color)
        {
            var go = new GameObject("SparkleLight");
            go.transform.SetParent(transform);
            go.transform.position = position;

            var ps = go.AddComponent<ParticleSystem>();
            var main = ps.main;
            main.startLifetime = 1.5f;
            main.startSpeed = 0.3f;
            main.startSize = new ParticleSystem.MinMaxCurve(0.05f, 0.15f);
            main.startColor = new ParticleSystem.MinMaxGradient(
                color, new Color(color.r, color.g, color.b, 0.3f));
            main.maxParticles = 30;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.loop = true;
            main.gravityModifier = -0.1f; // 上にふわっと浮く

            var emission = ps.emission;
            emission.rateOverTime = 8f;

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = radius;

            var col = ps.colorOverLifetime;
            col.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(
                new[] { new GradientColorKey(color, 0f), new GradientColorKey(color, 1f) },
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.2f),
                        new GradientAlphaKey(1f, 0.7f), new GradientAlphaKey(0f, 1f) }
            );
            col.color = gradient;

            var sizeOverLifetime = ps.sizeOverLifetime;
            sizeOverLifetime.enabled = true;
            sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f,
                new AnimationCurve(
                    new Keyframe(0f, 0.5f), new Keyframe(0.3f, 1f),
                    new Keyframe(0.7f, 1f), new Keyframe(1f, 0f)
                ));

            // レンダラー設定
            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.material = CreateParticleMaterial(color);
            renderer.renderMode = ParticleSystemRenderMode.Billboard;

            _activeEffects.Add(ps);
            return ps;
        }

        // ============================================================
        // 噴水エフェクト
        // ============================================================

        /// <summary>噴水パーティクルを生成</summary>
        public ParticleSystem CreateFountain(Vector3 position, float height)
        {
            var go = new GameObject("Fountain");
            go.transform.SetParent(transform);
            go.transform.position = position;

            // 噴水の土台（円盤）
            var baseGo = ProceduralMeshGenerator.CreateCylinderGameObject(1.5f, 0.3f, 16);
            baseGo.name = "FountainBase";
            baseGo.transform.SetParent(go.transform, false);
            baseGo.transform.localPosition = new Vector3(0f, 0.15f, 0f);
            ProceduralMeshGenerator.ApplyMaterial(baseGo,
                new Color(0.7f, 0.72f, 0.75f), 0.3f, 0.6f);

            // 噴水の縁（リング）
            var rimGo = ProceduralMeshGenerator.CreateCylinderGameObject(1.6f, 0.5f, 16);
            rimGo.name = "FountainRim";
            rimGo.transform.SetParent(go.transform, false);
            rimGo.transform.localPosition = new Vector3(0f, 0.25f, 0f);
            // 内部をくり抜く代わりに色で差を出す
            ProceduralMeshGenerator.ApplyMaterial(rimGo,
                new Color(0.6f, 0.62f, 0.65f), 0.2f, 0.5f);

            // 中央ノズル
            var nozzle = ProceduralMeshGenerator.CreateCylinderGameObject(0.15f, 1f, 8);
            nozzle.name = "Nozzle";
            nozzle.transform.SetParent(go.transform, false);
            nozzle.transform.localPosition = new Vector3(0f, 0.8f, 0f);
            ProceduralMeshGenerator.ApplyMaterial(nozzle,
                new Color(0.5f, 0.52f, 0.55f), 0.5f, 0.7f);

            // 水パーティクル（上昇→落下）
            var ps = go.AddComponent<ParticleSystem>();
            var main = ps.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.8f, 1.5f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(height * 0.8f, height * 1.2f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.03f, 0.08f);
            main.startColor = new Color(0.6f, 0.85f, 1f, 0.7f);
            main.maxParticles = 200;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.loop = true;
            main.gravityModifier = 1.0f;

            var emission = ps.emission;
            emission.rateOverTime = 60f;

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 8f;
            shape.radius = 0.05f;
            shape.position = new Vector3(0f, 1.3f, 0f);

            var colOverLife = ps.colorOverLifetime;
            colOverLife.enabled = true;
            var waterGradient = new Gradient();
            waterGradient.SetKeys(
                new[] {
                    new GradientColorKey(new Color(0.7f, 0.9f, 1f), 0f),
                    new GradientColorKey(new Color(0.5f, 0.8f, 1f), 0.5f),
                    new GradientColorKey(new Color(0.4f, 0.7f, 0.95f), 1f)
                },
                new[] {
                    new GradientAlphaKey(0.2f, 0f), new GradientAlphaKey(0.8f, 0.3f),
                    new GradientAlphaKey(0.6f, 0.7f), new GradientAlphaKey(0f, 1f)
                }
            );
            colOverLife.color = waterGradient;

            // スプラッシュ（水面に落ちた時のパーティクル）
            var splashGo = new GameObject("Splash");
            splashGo.transform.SetParent(go.transform);
            splashGo.transform.localPosition = new Vector3(0f, 0.3f, 0f);

            var splash = splashGo.AddComponent<ParticleSystem>();
            var splashMain = splash.main;
            splashMain.startLifetime = 0.5f;
            splashMain.startSpeed = new ParticleSystem.MinMaxCurve(0.5f, 1.5f);
            splashMain.startSize = new ParticleSystem.MinMaxCurve(0.02f, 0.05f);
            splashMain.startColor = new Color(0.7f, 0.9f, 1f, 0.5f);
            splashMain.maxParticles = 100;
            splashMain.loop = true;
            splashMain.gravityModifier = 0.5f;

            var splashEmission = splash.emission;
            splashEmission.rateOverTime = 30f;

            var splashShape = splash.shape;
            splashShape.shapeType = ParticleSystemShapeType.Circle;
            splashShape.radius = 1f;

            // レンダラー設定
            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.material = CreateParticleMaterial(new Color(0.6f, 0.85f, 1f, 0.7f));
            renderer.renderMode = ParticleSystemRenderMode.Billboard;

            var splashRenderer = splashGo.GetComponent<ParticleSystemRenderer>();
            splashRenderer.material = CreateParticleMaterial(new Color(0.7f, 0.9f, 1f, 0.5f));
            splashRenderer.renderMode = ParticleSystemRenderMode.Billboard;

            _activeEffects.Add(ps);
            _activeEffects.Add(splash);
            return ps;
        }

        // ============================================================
        // アンビエント煙/霧エフェクト
        // ============================================================

        /// <summary>周囲の霧/煙エフェクトを生成</summary>
        public ParticleSystem CreateAmbientSmoke(Vector3 position)
        {
            var go = new GameObject("AmbientSmoke");
            go.transform.SetParent(transform);
            go.transform.position = position;

            var ps = go.AddComponent<ParticleSystem>();
            var main = ps.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(4f, 8f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.1f, 0.3f);
            main.startSize = new ParticleSystem.MinMaxCurve(1f, 3f);
            main.startColor = new ParticleSystem.MinMaxGradient(
                new Color(0.9f, 0.9f, 0.92f, 0.08f),
                new Color(0.85f, 0.85f, 0.9f, 0.15f));
            main.maxParticles = 20;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.loop = true;
            main.gravityModifier = -0.02f;

            var emission = ps.emission;
            emission.rateOverTime = 2f;

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(8f, 0.5f, 8f);

            var sizeOverLifetime = ps.sizeOverLifetime;
            sizeOverLifetime.enabled = true;
            sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f,
                new AnimationCurve(
                    new Keyframe(0f, 0.3f), new Keyframe(0.5f, 1f), new Keyframe(1f, 1.5f)
                ));

            var colOverLife = ps.colorOverLifetime;
            colOverLife.enabled = true;
            var smokeGrad = new Gradient();
            smokeGrad.SetKeys(
                new[] {
                    new GradientColorKey(new Color(0.9f, 0.9f, 0.92f), 0f),
                    new GradientColorKey(new Color(0.88f, 0.88f, 0.9f), 1f)
                },
                new[] {
                    new GradientAlphaKey(0f, 0f), new GradientAlphaKey(0.12f, 0.3f),
                    new GradientAlphaKey(0.08f, 0.7f), new GradientAlphaKey(0f, 1f)
                }
            );
            colOverLife.color = smokeGrad;

            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.material = CreateParticleMaterial(new Color(0.9f, 0.9f, 0.92f, 0.1f));
            renderer.renderMode = ParticleSystemRenderMode.Billboard;

            _activeEffects.Add(ps);
            return ps;
        }

        // ============================================================
        // アトラクション用煙エフェクト（故障時など）
        // ============================================================

        /// <summary>アトラクション故障時の煙を生成</summary>
        public ParticleSystem CreateBreakdownSmoke(Vector3 position)
        {
            var go = new GameObject("BreakdownSmoke");
            go.transform.SetParent(transform);
            go.transform.position = position;

            var ps = go.AddComponent<ParticleSystem>();
            var main = ps.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(1.5f, 3f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.5f, 1.5f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.3f, 0.8f);
            main.startColor = new ParticleSystem.MinMaxGradient(
                new Color(0.3f, 0.3f, 0.3f, 0.6f),
                new Color(0.5f, 0.5f, 0.5f, 0.8f));
            main.maxParticles = 50;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.loop = true;
            main.gravityModifier = -0.3f;

            var emission = ps.emission;
            emission.rateOverTime = 15f;

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 25f;
            shape.radius = 0.3f;

            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.material = CreateParticleMaterial(new Color(0.4f, 0.4f, 0.4f, 0.7f));
            renderer.renderMode = ParticleSystemRenderMode.Billboard;

            _activeEffects.Add(ps);
            return ps;
        }

        /// <summary>花火/お祝いパーティクル</summary>
        public ParticleSystem CreateCelebrationBurst(Vector3 position, Color color)
        {
            var go = new GameObject("Celebration");
            go.transform.SetParent(transform);
            go.transform.position = position;

            var ps = go.AddComponent<ParticleSystem>();
            var main = ps.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(1f, 2f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(3f, 6f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.05f, 0.15f);
            main.startColor = color;
            main.maxParticles = 100;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.loop = false;
            main.gravityModifier = 0.5f;

            var emission = ps.emission;
            emission.rateOverTime = 0f;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 50) });

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.2f;

            var colOverLife = ps.colorOverLifetime;
            colOverLife.enabled = true;
            var grad = new Gradient();
            grad.SetKeys(
                new[] { new GradientColorKey(color, 0f), new GradientColorKey(color * 0.5f, 1f) },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0f, 1f) }
            );
            colOverLife.color = grad;

            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.material = CreateParticleMaterial(color);
            renderer.renderMode = ParticleSystemRenderMode.Billboard;

            _activeEffects.Add(ps);
            return ps;
        }

        // ============================================================
        // エフェクト管理
        // ============================================================

        /// <summary>全エフェクトを停止・削除する</summary>
        public void ClearAllEffects()
        {
            foreach (var ps in _activeEffects)
            {
                if (ps != null && ps.gameObject != null)
                    Destroy(ps.gameObject);
            }
            _activeEffects.Clear();
        }

        /// <summary>特定位置のエフェクトを削除する</summary>
        public void RemoveEffectsNear(Vector3 position, float radius)
        {
            for (int i = _activeEffects.Count - 1; i >= 0; i--)
            {
                if (_activeEffects[i] == null) { _activeEffects.RemoveAt(i); continue; }
                if (Vector3.Distance(_activeEffects[i].transform.position, position) < radius)
                {
                    Destroy(_activeEffects[i].gameObject);
                    _activeEffects.RemoveAt(i);
                }
            }
        }

        // ============================================================
        // パーティクルマテリアル
        // ============================================================

        /// <summary>パーティクル用マテリアルを作成（半透明加算合成）</summary>
        private static Material CreateParticleMaterial(Color color)
        {
            // Particles/Standard Unlit を優先的に使う
            var shader = Shader.Find("Particles/Standard Unlit");
            if (shader == null) shader = Shader.Find("Legacy Shaders/Particles/Alpha Blended");
            if (shader == null) shader = Shader.Find("UI/Default");

            var mat = new Material(shader);
            mat.color = color;

            // WebGL対応: 基本的なブレンド設定
            if (shader.name.Contains("Particles"))
            {
                mat.SetFloat("_Mode", 0); // Additive
                mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.One);
            }

            // テクスチャなし（ソフトパーティクル用の白テクスチャを生成）
            var whiteTex = new Texture2D(4, 4, TextureFormat.RGBA32, false);
            var whitePixels = new Color32[16];
            for (int i = 0; i < 16; i++)
            {
                // 中心が明るく、端が暗い（ガウシアン風）
                int x = i % 4, y = i / 4;
                float dx = (x - 1.5f) / 1.5f;
                float dy = (y - 1.5f) / 1.5f;
                float d = 1f - Mathf.Clamp01(dx * dx + dy * dy);
                byte v = (byte)(d * 255f);
                whitePixels[i] = new Color32(255, 255, 255, v);
            }
            whiteTex.SetPixels32(whitePixels);
            whiteTex.Apply();
            mat.mainTexture = whiteTex;

            return mat;
        }
    }
}
