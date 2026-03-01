using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using ThemeParkGame.Core;

namespace ThemeParkGame.Staff
{
    /// <summary>
    /// スタッフスキルのデータ定義クラス。
    /// 各スキルの表示名、説明、解放条件、コストなどを保持する。
    /// </summary>
    [System.Serializable]
    public class StaffSkillData
    {
        /// <summary>スキルの一意識別子</summary>
        public StaffSkillId Id { get; set; }

        /// <summary>スキルの表示名（日本語）</summary>
        public string DisplayName { get; set; }

        /// <summary>スキルの説明文（日本語）</summary>
        public string Description { get; set; }

        /// <summary>このスキルを習得できるスタッフタイプ</summary>
        public StaffType RequiredStaffType { get; set; }

        /// <summary>スキル解放に必要なスタッフレベル（1～5）</summary>
        public int RequiredLevel { get; set; }

        /// <summary>ブランチインデックス（0=A分岐、1=B分岐）</summary>
        public int BranchIndex { get; set; }

        /// <summary>ブランチ内のティアインデックス（0, 1, 2）</summary>
        public int TierIndex { get; set; }

        /// <summary>スキル解放に必要な通貨コスト</summary>
        public float UnlockCost { get; set; }

        /// <summary>前提スキル（同ブランチの前ティア、ティア0の場合はnull）</summary>
        public StaffSkillId? PrerequisiteSkill { get; set; }
    }

    /// <summary>
    /// スタッフスキルツリーシステム。
    /// 各スタッフメンバーのスキル解放・管理を行うシングルトンMonoBehaviour。
    /// スタッフタイプごとにA分岐・B分岐の2系統、各3段階のスキルツリーを提供する。
    /// </summary>
    public class StaffSkillTreeSystem : MonoBehaviour
    {
        // ---------------------------------------------------------------------------
        // シングルトン
        // ---------------------------------------------------------------------------

        /// <summary>シングルトンインスタンス</summary>
        public static StaffSkillTreeSystem Instance { get; private set; }

        // ---------------------------------------------------------------------------
        // 内部データ
        // ---------------------------------------------------------------------------

        /// <summary>全スキル定義データベース</summary>
        private Dictionary<StaffSkillId, StaffSkillData> _skillDatabase;

        /// <summary>スタッフID → 解放済みスキルセット</summary>
        private Dictionary<int, HashSet<StaffSkillId>> _unlockedSkills;

        /// <summary>スキルごとのボーナス値テーブル</summary>
        private Dictionary<StaffSkillId, float> _bonusValues;

        /// <summary>スキルごとのボーナスタイプテーブル</summary>
        private Dictionary<StaffSkillId, string> _bonusTypes;

        // ---------------------------------------------------------------------------
        // ティアごとのコスト定数
        // ---------------------------------------------------------------------------

        private const float Tier0Cost = 500f;
        private const float Tier1Cost = 1200f;
        private const float Tier2Cost = 2500f;

        // ---------------------------------------------------------------------------
        // Unity ライフサイクル
        // ---------------------------------------------------------------------------

        /// <summary>
        /// シングルトンの初期化。重複インスタンスがあれば破棄する。
        /// </summary>
        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                WebGLOptimizer.LogVerbose("[StaffSkillTreeSystem] 重複インスタンスを破棄します。");
                Destroy(gameObject);
                return;
            }

