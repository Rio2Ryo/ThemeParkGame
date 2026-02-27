// ============================================================
// ThemeParkGame - Hooligan
// フーリガン（不審者）のAI行動制御
// ============================================================

using UnityEngine;
using UnityEngine.AI;
using ThemeParkGame.Core;

namespace ThemeParkGame.Visitor
{
    /// <summary>
    /// フーリガン（不審者）のAI行動を制御するコンポーネント。
    ///
    /// 【ゲームデザイン】
    /// ・パーク内をうろつき、近くの来場者の幸福度を下げる
    /// ・アトラクション周辺でいたずらを行い、施設を汚す
    /// ・ガードマンに発見されると逃走を試みるが、追いつかれると確保・退場される
    /// ・ガードマン不足 → フーリガン増加 → 来場者不満 → パーク評価低下
    /// </summary>
    [RequireComponent(typeof(NavMeshAgent))]
    [RequireComponent(typeof(CapsuleCollider))]
    public class Hooligan : MonoBehaviour
    {
        // ============================================================
        // 定数
        // ============================================================

        /// <summary>うろつき行動の移動範囲（メートル）</summary>
        private const float WanderRadius = 20f;

        /// <summary>うろつき先に到着したと判定する距離</summary>
        private const float ArrivalThreshold = 1.5f;

        /// <summary>次のうろつき先を決めるまでの待機時間（秒）</summary>
        private const float WanderIdleTime = 3f;

        /// <summary>来場者の幸福度を下げる範囲（メートル）</summary>
        private const float HarassRadius = 5f;

        /// <summary>いたずら判定の間隔（秒）</summary>
        private const float HarassInterval = 4f;

        /// <summary>1回のいたずらで来場者の幸福度を下げる量</summary>
        private const float HappinessPenalty = 8f;

        /// <summary>いたずら時にゴミを散らかす確率</summary>
        private const float LitterChance = 0.4f;

        /// <summary>逃走時の速度倍率</summary>
        private const float FleeSpeedMultiplier = 1.3f;

        /// <summary>ガードマン検知範囲（逃走判定用）</summary>
        private const float GuardDetectionRadius = 12f;

        /// <summary>フーリガンの寿命（秒）。これを超えると自動退場</summary>
        private const float MaxLifetime = 180f;

        // ============================================================
        // フィールド
        // ============================================================

        private NavMeshAgent _agent;
        private float _wanderTimer;
        private float _harassTimer;
        private float _lifetimeTimer;
        private float _baseSpeed;
        private bool _isFleeing;
        private Vector3 _spawnPosition;

        /// <summary>フーリガンの一意ID</summary>
        public int HooliganId { get; private set; }

        // ============================================================
        // 初期化
        // ============================================================

        private void Awake()
        {
            _agent = GetComponent<NavMeshAgent>();
            gameObject.tag = "Hooligan";

            // カプセルコライダー設定
            var col = GetComponent<CapsuleCollider>();
            col.height = 1.8f;
            col.radius = 0.3f;
            col.center = new Vector3(0f, 0.9f, 0f);

            // NavMeshAgent設定
            _agent.speed = 2.5f;
            _agent.angularSpeed = 180f;
            _agent.acceleration = 8f;
            _agent.stoppingDistance = 0.5f;
            _agent.radius = 0.3f;
            _agent.height = 1.8f;
            _baseSpeed = _agent.speed;
        }

        /// <summary>フーリガンを初期化する</summary>
        public void Initialize(int id, Vector3 spawnPos)
        {
            HooliganId = id;
            _spawnPosition = spawnPos;
            _wanderTimer = 0f;
            _harassTimer = 0f;
            _lifetimeTimer = 0f;
            _isFleeing = false;
            _agent.speed = _baseSpeed;

            // プロシージャルメッシュ（見た目：黒っぽいカプセル）
            CreateVisual();

            PickNewWanderTarget();
        }

        // ============================================================
        // メインループ
        // ============================================================

