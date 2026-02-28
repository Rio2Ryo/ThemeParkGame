// ============================================================
// ThemeParkGame - GardenerStaff
// 園芸師 - パークの美観・ムード評価を向上
// ============================================================

using UnityEngine;
using UnityEngine.AI;
using ThemeParkGame.Core;
using ThemeParkGame.Park;

namespace ThemeParkGame.Staff
{
    /// <summary>
    /// 園芸師はパーク内の緑地・花壇の手入れと植栽を行い、ムード評価を向上させるスタッフ。
    ///
    /// 【ゲームデザイン】
    /// ・パトロールエリア内を巡回し、装飾オブジェクト付近で「手入れ」作業を行う
    /// ・装飾物がないエリアでは自由な地点で植栽（新規植え込み）作業を行う
    /// ・手入れ/植栽の完了ごとにパークのムード評価にボーナスが加算される
    /// ・スキルレベルが高いほど作業速度が速く、ボーナス量も増加する
    /// ・作業中は近くの来場者の幸福度が微量上昇する（花の香り効果）
    /// ・装飾物の近くでの作業はボーナスが大きい（既存の景観を強化する効果）
    /// ・雨天時は植栽効果が1.5倍になる（自然の恵み）
    /// </summary>
    public class GardenerStaff : StaffMember
    {
        // ============================================================
        // 園芸作業の種別
        // ============================================================

        /// <summary>園芸作業の種別</summary>
        public enum GardenTaskType
        {
            /// <summary>既存の装飾物・花壇の手入れ</summary>
            Maintenance,

            /// <summary>新規の植栽（空きスペースに植え込みを行う）</summary>
            Planting
        }

        // ============================================================
        // 定数
        // ============================================================

        /// <summary>手入れ作業の基本所要時間（秒）</summary>
        private const float BaseMaintenanceDuration = 4f;

        /// <summary>植栽作業の基本所要時間（秒）。手入れより長い</summary>
        private const float BasePlantingDuration = 6f;

        /// <summary>手入れ完了時のムード評価ボーナス（基本値）</summary>
        private const float BaseMaintenanceMoodBonus = 0.5f;

        /// <summary>植栽完了時のムード評価ボーナス（基本値）</summary>
        private const float BasePlantingMoodBonus = 0.3f;

        /// <summary>装飾物の近くで作業した場合の追加ボーナス倍率</summary>
        private const float DecorationProximityBonusMultiplier = 1.5f;

        /// <summary>雨天時の植栽効果ボーナス倍率</summary>
        private const float RainBonusMultiplier = 1.5f;

        /// <summary>園芸作業のクールダウン間隔（秒）</summary>
        private const float GardenInterval = 15f;

        /// <summary>幸福度オーラ（花の香り効果）の影響半径（メートル）</summary>
        private const float HappinessAuraRadius = 15f;

        /// <summary>幸福度オーラの毎秒加算量</summary>
        private const float HappinessAuraAmount = 0.02f;

        /// <summary>装飾物を検知する半径（メートル）</summary>
        private const float DecorationDetectionRadius = 20f;

        /// <summary>装飾物の近くとみなす距離（メートル）</summary>
        private const float DecorationProximityDistance = 5f;

        // ============================================================
        // フィールド
        // ============================================================

        [Header("Gardener Settings")]
        [SerializeField] private float searchRadius = 20f;

        /// <summary>園芸作業タイマー</summary>
        private float _gardenTimer;

        /// <summary>作業間隔タイマー</summary>
        private float _intervalTimer;

        /// <summary>オーラ適用タイマー</summary>
        private float _auraTimer;

        /// <summary>作業中かどうか</summary>
        private bool _isGardening;

        /// <summary>作業地点</summary>
        private Vector3 _gardenSpot;

        /// <summary>現在の作業種別</summary>
        private GardenTaskType _currentTaskType;

        /// <summary>現在の作業が装飾物の近くかどうか</summary>
        private bool _isNearDecoration;

        /// <summary>現在の天候が雨かどうか</summary>
        private bool _isRaining;

        /// <summary>園芸作業の合計回数</summary>
        public int TotalGardeningSessions { get; private set; }

        /// <summary>手入れ作業の回数</summary>
        public int MaintenanceCount { get; private set; }

        /// <summary>植栽作業の回数</summary>
        public int PlantingCount { get; private set; }

        // ============================================================
        // 初期化
        // ============================================================

        protected override void Awake()
        {
            base.Awake();
            StaffType = StaffType.Gardener;
        }

        // ============================================================
        // イベント購読
        // ============================================================

        protected override void SubscribeToEvents()
        {
            GameEvents.OnWeatherChanged += HandleWeatherChanged;
        }

        protected override void UnsubscribeFromEvents()
        {
            GameEvents.OnWeatherChanged -= HandleWeatherChanged;
        }

        /// <summary>
        /// 天候変化イベントハンドラ。
        /// 雨天時は植栽効果が上昇するため、雨フラグを更新する。
        /// </summary>
        private void HandleWeatherChanged(Weather newWeather)
        {
            _isRaining = newWeather == Weather.Rainy;
        }

