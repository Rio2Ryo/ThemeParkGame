// ============================================================
// ThemeParkGame - PathwaySystem
// パーク内通路システム: 施設間の経路管理・混雑度追跡・視覚表現
// 通路セグメントをコードから生成し、来場者密度に応じて色変化する
// ============================================================

using System.Collections.Generic;
using UnityEngine;
using ThemeParkGame.Core;
using ThemeParkGame.Visitor;

namespace ThemeParkGame.Park
{
    /// <summary>
    /// 通路セグメントのデータ。2点間の直線通路を表現する。
    /// </summary>
    public class PathwaySegment
    {
        /// <summary>セグメントID</summary>
        public int Id;

        /// <summary>開始地点（ワールド座標）</summary>
        public Vector3 StartPoint;

        /// <summary>終了地点（ワールド座標）</summary>
        public Vector3 EndPoint;

        /// <summary>通路の幅</summary>
        public float Width;

        /// <summary>中央点</summary>
        public Vector3 Center => (StartPoint + EndPoint) * 0.5f;

        /// <summary>通路の長さ</summary>
        public float Length => Vector3.Distance(StartPoint, EndPoint);

        /// <summary>通路の方向（正規化）</summary>
        public Vector3 Direction => (EndPoint - StartPoint).normalized;

        /// <summary>このセグメント上の現在の来場者数</summary>
        public int CurrentVisitorCount;

        /// <summary>混雑度 (0=空き ～ 1=満杯)</summary>
        public float Congestion;

        /// <summary>表示用のGameObject</summary>
        public GameObject Visual;

        /// <summary>通路メッシュのRenderer</summary>
        public Renderer MeshRenderer;

        /// <summary>当たり判定用のBounds（Y方向に膨らませた箱）</summary>
        public Bounds Bounds;
    }

    /// <summary>
    /// パーク内の通路ネットワークを管理するシステム。
    ///
    /// 【機能】
    /// ① 施設間の通路セグメントをコードから自動生成
    /// ② 来場者の位置を追跡し、各セグメントの混雑度を計算
    /// ③ 混雑度に応じて通路の色をリアルタイム変更
    ///    (緑→黄→オレンジ→赤)
    /// ④ 歩行エフェクト（足元のダスト）を混雑度に応じて発生
    /// </summary>
    public class PathwaySystem : MonoBehaviour
    {
        // ---- セグメント管理 ----
        private readonly List<PathwaySegment> _segments = new List<PathwaySegment>();
        private int _nextSegmentId;

        // ---- 通路パラメータ ----
        private const float DefaultPathWidth = 3f;
        private const float PathHeight = 0.05f;  // 地面から少し浮かせる
        private const float CongestionUpdateInterval = 0.5f;

        // ---- 混雑度閾値 ----
        /// <summary>セグメント長1mあたりの「満杯」来場者数</summary>
        private const float VisitorsPerMeterFull = 0.8f;

        // ---- 混雑度カラーグラデーション ----
        private static readonly Color ColorEmpty   = new Color(0.55f, 0.75f, 0.55f, 0.85f); // 緑
        private static readonly Color ColorLow     = new Color(0.75f, 0.85f, 0.45f, 0.85f); // 黄緑
        private static readonly Color ColorMedium  = new Color(0.95f, 0.85f, 0.30f, 0.85f); // 黄
        private static readonly Color ColorHigh    = new Color(0.95f, 0.55f, 0.20f, 0.85f); // オレンジ
        private static readonly Color ColorFull    = new Color(0.95f, 0.25f, 0.20f, 0.85f); // 赤

        // ---- 歩行エフェクト ----
        private ParticleSystem _footDustPS;

        // ---- キャッシュ ----
        private float _congestionTimer;
        private Transform _parentContainer;
        private Material _pathMaterial;

        // ---- 統計 ----

        /// <summary>全通路の平均混雑度 (0～1)</summary>
        public float AverageCongestion { get; private set; }

        /// <summary>最も混雑しているセグメント</summary>
        public PathwaySegment MostCongestedSegment { get; private set; }

        /// <summary>全セグメントの読み取り専用リスト</summary>
        public IReadOnlyList<PathwaySegment> Segments => _segments;

        /// <summary>通路の総延長(m)</summary>
        public float TotalPathLength { get; private set; }

        // ================================================================
        // 初期化
        // ================================================================

        private void Awake()
        {
            _parentContainer = new GameObject("--- Pathways ---").transform;
            _parentContainer.SetParent(transform);

            _pathMaterial = CreatePathMaterial();
            CreateFootDustParticleSystem();
        }

