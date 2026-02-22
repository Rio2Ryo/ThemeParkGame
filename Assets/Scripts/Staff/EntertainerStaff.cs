// ============================================================
// ThemeParkGame - EntertainerStaff
// エンターテイナー（接客・演出スタッフ）
// ============================================================

using System.Collections.Generic;
using UnityEngine;
using ThemeParkGame.Core;

namespace ThemeParkGame.Staff
{
    /// <summary>
    /// エンターテイナーは着ぐるみを着て来場者を楽しませるスタッフ。
    ///
    /// 【ゲームデザイン】
    /// ・行列（キュー）付近に配置すると、待ち時間によるフラストレーションを軽減できる
    /// ・エリア内の来場者幸福度を継続的に上昇させる
    /// ・テーマゾーンごとに異なるコスチュームを着用し、ゾーンのテーマ性を強化する
    ///   - LostKingdom: 騎士、ドラゴン
    ///   - HalloweenWorld: ゴースト、ヴァンパイア
    ///   - Wonderland: 妖精、ユニコーン
    ///   - SpaceZone: 宇宙飛行士、エイリアン
    /// ・スキルレベルが高いほど効果範囲が広く、幸福度上昇量が大きい
    /// ・原作ではエンターテイナーの配置が来場者の待ち時間体験を大きく左右した
    /// </summary>
    public class EntertainerStaff : StaffMember
    {
        // ============================================================
        // コスチューム定義
        // ============================================================

        /// <summary>
        /// エンターテイナーのコスチューム種別。
        /// テーマゾーンに合ったコスチュームを着用することでボーナス効果が得られる。
        /// </summary>
        public enum CostumeType
        {
            // LostKingdom（ロストキングダム）
            Knight,     // 騎士
            Dragon,     // ドラゴン

            // HalloweenWorld（ハロウィーンワールド）
            Ghost,      // ゴースト
            Vampire,    // ヴァンパイア

            // Wonderland（ワンダーランド）
            Fairy,      // 妖精
            Unicorn,    // ユニコーン

            // SpaceZone（スペースゾーン）
            Astronaut,  // 宇宙飛行士
            Alien       // エイリアン
        }

        // ============================================================
        // 定数
        // ============================================================

        /// <summary>基本効果範囲（メートル）。スキルレベルで拡大される</summary>
        private const float BaseEffectRadius = 10f;

        /// <summary>効果範囲のスキルレベル1あたりの拡大量（メートル）</summary>
        private const float RadiusPerSkillLevel = 3f;

        /// <summary>来場者幸福度の基本上昇量（1秒あたり）</summary>
        private const float BaseHappinessBoostPerSecond = 0.5f;

        /// <summary>キューフラストレーション軽減率（基本値）</summary>
        private const float BaseQueueFrustrationReduction = 0.3f;

        /// <summary>コスチュームがテーマゾーンに一致する場合のボーナス倍率</summary>
        private const float ThemeMatchBonus = 1.5f;

        /// <summary>パフォーマンス1回あたりの所要時間（秒）</summary>
        private const float PerformanceDuration = 8f;

        /// <summary>パフォーマンス間のクールダウン（秒）</summary>
        private const float PerformanceCooldown = 3f;

        // ============================================================
        // フィールド
        // ============================================================

        [Header("Entertainer Settings")]
        [SerializeField] private CostumeType currentCostume = CostumeType.Knight;
        [SerializeField] private ThemeZone assignedZone = ThemeZone.LostKingdom;

        /// <summary>パフォーマンス実行中かどうか</summary>
        private bool isPerforming;

        /// <summary>パフォーマンスタイマー</summary>
        private float performanceTimer;

        /// <summary>クールダウンタイマー</summary>
        private float cooldownTimer;

        /// <summary>効果を与えた来場者の累計数（統計用）</summary>
        public int TotalVisitorsEntertained { get; private set; }

        // ============================================================
        // プロパティ
        // ============================================================

        /// <summary>現在のコスチューム</summary>
        public CostumeType CurrentCostume
        {
            get => currentCostume;
            set => currentCostume = value;
        }

        /// <summary>担当テーマゾーン</summary>
        public ThemeZone AssignedZone
        {
            get => assignedZone;
            set => assignedZone = value;
        }