        // ============================================================
        // タスク検索
        // ============================================================

        /// <summary>
        /// 園芸タスクを検索する。
        ///
        /// 【検索優先度】
        /// 1. 近くの装飾オブジェクトの手入れ（Maintenance）
        ///    - 既存の花壇・植栽を手入れすることでムード評価を強化
        /// 2. 空きスペースへの植栽（Planting）
        ///    - パトロールエリア内のランダムな地点で新規植え込み
        ///
        /// 同種の中では距離が近いものを優先する。
        /// </summary>
        protected override bool FindAndAssignTask()
        {
            _intervalTimer -= Time.deltaTime;
            if (_intervalTimer > 0f) return false;

            // 優先: 近くの装飾物を手入れ
            if (TryFindDecorationToMaintain())
            {
                return true;
            }

            // 次点: 空きスペースに植栽
            if (TryAssignPlantingTask())
            {
                return true;
            }

            return false;
        }

        /// <summary>手入れ可能な装飾オブジェクトを検索する</summary>
        private bool TryFindDecorationToMaintain()
        {
            Collider[] hits = Physics.OverlapSphere(transform.position, DecorationDetectionRadius);
            Transform bestTarget = null;
            float bestDistance = float.MaxValue;

            foreach (var hit in hits)
            {
                if (!hit.CompareTag("Decoration")) continue;
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
                _gardenSpot = bestTarget.position;
                _currentTaskType = GardenTaskType.Maintenance;
                _isNearDecoration = true;
                _gardenTimer = 0f;
                _isGardening = false;
                NavigateTo(_gardenSpot);
                CurrentState = StaffBehaviorState.MovingToTask;
                return true;
            }

            return false;
        }

        /// <summary>
        /// パトロールエリア内のランダムな地点を植栽対象として設定する。
        /// NavMeshサンプリングにより到達可能な地点のみを選択する。
        /// </summary>
        private bool TryAssignPlantingTask()
        {
            Vector3 center = transform.position;
            Vector3 randomOffset = Random.insideUnitSphere * searchRadius;
            randomOffset.y = 0f;
            Vector3 candidate = center + randomOffset;

            // パトロールエリア内か確認
            if (!IsWithinPatrolArea(candidate))
            {
                candidate = center;
            }

            // NavMeshサンプリングで到達可能な地点を確認
            if (NavMesh.SamplePosition(candidate, out NavMeshHit hit, 10f, NavMesh.AllAreas))
            {
                _gardenSpot = hit.position;
            }
            else
            {
                _gardenSpot = center; // フォールバック: その場で作業
            }

            // 目的地付近に装飾物があるか確認
            _isNearDecoration = HasDecorationNearby(_gardenSpot);
            _currentTaskType = GardenTaskType.Planting;
            _gardenTimer = 0f;
            _isGardening = false;
            NavigateTo(_gardenSpot);
            CurrentState = StaffBehaviorState.MovingToTask;
            return true;
        }

        // ============================================================
        // 作業実行
        // ============================================================

        protected override void OnTaskReached()
        {
            _gardenTimer = 0f;
            _isGardening = true;
            StopNavigation();

            string taskName = _currentTaskType switch
            {
                GardenTaskType.Maintenance => "装飾物の手入れ",
                GardenTaskType.Planting => "植栽",
                _ => "園芸作業"
            };

            WebGLOptimizer.LogVerbose($"[Gardener] {Name} が{taskName}を開始" +
                (_isNearDecoration ? " (装飾物ボーナスあり)" : ""));
        }

        /// <summary>
        /// 園芸作業を毎フレーム進行させる。
        ///
        /// 【ゲームデザイン】
        /// ・作業中は近くの来場者の幸福度が微量上昇する（花の香り効果）
        /// ・スキルレベルが高いほど作業完了が速い
        /// ・作業完了時にムード評価にボーナスが加算される
        /// </summary>
        protected override void PerformWork()
        {
            if (!_isGardening)
            {
                CompleteCurrentTask();
                return;
            }

            _gardenTimer += Time.deltaTime;
            float duration = CalculateWorkDuration(_currentTaskType);

            // 作業中は近くの来場者の幸福度を微量上昇（花の香り効果）
            _auraTimer += Time.deltaTime;
            if (_auraTimer >= 1f)
            {
                _auraTimer = 0f;
                ApplyHappinessAura();
            }

            if (_gardenTimer >= duration)
            {
                CompleteGardening();
            }
        }

        /// <summary>園芸作業を完了し、ムード評価にボーナスを加算する</summary>
        private void CompleteGardening()
        {
            TotalGardeningSessions++;
            _isGardening = false;
            _intervalTimer = GardenInterval / WorkEfficiencyMultiplier;

            switch (_currentTaskType)
            {
                case GardenTaskType.Maintenance:
                    MaintenanceCount++;
                    break;
                case GardenTaskType.Planting:
                    PlantingCount++;
                    break;
            }

            // ムード評価にボーナスを加算
            float bonus = CalculateMoodBonus();
            var pm = GameManager.Instance?.ParkManager;
            if (pm?.Rating != null)
            {
                pm.Rating.ApplyExternalBonus(CertificateCategory.Mood, bonus);
            }

            string taskName = _currentTaskType switch
            {
                GardenTaskType.Maintenance => "手入れ",
                GardenTaskType.Planting => "植栽",
                _ => "園芸"
            };

            WebGLOptimizer.LogVerbose($"[Gardener] {Name} が{taskName}を完了 " +
                $"(ムード+{bonus:F2}, 累計{TotalGardeningSessions}回)");

            CompleteCurrentTask();
        }

