// ============================================================
// ThemeParkGame - CoasterDesignSystem
// カスタムコースター設計ツール（Phase 7: H4）
// レール形状・速度・高低差をカスタマイズしてオリジナルコースターを設計
// ============================================================

using System;
using System.Collections.Generic;
using UnityEngine;
using ThemeParkGame.Core;
using ThemeParkGame.Economy;

namespace ThemeParkGame.Attraction
{
    /// <summary>レール形状エレメントの種類</summary>
    public enum RailElementType
    {
        Straight,       // 直線
        GentleCurve,    // 緩やかなカーブ
        SharpCurve,     // 急カーブ
        SmallHill,      // 小さな丘（3m）
        MediumHill,     // 中程度の丘（8m）
        LargeHill,      // 大きな丘（15m）
        Loop,           // ループ（1回転）
        Corkscrew,      // コークスクリュー（螺旋回転）
        Helix,          // ヘリックス（大螺旋下降）
        Brake,          // ブレーキセクション
        LaunchBoost,    // 加速ブースト区間
        Drop,           // 急降下（20m以上）
        Tunnel,         // トンネル区間
        WaterSplash     // ウォータースプラッシュ
    }

    /// <summary>コースター速度クラス</summary>
    public enum CoasterSpeedClass
    {
        Slow,       // 低速 (30km/h) — 子供向け
        Medium,     // 中速 (60km/h) — ファミリー向け
        Fast,       // 高速 (90km/h) — スリル系
        Extreme     // 超高速 (120km/h) — 絶叫系
    }

    /// <summary>コースター高低差クラス</summary>
    public enum CoasterHeightClass
    {
        Low,        // 低い (5m) — 子供向け
        Medium,     // 中程度 (15m) — ファミリー向け
        High,       // 高い (30m) — スリル系
        Extreme     // 超高い (50m) — 絶叫系
    }

    /// <summary>
    /// レール要素1つ分のデータ。
    /// プレイヤーがドラッグ＆ドロップで配置する。
    /// </summary>
    [Serializable]
    public class RailElement
    {
        public RailElementType Type;
        public int OrderIndex;  // レイアウト内の順番

        /// <summary>興奮度への寄与</summary>
        public float ExcitementContribution => GetExcitementContribution();

        /// <summary>嘔吐度への寄与</summary>
        public float NauseaContribution => GetNauseaContribution();

        /// <summary>建設コストへの寄与</summary>
        public int CostContribution => GetCostContribution();

        private float GetExcitementContribution()
        {
            return Type switch
            {
                RailElementType.Straight => 0.1f,
                RailElementType.GentleCurve => 0.2f,
                RailElementType.SharpCurve => 0.4f,
                RailElementType.SmallHill => 0.3f,
                RailElementType.MediumHill => 0.6f,
                RailElementType.LargeHill => 1.0f,
                RailElementType.Loop => 1.5f,
                RailElementType.Corkscrew => 1.8f,
                RailElementType.Helix => 1.2f,
                RailElementType.Brake => -0.1f,
                RailElementType.LaunchBoost => 1.0f,
                RailElementType.Drop => 2.0f,
                RailElementType.Tunnel => 0.4f,
                RailElementType.WaterSplash => 0.8f,
                _ => 0f
            };
        }

        private float GetNauseaContribution()
        {
            return Type switch
            {
                RailElementType.Straight => 0f,
                RailElementType.GentleCurve => 0.01f,
                RailElementType.SharpCurve => 0.03f,
                RailElementType.SmallHill => 0.01f,
                RailElementType.MediumHill => 0.02f,
                RailElementType.LargeHill => 0.04f,
                RailElementType.Loop => 0.08f,
                RailElementType.Corkscrew => 0.10f,
                RailElementType.Helix => 0.06f,
                RailElementType.Brake => -0.02f,
                RailElementType.LaunchBoost => 0.03f,
                RailElementType.Drop => 0.07f,
                RailElementType.Tunnel => 0f,
                RailElementType.WaterSplash => 0.02f,
                _ => 0f
            };
        }

        private int GetCostContribution()
        {
            return Type switch
            {
                RailElementType.Straight => 100,
                RailElementType.GentleCurve => 150,
                RailElementType.SharpCurve => 200,
                RailElementType.SmallHill => 300,
                RailElementType.MediumHill => 600,
                RailElementType.LargeHill => 1200,
                RailElementType.Loop => 2000,
                RailElementType.Corkscrew => 2500,
                RailElementType.Helix => 1800,
                RailElementType.Brake => 400,
                RailElementType.LaunchBoost => 1500,
                RailElementType.Drop => 1800,
                RailElementType.Tunnel => 800,
                RailElementType.WaterSplash => 1000,
                _ => 0
            };
        }
    }

