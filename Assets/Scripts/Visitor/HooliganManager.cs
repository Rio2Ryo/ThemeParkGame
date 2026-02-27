// ============================================================
// ThemeParkGame - HooliganManager
// フーリガン（不審者）のスポーン・管理システム
// ============================================================

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using ThemeParkGame.Core;
using ThemeParkGame.Staff;

namespace ThemeParkGame.Visitor
{
    /// <summary>
    /// フーリガンのスポーンと管理を担当するシングルトンマネージャー。
    ///
    /// 【ゲームデザイン】
    /// ・難易度・時間帯・パーク規模に応じたスポーン頻度制御
    /// ・夜間（18時以降）はフーリガン出現率が上昇
    /// ・ガードマンの巡回範囲は抑止効果があり、フーリガンが出にくくなる
    /// ・ガードマン0人の場合、フーリガン発生率が大幅に上昇
    /// ・最大同時存在数を制限して過度な負荷を防ぐ
    /// </summary>
    public class HooliganManager : MonoBehaviour
    {
        // ============================================================
        // 定数
        // ============================================================

        /// <summary>スポーン判定間隔（秒）</summary>
        private const float SpawnCheckInterval = 15f;

        /// <summary>基本スポーン確率（判定1回あたり）</summary>
        private const float BaseSpawnChance = 0.15f;

        /// <summary>夜間（18時以降）のスポーン確率ボーナス</summary>
        private const float NightSpawnBonus = 0.15f;

        /// <summary>ガードマン0人時のスポーン確率ボーナス</summary>
        private const float NoGuardBonus = 0.2f;

        /// <summary>最大同時存在フーリガン数</summary>
        private const int MaxHooligans = 5;

        /// <summary>来場者数に対するフーリガン比率（100人につき1体の上限増加）</summary>
        private const float HooliganPerVisitors = 0.01f;

        // ============================================================
        // フィールド
        // ============================================================

        private float _spawnTimer;
        private int _nextHooliganId = 1;

        /// <summary>現在アクティブなフーリガンのID管理</summary>
        private readonly HashSet<int> _activeHooliganIds = new HashSet<int>();

        /// <summary>累計スポーンしたフーリガン数</summary>
        public int TotalSpawned { get; private set; }

        /// <summary>現在のアクティブフーリガン数</summary>
        public int ActiveCount => _activeHooliganIds.Count;

        // ============================================================
        // シングルトン
        // ============================================================

        public static HooliganManager Instance { get; private set; }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        // ============================================================
        // メインループ
        // ============================================================

        private void Update()
        {
            var gm = GameManager.Instance;
            if (gm == null || gm.IsPaused) return;
            if (gm.TimeManager == null || !gm.TimeManager.IsParkOpen) return;

            _spawnTimer += Time.deltaTime;
            if (_spawnTimer >= SpawnCheckInterval)
            {
                _spawnTimer = 0f;
                TrySpawnHooligan();
            }
        }

        // ============================================================
        // スポーン判定
        // ============================================================

        /// <summary>
        /// フーリガンのスポーンを試みる。
        /// 難易度・時間帯・ガードマン数・来場者数に応じた確率で判定する。
        /// </summary>
        private void TrySpawnHooligan()
        {
            var gm = GameManager.Instance;
            if (gm == null) return;

            // 最大数チェック
            int dynamicMax = CalculateMaxHooligans();
            if (_activeHooliganIds.Count >= dynamicMax) return;

            // スポーン確率を計算
            float chance = CalculateSpawnChance();

            if (Random.value < chance)
            {
                SpawnHooligan();
            }
        }

        /// <summary>
        /// 動的な最大フーリガン数を計算する。
        /// 来場者数に比例するが、MaxHooligansを上限とする。
        /// </summary>
        private int CalculateMaxHooligans()
        {
            var vm = GameManager.Instance?.VisitorManager;
            if (vm == null) return 1;

            int visitorBased = Mathf.CeilToInt(vm.ActiveVisitorCount * HooliganPerVisitors);
            return Mathf.Clamp(visitorBased, 1, MaxHooligans);
        }

