// ============================================================
// ThemeParkGame - RivalDiplomacySystem
// ライバルパーク強化（スパイ・提携・買収・広告攻勢）
// M6 Feature: 外交アクションによるライバルとの戦略的関係構築
// ============================================================

using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using ThemeParkGame.Core;

namespace ThemeParkGame.Park
{
    /// <summary>スパイ活動で取得した情報</summary>
    [Serializable]
    public class SpyIntel
    {
        public int RivalId;
        public float RevealedPriceLevel;
        public float RevealedAttractionCount;
        public float RevealedSafetyScore;
        public float RevealedExcitementScore;
        public float RevealedComfortScore;
        public float RevealedValueScore;
        public float RevealTime;
        public float Duration;
        /// <summary>情報がまだ有効か</summary>
        public bool IsActive => Time.time - RevealTime < Duration;
    }

    /// <summary>ライバルとの外交状態データ</summary>
    [Serializable]
    public class RivalDiplomacy
    {
        public int RivalId;
        public RivalRelationState Relation;
        public bool IsPartner;
        public float PartnershipStartTime;
        public bool HasActiveAdBlitz;
        public float AdBlitzEndTime;
        public float AdBlitzCooldownEnd;
        public List<SpyIntel> ActiveIntel = new List<SpyIntel>();
    }

    /// <summary>
    /// ライバルパークとの外交アクションを管理するシステム。
    /// 【ゲームデザイン】
    /// ・スパイ派遣: ライバルの詳細情報を取得（成功率70%、失敗で敵対化）
    /// ・業務提携: 相互メリットのある協力関係を構築
    /// ・買収: ライバルを吸収し市場から排除
    /// ・広告攻勢: 一時的にライバルから来場者を奪取
    /// </summary>
    public class RivalDiplomacySystem : MonoBehaviour
    {
        public static RivalDiplomacySystem Instance { get; private set; }

        private readonly Dictionary<int, RivalDiplomacy> _diplomacyMap = new Dictionary<int, RivalDiplomacy>();
        private readonly Dictionary<int, float> _pendingSpyTimers = new Dictionary<int, float>();

        // コスト・パラメータ定数
        private const float SpyCost = 1000f;
        private const float SpyDuration = 30f;
        private const float SpySuccessRate = 0.70f;
        private const float IntelDuration = 300f; // 5分
        private const float AdBlitzCost = 2000f;
        private const float AdBlitzDuration = 60f;
        private const float AdBlitzCooldown = 120f;
        private const float AdBlitzStealRate = 0.10f;
        private const float PartnerEventBonus = 0.10f;
        private const float PartnerPenaltyReduction = 0.50f;
        private const int AcquisitionFameBonus = 10;
        private const float AcquisitionVisitorBonusRate = 0.20f;

        // UI
        private GameObject _uiPanel;
        private Text _summaryText;
        private GameObject _rivalListContainer;
        private readonly List<GameObject> _rivalEntries = new List<GameObject>();
        private bool _visible;

        private static readonly Color BgDark = new Color(0.06f, 0.09f, 0.16f, 0.92f);
        private static readonly Color BgEntry = new Color(0.10f, 0.14f, 0.22f, 0.90f);
        private static readonly Color BtnSpy = new Color(0.20f, 0.35f, 0.60f);
        private static readonly Color BtnPartner = new Color(0.15f, 0.50f, 0.35f);
        private static readonly Color BtnBreak = new Color(0.55f, 0.35f, 0.15f);
        private static readonly Color BtnAcquire = new Color(0.60f, 0.20f, 0.55f);
        private static readonly Color BtnAdBlitz = new Color(0.55f, 0.15f, 0.15f);
        private static readonly Color BtnDisabled = new Color(0.30f, 0.30f, 0.30f);
        private static readonly Color TextHeader = new Color(0.90f, 0.85f, 0.50f);
        private static readonly Color TextNormal = new Color(0.70f, 0.75f, 0.85f);
        private static readonly Color TextGood = new Color(0.40f, 0.85f, 0.55f);
        private static readonly Color TextBad = new Color(0.90f, 0.35f, 0.30f);