    /// <summary>
    /// カスタムコースターの設計データ。
    /// プレイヤーがデザインしたレイアウト全体を保持する。
    /// </summary>
    [Serializable]
    public class CoasterDesign
    {
        /// <summary>コースター名</summary>
        public string Name = "カスタムコースター";

        /// <summary>レール要素のリスト（順番通り）</summary>
        public List<RailElement> RailElements = new List<RailElement>();

        /// <summary>速度クラス</summary>
        public CoasterSpeedClass SpeedClass = CoasterSpeedClass.Medium;

        /// <summary>高低差クラス</summary>
        public CoasterHeightClass HeightClass = CoasterHeightClass.Medium;

        /// <summary>車両定員</summary>
        public int TrainCapacity = 12;

        /// <summary>車両数</summary>
        public int TrainCount = 1;

        /// <summary>テーマゾーン</summary>
        public ThemeZone TargetZone = ThemeZone.LostKingdom;

        // ---- 計算済みスペック ----

        /// <summary>総興奮度（0-10スケール）</summary>
        public float CalculatedExcitement { get; private set; }

        /// <summary>嘔吐率（0-1）</summary>
        public float CalculatedNausea { get; private set; }

        /// <summary>建設費用</summary>
        public int CalculatedBuildCost { get; private set; }

        /// <summary>月間維持費</summary>
        public int CalculatedMaintenanceCost { get; private set; }

        /// <summary>推定乗車時間（秒）</summary>
        public float CalculatedRideDuration { get; private set; }

        /// <summary>推奨チケット価格</summary>
        public int CalculatedTicketPrice { get; private set; }

        /// <summary>設計品質スコア（0-100、バランスの良さ）</summary>
        public float DesignQualityScore { get; private set; }

        /// <summary>設計の評価コメント</summary>
        public string DesignComment { get; private set; }

        /// <summary>
        /// 全スペックを再計算する。レール変更時に呼ぶ。
        /// </summary>
        public void Recalculate()
        {
            CalculateExcitement();
            CalculateNausea();
            CalculateCosts();
            CalculateRideDuration();
            CalculateDesignQuality();
        }

        private void CalculateExcitement()
        {
            float baseExcitement = 0f;
            foreach (var elem in RailElements)
            {
                baseExcitement += elem.ExcitementContribution;
            }

            // 速度クラスによる倍率
            float speedMultiplier = SpeedClass switch
            {
                CoasterSpeedClass.Slow => 0.6f,
                CoasterSpeedClass.Medium => 1.0f,
                CoasterSpeedClass.Fast => 1.4f,
                CoasterSpeedClass.Extreme => 1.8f,
                _ => 1.0f
            };

            // 高低差クラスによる倍率
            float heightMultiplier = HeightClass switch
            {
                CoasterHeightClass.Low => 0.7f,
                CoasterHeightClass.Medium => 1.0f,
                CoasterHeightClass.High => 1.3f,
                CoasterHeightClass.Extreme => 1.6f,
                _ => 1.0f
            };

            CalculatedExcitement = Mathf.Clamp(
                baseExcitement * speedMultiplier * heightMultiplier * 0.5f, 0f, 10f);
        }

        private void CalculateNausea()
        {
            float baseNausea = 0f;
            foreach (var elem in RailElements)
            {
                baseNausea += elem.NauseaContribution;
            }

            // 速度クラスで嘔吐率増加
            float speedFactor = SpeedClass switch
            {
                CoasterSpeedClass.Slow => 0.5f,
                CoasterSpeedClass.Medium => 1.0f,
                CoasterSpeedClass.Fast => 1.5f,
                CoasterSpeedClass.Extreme => 2.0f,
                _ => 1.0f
            };

            CalculatedNausea = Mathf.Clamp01(baseNausea * speedFactor);
        }