            Instance = this;
            _unlockedSkills = new Dictionary<int, HashSet<StaffSkillId>>();
            _bonusValues = new Dictionary<StaffSkillId, float>();
            _bonusTypes = new Dictionary<StaffSkillId, string>();
            InitializeSkillDatabase();
            WebGLOptimizer.LogVerbose("[StaffSkillTreeSystem] 初期化完了。");
        }

        /// <summary>
        /// シングルトン参照のクリーンアップ。
        /// </summary>
        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
                WebGLOptimizer.LogVerbose("[StaffSkillTreeSystem] インスタンスを破棄しました。");
            }
        }

        // ---------------------------------------------------------------------------
        // スキルデータベース初期化
        // ---------------------------------------------------------------------------

        /// <summary>
        /// 全スタッフタイプの全スキルをデータベースに登録する。
        /// 各スタッフタイプにつきA分岐3スキル・B分岐3スキルの計6スキル、
        /// 8タイプ合計48スキルを登録する。
        /// </summary>
        private void InitializeSkillDatabase()
        {
            _skillDatabase = new Dictionary<StaffSkillId, StaffSkillData>();

            // ===================================================================
            // メカニック (Mechanic) スキル
            // ===================================================================

            // --- Branch A: 修理特化 ---
            RegisterSkill(StaffSkillId.Mechanic_BranchA_Tier0,
                "迅速修理", "修理速度が20%向上する",
                StaffType.Mechanic, 1, 0, 0, Tier0Cost, null,
                0.20f, "repair_speed");

            RegisterSkill(StaffSkillId.Mechanic_BranchA_Tier1,
                "精密修理", "修理品質が30%向上し、再故障率が低下する",
                StaffType.Mechanic, 2, 0, 1, Tier1Cost, StaffSkillId.Mechanic_BranchA_Tier0,
                0.30f, "repair_quality");

            RegisterSkill(StaffSkillId.Mechanic_BranchA_Tier2,
                "マスターメカニック", "修理速度がさらに40%向上し、部品コストが削減される",
                StaffType.Mechanic, 4, 0, 2, Tier2Cost, StaffSkillId.Mechanic_BranchA_Tier1,
                0.40f, "repair_speed");

            // --- Branch B: 予防保全特化 ---
            RegisterSkill(StaffSkillId.Mechanic_BranchB_Tier0,
                "定期点検", "アトラクションの故障率が15%低下する",
                StaffType.Mechanic, 1, 1, 0, Tier0Cost, null,
                0.15f, "breakdown_prevention");

            RegisterSkill(StaffSkillId.Mechanic_BranchB_Tier1,
                "予防保全", "点検範囲が拡大し、故障予測精度が25%向上する",
                StaffType.Mechanic, 3, 1, 1, Tier1Cost, StaffSkillId.Mechanic_BranchB_Tier0,
                0.25f, "breakdown_prevention");

            RegisterSkill(StaffSkillId.Mechanic_BranchB_Tier2,
                "設備マイスター", "担当エリア全体の設備寿命が35%延長される",
                StaffType.Mechanic, 5, 1, 2, Tier2Cost, StaffSkillId.Mechanic_BranchB_Tier1,
                0.35f, "equipment_lifespan");

            // ===================================================================
            // エンターテイナー (Entertainer) スキル
            // ===================================================================

            // --- Branch A: パフォーマンス特化 ---
            RegisterSkill(StaffSkillId.Entertainer_BranchA_Tier0,
                "華麗な演技", "ゲストの満足度ボーナスが15%増加する",
                StaffType.Entertainer, 1, 0, 0, Tier0Cost, null,
                0.15f, "guest_happiness");

            RegisterSkill(StaffSkillId.Entertainer_BranchA_Tier1,
                "群衆魅了", "パフォーマンスの効果範囲が30%拡大する",
                StaffType.Entertainer, 2, 0, 1, Tier1Cost, StaffSkillId.Entertainer_BranchA_Tier0,
                0.30f, "performance_range");

            RegisterSkill(StaffSkillId.Entertainer_BranchA_Tier2,
                "スターパフォーマー", "特別イベント発生確率が上昇し、パーク評価が大幅に向上する",
                StaffType.Entertainer, 4, 0, 2, Tier2Cost, StaffSkillId.Entertainer_BranchA_Tier1,
                0.40f, "park_rating_boost");

            // --- Branch B: インタラクション特化 ---
            RegisterSkill(StaffSkillId.Entertainer_BranchB_Tier0,
                "フレンドリー対応", "ゲストとの対話成功率が20%向上する",
                StaffType.Entertainer, 1, 1, 0, Tier0Cost, null,
                0.20f, "interaction_success");

            RegisterSkill(StaffSkillId.Entertainer_BranchB_Tier1,
                "ムードメーカー", "周囲のゲストの不満を25%軽減する",
                StaffType.Entertainer, 3, 1, 1, Tier1Cost, StaffSkillId.Entertainer_BranchB_Tier0,
                0.25f, "complaint_reduction");

            RegisterSkill(StaffSkillId.Entertainer_BranchB_Tier2,
                "カリスマエンターテイナー", "エリア全体のゲスト滞在時間が30%延長される",
                StaffType.Entertainer, 5, 1, 2, Tier2Cost, StaffSkillId.Entertainer_BranchB_Tier1,
                0.30f, "guest_retention");

            // ===================================================================
            // クリーナー (Cleaner) スキル
            // ===================================================================

            // --- Branch A: 清掃速度特化 ---
            RegisterSkill(StaffSkillId.Cleaner_BranchA_Tier0,
                "素早い清掃", "清掃速度が20%向上する",
                StaffType.Cleaner, 1, 0, 0, Tier0Cost, null,
                0.20f, "cleaning_speed");

            RegisterSkill(StaffSkillId.Cleaner_BranchA_Tier1,
                "効率清掃", "一度の清掃で広範囲をカバーできるようになる",
                StaffType.Cleaner, 2, 0, 1, Tier1Cost, StaffSkillId.Cleaner_BranchA_Tier0,
                0.30f, "cleaning_range");

            RegisterSkill(StaffSkillId.Cleaner_BranchA_Tier2,
                "清掃マスター", "清掃速度がさらに向上し、移動速度も25%増加する",
                StaffType.Cleaner, 4, 0, 2, Tier2Cost, StaffSkillId.Cleaner_BranchA_Tier1,
                0.25f, "movement_speed");

            // --- Branch B: 美化特化 ---
            RegisterSkill(StaffSkillId.Cleaner_BranchB_Tier0,
                "丁寧な仕上げ", "清掃品質が向上し、エリアの美観が15%改善される",
                StaffType.Cleaner, 1, 1, 0, Tier0Cost, null,
                0.15f, "area_beauty");

            RegisterSkill(StaffSkillId.Cleaner_BranchB_Tier1,
                "環境整備", "花壇やベンチの維持管理も行えるようになる",
                StaffType.Cleaner, 3, 1, 1, Tier1Cost, StaffSkillId.Cleaner_BranchB_Tier0,
                0.25f, "area_beauty");

            RegisterSkill(StaffSkillId.Cleaner_BranchB_Tier2,
                "美化のプロ", "担当エリアの汚れ発生率が35%低下する",
                StaffType.Cleaner, 5, 1, 2, Tier2Cost, StaffSkillId.Cleaner_BranchB_Tier1,
                0.35f, "litter_prevention");

            // ===================================================================
            // ガード (Guard) スキル
            // ===================================================================

            // --- Branch A: 巡回特化 ---
            RegisterSkill(StaffSkillId.Guard_BranchA_Tier0,
                "鋭い観察眼", "不審者の検知範囲が20%拡大する",
                StaffType.Guard, 1, 0, 0, Tier0Cost, null,
                0.20f, "detection_range");

            RegisterSkill(StaffSkillId.Guard_BranchA_Tier1,
                "機敏な巡回", "巡回速度が25%向上し、カバー範囲が拡大する",
                StaffType.Guard, 2, 0, 1, Tier1Cost, StaffSkillId.Guard_BranchA_Tier0,
                0.25f, "patrol_speed");

            RegisterSkill(StaffSkillId.Guard_BranchA_Tier2,
                "エリート警備員", "犯罪抑止効果が40%向上し、ゲストの安心感が大幅に増加する",
                StaffType.Guard, 4, 0, 2, Tier2Cost, StaffSkillId.Guard_BranchA_Tier1,
                0.40f, "crime_deterrence");

            // --- Branch B: 対応特化 ---
            RegisterSkill(StaffSkillId.Guard_BranchB_Tier0,
                "冷静な対処", "トラブル解決速度が15%向上する",
                StaffType.Guard, 1, 1, 0, Tier0Cost, null,
                0.15f, "incident_resolution");

            RegisterSkill(StaffSkillId.Guard_BranchB_Tier1,
                "交渉術", "ゲスト間のトラブル仲裁成功率が30%向上する",
                StaffType.Guard, 3, 1, 1, Tier1Cost, StaffSkillId.Guard_BranchB_Tier0,
                0.30f, "mediation_success");

            RegisterSkill(StaffSkillId.Guard_BranchB_Tier2,
                "治安維持の達人", "担当エリアのトラブル発生率が35%低下する",
                StaffType.Guard, 5, 1, 2, Tier2Cost, StaffSkillId.Guard_BranchB_Tier1,
                0.35f, "trouble_prevention");

            // ===================================================================
            // サイエンティスト (Scientist) スキル
            // ===================================================================

            // --- Branch A: 研究特化 ---
            RegisterSkill(StaffSkillId.Scientist_BranchA_Tier0,
                "基礎研究", "研究速度が20%向上する",
                StaffType.Scientist, 1, 0, 0, Tier0Cost, null,
                0.20f, "research_speed");

            RegisterSkill(StaffSkillId.Scientist_BranchA_Tier1,
                "応用研究", "研究成果の品質が25%向上する",
                StaffType.Scientist, 2, 0, 1, Tier1Cost, StaffSkillId.Scientist_BranchA_Tier0,
                0.25f, "research_quality");

            RegisterSkill(StaffSkillId.Scientist_BranchA_Tier2,
                "天才研究者", "画期的な発見の確率が上昇し、研究コストが30%削減される",
                StaffType.Scientist, 4, 0, 2, Tier2Cost, StaffSkillId.Scientist_BranchA_Tier1,
                0.30f, "research_cost_reduction");

            // --- Branch B: 開発特化 ---
            RegisterSkill(StaffSkillId.Scientist_BranchB_Tier0,
                "技術改良", "アトラクションの技術改善効率が15%向上する",
                StaffType.Scientist, 1, 1, 0, Tier0Cost, null,
                0.15f, "tech_improvement");

            RegisterSkill(StaffSkillId.Scientist_BranchB_Tier1,
                "革新設計", "新アトラクション開発時の性能ボーナスが20%増加する",
                StaffType.Scientist, 3, 1, 1, Tier1Cost, StaffSkillId.Scientist_BranchB_Tier0,
                0.20f, "development_bonus");

            RegisterSkill(StaffSkillId.Scientist_BranchB_Tier2,
                "イノベーター", "全研究プロジェクトの完了時間が35%短縮される",
                StaffType.Scientist, 5, 1, 2, Tier2Cost, StaffSkillId.Scientist_BranchB_Tier1,
                0.35f, "project_acceleration");

            // ===================================================================
            // ドクター (Doctor) スキル
            // ===================================================================

            // --- Branch A: 治療特化 ---
            RegisterSkill(StaffSkillId.Doctor_BranchA_Tier0,
                "応急処置", "治療速度が20%向上する",
                StaffType.Doctor, 1, 0, 0, Tier0Cost, null,
                0.20f, "treatment_speed");

            RegisterSkill(StaffSkillId.Doctor_BranchA_Tier1,
                "的確な診断", "診断精度が向上し、誤診率が25%低下する",
                StaffType.Doctor, 2, 0, 1, Tier1Cost, StaffSkillId.Doctor_BranchA_Tier0,
                0.25f, "diagnosis_accuracy");

            RegisterSkill(StaffSkillId.Doctor_BranchA_Tier2,
                "名医", "重症患者の治療成功率が40%向上し、回復時間が大幅に短縮される",
                StaffType.Doctor, 4, 0, 2, Tier2Cost, StaffSkillId.Doctor_BranchA_Tier1,
                0.40f, "treatment_speed");

            // --- Branch B: 予防医療特化 ---
            RegisterSkill(StaffSkillId.Doctor_BranchB_Tier0,
                "衛生管理", "担当エリアの病気発生率が15%低下する",
                StaffType.Doctor, 1, 1, 0, Tier0Cost, null,
                0.15f, "illness_prevention");

            RegisterSkill(StaffSkillId.Doctor_BranchB_Tier1,
                "健康指導", "ゲストの体調悪化を事前に察知し、予防措置を取れる",
                StaffType.Doctor, 3, 1, 1, Tier1Cost, StaffSkillId.Doctor_BranchB_Tier0,
                0.25f, "early_detection");

            RegisterSkill(StaffSkillId.Doctor_BranchB_Tier2,
                "公衆衛生の専門家", "パーク全体の衛生レベルが30%向上する",
                StaffType.Doctor, 5, 1, 2, Tier2Cost, StaffSkillId.Doctor_BranchB_Tier1,
                0.30f, "park_hygiene");

            // ===================================================================
            // ベンダー (Vendor) スキル
            // ===================================================================

            // --- Branch A: 販売特化 ---
            RegisterSkill(StaffSkillId.Vendor_BranchA_Tier0,
                "接客の基本", "商品の販売速度が20%向上する",
                StaffType.Vendor, 1, 0, 0, Tier0Cost, null,
                0.20f, "sales_speed");

            RegisterSkill(StaffSkillId.Vendor_BranchA_Tier1,
                "セールストーク", "客単価が25%向上する",
                StaffType.Vendor, 2, 0, 1, Tier1Cost, StaffSkillId.Vendor_BranchA_Tier0,
                0.25f, "revenue_boost");

            RegisterSkill(StaffSkillId.Vendor_BranchA_Tier2,
                "カリスマ販売員", "ショップの売上が35%増加し、リピーター率が向上する",
                StaffType.Vendor, 4, 0, 2, Tier2Cost, StaffSkillId.Vendor_BranchA_Tier1,
                0.35f, "revenue_boost");

            // --- Branch B: 在庫管理特化 ---
            RegisterSkill(StaffSkillId.Vendor_BranchB_Tier0,
                "在庫確認", "在庫切れの早期検知が可能になる",
                StaffType.Vendor, 1, 1, 0, Tier0Cost, null,
                0.15f, "stock_efficiency");

            RegisterSkill(StaffSkillId.Vendor_BranchB_Tier1,
                "効率的な補充", "商品の補充速度が25%向上し、ロスが削減される",
                StaffType.Vendor, 3, 1, 1, Tier1Cost, StaffSkillId.Vendor_BranchB_Tier0,
                0.25f, "restock_speed");

            RegisterSkill(StaffSkillId.Vendor_BranchB_Tier2,
                "物流マネージャー", "仕入れコストが30%削減され、品揃えが最適化される",
                StaffType.Vendor, 5, 1, 2, Tier2Cost, StaffSkillId.Vendor_BranchB_Tier1,
                0.30f, "supply_cost_reduction");

            // ===================================================================
            // ガーデナー (Gardener) スキル
            // ===================================================================

            // --- Branch A: 植栽特化 ---
            RegisterSkill(StaffSkillId.Gardener_BranchA_Tier0,
                "基本園芸", "植物の成長速度が20%向上する",
                StaffType.Gardener, 1, 0, 0, Tier0Cost, null,
                0.20f, "plant_growth");

            RegisterSkill(StaffSkillId.Gardener_BranchA_Tier1,
                "造園技術", "装飾植物の美観効果が30%向上する",
                StaffType.Gardener, 2, 0, 1, Tier1Cost, StaffSkillId.Gardener_BranchA_Tier0,
                0.30f, "decoration_beauty");

            RegisterSkill(StaffSkillId.Gardener_BranchA_Tier2,
                "マスターガーデナー", "特別な景観ボーナスが発生し、エリア評価が大幅に向上する",
                StaffType.Gardener, 4, 0, 2, Tier2Cost, StaffSkillId.Gardener_BranchA_Tier1,
                0.40f, "area_rating_boost");

            // --- Branch B: 維持管理特化 ---
            RegisterSkill(StaffSkillId.Gardener_BranchB_Tier0,
                "植物管理", "植物の枯れ率が15%低下する",
                StaffType.Gardener, 1, 1, 0, Tier0Cost, null,
                0.15f, "plant_durability");

            RegisterSkill(StaffSkillId.Gardener_BranchB_Tier1,
                "病害虫対策", "植物の病気・害虫発生率が25%低下する",
                StaffType.Gardener, 3, 1, 1, Tier1Cost, StaffSkillId.Gardener_BranchB_Tier0,
                0.25f, "pest_prevention");

            RegisterSkill(StaffSkillId.Gardener_BranchB_Tier2,
                "エコロジスト", "担当エリアの環境評価が35%向上し、維持コストが削減される",
                StaffType.Gardener, 5, 1, 2, Tier2Cost, StaffSkillId.Gardener_BranchB_Tier1,
                0.35f, "maintenance_cost_reduction");

            WebGLOptimizer.LogVerbose(
                $"[StaffSkillTreeSystem] スキルデータベース初期化完了: {_skillDatabase.Count}件登録");
        }

        /// <summary>
        /// スキルをデータベースに登録するヘルパーメソッド。
        /// ボーナス値とボーナスタイプも同時に登録する。
        /// </summary>
        /// <param name="id">スキルID</param>
        /// <param name="displayName">表示名（日本語）</param>
        /// <param name="description">説明文（日本語）</param>
        /// <param name="staffType">対象スタッフタイプ</param>
        /// <param name="requiredLevel">必要レベル</param>
        /// <param name="branchIndex">ブランチインデックス</param>
        /// <param name="tierIndex">ティアインデックス</param>
        /// <param name="cost">解放コスト</param>
        /// <param name="prerequisite">前提スキル</param>
        /// <param name="bonusValue">ボーナス値</param>
        /// <param name="bonusType">ボーナスタイプ文字列</param>
        private void RegisterSkill(
            StaffSkillId id,
            string displayName,
            string description,
            StaffType staffType,
            int requiredLevel,
            int branchIndex,
            int tierIndex,
            float cost,
            StaffSkillId? prerequisite,
            float bonusValue,
            string bonusType)
        {
            var data = new StaffSkillData
            {
                Id = id,
                DisplayName = displayName,
                Description = description,
                RequiredStaffType = staffType,
                RequiredLevel = requiredLevel,
                BranchIndex = branchIndex,
                TierIndex = tierIndex,
                UnlockCost = cost,
                PrerequisiteSkill = prerequisite
            };

            _skillDatabase[id] = data;
            _bonusValues[id] = bonusValue;
            _bonusTypes[id] = bonusType;
        }

        // ---------------------------------------------------------------------------
        // 公開API: スキル解放判定
        // ---------------------------------------------------------------------------

        /// <summary>
        /// 指定スタッフが指定スキルを解放可能かどうかを判定する。
        /// スタッフの存在、タイプ一致、レベル要件、前提スキル、未解放、資金の全条件を確認する。
        /// </summary>
        /// <param name="staffId">対象スタッフのID</param>
        /// <param name="skillId">解放判定するスキルID</param>
        /// <returns>解放可能であればtrue</returns>
        public bool CanUnlockSkill(int staffId, StaffSkillId skillId)
        {
            // スキルデータの存在確認
            if (!_skillDatabase.TryGetValue(skillId, out StaffSkillData skillData))
            {
                WebGLOptimizer.LogVerbose(
                    $"[StaffSkillTreeSystem] CanUnlockSkill: スキル {skillId} はデータベースに存在しません。");
                return false;
            }

            // スタッフの存在確認
            StaffMember staff = GetStaffMember(staffId);
            if (staff == null)
            {
                WebGLOptimizer.LogVerbose(
                    $"[StaffSkillTreeSystem] CanUnlockSkill: スタッフID {staffId} が見つかりません。");
                return false;
            }

            // スタッフタイプの一致確認
            if (staff.StaffType != skillData.RequiredStaffType)
            {
                WebGLOptimizer.LogVerbose(
                    $"[StaffSkillTreeSystem] CanUnlockSkill: スタッフタイプ不一致 " +
                    $"(必要: {skillData.RequiredStaffType}, 実際: {staff.StaffType})");
                return false;
            }

            // レベル要件の確認
            if (staff.SkillLevel < skillData.RequiredLevel)
            {
                WebGLOptimizer.LogVerbose(
                    $"[StaffSkillTreeSystem] CanUnlockSkill: レベル不足 " +
                    $"(必要: {skillData.RequiredLevel}, 現在: {staff.SkillLevel})");
                return false;
            }

            // 前提スキルの確認
            if (skillData.PrerequisiteSkill.HasValue)
            {
                if (!HasSkill(staffId, skillData.PrerequisiteSkill.Value))
                {
                    WebGLOptimizer.LogVerbose(
                        $"[StaffSkillTreeSystem] CanUnlockSkill: 前提スキル " +
                        $"{skillData.PrerequisiteSkill.Value} が未解放です。");
                    return false;
                }
            }

            // 既に解放済みでないことの確認
            if (HasSkill(staffId, skillId))
            {
                WebGLOptimizer.LogVerbose(
                    $"[StaffSkillTreeSystem] CanUnlockSkill: スキル {skillId} は既に解放済みです。");
                return false;
            }

            // 資金の確認
            if (GameManager.Instance == null || GameManager.Instance.EconomyManager == null)
            {
                WebGLOptimizer.LogVerbose(
                    "[StaffSkillTreeSystem] CanUnlockSkill: EconomyManagerが利用できません。");
                return false;
            }

            if (!GameManager.Instance.EconomyManager.CanAfford(skillData.UnlockCost))
            {
                WebGLOptimizer.LogVerbose(
                    $"[StaffSkillTreeSystem] CanUnlockSkill: 資金不足 (必要: ${skillData.UnlockCost})");
                return false;
            }

            return true;
        }

        // ---------------------------------------------------------------------------
        // 公開API: スキル解放実行
        // ---------------------------------------------------------------------------

        /// <summary>
        /// 指定スタッフの指定スキルを解放する。
        /// 全条件を検証後、コストを支払い、スキルを解放済みとして記録する。
        /// 解放成功時には通知を送信する。
        /// </summary>
        /// <param name="staffId">対象スタッフのID</param>
        /// <param name="skillId">解放するスキルID</param>
        /// <returns>解放に成功した場合true</returns>
        public bool UnlockSkill(int staffId, StaffSkillId skillId)
        {
            if (!CanUnlockSkill(staffId, skillId))
            {
                WebGLOptimizer.LogVerbose(
                    $"[StaffSkillTreeSystem] UnlockSkill: スキル {skillId} の解放条件を満たしていません。");
                return false;
            }

            StaffSkillData skillData = _skillDatabase[skillId];
            StaffMember staff = GetStaffMember(staffId);

            // コストの支払い
            GameManager.Instance.EconomyManager.PayExpense(
                skillData.UnlockCost, Economy.ExpenseCategory.Other);

            // スキルの解放記録
            if (!_unlockedSkills.ContainsKey(staffId))
            {
                _unlockedSkills[staffId] = new HashSet<StaffSkillId>();
            }
            _unlockedSkills[staffId].Add(skillId);

            WebGLOptimizer.LogVerbose(
                $"[StaffSkillTreeSystem] UnlockSkill: スタッフ '{staff.Name}' (ID:{staffId}) が " +
                $"スキル '{skillData.DisplayName}' を解放しました。(コスト: ${skillData.UnlockCost})");

            // 通知の送信
            NotificationSystem.Instance?.Notify(
                $"{staff.Name}が「{skillData.DisplayName}」を習得しました！",
                NotifLevel.Info);

            // イベント発火
            GameEvents.OnStaffSkillUnlocked?.Invoke(staffId, skillId);

            return true;
        }

        // ---------------------------------------------------------------------------
        // 公開API: スキル状態照会
        // ---------------------------------------------------------------------------

        /// <summary>
        /// 指定スタッフが指定スキルを解放済みかどうかを確認する。
        /// </summary>
        /// <param name="staffId">対象スタッフのID</param>
        /// <param name="skillId">確認するスキルID</param>
        /// <returns>解放済みであればtrue</returns>
        public bool HasSkill(int staffId, StaffSkillId skillId)
        {
            if (_unlockedSkills.TryGetValue(staffId, out HashSet<StaffSkillId> skills))
            {
                return skills.Contains(skillId);
            }
            return false;
        }

        /// <summary>
        /// 指定スタッフが解放済みの全スキルIDリストを取得する。
        /// </summary>
        /// <param name="staffId">対象スタッフのID</param>
        /// <returns>解放済みスキルIDのリスト（未登録の場合は空リスト）</returns>
        public List<StaffSkillId> GetUnlockedSkills(int staffId)
        {
            if (_unlockedSkills.TryGetValue(staffId, out HashSet<StaffSkillId> skills))
            {
                return skills.ToList();
            }
            return new List<StaffSkillId>();
        }

        /// <summary>
        /// 指定スタッフが現在解放可能な全スキルのデータリストを取得する。
        /// タイプ一致・レベル要件・前提スキル・未解放・資金の全条件を満たすスキルのみ返す。
        /// </summary>
        /// <param name="staffId">対象スタッフのID</param>
        /// <returns>解放可能なスキルデータのリスト</returns>
        public List<StaffSkillData> GetAvailableSkills(int staffId)
        {
            List<StaffSkillData> available = new List<StaffSkillData>();

            StaffMember staff = GetStaffMember(staffId);
            if (staff == null)
            {
                WebGLOptimizer.LogVerbose(
                    $"[StaffSkillTreeSystem] GetAvailableSkills: スタッフID {staffId} が見つかりません。");
                return available;
            }

            foreach (var kvp in _skillDatabase)
            {
                StaffSkillData skillData = kvp.Value;

                // スタッフタイプが一致しないスキルはスキップ
                if (skillData.RequiredStaffType != staff.StaffType)
                {
                    continue;
                }

                // 既に解放済みのスキルはスキップ
                if (HasSkill(staffId, skillData.Id))
                {
                    continue;
                }

                // レベル要件を満たさないスキルはスキップ
                if (staff.SkillLevel < skillData.RequiredLevel)
                {
                    continue;
                }

                // 前提スキルが未解放のスキルはスキップ
                if (skillData.PrerequisiteSkill.HasValue &&
                    !HasSkill(staffId, skillData.PrerequisiteSkill.Value))
                {
                    continue;
                }

                available.Add(skillData);
            }

            WebGLOptimizer.LogVerbose(
                $"[StaffSkillTreeSystem] GetAvailableSkills: スタッフID {staffId} に " +
                $"{available.Count}件の解放可能スキルがあります。");

            return available;
        }

        // ---------------------------------------------------------------------------
        // 公開API: ボーナス値取得
        // ---------------------------------------------------------------------------

        /// <summary>
        /// 指定スタッフの指定スキルによるボーナス値を取得する。
        /// スキルが解放済みでない場合は0を返す。
        /// </summary>
        /// <param name="staffId">対象スタッフのID</param>
        /// <param name="skillId">ボーナス値を取得するスキルID</param>
        /// <returns>ボーナス値（未解放の場合は0）</returns>
        public float GetSkillBonus(int staffId, StaffSkillId skillId)
        {
            if (!HasSkill(staffId, skillId))
            {
                return 0f;
            }

            if (_bonusValues.TryGetValue(skillId, out float bonus))
            {
                return bonus;
            }

            WebGLOptimizer.LogVerbose(
                $"[StaffSkillTreeSystem] GetSkillBonus: スキル {skillId} のボーナス値が未定義です。");
            return 0f;
        }

        /// <summary>
        /// 指定スタッフの、指定ボーナスタイプに該当する全解放済みスキルのボーナス値を合算して返す。
        /// 例えば "repair_speed" を指定すると、修理速度に関連する全スキルのボーナスが合算される。
        /// </summary>
        /// <param name="staffId">対象スタッフのID</param>
        /// <param name="bonusType">ボーナスタイプ文字列（例: "repair_speed", "cleaning_speed"）</param>
        /// <returns>合算されたボーナス値</returns>
        public float GetCombinedBonus(int staffId, string bonusType)
        {
            if (!_unlockedSkills.TryGetValue(staffId, out HashSet<StaffSkillId> unlockedSet))
            {
                return 0f;
            }

            float combined = 0f;

            foreach (StaffSkillId skillId in unlockedSet)
            {
                if (_bonusTypes.TryGetValue(skillId, out string type) && type == bonusType)
                {
                    if (_bonusValues.TryGetValue(skillId, out float bonus))
                    {
                        combined += bonus;
                    }
                }
            }

            WebGLOptimizer.LogVerbose(
                $"[StaffSkillTreeSystem] GetCombinedBonus: スタッフID {staffId}, " +
                $"タイプ '{bonusType}' = {combined:F2}");

            return combined;
        }

        // ---------------------------------------------------------------------------
        // 公開API: スキルデータ照会
        // ---------------------------------------------------------------------------

        /// <summary>
        /// 指定スキルIDのスキルデータを取得する。
        /// </summary>
        /// <param name="skillId">取得するスキルID</param>
        /// <returns>スキルデータ（存在しない場合はnull）</returns>
        public StaffSkillData GetSkillData(StaffSkillId skillId)
        {
            if (_skillDatabase.TryGetValue(skillId, out StaffSkillData data))
            {
                return data;
            }
            return null;
        }

        /// <summary>
        /// 指定スタッフタイプの全スキルデータをリストで取得する。
        /// ブランチ・ティア順にソートされて返される。
        /// </summary>
        /// <param name="staffType">対象スタッフタイプ</param>
        /// <returns>該当スタッフタイプの全スキルデータリスト</returns>
        public List<StaffSkillData> GetSkillsForStaffType(StaffType staffType)
        {
            return _skillDatabase.Values
                .Where(s => s.RequiredStaffType == staffType)
                .OrderBy(s => s.BranchIndex)
                .ThenBy(s => s.TierIndex)
                .ToList();
        }

        /// <summary>
        /// 指定スキルIDのボーナスタイプ文字列を取得する。
        /// </summary>
        /// <param name="skillId">対象スキルID</param>
        /// <returns>ボーナスタイプ文字列（未定義の場合は空文字列）</returns>
        public string GetBonusType(StaffSkillId skillId)
        {
            if (_bonusTypes.TryGetValue(skillId, out string type))
            {
                return type;
            }
            return string.Empty;
        }

        // ---------------------------------------------------------------------------
        // 公開API: スタッフスキル状態のリセット
        // ---------------------------------------------------------------------------

        /// <summary>
        /// 指定スタッフの全解放済みスキルをリセットする。
        /// 主にスタッフの解雇や再配置時に使用する。
        /// </summary>
        /// <param name="staffId">対象スタッフのID</param>
        public void ResetStaffSkills(int staffId)
        {
            if (_unlockedSkills.ContainsKey(staffId))
            {
                int count = _unlockedSkills[staffId].Count;
                _unlockedSkills.Remove(staffId);
                WebGLOptimizer.LogVerbose(
                    $"[StaffSkillTreeSystem] ResetStaffSkills: スタッフID {staffId} の " +
                    $"{count}件のスキルをリセットしました。");
            }
        }

        // ---------------------------------------------------------------------------
        // 内部ヘルパー
        // ---------------------------------------------------------------------------

        /// <summary>
        /// GameManagerのStaffManagerを経由してスタッフメンバーを取得する。
        /// </summary>
        /// <param name="staffId">取得するスタッフのID</param>
        /// <returns>該当するStaffMember、存在しない場合はnull</returns>
        private StaffMember GetStaffMember(int staffId)
        {
            if (GameManager.Instance == null)
            {
                WebGLOptimizer.LogVerbose(
                    "[StaffSkillTreeSystem] GetStaffMember: GameManagerが利用できません。");
                return null;
            }

            if (GameManager.Instance.StaffManager == null)
            {
                WebGLOptimizer.LogVerbose(
                    "[StaffSkillTreeSystem] GetStaffMember: StaffManagerが利用できません。");
                return null;
            }

            return GameManager.Instance.StaffManager.GetStaffById(staffId);
        }
    }
}