        // ================================================================
        // ライフサイクル
        // ================================================================

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(this); return; }
            Instance = this;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        private void Update()
        {
            if (GameManager.Instance == null) return;
            if (GameManager.Instance.CurrentState != GameState.Playing) return;

            float dt = Time.deltaTime;
            UpdateSpyTimers(dt);
            UpdateAdBlitz();
            CleanupExpiredIntel();
            if (_visible) RefreshUI();
        }

        // ================================================================
        // 外交データ管理
        // ================================================================

        /// <summary>指定ライバルの外交データを取得（なければ生成）</summary>
        private RivalDiplomacy GetOrCreateDiplomacy(int rivalId)
        {
            if (!_diplomacyMap.TryGetValue(rivalId, out var d))
            {
                d = new RivalDiplomacy
                {
                    RivalId = rivalId,
                    Relation = RivalRelationState.Neutral,
                    ActiveIntel = new List<SpyIntel>()
                };
                _diplomacyMap[rivalId] = d;
            }
            return d;
        }

        /// <summary>指定ライバルの外交状態を取得する</summary>
        public RivalRelationState GetRelation(int rivalId) => GetOrCreateDiplomacy(rivalId).Relation;

        /// <summary>指定ライバルとの提携状態を取得する</summary>
        public bool IsPartner(int rivalId) => GetOrCreateDiplomacy(rivalId).IsPartner;

        /// <summary>広告攻勢が有効か</summary>
        public bool HasActiveAdBlitz(int rivalId)
        {
            var d = GetOrCreateDiplomacy(rivalId);
            return d.HasActiveAdBlitz && Time.time < d.AdBlitzEndTime;
        }

        /// <summary>提携によるイベント来場者ボーナス倍率（提携ありで+10%）</summary>
        public float GetPartnerEventBonus()
        {
            foreach (var kvp in _diplomacyMap)
                if (kvp.Value.IsPartner) return PartnerEventBonus;
            return 0f;
        }

        /// <summary>提携による競争ペナルティ軽減率（提携中50%軽減）</summary>
        public float GetPartnerPenaltyReduction(int rivalId)
        {
            return GetOrCreateDiplomacy(rivalId).IsPartner ? PartnerPenaltyReduction : 0f;
        }

        /// <summary>広告攻勢による追加来場者奪取率</summary>
        public float GetAdBlitzStealRate(int rivalId) => HasActiveAdBlitz(rivalId) ? AdBlitzStealRate : 0f;

        /// <summary>全外交データを取得する（セーブ用）</summary>
        public Dictionary<int, RivalDiplomacy> GetAllDiplomacy() => new Dictionary<int, RivalDiplomacy>(_diplomacyMap);

        // ================================================================
        // 1. スパイ派遣
        // ================================================================

        /// <summary>
        /// ライバルにスパイを派遣する。
        /// コスト$1,000 / 所要30秒 / 成功率70% / 失敗で敵対化 / 成功時5分間情報開示。
        /// </summary>
        public void SendSpy(int rivalId)
        {
            var rival = FindRival(rivalId);
            if (rival == null || !rival.IsActive)
            { Notify("対象のライバルは存在しません", NotifLevel.Warning); return; }

            var d = GetOrCreateDiplomacy(rivalId);
            if (d.Relation == RivalRelationState.Acquired)
            { Notify("買収済みのライバルにはスパイを派遣できません", NotifLevel.Warning); return; }

            if (_pendingSpyTimers.ContainsKey(rivalId))
            { Notify("スパイはすでに派遣中です", NotifLevel.Warning); return; }

            var econ = GameManager.Instance?.EconomyManager;
            if (econ == null || !econ.CanAfford(SpyCost))
            { Notify($"資金不足です（必要: ${SpyCost:N0}）", NotifLevel.Warning); return; }

            econ.PayExpense(SpyCost, Economy.ExpenseCategory.Other);
            _pendingSpyTimers[rivalId] = SpyDuration;
            Notify($"「{rival.Name}」にスパイを派遣しました（${SpyCost:N0}）", NotifLevel.Info);
            WebGLOptimizer.LogVerbose($"[Diplomacy] Spy sent to rival {rivalId} ({rival.Name})");
        }