        private void CalculateCosts()
        {
            int baseCost = 0;
            foreach (var elem in RailElements)
            {
                baseCost += elem.CostContribution;
            }

            // 速度クラスによるコスト増
            float speedCostMultiplier = SpeedClass switch
            {
                CoasterSpeedClass.Slow => 0.8f,
                CoasterSpeedClass.Medium => 1.0f,
                CoasterSpeedClass.Fast => 1.5f,
                CoasterSpeedClass.Extreme => 2.2f,
                _ => 1.0f
            };

            // 高低差によるコスト増
            float heightCostMultiplier = HeightClass switch
            {
                CoasterHeightClass.Low => 0.8f,
                CoasterHeightClass.Medium => 1.0f,
                CoasterHeightClass.High => 1.6f,
                CoasterHeightClass.Extreme => 2.5f,
                _ => 1.0f
            };

            // 車両数によるコスト
            float trainCostMultiplier = 1.0f + (TrainCount - 1) * 0.3f;

            CalculatedBuildCost = Mathf.RoundToInt(
                baseCost * speedCostMultiplier * heightCostMultiplier * trainCostMultiplier);

            // 最低建設費
            CalculatedBuildCost = Mathf.Max(2000, CalculatedBuildCost);

            // 月間維持費は建設費の2-3%
            CalculatedMaintenanceCost = Mathf.Max(100, Mathf.RoundToInt(CalculatedBuildCost * 0.025f));

            // 推奨チケット価格は興奮度ベース
            CalculatedTicketPrice = Mathf.Max(100, Mathf.RoundToInt(CalculatedExcitement * 100f));
        }

        private void CalculateRideDuration()
        {
            // 基本: レール要素数 × 速度クラス係数
            float baseTime = RailElements.Count * 3f; // 1要素3秒

            float speedTimeFactor = SpeedClass switch
            {
                CoasterSpeedClass.Slow => 1.5f,
                CoasterSpeedClass.Medium => 1.0f,
                CoasterSpeedClass.Fast => 0.8f,
                CoasterSpeedClass.Extreme => 0.6f,
                _ => 1.0f
            };

            CalculatedRideDuration = Mathf.Max(30f, baseTime * speedTimeFactor);
        }

        private void CalculateDesignQuality()
        {
            if (RailElements.Count == 0)
            {
                DesignQualityScore = 0f;
                DesignComment = "レール要素を追加してください。";
                return;
            }

            float score = 50f; // 基本スコア

            // バラエティボーナス: 異なる要素タイプが多いほど高評価
            var uniqueTypes = new HashSet<RailElementType>();
            foreach (var elem in RailElements)
                uniqueTypes.Add(elem.Type);
            float varietyBonus = Mathf.Min(20f, uniqueTypes.Count * 3f);
            score += varietyBonus;

            // レール長さボーナス: 5-15要素が理想
            int elemCount = RailElements.Count;
            if (elemCount >= 5 && elemCount <= 15)
                score += 10f;
            else if (elemCount < 3)
                score -= 15f;
            else if (elemCount > 20)
                score -= 10f;

            // 興奮度と嘔吐度のバランス
            float exciteNauseaRatio = CalculatedNausea > 0.01f
                ? CalculatedExcitement / (CalculatedNausea * 100f)
                : CalculatedExcitement;
            if (exciteNauseaRatio >= 1.5f && exciteNauseaRatio <= 5f)
                score += 15f; // 良いバランス
            else if (exciteNauseaRatio < 0.5f)
                score -= 10f; // 嘔吐度が高すぎ

            // ブレーキの有無（安全性ボーナス）
            bool hasBrake = false;
            foreach (var elem in RailElements)
            {
                if (elem.Type == RailElementType.Brake) { hasBrake = true; break; }
            }
            if (hasBrake) score += 5f;

            DesignQualityScore = Mathf.Clamp(score, 0f, 100f);

            // コメント生成
            if (DesignQualityScore >= 85f)
                DesignComment = "傑作！来場者を魅了する最高のコースターです！";
            else if (DesignQualityScore >= 70f)
                DesignComment = "素晴らしい設計です。バランスの良いコースターになるでしょう。";
            else if (DesignQualityScore >= 50f)
                DesignComment = "良い設計ですが、もう少しバラエティを加えると改善できます。";
            else if (DesignQualityScore >= 30f)
                DesignComment = "もっとレール要素を追加して、スリルと安全性のバランスを取りましょう。";
            else
                DesignComment = "設計を見直してください。レール要素が不足しています。";
        }
    }