        /// <summary>通路用マテリアルを作成する</summary>
        private Material CreatePathMaterial()
        {
            var shader = Shader.Find("Standard");
            if (shader == null) shader = Shader.Find("UI/Default");
            if (shader == null) shader = Shader.Find("Sprites/Default");

            var mat = new Material(shader);
            mat.color = ColorEmpty;

            // 半透明を有効にする
            if (mat.HasProperty("_Mode"))
            {
                mat.SetFloat("_Mode", 3); // Transparent
                mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                mat.SetInt("_ZWrite", 0);
                mat.DisableKeyword("_ALPHATEST_ON");
                mat.EnableKeyword("_ALPHABLEND_ON");
                mat.DisableKeyword("_ALPHAPREMULTIPLY_ON");
                mat.renderQueue = 3000;
            }

            return mat;
        }

        /// <summary>歩行足元ダストパーティクルを生成する</summary>
        private void CreateFootDustParticleSystem()
        {
            var go = new GameObject("FootDustEffect");
            go.transform.SetParent(transform);

            _footDustPS = go.AddComponent<ParticleSystem>();
            var main = _footDustPS.main;
            main.maxParticles = 500;
            main.startLifetime = 0.8f;
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.2f, 0.6f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.08f, 0.2f);
            main.startColor = new Color(0.7f, 0.65f, 0.55f, 0.4f);
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.gravityModifier = 0.3f;
            main.loop = true;
            main.playOnAwake = false;

            var emission = _footDustPS.emission;
            emission.rateOverTime = 0f; // スクリプトから制御

            var shape = _footDustPS.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(1f, 0.1f, 1f);

            // フェードアウト
            var colorOverLifetime = _footDustPS.colorOverLifetime;
            colorOverLifetime.enabled = true;
            var grad = new Gradient();
            grad.SetKeys(
                new GradientColorKey[] {
                    new GradientColorKey(new Color(0.7f, 0.65f, 0.55f), 0f),
                    new GradientColorKey(new Color(0.7f, 0.65f, 0.55f), 1f)
                },
                new GradientAlphaKey[] {
                    new GradientAlphaKey(0.4f, 0f),
                    new GradientAlphaKey(0.2f, 0.5f),
                    new GradientAlphaKey(0f, 1f)
                }
            );
            colorOverLifetime.color = grad;

            // レンダラー
            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Billboard;

            var dustShader = Shader.Find("Particles/Standard Unlit");
            if (dustShader == null) dustShader = Shader.Find("Legacy Shaders/Particles/Alpha Blended");
            if (dustShader == null) dustShader = Shader.Find("Sprites/Default");
            if (dustShader != null)
            {
                var dustMat = new Material(dustShader);
                dustMat.color = new Color(0.7f, 0.65f, 0.55f, 0.3f);
                renderer.material = dustMat;
            }

            _footDustPS.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        }

        // ================================================================
        // 通路セグメント生成API
        // ================================================================

        /// <summary>
        /// 2点間の通路セグメントを生成する。
        /// </summary>
        /// <param name="start">開始地点</param>
        /// <param name="end">終了地点</param>
        /// <param name="width">通路幅（省略時はデフォルト）</param>
        /// <returns>生成されたセグメント</returns>
        public PathwaySegment CreateSegment(Vector3 start, Vector3 end, float width = DefaultPathWidth)
        {
            // Y座標を地面に合わせる
            start.y = PathHeight;
            end.y = PathHeight;

            var segment = new PathwaySegment
            {
                Id = _nextSegmentId++,
                StartPoint = start,
                EndPoint = end,
                Width = width,
                CurrentVisitorCount = 0,
                Congestion = 0f
            };

            // Boundsを計算
            segment.Bounds = CalculateSegmentBounds(segment);

            // ビジュアル生成
            CreateSegmentVisual(segment);

            _segments.Add(segment);
            TotalPathLength += segment.Length;

            return segment;
        }

        /// <summary>
        /// 施設間をL字型に接続する通路を生成する（直角に曲がる2セグメント）
        /// </summary>
        public (PathwaySegment, PathwaySegment) CreateLShapedPath(
            Vector3 from, Vector3 to, float width = DefaultPathWidth)
        {
            // 中継点：fromのX, toのZで直角に曲がる
            Vector3 corner = new Vector3(to.x, PathHeight, from.z);

            var seg1 = CreateSegment(from, corner, width);
            var seg2 = CreateSegment(corner, to, width);

            return (seg1, seg2);
        }