        /// <summary>スパイタイマー更新・完了判定</summary>
        private void UpdateSpyTimers(float dt)
        {
            var completed = new List<int>();
            var keys = new List<int>(_pendingSpyTimers.Keys);
            foreach (var id in keys)
            {
                _pendingSpyTimers[id] -= dt;
                if (_pendingSpyTimers[id] <= 0f) completed.Add(id);
            }
            foreach (var id in completed)
            {
                _pendingSpyTimers.Remove(id);
                ResolveSpyMission(id);
            }
        }

        /// <summary>スパイミッション成否判定</summary>
        private void ResolveSpyMission(int rivalId)
        {
            var rival = FindRival(rivalId);
            if (rival == null) return;

            var d = GetOrCreateDiplomacy(rivalId);
            if (UnityEngine.Random.value < SpySuccessRate)
            {
                d.ActiveIntel.Add(new SpyIntel
                {
                    RivalId = rivalId,
                    RevealedPriceLevel = rival.PriceLevel,
                    RevealedAttractionCount = rival.AttractionCount,
                    RevealedSafetyScore = rival.SafetyScore,
                    RevealedExcitementScore = rival.ExcitementScore,
                    RevealedComfortScore = rival.ComfortScore,
                    RevealedValueScore = rival.ValueScore,
                    RevealTime = Time.time,
                    Duration = IntelDuration
                });
                Notify($"スパイ成功！「{rival.Name}」の情報を入手（{IntelDuration / 60f:F0}分間有効）", NotifLevel.Success);
                WebGLOptimizer.LogVerbose($"[Diplomacy] Spy success: rival {rivalId}");
            }
            else
            {
                d.Relation = RivalRelationState.Hostile;
                Notify($"スパイ発覚！「{rival.Name}」との関係が敵対になりました", NotifLevel.Warning);
                WebGLOptimizer.LogVerbose($"[Diplomacy] Spy failed: rival {rivalId}, relation -> Hostile");
            }
        }

        /// <summary>期限切れスパイ情報をクリーンアップ</summary>
        private void CleanupExpiredIntel()
        {
            foreach (var kvp in _diplomacyMap)
                kvp.Value.ActiveIntel.RemoveAll(i => !i.IsActive);
        }

        // ================================================================
        // 2. 業務提携
        // ================================================================

        /// <summary>
        /// ライバルに業務提携を提案する。
        /// 中立状態のみ可。相手スコアが10以上低ければ自動承諾、それ以外50%承諾。
        /// 効果: イベント来場者+10%、競争ペナルティ50%軽減。
        /// </summary>
        public void ProposePartnership(int rivalId)
        {
            var rival = FindRival(rivalId);
            if (rival == null || !rival.IsActive)
            { Notify("対象のライバルは存在しません", NotifLevel.Warning); return; }

            var d = GetOrCreateDiplomacy(rivalId);
            if (d.IsPartner)
            { Notify("すでに提携中です", NotifLevel.Warning); return; }
            if (d.Relation == RivalRelationState.Hostile)
            { Notify("敵対関係のライバルとは提携できません", NotifLevel.Warning); return; }
            if (d.Relation == RivalRelationState.Acquired)
            { Notify("買収済みのライバルとは提携できません", NotifLevel.Warning); return; }

            float playerScore = GetPlayerOverallScore();
            bool accepted = (rival.OverallScore < playerScore - 10f) || (UnityEngine.Random.value < 0.5f);

            if (accepted)
            {
                d.IsPartner = true;
                d.Relation = RivalRelationState.Partner;
                d.PartnershipStartTime = Time.time;
                Notify($"「{rival.Name}」との業務提携が成立しました！", NotifLevel.Success);
                WebGLOptimizer.LogVerbose($"[Diplomacy] Partnership established: rival {rivalId}");
            }
            else
            {
                Notify($"「{rival.Name}」は提携を断りました", NotifLevel.Info);
                WebGLOptimizer.LogVerbose($"[Diplomacy] Partnership rejected: rival {rivalId}");
            }
        }