    /// <summary>
    /// カスタムコースター設計システム。
    /// プレイヤーがレール形状・速度・高低差を自由にカスタマイズし、
    /// オリジナルのコースターを設計・建設できる。
    ///
    /// 【ゲームデザイン: Phase 7 最終フェーズ】
    /// - レール要素をドラッグ＆ドロップで配置
    /// - 速度/高低差のクラスを選択
    /// - リアルタイムでスペック（興奮度・嘔吐率・コスト）が計算される
    /// - 設計品質スコアで「傑作」から「要改善」まで評価
    /// - 設計確定でAttractionDataを自動生成し、パークに建設可能
    /// </summary>
    public class CoasterDesignSystem : MonoBehaviour
    {
        public static CoasterDesignSystem Instance { get; private set; }

        /// <summary>現在編集中のデザイン</summary>
        public CoasterDesign CurrentDesign { get; private set; }

        /// <summary>保存済みデザインの一覧</summary>
        private readonly List<CoasterDesign> _savedDesigns = new List<CoasterDesign>();
        public IReadOnlyList<CoasterDesign> SavedDesigns => _savedDesigns;

        /// <summary>建設済みカスタムコースターの数</summary>
        public int BuiltCoasterCount { get; private set; }

        /// <summary>デザイン変更イベント</summary>
        public event Action<CoasterDesign> OnDesignChanged;

        /// <summary>コースター建設完了イベント</summary>
        public event Action<string> OnCoasterBuilt;

        /// <summary>利用可能なレール要素タイプ（研究で解放される）</summary>
        private readonly HashSet<RailElementType> _unlockedElements = new HashSet<RailElementType>();

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            InitializeUnlockedElements();
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        /// <summary>初期解放レール要素を設定する</summary>
        private void InitializeUnlockedElements()
        {
            // 基本要素は最初から利用可能
            _unlockedElements.Add(RailElementType.Straight);
            _unlockedElements.Add(RailElementType.GentleCurve);
            _unlockedElements.Add(RailElementType.SharpCurve);
            _unlockedElements.Add(RailElementType.SmallHill);
            _unlockedElements.Add(RailElementType.MediumHill);
            _unlockedElements.Add(RailElementType.Brake);

            // 上級要素は研究で解放
            // Loop, Corkscrew, Helix, LaunchBoost, Drop, LargeHill, Tunnel, WaterSplash
            // → UnlockElement()で個別に解放
        }

        /// <summary>レール要素を研究解放する</summary>
        public void UnlockElement(RailElementType type)
        {
            if (_unlockedElements.Add(type))
            {
                string name = GetElementDisplayName(type);
                NotificationSystem.Instance?.Notify(
                    $"新レール要素「{name}」が使用可能になりました！", NotifLevel.Success);
                WebGLOptimizer.LogVerbose($"[CoasterDesign] レール要素解放: {type}");
            }
        }

        /// <summary>指定要素が解放済みか</summary>
        public bool IsElementUnlocked(RailElementType type)
        {
            return _unlockedElements.Contains(type);
        }

        /// <summary>解放済み要素一覧</summary>
        public IReadOnlyCollection<RailElementType> GetUnlockedElements()
        {
            return _unlockedElements;
        }

        // ================================================================
        // デザイン編集
        // ================================================================

        /// <summary>新しいデザインを開始する</summary>
        public void StartNewDesign()
        {
            CurrentDesign = new CoasterDesign();
            CurrentDesign.Recalculate();
            OnDesignChanged?.Invoke(CurrentDesign);
            WebGLOptimizer.LogVerbose("[CoasterDesign] 新規デザイン開始");
        }

        /// <summary>レール要素を末尾に追加する</summary>
        public bool AddRailElement(RailElementType type)
        {
            if (CurrentDesign == null) return false;
            if (!_unlockedElements.Contains(type))
            {
                NotificationSystem.Instance?.Notify(
                    $"「{GetElementDisplayName(type)}」はまだ研究されていません。", NotifLevel.Warning);
                return false;
            }

            // 最大20要素まで
            if (CurrentDesign.RailElements.Count >= 20)
            {
                NotificationSystem.Instance?.Notify(
                    "レール要素の上限（20）に達しています。", NotifLevel.Warning);
                return false;
            }

            var element = new RailElement
            {
                Type = type,
                OrderIndex = CurrentDesign.RailElements.Count
            };
            CurrentDesign.RailElements.Add(element);
            CurrentDesign.Recalculate();
            OnDesignChanged?.Invoke(CurrentDesign);
            return true;
        }

        /// <summary>指定インデックスのレール要素を削除する</summary>
        public bool RemoveRailElement(int index)
        {
            if (CurrentDesign == null) return false;
            if (index < 0 || index >= CurrentDesign.RailElements.Count) return false;

            CurrentDesign.RailElements.RemoveAt(index);

            // インデックスを再採番
            for (int i = 0; i < CurrentDesign.RailElements.Count; i++)
                CurrentDesign.RailElements[i].OrderIndex = i;

            CurrentDesign.Recalculate();
            OnDesignChanged?.Invoke(CurrentDesign);
            return true;
        }