        // ============================================================
        // ボーナス計算
        // ============================================================

        /// <summary>
        /// ムード評価ボーナスを計算する。
        ///
        /// 【計算式】
        /// 基本ボーナス × スキル倍率 × 装飾物ボーナス × 天候ボーナス
        ///
        /// ・スキルレベルが高いほどボーナスが大きい
        /// ・装飾物の近くでの作業は1.5倍のボーナス
        /// ・雨天時の植栽は1.5倍のボーナス（雨の恵み）
        /// </summary>
        private float CalculateMoodBonus()
        {
            float baseBonus = _currentTaskType switch
            {
                GardenTaskType.Maintenance => BaseMaintenanceMoodBonus,
                GardenTaskType.Planting => BasePlantingMoodBonus,
                _ => BasePlantingMoodBonus
            };

            float bonus = baseBonus * WorkEfficiencyMultiplier;

            // 装飾物近接ボーナス
            if (_isNearDecoration)
            {
                bonus *= DecorationProximityBonusMultiplier;
            }

            // 雨天ボーナス（植栽のみ）
            if (_isRaining && _currentTaskType == GardenTaskType.Planting)
            {
                bonus *= RainBonusMultiplier;
            }

            return bonus;
        }

        /// <summary>
        /// 作業時間を算出する。スキルレベルが高いほど高速に作業できる。
        /// 手入れ: スキル1で4秒、スキル5で2秒
        /// 植栽:   スキル1で6秒、スキル5で3秒
        /// </summary>
        private float CalculateWorkDuration(GardenTaskType taskType)
        {
            float baseDuration = taskType switch
            {
                GardenTaskType.Maintenance => BaseMaintenanceDuration,
                GardenTaskType.Planting => BasePlantingDuration,
                _ => BasePlantingDuration
            };

            return baseDuration / WorkEfficiencyMultiplier;
        }

        // ============================================================
        // 幸福度オーラ（花の香り効果）
        // ============================================================

        /// <summary>
        /// 作業中に近くの来場者の幸福度を微量上昇させる。
        /// スキルレベルが高いほどオーラの効果が大きくなる。
        /// </summary>
        private void ApplyHappinessAura()
        {
            if (GameManager.Instance?.VisitorManager == null) return;

            float auraAmount = HappinessAuraAmount * WorkEfficiencyMultiplier;

            var visitors = GameManager.Instance.VisitorManager.GetAllActiveVisitors();
            for (int i = 0; i < visitors.Count; i++)
            {
                var v = visitors[i];
                if (v == null || !v.IsActive) continue;

                float dist = Vector3.Distance(transform.position, v.transform.position);
                if (dist <= HappinessAuraRadius)
                {
                    v.Parameters.ModifyHappiness(auraAmount);
                }
            }
        }

        // ============================================================
        // ユーティリティ
        // ============================================================

        /// <summary>指定地点の近くに装飾オブジェクトがあるかを判定する</summary>
        private bool HasDecorationNearby(Vector3 position)
        {
            Collider[] hits = Physics.OverlapSphere(position, DecorationProximityDistance);
            foreach (var hit in hits)
            {
                if (hit.CompareTag("Decoration"))
                {
                    return true;
                }
            }
            return false;
        }

        // ============================================================
        // デバッグ
        // ============================================================

#if UNITY_EDITOR
        protected override void OnDrawGizmosSelected()
        {
            base.OnDrawGizmosSelected();

            // 検索範囲
            Gizmos.color = new Color(0.2f, 0.8f, 0.2f, 0.1f);
            Gizmos.DrawSphere(transform.position, searchRadius);
            Gizmos.color = new Color(0.2f, 0.8f, 0.2f, 0.4f);
            Gizmos.DrawWireSphere(transform.position, searchRadius);

            // 幸福度オーラ範囲（作業中のみ表示が意味がある）
            if (_isGardening)
            {
                Gizmos.color = new Color(1f, 0.9f, 0f, 0.08f);
                Gizmos.DrawSphere(transform.position, HappinessAuraRadius);
            }

            // 作業地点への線
            if (CurrentState == StaffBehaviorState.MovingToTask || _isGardening)
            {
                Gizmos.color = _currentTaskType switch
                {
                    GardenTaskType.Maintenance => new Color(0f, 1f, 0.5f),
                    GardenTaskType.Planting => new Color(0.5f, 1f, 0f),
                    _ => Color.green
                };
                Gizmos.DrawLine(transform.position, _gardenSpot);
                Gizmos.DrawSphere(_gardenSpot, 0.5f);
            }
        }
#endif
    }
}