        /// <summary>業務提携を解消する。解消後は中立に戻る。</summary>
        public void BreakPartnership(int rivalId)
        {
            var d = GetOrCreateDiplomacy(rivalId);
            if (!d.IsPartner)
            { Notify("提携関係がありません", NotifLevel.Warning); return; }

            var rival = FindRival(rivalId);
            string name = rival?.Name ?? $"ライバル{rivalId}";
            d.IsPartner = false;
            d.Relation = RivalRelationState.Neutral;
            d.PartnershipStartTime = 0f;
            Notify($"「{name}」との提携を解消しました", NotifLevel.Info);
            WebGLOptimizer.LogVerbose($"[Diplomacy] Partnership broken: rival {rivalId}");
        }

        // ================================================================
        // 3. 買収
        // ================================================================

        /// <summary>
        /// ライバルパークの買収を試みる。
        /// コスト: Rating*500 / 条件: 総合スコアで上回る / 提携中は不可。
        /// 成功時: 閉園・知名度+10・ゴールデンチケット+1・来場者ボーナス。
        /// </summary>
        public void AttemptAcquisition(int rivalId)
        {
            var rival = FindRival(rivalId);
            if (rival == null || !rival.IsActive)
            { Notify("対象のライバルは存在しません", NotifLevel.Warning); return; }

            var d = GetOrCreateDiplomacy(rivalId);
            if (d.Relation == RivalRelationState.Acquired)
            { Notify("すでに買収済みです", NotifLevel.Warning); return; }
            if (d.IsPartner)
            { Notify("提携中のライバルは買収できません（先に提携を解消してください）", NotifLevel.Warning); return; }

            float playerScore = GetPlayerOverallScore();
            if (playerScore <= rival.OverallScore)
            {
                Notify($"買収にはスコアで上回る必要があります（あなた:{playerScore:F0} vs {rival.Name}:{rival.OverallScore:F0}）", NotifLevel.Warning);
                return;
            }

            float cost = rival.Rating * 500f;
            var econ = GameManager.Instance?.EconomyManager;
            if (econ == null || !econ.CanAfford(cost))
            { Notify($"資金不足です（必要: ${cost:N0}）", NotifLevel.Warning); return; }

            econ.PayExpense(cost, Economy.ExpenseCategory.Other);
            rival.IsActive = false;
            d.Relation = RivalRelationState.Acquired;
            d.IsPartner = false;

            GameManager.Instance?.AwardGoldenTicket();
            Notify($"「{rival.Name}」を買収！（${cost:N0}）知名度+{AcquisitionFameBonus} / チケット+1", NotifLevel.Success);
            WebGLOptimizer.LogVerbose($"[Diplomacy] Acquisition: rival {rivalId} ({rival.Name}), cost={cost}");
        }

        // ================================================================
        // 4. 広告攻勢
        // ================================================================