        /// <summary>
        /// 現在の効果範囲（メートル）。
        /// スキルレベルが高いほど広範囲に影響を与えられる。
        /// </summary>
        public float EffectRadius => BaseEffectRadius + (SkillLevel - 1) * RadiusPerSkillLevel;

        /// <summary>コスチュームがテーマゾーンに一致しているか</summary>
        public bool IsCostumeMatchingZone => GetMatchingZone(currentCostume) == assignedZone;

        // ============================================================
        // 初期化
        // ============================================================

        protected override void Awake()
        {
            base.Awake();
            StaffType = StaffType.Entertainer;
        }

        // ============================================================
        // タスク検索
        // ============================================================

        /// <summary>
        /// エンターテイナーのタスク検索。
        /// 近くに来場者が集まっている場所（キュー付近等）を優先的に探す。
        /// </summary>
        protected override bool FindAndAssignTask()
        {
            if (cooldownTimer > 0f)
            {
                cooldownTimer -= Time.deltaTime;
                return false;
            }

            // 効果範囲内に来場者がいればその場でパフォーマンスを開始
            if (HasVisitorsNearby())
            {
                StartPerformance();
                return true;
            }

            // 来場者が集まっている場所（キュー等）を探す
            if (TryFindCrowdedArea())
            {
                return true;
            }

            return false;
        }

