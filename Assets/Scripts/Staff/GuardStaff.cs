// ============================================================
// ThemeParkGame - GuardStaff
// ガードマン（治安維持スタッフ）
// ============================================================

using System.Collections.Generic;
using UnityEngine;
using ThemeParkGame.Core;

namespace ThemeParkGame.Staff
{
    /// <summary>
    /// ガードマンはパーク内の治安維持を担当するスタッフ。
    /// 不良来場者（フーリガン）の取り締まり・器物破損の抑止を行う。
    ///
    /// 【ゲームデザイン】
    /// ・パーク内には一定確率で不良来場者（フーリガン）が紛れ込む
    /// ・フーリガンはゴミのポイ捨て、アトラクション破壊、来場者への嫌がらせを行う
    /// ・ガードマンの巡回範囲内ではフーリガンの発生率が低下する（抑止効果）
    /// ・フーリガンを発見すると追跡・確保して退場させる
    /// ・スキルレベルが高いほど発見速度・追跡速度が向上し、抑止範囲も広がる
    /// ・ガードマン不足 → 治安低下 → 器物破損増加 → 来場者不安 → パーク評価低下
    /// </summary>
    public class GuardStaff : StaffMember
    {
        // ============================================================
        // 定数
        // ============================================================

        /// <summary>基本巡回検知範囲（メートル）</summary>
        private const float BaseDetectionRadius = 20f;

        /// <summary>スキルレベル1あたりの検知範囲拡大量</summary>
        private const float DetectionRadiusPerSkill = 5f;

        /// <summary>抑止効果範囲（メートル）。この範囲内ではフーリガン発生率が低下する</summary>
        private const float BaseDeterrentRadius = 25f;

        /// <summary>抑止効果の強さ（0-1）。スキルレベルで増加する</summary>
        private const float BaseDeterrentStrength = 0.3f;

        /// <summary>フーリガン確保にかかる基本時間（秒）</summary>
        private const float BaseApprehendDuration = 5f;

        /// <summary>追跡時のNavMeshAgentの速度倍率</summary>
        private const float ChaseSpeedMultiplier = 1.5f;

        /// <summary>確保完了後のクールダウン時間（秒）</summary>
        private const float PostApprehendCooldown = 2f;

        // ============================================================
        // フィールド
        // ============================================================

        [Header("Guard Settings")]
        [SerializeField] private float basePatrolSpeed = 3.5f;
        [SerializeField] private float chaseSpeed = 5f;

        /// <summary>現在追跡中のフーリガン</summary>
        private GameObject currentTarget;

        /// <summary>確保中の経過時間</summary>
        private float apprehendTimer;

        /// <summary>確保にかかる所要時間</summary>
        private float apprehendDuration;

        /// <summary>クールダウンタイマー</summary>
        private float cooldownTimer;

        /// <summary>追跡モード中かどうか</summary>
        private bool isChasing;

        /// <summary>確保した不良来場者の累計数（統計用）</summary>
        public int TotalApprehensions { get; private set; }

        /// <summary>通常の移動速度（追跡モード終了時に戻す用）</summary>
        private float originalSpeed;

        // ============================================================
        // プロパティ
        // ============================================================

        /// <summary>現在の検知範囲</summary>
        public float DetectionRadius => BaseDetectionRadius + (SkillLevel - 1) * DetectionRadiusPerSkill;

        /// <summary>現在の抑止範囲</summary>
        public float DeterrentRadius => BaseDeterrentRadius + (SkillLevel - 1) * 5f;

        /// <summary>
        /// 抑止効果の強さ (0-1)。
        /// この範囲内でのフーリガン発生確率に (1 - DeterrentStrength) を乗じる。
        /// </summary>
        public float DeterrentStrength => Mathf.Clamp01(BaseDeterrentStrength + (SkillLevel - 1) * 0.1f);

        // ============================================================
        // 初期化
        // ============================================================

        protected override void Awake()
        {
            base.Awake();
            StaffType = StaffType.Guard;
        }

        protected override void Start()
        {
            base.Start();
            if (navAgent != null)
            {
                originalSpeed = basePatrolSpeed;
                navAgent.speed = basePatrolSpeed;
            }
        }

        // ============================================================
        // 更新
        // ============================================================

        protected override void Update()
        {
            base.Update();

            // クールダウン更新
            if (cooldownTimer > 0f)
            {
                cooldownTimer -= Time.deltaTime;
            }

            // 追跡中にターゲットが消滅した場合の対処
            if (isChasing && currentTarget == null)
            {
                EndChase();
            }

            // 追跡中はターゲットの位置を追従する
            if (isChasing && currentTarget != null)
            {
                NavigateTo(currentTarget.transform.position);
            }
        }

        // ============================================================
        // タスク検索
        // ============================================================

        /// <summary>
        /// フーリガン（不良来場者）を検索する。
        /// 最も近いフーリガンを優先的にターゲットにする。
        /// </summary>
        protected override bool FindAndAssignTask()
        {
            if (cooldownTimer > 0f) return false;

            if (TryFindHooligan())
            {
                return true;
            }

            return false;
        }