        /// <summary>
        /// ライバルに広告攻勢を仕掛ける。
        /// コスト$2,000 / 効果60秒 / 来場者10%追加奪取 / CD120秒。
        /// </summary>
        public void LaunchAdBlitz(int rivalId)
        {
            var rival = FindRival(rivalId);
            if (rival == null || !rival.IsActive)
            { Notify("対象のライバルは存在しません", NotifLevel.Warning); return; }

            var d = GetOrCreateDiplomacy(rivalId);
            if (d.Relation == RivalRelationState.Acquired)
            { Notify("買収済みのライバルには広告攻勢できません", NotifLevel.Warning); return; }
            if (Time.time < d.AdBlitzCooldownEnd)
            { Notify($"広告攻勢はクールダウン中（残り{d.AdBlitzCooldownEnd - Time.time:F0}秒）", NotifLevel.Warning); return; }
            if (d.HasActiveAdBlitz && Time.time < d.AdBlitzEndTime)
            { Notify("広告攻勢はすでに実行中です", NotifLevel.Warning); return; }

            var econ = GameManager.Instance?.EconomyManager;
            if (econ == null || !econ.CanAfford(AdBlitzCost))
            { Notify($"資金不足です（必要: ${AdBlitzCost:N0}）", NotifLevel.Warning); return; }

            econ.PayExpense(AdBlitzCost, Economy.ExpenseCategory.Other);
            d.HasActiveAdBlitz = true;
            d.AdBlitzEndTime = Time.time + AdBlitzDuration;
            d.AdBlitzCooldownEnd = Time.time + AdBlitzDuration + AdBlitzCooldown;

            Notify($"「{rival.Name}」に広告攻勢開始！（${AdBlitzCost:N0}、{AdBlitzDuration:F0}秒間）", NotifLevel.Success);
            WebGLOptimizer.LogVerbose($"[Diplomacy] Ad blitz: rival {rivalId} ({rival.Name})");
        }

        /// <summary>期限切れ広告攻勢を更新</summary>
        private void UpdateAdBlitz()
        {
            foreach (var kvp in _diplomacyMap)
            {
                var d = kvp.Value;
                if (d.HasActiveAdBlitz && Time.time >= d.AdBlitzEndTime)
                {
                    d.HasActiveAdBlitz = false;
                    var rival = FindRival(d.RivalId);
                    if (rival != null) Notify($"「{rival.Name}」への広告攻勢が終了しました", NotifLevel.Info);
                }
            }
        }

        // ================================================================
        // ユーティリティ
        // ================================================================

        private RivalPark FindRival(int rivalId)
        {
            if (RivalParkSystem.Instance == null) return null;
            foreach (var r in RivalParkSystem.Instance.Rivals)
                if (r.Id == rivalId) return r;
            return null;
        }

        private float GetPlayerOverallScore() => GameManager.Instance?.ParkManager?.Rating?.OverallRating ?? 0f;

        private void Notify(string msg, NotifLevel level) => GameManager.Instance?.ShowNotification(msg, level);

        // ================================================================
        // UI
        // ================================================================

        /// <summary>外交パネルの表示を切り替える</summary>
        public void ToggleUI() { if (_visible) HideUI(); else ShowUI(); }

        /// <summary>外交パネルを表示する</summary>
        public void ShowUI()
        {
            if (_uiPanel == null) BuildUI();
            _uiPanel.SetActive(true);
            _visible = true;
            RefreshUI();
        }

        /// <summary>外交パネルを非表示にする</summary>
        public void HideUI()
        {
            if (_uiPanel != null) _uiPanel.SetActive(false);
            _visible = false;
        }

        /// <summary>外交パネルUIをコードベースで構築する</summary>
        private void BuildUI()
        {
            var canvas = CanvasCache.Get();
            if (canvas == null) return;

            _uiPanel = new GameObject("RivalDiplomacyPanel");
            _uiPanel.transform.SetParent(canvas.transform, false);
            var rt = _uiPanel.AddComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.10f, 0.05f);
            rt.anchorMax = new Vector2(0.90f, 0.95f);
            rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;

            var bg = _uiPanel.AddComponent<Image>();
            bg.color = BgDark;