        /// <summary>
        /// スター型（中央ハブから放射状に接続）の通路網を生成する
        /// </summary>
        public void CreateStarLayout(Vector3 hub, Vector3[] destinations, float width = DefaultPathWidth)
        {
            foreach (var dest in destinations)
            {
                // 目的地のXZ距離を見て、直線 or L字を決定
                float dx = Mathf.Abs(dest.x - hub.x);
                float dz = Mathf.Abs(dest.z - hub.z);

                if (dx < 2f || dz < 2f)
                {
                    // ほぼ直線
                    CreateSegment(hub, dest, width);
                }
                else
                {
                    // L字接続
                    CreateLShapedPath(hub, dest, width);
                }
            }
        }

        // ================================================================
        // ビジュアル生成
        // ================================================================

        /// <summary>セグメント用の3Dメッシュを生成する</summary>
        private void CreateSegmentVisual(PathwaySegment segment)
        {
            var go = new GameObject($"Pathway_{segment.Id}");
            go.transform.SetParent(_parentContainer);
            go.layer = 8; // Ground layer

            // メッシュ生成: 通路方向に沿った矩形
            var mf = go.AddComponent<MeshFilter>();
            var mr = go.AddComponent<MeshRenderer>();

            var mesh = CreatePathMesh(segment);
            mf.sharedMesh = mesh;

            // 個別マテリアルインスタンス（セグメントごとに色を変えるため）
            mr.material = new Material(_pathMaterial);

            segment.Visual = go;
            segment.MeshRenderer = mr;
        }

        /// <summary>通路セグメントのメッシュを生成する</summary>
        private Mesh CreatePathMesh(PathwaySegment segment)
        {
            var mesh = new Mesh { name = $"PathMesh_{segment.Id}" };

            Vector3 dir = segment.Direction;
            // 通路の横方向（法線がYの平面上で、dir と直交する方向）
            Vector3 right = Vector3.Cross(Vector3.up, dir).normalized * (segment.Width * 0.5f);

            Vector3 s = segment.StartPoint;
            Vector3 e = segment.EndPoint;

            // 4頂点の矩形
            Vector3[] vertices = new Vector3[]
            {
                s - right, // 左手前
                s + right, // 右手前
                e + right, // 右奥
                e - right  // 左奥
            };

            // ローカル座標に変換（Visual位置は(0,0,0)）
            Vector3[] normals = { Vector3.up, Vector3.up, Vector3.up, Vector3.up };

            float len = segment.Length;
            Vector2[] uvs = {
                new Vector2(0, 0), new Vector2(1, 0),
                new Vector2(1, len / segment.Width), new Vector2(0, len / segment.Width)
            };

            int[] triangles = { 0, 2, 1, 0, 3, 2 };

            mesh.vertices = vertices;
            mesh.normals = normals;
            mesh.uv = uvs;
            mesh.triangles = triangles;
            mesh.RecalculateBounds();

            return mesh;
        }

        /// <summary>セグメントのバウンディングボックスを計算する</summary>
        private Bounds CalculateSegmentBounds(PathwaySegment segment)
        {
            Vector3 center = segment.Center;
            center.y = 1f; // 来場者の高さの中央付近

            Vector3 dir = segment.Direction;
            Vector3 right = Vector3.Cross(Vector3.up, dir).normalized;

            float halfLen = segment.Length * 0.5f;
            float halfWidth = segment.Width * 0.5f;

            // 方向に沿ったバウンディングボックスを生成
            // (AABB近似: 実際は回転矩形だが簡略化)
            float extentX = Mathf.Abs(dir.x) * halfLen + Mathf.Abs(right.x) * halfWidth;
            float extentZ = Mathf.Abs(dir.z) * halfLen + Mathf.Abs(right.z) * halfWidth;

            Vector3 size = new Vector3(
                Mathf.Max(extentX * 2f, segment.Width),
                3f, // Y方向は余裕を持たせる
                Mathf.Max(extentZ * 2f, segment.Width)
            );

            return new Bounds(center, size);
        }

        // ================================================================
        // 混雑度更新
        // ================================================================

        private void Update()
        {
            if (GameManager.Instance == null) return;
            if (GameManager.Instance.CurrentState != GameState.Playing) return;

            _congestionTimer += Time.deltaTime;
            if (_congestionTimer >= CongestionUpdateInterval)
            {
                _congestionTimer = 0f;
                UpdateCongestion();
                UpdateVisuals();
                UpdateFootDust();
            }
        }