        private void Update()
        {
            if (GameManager.Instance != null && GameManager.Instance.IsPaused) return;

            float dt = Time.deltaTime;
            _lifetimeTimer += dt;

            // 寿命チェック
            if (_lifetimeTimer >= MaxLifetime)
            {
                SelfDestruct();
                return;
            }

            // ガードマン接近チェック（逃走判定）
            CheckForGuards();

            // うろつき行動
            UpdateWander(dt);

            // いたずら行動
            _harassTimer += dt;
            if (_harassTimer >= HarassInterval)
            {
                _harassTimer = 0f;
                PerformHarassment();
            }
        }

        // ============================================================
        // うろつき行動
        // ============================================================

        private void UpdateWander(float deltaTime)
        {
            if (_agent == null || !_agent.isOnNavMesh) return;

            // 目的地に到着したら待機→次の目的地
            if (!_agent.pathPending && _agent.remainingDistance <= ArrivalThreshold)
            {
                _wanderTimer += deltaTime;
                if (_wanderTimer >= WanderIdleTime)
                {
                    _wanderTimer = 0f;
                    if (_isFleeing)
                    {
                        PickFleeTarget();
                    }
                    else
                    {
                        PickNewWanderTarget();
                    }
                }
            }
        }

        private void PickNewWanderTarget()
        {
            Vector3 randomDir = Random.insideUnitSphere * WanderRadius;
            randomDir += transform.position;
            randomDir.y = 0f;

            if (NavMesh.SamplePosition(randomDir, out NavMeshHit hit, WanderRadius, NavMesh.AllAreas))
            {
                _agent.SetDestination(hit.position);
            }
        }

        private void PickFleeTarget()
        {
            // ガードマンから離れる方向に逃走
            Vector3 fleeDir = transform.position - _spawnPosition;
            fleeDir.y = 0f;
            if (fleeDir.sqrMagnitude < 0.01f)
                fleeDir = Random.insideUnitSphere;
            fleeDir = fleeDir.normalized * WanderRadius;

            Vector3 targetPos = transform.position + fleeDir;
            if (NavMesh.SamplePosition(targetPos, out NavMeshHit hit, WanderRadius, NavMesh.AllAreas))
            {
                _agent.SetDestination(hit.position);
            }
        }

        // ============================================================
        // ガードマン検知・逃走
        // ============================================================

        private void CheckForGuards()
        {
            Collider[] hits = Physics.OverlapSphere(transform.position, GuardDetectionRadius);
            bool guardNearby = false;

            foreach (var hit in hits)
            {
                if (hit.CompareTag("Staff"))
                {
                    var guard = hit.GetComponent<ThemeParkGame.Staff.GuardStaff>();
                    if (guard != null)
                    {
                        guardNearby = true;
                        break;
                    }
                }
            }

            if (guardNearby && !_isFleeing)
            {
                _isFleeing = true;
                _agent.speed = _baseSpeed * FleeSpeedMultiplier;
                PickFleeTarget();
                WebGLOptimizer.LogVerbose($"[Hooligan] ID={HooliganId} がガードマンを発見して逃走中");
            }
            else if (!guardNearby && _isFleeing)
            {
                _isFleeing = false;
                _agent.speed = _baseSpeed;
            }
        }

        // ============================================================
        // いたずら行動
        // ============================================================

        /// <summary>
        /// 周囲の来場者に嫌がらせを行い、幸福度を下げる。
        /// 確率でゴミも散らかす。
        /// </summary>
        private void PerformHarassment()
        {
            var vm = GameManager.Instance?.VisitorManager;
            if (vm == null) return;

            // 周囲の来場者の幸福度を下げる
            Collider[] hits = Physics.OverlapSphere(transform.position, HarassRadius);
            int affectedCount = 0;

            foreach (var hit in hits)
            {
                if (hit.CompareTag("Visitor"))
                {
                    var ai = hit.GetComponent<VisitorAI>();
                    if (ai != null && ai.Parameters != null)
                    {
                        ai.Parameters.ModifyHappiness(-HappinessPenalty);
                        affectedCount++;
                    }
                }
            }

            // ゴミを散らかす
            if (Random.value < LitterChance)
            {
                SpawnLitter();
            }

            if (affectedCount > 0)
            {
                WebGLOptimizer.LogVerbose(
                    $"[Hooligan] ID={HooliganId} が{affectedCount}人の来場者に嫌がらせ（幸福度-{HappinessPenalty}）");
            }
        }