        /// <summary>速度クラスを変更する</summary>
        public void SetSpeedClass(CoasterSpeedClass speedClass)
        {
            if (CurrentDesign == null) return;
            CurrentDesign.SpeedClass = speedClass;
            CurrentDesign.Recalculate();
            OnDesignChanged?.Invoke(CurrentDesign);
        }

        /// <summary>高低差クラスを変更する</summary>
        public void SetHeightClass(CoasterHeightClass heightClass)
        {
            if (CurrentDesign == null) return;
            CurrentDesign.HeightClass = heightClass;
            CurrentDesign.Recalculate();
            OnDesignChanged?.Invoke(CurrentDesign);
        }

        /// <summary>コースター名を変更する</summary>
        public void SetCoasterName(string name)
        {
            if (CurrentDesign == null) return;
            CurrentDesign.Name = string.IsNullOrWhiteSpace(name) ? "カスタムコースター" : name;
        }

        /// <summary>車両数を変更する（1-3）</summary>
        public void SetTrainCount(int count)
        {
            if (CurrentDesign == null) return;
            CurrentDesign.TrainCount = Mathf.Clamp(count, 1, 3);
            CurrentDesign.Recalculate();
            OnDesignChanged?.Invoke(CurrentDesign);
        }

        // ================================================================
        // デザイン確定・建設
        // ================================================================

        /// <summary>
        /// 現在のデザインを確定し、カスタムコースターを建設する。
        /// EconomyManagerから建設費を支払い、AttractionDataを自動生成する。
        /// </summary>
        public bool FinalizeAndBuild()
        {
            if (CurrentDesign == null || CurrentDesign.RailElements.Count < 3)
            {
                NotificationSystem.Instance?.Notify(
                    "レール要素が3つ以上必要です。", NotifLevel.Warning);
                return false;
            }

            var em = GameManager.Instance?.EconomyManager;
            if (em == null) return false;

            if (!em.CanAfford(CurrentDesign.CalculatedBuildCost))
            {
                NotificationSystem.Instance?.Notify(
                    $"建設費${CurrentDesign.CalculatedBuildCost:N0}が不足しています。", NotifLevel.Warning);
                return false;
            }

            // 建設費支払い
            em.PayExpense(CurrentDesign.CalculatedBuildCost, ExpenseCategory.Construction);

            // AttractionDataを動的生成
            var attrData = ScriptableObject.CreateInstance<AttractionData>();
            attrData.AttractionId = $"CUSTOM_COASTER_{BuiltCoasterCount + 1:D3}";
            attrData.NameJP = CurrentDesign.Name;
            attrData.NameEN = CurrentDesign.Name;
            attrData.Description = GenerateDesignDescription();
            attrData.Category = AttractionCategory.GForce;
            attrData.PrimaryThemeZone = CurrentDesign.TargetZone;
            attrData.ExcitementRating = CurrentDesign.CalculatedExcitement;
            attrData.NauseaFactor = CurrentDesign.CalculatedNausea;
            attrData.Capacity = CurrentDesign.TrainCapacity * CurrentDesign.TrainCount;
            attrData.RideDuration = CurrentDesign.CalculatedRideDuration;
            attrData.BuildCost = CurrentDesign.CalculatedBuildCost;
            attrData.MaintenanceCost = CurrentDesign.CalculatedMaintenanceCost;
            attrData.SuggestedTicketPrice = CurrentDesign.CalculatedTicketPrice;
            attrData.Size = CalculateGridSize();
            attrData.BaseBreakdownRate = 0.02f + CurrentDesign.CalculatedNausea * 0.02f;
            attrData.Durability = 1.0f;
            attrData.ConditionLossPerCycle = 0.15f;

            // AttractionManagerに登録
            var am = GameManager.Instance?.AttractionManager;
            if (am != null)
            {
                am.RegisterCustomAttraction(attrData);
            }

            BuiltCoasterCount++;

            // デザインを保存
            _savedDesigns.Add(CurrentDesign);

            // 通知
            string qualityLabel = CurrentDesign.DesignQualityScore >= 80f ? "傑作" :
                                  CurrentDesign.DesignQualityScore >= 60f ? "優良" :
                                  CurrentDesign.DesignQualityScore >= 40f ? "普通" : "要改善";
            NotificationSystem.Instance?.Notify(
                $"カスタムコースター「{CurrentDesign.Name}」建設完了！（品質: {qualityLabel}）",
                NotifLevel.Success);

            OnCoasterBuilt?.Invoke(CurrentDesign.Name);

            WebGLOptimizer.LogVerbose(
                $"[CoasterDesign] 建設完了: {CurrentDesign.Name}, " +
                $"興奮度={CurrentDesign.CalculatedExcitement:F1}, " +
                $"嘔吐率={CurrentDesign.CalculatedNausea:F2}, " +
                $"費用=${CurrentDesign.CalculatedBuildCost:N0}, " +
                $"品質={CurrentDesign.DesignQualityScore:F0}");

            // 新規デザインを開始
            StartNewDesign();
            return true;
        }