        /// <summary>各セグメントの混雑度を来場者位置から計算する</summary>
        private void UpdateCongestion()
        {
            // 全セグメントのカウントをリセット
            for (int i = 0; i < _segments.Count; i++)
            {
                _segments[i].CurrentVisitorCount = 0;
            }

            // VisitorManagerから全アクティブ来場者を取得
            var vm = GameManager.Instance?.VisitorManager;
            if (vm == null) return;

            var visitors = vm.GetAllActiveVisitors();
            if (visitors == null) return;

            // 各来場者がどのセグメント上にいるか判定
            foreach (var visitor in visitors)
            {
                if (visitor == null || !visitor.IsActive) continue;

                // 移動中の来場者のみカウント
                var state = visitor.CurrentState;
                if (state != VisitorBehaviorState.WalkingToAttraction &&
                    state != VisitorBehaviorState.WalkingToShop &&
                    state != VisitorBehaviorState.WalkingToToilet &&
                    state != VisitorBehaviorState.LeavingPark &&
                    state != VisitorBehaviorState.Idle)
                    continue;

                Vector3 pos = visitor.transform.position;

                // 最も近いセグメントを判定
                int closestIdx = -1;
                float closestDist = float.MaxValue;

                for (int i = 0; i < _segments.Count; i++)
                {
                    var seg = _segments[i];
                    // AABB事前チェック
                    if (!seg.Bounds.Contains(new Vector3(pos.x, seg.Bounds.center.y, pos.z)))
                        continue;

                    // 線分への最短距離
                    float dist = DistanceToSegment(pos, seg.StartPoint, seg.EndPoint);
                    if (dist < seg.Width * 0.6f && dist < closestDist)
                    {
                        closestDist = dist;
                        closestIdx = i;
                    }
                }

                if (closestIdx >= 0)
                {
                    _segments[closestIdx].CurrentVisitorCount++;
                }
            }

            // 混雑度を計算
            float totalCongestion = 0f;
            PathwaySegment mostCongested = null;
            float maxCongestion = 0f;

            for (int i = 0; i < _segments.Count; i++)
            {
                var seg = _segments[i];
                float capacity = seg.Length * VisitorsPerMeterFull;
                seg.Congestion = (capacity > 0f) ?
                    Mathf.Clamp01(seg.CurrentVisitorCount / capacity) : 0f;

                totalCongestion += seg.Congestion;

                if (seg.Congestion > maxCongestion)
                {
                    maxCongestion = seg.Congestion;
                    mostCongested = seg;
                }
            }

            AverageCongestion = _segments.Count > 0 ?
                totalCongestion / _segments.Count : 0f;
            MostCongestedSegment = mostCongested;
        }

        /// <summary>点から線分への最短距離を求める</summary>
        private static float DistanceToSegment(Vector3 point, Vector3 a, Vector3 b)
        {
            // XZ平面上で計算
            Vector3 p = new Vector3(point.x, 0f, point.z);
            Vector3 s = new Vector3(a.x, 0f, a.z);
            Vector3 e = new Vector3(b.x, 0f, b.z);

            Vector3 se = e - s;
            float len2 = se.sqrMagnitude;
            if (len2 < 0.001f) return Vector3.Distance(p, s);

            float t = Mathf.Clamp01(Vector3.Dot(p - s, se) / len2);
            Vector3 proj = s + se * t;
            return Vector3.Distance(p, proj);
        }

        // ================================================================
        // ビジュアル更新（混雑度 → 色変化）
        // ================================================================

        /// <summary>各セグメントのマテリアル色を混雑度に応じて更新する</summary>
        private void UpdateVisuals()
        {
            for (int i = 0; i < _segments.Count; i++)
            {
                var seg = _segments[i];
                if (seg.MeshRenderer == null) continue;

                Color targetColor = GetCongestionColor(seg.Congestion);
                seg.MeshRenderer.material.color = Color.Lerp(
                    seg.MeshRenderer.material.color, targetColor, Time.deltaTime * 3f);
            }
        }

        /// <summary>混雑度(0～1)に応じた色を返す</summary>
        private Color GetCongestionColor(float congestion)
        {
            if (congestion < 0.25f)
                return Color.Lerp(ColorEmpty, ColorLow, congestion / 0.25f);
            if (congestion < 0.5f)
                return Color.Lerp(ColorLow, ColorMedium, (congestion - 0.25f) / 0.25f);
            if (congestion < 0.75f)
                return Color.Lerp(ColorMedium, ColorHigh, (congestion - 0.5f) / 0.25f);
            return Color.Lerp(ColorHigh, ColorFull, (congestion - 0.75f) / 0.25f);
        }