        /// <summary>効果範囲内に来場者がいるかを確認する</summary>
        private bool HasVisitorsNearby()
        {
            Collider[] hits = Physics.OverlapSphere(transform.position, EffectRadius);
            foreach (var hit in hits)
            {
                if (hit.CompareTag("Visitor"))
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>来場者が集まっている場所を探して移動する</summary>
        private bool TryFindCrowdedArea()
        {
            // キュー（行列）オブジェクトを検索して最も混雑している場所へ向かう
            float searchRadius = EffectRadius * 3f;
            Collider[] hits = Physics.OverlapSphere(transform.position, searchRadius);
            Transform bestTarget = null;
            int bestVisitorCount = 0;

            foreach (var hit in hits)
            {
                if (!hit.CompareTag("QueueArea")) continue;
                if (!IsWithinPatrolArea(hit.transform.position)) continue;

                // キューエリア周辺の来場者数をカウント
                int visitorCount = CountVisitorsAround(hit.transform.position, 8f);
                if (visitorCount > bestVisitorCount)
                {
                    bestVisitorCount = visitorCount;
                    bestTarget = hit.transform;
                }
            }

            if (bestTarget != null && bestVisitorCount > 0)
            {
                NavigateTo(bestTarget.position);
                CurrentState = StaffBehaviorState.MovingToTask;
                return true;
            }

            return false;
        }

        /// <summary>指定地点周辺の来場者数をカウントする</summary>
        private int CountVisitorsAround(Vector3 position, float radius)
        {
            int count = 0;
            Collider[] hits = Physics.OverlapSphere(position, radius);
            foreach (var hit in hits)
            {
                if (hit.CompareTag("Visitor"))
                {
                    count++;
                }
            }
            return count;
        }

        // ============================================================
        // パフォーマンス実行
        // ============================================================

        /// <summary>パフォーマンスを開始する</summary>
        private void StartPerformance()
        {
            isPerforming = true;
            performanceTimer = 0f;
            CurrentState = StaffBehaviorState.Working;
            StopNavigation();

            Debug.Log($"[Entertainer] {Name} がパフォーマンスを開始しました"
                      + $"（コスチューム: {currentCostume}）");
        }

        protected override void OnTaskReached()
        {
            StartPerformance();
        }

        /// <summary>
        /// パフォーマンスを毎フレーム進行させる。
        ///
        /// 【ゲームデザイン】
        /// ・効果範囲内の全来場者に幸福度ブーストを付与する
        /// ・キュー内の来場者にはフラストレーション軽減効果を追加で付与する
        /// ・コスチュームがテーマゾーンに一致している場合、効果が1.5倍になる
        /// </summary>
        protected override void PerformWork()
        {
            if (!isPerforming) return;

            performanceTimer += Time.deltaTime;
            float deltaTime = Time.deltaTime;

            // テーマゾーン一致ボーナス
            float themeMultiplier = IsCostumeMatchingZone ? ThemeMatchBonus : 1f;

            // 効果範囲内の来場者に効果を付与
            ApplyEntertainmentEffect(deltaTime, themeMultiplier);

            // パフォーマンス完了
            if (performanceTimer >= PerformanceDuration)
            {
                CompletePerformance();
            }
        }

        /// <summary>
        /// 効果範囲内の来場者に幸福度上昇とフラストレーション軽減を適用する。
        /// </summary>
        private void ApplyEntertainmentEffect(float deltaTime, float themeMultiplier)
        {
            float happinessBoost = BaseHappinessBoostPerSecond
                                   * WorkEfficiencyMultiplier
                                   * themeMultiplier
                                   * deltaTime;

            Collider[] hits = Physics.OverlapSphere(transform.position, EffectRadius);
            foreach (var hit in hits)
            {
                if (!hit.CompareTag("Visitor")) continue;

                // 来場者の幸福度を上昇させる
                // 実際にはVisitorコンポーネントのインターフェースを通じて適用する
                int visitorId = hit.GetInstanceID();
                GameEvents.FireVisitorHappinessChanged(visitorId, happinessBoost);
                TotalVisitorsEntertained++;
            }
        }

        /// <summary>パフォーマンスを完了する</summary>
        private void CompletePerformance()
        {
            isPerforming = false;
            cooldownTimer = PerformanceCooldown;

            Debug.Log($"[Entertainer] {Name} がパフォーマンスを完了しました"
                      + $"（累計来場者数: {TotalVisitorsEntertained}）");

            CompleteCurrentTask();
        }

        // ============================================================
        // コスチューム管理
        // ============================================================

        /// <summary>
        /// コスチュームを変更する。
        /// テーマゾーンに合ったコスチュームを選ぶとボーナス効果が得られる。
        /// </summary>
        public void ChangeCostume(CostumeType newCostume)
        {
            currentCostume = newCostume;
            Debug.Log($"[Entertainer] {Name} のコスチュームを {newCostume} に変更しました"
                      + $"（ゾーン一致: {IsCostumeMatchingZone}）");
        }

        /// <summary>指定テーマゾーンに最適なコスチュームを自動選択する</summary>
        public void AutoSelectCostume(ThemeZone zone)
        {
            assignedZone = zone;
            currentCostume = GetDefaultCostumeForZone(zone);
            Debug.Log($"[Entertainer] {Name} が {zone} 用コスチューム {currentCostume} を装着しました");
        }

        /// <summary>テーマゾーンに対応するデフォルトコスチュームを取得する</summary>
        public static CostumeType GetDefaultCostumeForZone(ThemeZone zone)
        {
            return zone switch
            {
                ThemeZone.LostKingdom => CostumeType.Knight,
                ThemeZone.HalloweenWorld => CostumeType.Ghost,
                ThemeZone.Wonderland => CostumeType.Fairy,
                ThemeZone.SpaceZone => CostumeType.Astronaut,
                _ => CostumeType.Knight
            };
        }

        /// <summary>コスチュームが所属するテーマゾーンを取得する</summary>
        public static ThemeZone GetMatchingZone(CostumeType costume)
        {
            return costume switch
            {
                CostumeType.Knight or CostumeType.Dragon => ThemeZone.LostKingdom,
                CostumeType.Ghost or CostumeType.Vampire => ThemeZone.HalloweenWorld,
                CostumeType.Fairy or CostumeType.Unicorn => ThemeZone.Wonderland,
                CostumeType.Astronaut or CostumeType.Alien => ThemeZone.SpaceZone,
                _ => ThemeZone.LostKingdom
            };
        }

        // ============================================================
        // デバッグ
        // ============================================================

#if UNITY_EDITOR
        protected override void OnDrawGizmosSelected()
        {
            base.OnDrawGizmosSelected();

            // 効果範囲
            Gizmos.color = new Color(1f, 0f, 1f, 0.1f);
            Gizmos.DrawSphere(transform.position, EffectRadius);
            Gizmos.color = new Color(1f, 0f, 1f, 0.5f);
            Gizmos.DrawWireSphere(transform.position, EffectRadius);
        }
#endif
    }
}