        /// <summary>周囲のフーリガンを検索する</summary>
        private bool TryFindHooligan()
        {
            Collider[] hits = Physics.OverlapSphere(transform.position, DetectionRadius);
            Transform bestTarget = null;
            float bestDistance = float.MaxValue;

            foreach (var hit in hits)
            {
                // "Hooligan" タグでフーリガンを識別する
                if (!hit.CompareTag("Hooligan")) continue;
                if (!IsWithinPatrolArea(hit.transform.position)) continue;

                float dist = Vector3.Distance(transform.position, hit.transform.position);
                if (dist < bestDistance)
                {
                    bestDistance = dist;
                    bestTarget = hit.transform;
                }
            }

            if (bestTarget != null)
            {
                currentTarget = bestTarget.gameObject;
                BeginChase();
                return true;
            }

            return false;
        }

        // ============================================================
        // 追跡・確保
        // ============================================================

        /// <summary>追跡を開始する</summary>
        private void BeginChase()
        {
            isChasing = true;
            CurrentState = StaffBehaviorState.MovingToTask;

            // 追跡時は速度を上げる
            if (navAgent != null)
            {
                navAgent.speed = chaseSpeed * (1f + (SkillLevel - 1) * 0.1f);
            }

            if (currentTarget != null)
            {
                NavigateTo(currentTarget.transform.position);
            }

            Debug.Log($"[Guard] {Name} がフーリガンの追跡を開始しました");
        }

        /// <summary>追跡を終了して通常速度に戻す</summary>
        private void EndChase()
        {
            isChasing = false;
            currentTarget = null;

            if (navAgent != null)
            {
                navAgent.speed = originalSpeed;
            }

            CompleteCurrentTask();
        }

        protected override void OnTaskReached()
        {
            if (currentTarget == null)
            {
                EndChase();
                return;
            }

            // フーリガンに到達したら確保を開始
            isChasing = false;
            StopNavigation();
            apprehendTimer = 0f;
            apprehendDuration = CalculateApprehendDuration();

            Debug.Log($"[Guard] {Name} がフーリガンの確保を開始しました");
        }

        /// <summary>
        /// 確保作業を毎フレーム進行させる。
        ///
        /// 【ゲームデザイン】
        /// ・スキルレベルが高いほど確保が速い
        /// ・確保完了でフーリガンをパークから退場させる
        /// ・退場させた数が統計に記録され、パークの治安評価に寄与する
        /// </summary>
        protected override void PerformWork()
        {
            if (currentTarget == null)
            {
                EndChase();
                return;
            }

            apprehendTimer += Time.deltaTime;

            if (apprehendTimer >= apprehendDuration)
            {
                CompleteApprehension();
            }
        }

        /// <summary>確保を完了する</summary>
        private void CompleteApprehension()
        {
            TotalApprehensions++;

            Debug.Log($"[Guard] {Name} がフーリガンを確保しました"
                      + $"（累計確保数: {TotalApprehensions}）");

            // フーリガンを退場させる
            // 実際にはVisitorManagerを通じて退場処理を行う
            if (currentTarget != null)
            {
                // フーリガンの退場フラグを設定する想定
                // visitorManager.EjectVisitor(currentTarget);
                Debug.Log($"[Guard] フーリガンをパークから退場させました");
            }

            currentTarget = null;
            cooldownTimer = PostApprehendCooldown;

            if (navAgent != null)
            {
                navAgent.speed = originalSpeed;
            }

            CompleteCurrentTask();
        }

        // ============================================================
        // 抑止効果
        // ============================================================

        /// <summary>
        /// 指定位置でのフーリガン発生抑止率を計算する。
        /// VisitorManagerがフーリガン発生判定時に呼び出す想定。
        ///
        /// 【ゲームデザイン】
        /// ・ガードマンの存在そのものが犯罪の抑止力となる
        /// ・距離が近いほど、スキルが高いほど抑止効果が強い
        /// ・複数のガードマンの効果は加算される（ただし上限1.0）
        /// </summary>
        /// <returns>抑止率 (0-1)。発生確率にこの値の補数を乗じる</returns>
        public float GetDeterrentFactorAt(Vector3 position)
        {
            float distance = Vector3.Distance(transform.position, position);

            if (distance > DeterrentRadius) return 0f;

            // 距離に応じた減衰（線形）
            float distanceFactor = 1f - (distance / DeterrentRadius);

            return DeterrentStrength * distanceFactor;
        }

        // ============================================================
        // 所要時間算出
        // ============================================================

        /// <summary>確保にかかる時間を算出する</summary>
        private float CalculateApprehendDuration()
        {
            return BaseApprehendDuration / WorkEfficiencyMultiplier;
        }

        // ============================================================
        // デバッグ
        // ============================================================

#if UNITY_EDITOR
        protected override void OnDrawGizmosSelected()
        {
            base.OnDrawGizmosSelected();

            // 検知範囲
            Gizmos.color = new Color(0f, 0f, 1f, 0.1f);
            Gizmos.DrawSphere(transform.position, DetectionRadius);
            Gizmos.color = new Color(0f, 0f, 1f, 0.4f);
            Gizmos.DrawWireSphere(transform.position, DetectionRadius);

            // 抑止範囲
            Gizmos.color = new Color(1f, 1f, 0f, 0.05f);
            Gizmos.DrawSphere(transform.position, DeterrentRadius);
            Gizmos.color = new Color(1f, 1f, 0f, 0.3f);
            Gizmos.DrawWireSphere(transform.position, DeterrentRadius);

            // 追跡対象への線
            if (currentTarget != null)
            {
                Gizmos.color = Color.red;
                Gizmos.DrawLine(transform.position, currentTarget.transform.position);
            }
        }
#endif
    }
}