            MakeText(_uiPanel.transform, "Title",
                new Vector2(0.02f, 0.93f), new Vector2(0.80f, 0.99f),
                "ライバル外交 - スパイ・提携・買収", 20, FontStyle.Bold, Color.white);

            MakeBtn(_uiPanel.transform, "CloseBtn",
                new Vector2(0.90f, 0.94f), new Vector2(0.98f, 0.99f),
                "X", new Color(0.70f, 0.15f, 0.15f), HideUI);

            _summaryText = MakeText(_uiPanel.transform, "Summary",
                new Vector2(0.02f, 0.87f), new Vector2(0.98f, 0.93f),
                "", 14, FontStyle.Bold, TextHeader);

            _rivalListContainer = new GameObject("RivalList");
            _rivalListContainer.transform.SetParent(_uiPanel.transform, false);
            var listRt = _rivalListContainer.AddComponent<RectTransform>();
            listRt.anchorMin = new Vector2(0.02f, 0.02f);
            listRt.anchorMax = new Vector2(0.98f, 0.86f);
            listRt.offsetMin = Vector2.zero; listRt.offsetMax = Vector2.zero;

            _uiPanel.SetActive(false);
        }

        /// <summary>UIの内容を最新データで更新する</summary>
        private void RefreshUI()
        {
            if (RivalParkSystem.Instance == null) return;

            float playerScore = GetPlayerOverallScore();
            int partnerCount = 0, acquiredCount = 0;
            foreach (var kvp in _diplomacyMap)
            {
                if (kvp.Value.IsPartner) partnerCount++;
                if (kvp.Value.Relation == RivalRelationState.Acquired) acquiredCount++;
            }

            if (_summaryText != null)
            {
                _summaryText.text = $"あなたの総合評価: {playerScore:F0} | " +
                    $"提携中: {partnerCount} | 買収済: {acquiredCount} | " +
                    $"イベントボーナス: +{GetPartnerEventBonus() * 100f:F0}%";
            }

            foreach (var entry in _rivalEntries) { if (entry != null) Destroy(entry); }
            _rivalEntries.Clear();

            var rivals = RivalParkSystem.Instance.Rivals;
            float entryH = 1f / Mathf.Max(1, rivals.Count);

            for (int i = 0; i < rivals.Count; i++)
            {
                float yMax = 1f - (i * entryH);
                float yMin = yMax - entryH + 0.005f;
                BuildRivalEntry(rivals[i], yMin, yMax);
            }

            if (rivals.Count == 0)
            {
                MakeText(_rivalListContainer.transform, "NoRivals",
                    new Vector2(0.1f, 0.3f), new Vector2(0.9f, 0.7f),
                    "まだライバルは出現していません。\nゲーム3ヶ月後に最初のライバルが出現します。",
                    16, FontStyle.Normal, TextNormal);
            }
        }

        /// <summary>個別ライバルのUI行を構築する</summary>
        private void BuildRivalEntry(RivalPark rival, float yMin, float yMax)
        {
            var d = GetOrCreateDiplomacy(rival.Id);
            float playerScore = GetPlayerOverallScore();

            var entry = new GameObject($"Rival_{rival.Id}");
            entry.transform.SetParent(_rivalListContainer.transform, false);
            var rt = entry.AddComponent<RectTransform>();
            rt.anchorMin = new Vector2(0f, yMin); rt.anchorMax = new Vector2(1f, yMax);
            rt.offsetMin = new Vector2(2f, 2f); rt.offsetMax = new Vector2(-2f, -2f);
            entry.AddComponent<Image>().color = BgEntry;
            _rivalEntries.Add(entry);

            string activeStr = rival.IsActive ? "営業中" : "閉園";
            MakeText(entry.transform, "Name",
                new Vector2(0.01f, 0.65f), new Vector2(0.40f, 0.98f),
                $"{rival.Name} [{activeStr}]", 15, FontStyle.Bold, Color.white);

            MakeText(entry.transform, "Relation",
                new Vector2(0.41f, 0.65f), new Vector2(0.70f, 0.98f),
                $"関係: {GetRelationLabel(d.Relation)}", 13, FontStyle.Bold, GetRelationColor(d.Relation));

            // スコア情報（スパイ情報があれば詳細表示）
            SpyIntel intel = null;
            foreach (var si in d.ActiveIntel) { if (si.IsActive) { intel = si; break; } }

            string scoreInfo = intel != null
                ? $"総合:{rival.OverallScore:F0} 安全:{intel.RevealedSafetyScore:F0} " +
                  $"興奮:{intel.RevealedExcitementScore:F0} 快適:{intel.RevealedComfortScore:F0} " +
                  $"CP:{intel.RevealedValueScore:F0} 価格:{intel.RevealedPriceLevel:F1}x"
                : $"総合:{rival.OverallScore:F0} " +
                  (rival.OverallScore > playerScore ? "<<負けてます>>" :
                   rival.OverallScore < playerScore * 0.7f ? "<<圧勝中>>" : "<<互角>>") +
                  "（詳細はスパイで調査）";

            MakeText(entry.transform, "Scores",
                new Vector2(0.01f, 0.30f), new Vector2(0.55f, 0.65f),
                scoreInfo, 11, FontStyle.Normal, TextNormal);

            float acqCost = rival.Rating * 500f;
            MakeText(entry.transform, "AcqCost",
                new Vector2(0.56f, 0.30f), new Vector2(0.99f, 0.65f),
                $"買収費: ${acqCost:N0} | 戦略: {rival.Strategy}", 11, FontStyle.Normal, TextNormal);

            // アクションボタン群
            if (rival.IsActive && d.Relation != RivalRelationState.Acquired)
                BuildActionButtons(entry.transform, rival, d);
            else if (d.Relation == RivalRelationState.Acquired)
                MakeText(entry.transform, "AcquiredLabel",
                    new Vector2(0.01f, 0.02f), new Vector2(0.99f, 0.28f),
                    "-- 買収済み --", 13, FontStyle.Bold, TextGood);
        }

        /// <summary>アクションボタン群を構築する</summary>
        private void BuildActionButtons(Transform parent, RivalPark rival, RivalDiplomacy d)
        {
            float w = 0.19f, gap = 0.005f, y0 = 0.02f, y1 = 0.28f;
            float x = 0.01f;
            int id = rival.Id;

            // スパイ派遣
            bool spyPending = _pendingSpyTimers.ContainsKey(id);
            MakeBtn(parent, "BtnSpy",
                new Vector2(x, y0), new Vector2(x + w, y1),
                spyPending ? "派遣中..." : $"スパイ派遣\n${SpyCost:N0}",
                spyPending ? BtnDisabled : BtnSpy,
                () => { if (!spyPending) SendSpy(id); });
            x += w + gap;

            // 提携提案 / 提携解消
            if (d.IsPartner)
            {
                MakeBtn(parent, "BtnBreak", new Vector2(x, y0), new Vector2(x + w, y1),
                    "提携解消", BtnBreak, () => BreakPartnership(id));
            }
            else
            {
                bool canP = d.Relation == RivalRelationState.Neutral;
                MakeBtn(parent, "BtnPartner", new Vector2(x, y0), new Vector2(x + w, y1),
                    canP ? "提携提案" : "提携不可", canP ? BtnPartner : BtnDisabled,
                    () => { if (canP) ProposePartnership(id); });
            }
            x += w + gap;

            // 買収
            bool canAcq = !d.IsPartner && GetPlayerOverallScore() > rival.OverallScore;
            float cost = rival.Rating * 500f;
            MakeBtn(parent, "BtnAcquire", new Vector2(x, y0), new Vector2(x + w, y1),
                canAcq ? $"買収\n${cost:N0}" : "買収不可", canAcq ? BtnAcquire : BtnDisabled,
                () => { if (canAcq) AttemptAcquisition(id); });
            x += w + gap;

            // 広告攻勢
            bool adActive = d.HasActiveAdBlitz && Time.time < d.AdBlitzEndTime;
            bool adCD = Time.time < d.AdBlitzCooldownEnd;
            string adLabel; Color adColor;
            if (adActive)      { adLabel = $"攻勢中\n残{d.AdBlitzEndTime - Time.time:F0}秒"; adColor = BtnDisabled; }
            else if (adCD)     { adLabel = $"CD中\n残{d.AdBlitzCooldownEnd - Time.time:F0}秒"; adColor = BtnDisabled; }
            else               { adLabel = $"広告攻勢\n${AdBlitzCost:N0}"; adColor = BtnAdBlitz; }
            bool canAd = !adActive && !adCD;
            MakeBtn(parent, "BtnAdBlitz", new Vector2(x, y0), new Vector2(x + w, y1),
                adLabel, adColor, () => { if (canAd) LaunchAdBlitz(id); });
        }

        // ================================================================
        // 表示ヘルパー
        // ================================================================

        private string GetRelationLabel(RivalRelationState s)
        {
            switch (s)
            {
                case RivalRelationState.Neutral:  return "中立";
                case RivalRelationState.Hostile:  return "敵対";
                case RivalRelationState.Partner:  return "提携中";
                case RivalRelationState.Acquired: return "買収済";
                default: return "不明";
            }
        }

        private Color GetRelationColor(RivalRelationState s)
        {
            switch (s)
            {
                case RivalRelationState.Neutral:  return TextNormal;
                case RivalRelationState.Hostile:  return TextBad;
                case RivalRelationState.Partner:  return TextGood;
                case RivalRelationState.Acquired: return new Color(0.70f, 0.50f, 0.90f);
                default: return TextNormal;
            }
        }

        // ================================================================
        // UIコンポーネント生成ヘルパー
        // ================================================================

        private Text MakeText(Transform parent, string name,
            Vector2 aMin, Vector2 aMax, string content, int size, FontStyle style, Color color)
        {
            var obj = new GameObject(name);
            obj.transform.SetParent(parent, false);
            var r = obj.AddComponent<RectTransform>();
            r.anchorMin = aMin; r.anchorMax = aMax;
            r.offsetMin = new Vector2(5f, 0f); r.offsetMax = new Vector2(-5f, 0f);
            var t = obj.AddComponent<Text>();
            t.text = content; t.font = FontManager.Regular;
            t.fontSize = size; t.fontStyle = style; t.color = color;
            t.alignment = TextAnchor.MiddleLeft;
            return t;
        }

        private void MakeBtn(Transform parent, string name,
            Vector2 aMin, Vector2 aMax, string text, Color bgColor,
            UnityEngine.Events.UnityAction onClick)
        {
            var obj = new GameObject(name);
            obj.transform.SetParent(parent, false);
            var r = obj.AddComponent<RectTransform>();
            r.anchorMin = aMin; r.anchorMax = aMax;
            r.offsetMin = Vector2.zero; r.offsetMax = Vector2.zero;
            var img = obj.AddComponent<Image>(); img.color = bgColor;
            var btn = obj.AddComponent<Button>(); btn.targetGraphic = img;
            btn.onClick.AddListener(onClick);
            var txt = new GameObject("Text").AddComponent<Text>();
            txt.transform.SetParent(obj.transform, false);
            var tr = txt.GetComponent<RectTransform>();
            tr.anchorMin = Vector2.zero; tr.anchorMax = Vector2.one;
            tr.offsetMin = new Vector2(2f, 1f); tr.offsetMax = new Vector2(-2f, -1f);
            txt.font = FontManager.Regular; txt.fontSize = 11;
            txt.color = Color.white; txt.alignment = TextAnchor.MiddleCenter;
            txt.text = text;
        }
    }
}