        /// <summary>
        /// スポーン確率を計算する。
        /// 基本確率に対して以下の補正を適用:
        /// - 難易度: Easy=0.5倍, Hard=1.5倍
        /// - 夜間: +15%
        /// - ガードマン0人: +20%
        /// - ガードマン抑止: 各ガードマンの抑止効果で確率を低減
        /// </summary>
        private float CalculateSpawnChance()
        {
            var gm = GameManager.Instance;
            float chance = BaseSpawnChance;

            // 難易度補正
            if (gm != null)
            {
                switch (gm.CurrentDifficulty)
                {
                    case GameDifficulty.Easy:
                        chance *= 0.5f;
                        break;
                    case GameDifficulty.Hard:
                        chance *= 1.5f;
                        break;
                }
            }

            // 夜間ボーナス（18時以降）
            if (gm?.TimeManager != null && gm.TimeManager.CurrentHour >= 18f)
            {
                chance += NightSpawnBonus;
            }

            // ガードマン配備状況
            var sm = gm?.StaffManager;
            if (sm != null)
            {
                int guardCount = sm.GetStaffCountByType(StaffType.Guard);
                if (guardCount == 0)
                {
                    // ガードマン0人: 大幅上昇
                    chance += NoGuardBonus;
                }
                else
                {
                    // ガードマンの抑止効果を適用
                    // スポーン予定位置の抑止率は不明なので、全ガードの平均抑止力で代用
                    float deterrent = CalculateAverageDeterrent(sm);
                    chance *= (1f - deterrent);
                }
            }

            return Mathf.Clamp01(chance);
        }

        /// <summary>全ガードマンの平均抑止効果を計算する</summary>
        private float CalculateAverageDeterrent(StaffManager sm)
        {
            var guards = sm.GetStaffByType(StaffType.Guard);
            if (guards.Count == 0) return 0f;

            float totalDeterrent = 0f;
            foreach (var staff in guards)
            {
                var guard = staff as GuardStaff;
                if (guard != null)
                {
                    totalDeterrent += guard.DeterrentStrength;
                }
            }

            // 複数ガードの合算（上限0.8 = 完全抑制はしない）
            return Mathf.Clamp(totalDeterrent, 0f, 0.8f);
        }

        // ============================================================
        // スポーン実行
        // ============================================================

        /// <summary>フーリガンを生成する</summary>
        private void SpawnHooligan()
        {
            // スポーン位置: パーク入口付近からランダム方向
            Vector3 spawnPos = GetSpawnPosition();
            if (!NavMesh.SamplePosition(spawnPos, out NavMeshHit hit, 10f, NavMesh.AllAreas))
            {
                return; // NavMesh上に有効な位置が見つからない
            }

            var go = new GameObject($"Hooligan_{_nextHooliganId}");
            go.transform.position = hit.position;

            var hooligan = go.AddComponent<Hooligan>();
            hooligan.Initialize(_nextHooliganId, hit.position);

            _activeHooliganIds.Add(_nextHooliganId);
            _nextHooliganId++;
            TotalSpawned++;

            WebGLOptimizer.LogVerbose(
                $"[HooliganManager] フーリガン出現 (ID={hooligan.HooliganId}, " +
                $"現在{_activeHooliganIds.Count}体, 累計{TotalSpawned}体)");

            // 通知
            GameManager.Instance?.ShowNotification(
                "パーク内に不審者が出現しました！ガードマンを配備してください", NotifLevel.Warning);
        }

        /// <summary>スポーン位置を決定する</summary>
        private Vector3 GetSpawnPosition()
        {
            // ParkExitタグからスポーン位置を取得
            var exit = GameObject.FindGameObjectWithTag("ParkExit");
            Vector3 basePos = exit != null ? exit.transform.position : Vector3.zero;

            // パーク入口からランダムオフセット
            Vector3 offset = Random.insideUnitSphere * 15f;
            offset.y = 0f;
            return basePos + offset;
        }

        // ============================================================
        // フーリガン除去通知
        // ============================================================

        /// <summary>
        /// フーリガンが除去された時に呼ばれるコールバック。
        /// Hooligan.OnApprehended() または Hooligan.SelfDestruct() から呼ばれる。
        /// </summary>
        public void OnHooliganRemoved(int hooliganId)
        {
            _activeHooliganIds.Remove(hooliganId);
            WebGLOptimizer.LogVerbose(
                $"[HooliganManager] フーリガン除去 (ID={hooliganId}, 残り{_activeHooliganIds.Count}体)");
        }

        // ============================================================
        // 公開API
        // ============================================================

        /// <summary>全フーリガンを強制除去する（ゲームリセット時等）</summary>
        public void ClearAll()
        {
            var hooligans = FindObjectsOfType<Hooligan>();
            foreach (var h in hooligans)
            {
                Destroy(h.gameObject);
            }
            _activeHooliganIds.Clear();
            WebGLOptimizer.LogVerbose("[HooliganManager] 全フーリガンをクリア");
        }
    }
}