        /// <summary>ゴミをポイ捨てする</summary>
        private void SpawnLitter()
        {
            var obj = new GameObject("Mess_Litter");
            obj.tag = "Litter";

            Vector3 pos = transform.position + new Vector3(
                Random.Range(-1f, 1f), 0f, Random.Range(-1f, 1f));
            pos.y = 0.05f;
            obj.transform.position = pos;

            var col = obj.AddComponent<SphereCollider>();
            col.radius = 0.3f;
            col.isTrigger = true;

            // 簡素なメッシュ
            var mf = obj.AddComponent<MeshFilter>();
            var mr = obj.AddComponent<MeshRenderer>();
            var mesh = new Mesh();
            float s = 0.08f;
            mesh.vertices = new[]
            {
                new Vector3(-s, 0, -s), new Vector3(s, 0, -s),
                new Vector3(s, 0, s), new Vector3(-s, 0, s),
                new Vector3(-s, s*2, -s), new Vector3(s, s*2, -s),
                new Vector3(s, s*2, s), new Vector3(-s, s*2, s)
            };
            mesh.triangles = new[]
            {
                0,2,1, 0,3,2, 4,5,6, 4,6,7,
                0,1,5, 0,5,4, 2,3,7, 2,7,6,
                1,2,6, 1,6,5, 0,4,7, 0,7,3
            };
            mesh.RecalculateNormals();
            mf.mesh = mesh;
            mr.material = new Material(Shader.Find("Sprites/Default"))
            {
                color = new Color(0.55f, 0.45f, 0.3f, 0.9f)
            };
        }

        // ============================================================
        // 退場
        // ============================================================

        /// <summary>寿命切れ等で自動退場する</summary>
        private void SelfDestruct()
        {
            WebGLOptimizer.LogVerbose($"[Hooligan] ID={HooliganId} が自動退場しました");
            if (HooliganManager.Instance != null)
            {
                HooliganManager.Instance.OnHooliganRemoved(HooliganId);
            }
            Destroy(gameObject);
        }

        /// <summary>ガードマンに確保されて退場する際に呼ばれる</summary>
        public void OnApprehended()
        {
            WebGLOptimizer.LogVerbose($"[Hooligan] ID={HooliganId} がガードマンに確保されました");
            if (HooliganManager.Instance != null)
            {
                HooliganManager.Instance.OnHooliganRemoved(HooliganId);
            }
        }

        // ============================================================
        // ビジュアル
        // ============================================================

        /// <summary>プロシージャルメッシュで外見を生成する（黒っぽい人型）</summary>
        private void CreateVisual()
        {
            // 既存の子を削除
            for (int i = transform.childCount - 1; i >= 0; i--)
                Destroy(transform.GetChild(i).gameObject);

            // 本体
            var body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            body.transform.SetParent(transform, false);
            body.transform.localPosition = new Vector3(0f, 0.9f, 0f);
            body.transform.localScale = new Vector3(0.4f, 0.5f, 0.4f);
            Destroy(body.GetComponent<Collider>()); // 子のコライダーは不要

            var bodyMat = new Material(Shader.Find("Sprites/Default"))
            {
                color = new Color(0.15f, 0.12f, 0.1f) // 黒っぽい
            };
            body.GetComponent<MeshRenderer>().material = bodyMat;

            // 頭
            var head = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            head.transform.SetParent(transform, false);
            head.transform.localPosition = new Vector3(0f, 1.6f, 0f);
            head.transform.localScale = new Vector3(0.3f, 0.3f, 0.3f);
            Destroy(head.GetComponent<Collider>());

            var headMat = new Material(Shader.Find("Sprites/Default"))
            {
                color = new Color(0.9f, 0.75f, 0.6f) // 肌色
            };
            head.GetComponent<MeshRenderer>().material = headMat;
        }

        // ============================================================
        // デバッグ
        // ============================================================

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            // いたずら範囲
            Gizmos.color = new Color(1f, 0f, 0f, 0.15f);
            Gizmos.DrawSphere(transform.position, HarassRadius);

            // ガードマン検知範囲
            Gizmos.color = new Color(0f, 0f, 1f, 0.1f);
            Gizmos.DrawWireSphere(transform.position, GuardDetectionRadius);
        }
#endif
    }
}