        /// <summary>混雑度に応じた日本語ラベルを返す</summary>
        public static string GetCongestionLabel(float congestion)
        {
            if (congestion < 0.2f) return "空き";
            if (congestion < 0.4f) return "やや空き";
            if (congestion < 0.6f) return "普通";
            if (congestion < 0.8f) return "混雑";
            return "大混雑";
        }

        /// <summary>混雑度に応じた色を外部から取得する</summary>
        public static Color GetCongestionDisplayColor(float congestion)
        {
            if (congestion < 0.3f) return new Color(0.4f, 0.9f, 0.45f);
            if (congestion < 0.6f) return new Color(0.95f, 0.85f, 0.3f);
            return new Color(0.95f, 0.35f, 0.3f);
        }

        // ================================================================
        // 歩行エフェクト
        // ================================================================

        /// <summary>混雑セグメントに足元ダストを発生させる</summary>
        private void UpdateFootDust()
        {
            if (_footDustPS == null) return;

            // 最混雑セグメント付近にダストを発生
            if (MostCongestedSegment != null && MostCongestedSegment.Congestion > 0.3f)
            {
                if (!_footDustPS.isPlaying) _footDustPS.Play();

                // パーティクルを混雑セグメントの中心に配置
                _footDustPS.transform.position = MostCongestedSegment.Center +
                    new Vector3(0f, 0.1f, 0f);

                // 混雑度に応じてパーティクル量を増減
                var emission = _footDustPS.emission;
                emission.rateOverTime = MostCongestedSegment.Congestion * 80f;

                // シェイプをセグメントの長さに合わせる
                var shape = _footDustPS.shape;
                shape.scale = new Vector3(
                    Mathf.Max(MostCongestedSegment.Length * 0.5f, 2f),
                    0.1f,
                    MostCongestedSegment.Width * 0.5f
                );

                // セグメントの方向に合わせて回転
                _footDustPS.transform.rotation = Quaternion.LookRotation(
                    MostCongestedSegment.Direction, Vector3.up);
            }
            else
            {
                if (_footDustPS.isPlaying)
                {
                    var emission = _footDustPS.emission;
                    emission.rateOverTime = 0f;
                }
            }
        }

        // ================================================================
        // 外部API
        // ================================================================

        /// <summary>全セグメントを削除する</summary>
        public void ClearAll()
        {
            foreach (var seg in _segments)
            {
                if (seg.Visual != null)
                    Destroy(seg.Visual);
            }
            _segments.Clear();
            TotalPathLength = 0f;
            _nextSegmentId = 0;
        }

        /// <summary>特定の位置が通路上かどうか判定する</summary>
        public bool IsOnPathway(Vector3 position)
        {
            for (int i = 0; i < _segments.Count; i++)
            {
                var seg = _segments[i];
                float dist = DistanceToSegment(position, seg.StartPoint, seg.EndPoint);
                if (dist < seg.Width * 0.5f)
                    return true;
            }
            return false;
        }

        /// <summary>指定位置に最も近い通路上の点を返す</summary>
        public Vector3 GetNearestPointOnPath(Vector3 position)
        {
            float bestDist = float.MaxValue;
            Vector3 bestPoint = position;

            for (int i = 0; i < _segments.Count; i++)
            {
                var seg = _segments[i];
                Vector3 proj = ProjectOntoSegment(position, seg.StartPoint, seg.EndPoint);
                float dist = Vector3.Distance(position, proj);
                if (dist < bestDist)
                {
                    bestDist = dist;
                    bestPoint = proj;
                }
            }

            return bestPoint;
        }

        /// <summary>点を線分上に射影する</summary>
        private static Vector3 ProjectOntoSegment(Vector3 point, Vector3 a, Vector3 b)
        {
            Vector3 ab = b - a;
            float len2 = ab.sqrMagnitude;
            if (len2 < 0.001f) return a;

            float t = Mathf.Clamp01(Vector3.Dot(point - a, ab) / len2);
            return a + ab * t;
        }

        /// <summary>指定位置の混雑度を返す（通路外は0）</summary>
        public float GetCongestionAt(Vector3 position)
        {
            for (int i = 0; i < _segments.Count; i++)
            {
                var seg = _segments[i];
                float dist = DistanceToSegment(position, seg.StartPoint, seg.EndPoint);
                if (dist < seg.Width * 0.5f)
                    return seg.Congestion;
            }
            return 0f;
        }
    }
}