        private string GenerateDesignDescription()
        {
            int loops = 0, drops = 0, corkscrews = 0;
            foreach (var elem in CurrentDesign.RailElements)
            {
                if (elem.Type == RailElementType.Loop) loops++;
                else if (elem.Type == RailElementType.Drop) drops++;
                else if (elem.Type == RailElementType.Corkscrew) corkscrews++;
            }

            string speedText = CurrentDesign.SpeedClass switch
            {
                CoasterSpeedClass.Slow => "ゆったりとした",
                CoasterSpeedClass.Medium => "爽快な",
                CoasterSpeedClass.Fast => "スリル満点の",
                CoasterSpeedClass.Extreme => "絶叫必至の",
                _ => ""
            };

            string features = "";
            if (loops > 0) features += $"ループ{loops}回 ";
            if (corkscrews > 0) features += $"コークスクリュー{corkscrews}回 ";
            if (drops > 0) features += $"急降下{drops}回 ";

            return $"{speedText}カスタムコースター。{features}" +
                   $"最高高度{GetHeightText()}、{CurrentDesign.RailElements.Count}セクション構成。";
        }

        private string GetHeightText()
        {
            return CurrentDesign.HeightClass switch
            {
                CoasterHeightClass.Low => "5m",
                CoasterHeightClass.Medium => "15m",
                CoasterHeightClass.High => "30m",
                CoasterHeightClass.Extreme => "50m",
                _ => "15m"
            };
        }

        private Vector2Int CalculateGridSize()
        {
            int elemCount = CurrentDesign?.RailElements.Count ?? 0;
            int size = Mathf.Clamp(3 + elemCount / 4, 3, 8);
            return new Vector2Int(size, size);
        }

        // ================================================================
        // 表示用ヘルパー
        // ================================================================

        /// <summary>レール要素タイプの日本語表示名</summary>
        public static string GetElementDisplayName(RailElementType type)
        {
            return type switch
            {
                RailElementType.Straight => "直線",
                RailElementType.GentleCurve => "緩カーブ",
                RailElementType.SharpCurve => "急カーブ",
                RailElementType.SmallHill => "小丘",
                RailElementType.MediumHill => "中丘",
                RailElementType.LargeHill => "大丘",
                RailElementType.Loop => "ループ",
                RailElementType.Corkscrew => "コークスクリュー",
                RailElementType.Helix => "ヘリックス",
                RailElementType.Brake => "ブレーキ",
                RailElementType.LaunchBoost => "加速ブースト",
                RailElementType.Drop => "急降下",
                RailElementType.Tunnel => "トンネル",
                RailElementType.WaterSplash => "スプラッシュ",
                _ => type.ToString()
            };
        }

        /// <summary>速度クラスの日本語表示名</summary>
        public static string GetSpeedClassName(CoasterSpeedClass cls)
        {
            return cls switch
            {
                CoasterSpeedClass.Slow => "低速 (30km/h)",
                CoasterSpeedClass.Medium => "中速 (60km/h)",
                CoasterSpeedClass.Fast => "高速 (90km/h)",
                CoasterSpeedClass.Extreme => "超高速 (120km/h)",
                _ => cls.ToString()
            };
        }

        /// <summary>高低差クラスの日本語表示名</summary>
        public static string GetHeightClassName(CoasterHeightClass cls)
        {
            return cls switch
            {
                CoasterHeightClass.Low => "低い (5m)",
                CoasterHeightClass.Medium => "中程度 (15m)",
                CoasterHeightClass.High => "高い (30m)",
                CoasterHeightClass.Extreme => "超高い (50m)",
                _ => cls.ToString()
            };
        }
    }
}
