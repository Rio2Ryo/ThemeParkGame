// ============================================================
// ThemeParkGame - ResearchItem
// 個別の研究項目データ
// 研究ツリーの各ノードを定義する
// ============================================================

using System;
using System.Collections.Generic;
using UnityEngine;
using ThemeParkGame.Core;

namespace ThemeParkGame.Attraction
{
    /// <summary>
    /// 研究カテゴリ。
    /// 研究ツリーを大分類するために使用する。
    /// </summary>
    public enum ResearchCategory
    {
        /// <summary>アトラクション研究 - 新しいアトラクションのアンロック</summary>
        Attractions,
        /// <summary>ショップ研究 - 新しいショップ・商品のアンロック</summary>
        Shops,
        /// <summary>施設研究 - パーク施設（トイレ・ベンチ等）のアンロック</summary>
        Facilities,
        /// <summary>アップグレード研究 - 既存施設の強化オプションのアンロック</summary>
        Upgrades
    }

    /// <summary>
    /// 個別の研究項目データ。
    /// 研究ツリーの各ノードとして機能し、前提条件・コスト・進捗・解放内容を管理する。
    ///
    /// 研究ツリーの構造:
    /// - 各研究項目は複数の前提研究（Prerequisites）を持てる
    /// - 前提研究がすべてCompletedにならないとAvailableにならない
    /// - 研究の進行速度はサイエンティストの人数とスキルに依存する
    /// - 完了時に対応する施設やアトラクションがアンロックされる
    /// </summary>
    [Serializable]
    public class ResearchItem
    {
        // ---- 識別情報 ----

        /// <summary>研究項目の一意な識別子（例: "RES_LK_COASTER"）</summary>
        [Tooltip("研究項目の一意な識別子")]
        public string ResearchId;

        /// <summary>日本語名</summary>
        [Tooltip("研究項目の日本語名")]
        public string NameJP;

        /// <summary>英語名</summary>
        [Tooltip("研究項目の英語名")]
        public string NameEN;

        /// <summary>研究の説明文</summary>
        [TextArea(2, 4)]
        [Tooltip("研究の説明文")]
        public string Description;

        // ---- カテゴリ・分類 ----

        /// <summary>研究カテゴリ</summary>
        [Tooltip("この研究項目のカテゴリ")]
        public ResearchCategory Category;

        /// <summary>関連テーマゾーン（特定ゾーンに紐づく研究の場合）</summary>
        [Tooltip("関連テーマゾーン（ゾーン共通の場合はnull扱い）")]
        public ThemeZone RelatedZone;

        /// <summary>この研究がゾーン固有かどうか</summary>
        [Tooltip("テーマゾーン固有の研究かどうか")]
        public bool IsZoneSpecific = true;

        // ---- 前提条件 ----

        /// <summary>
        /// この研究を開始するために必要な前提研究IDのリスト。
        /// すべての前提研究がCompletedである必要がある。
        /// 空リストの場合、前提条件なしで開始可能。
        /// </summary>
        [Tooltip("前提となる研究IDのリスト（すべて完了している必要がある）")]
        public List<string> RequiredPrerequisites = new List<string>();

        // ---- コスト・時間 ----

        /// <summary>
        /// 研究開始に必要なコスト（一括払い）。
        /// 研究開始時にEconomyManagerから引かれる。
        /// </summary>
        [Tooltip("研究開始に必要な費用")]
        [Min(0)]
        public int ResearchCost;

        /// <summary>
        /// 研究完了までの基本所要時間（秒）。
        /// サイエンティストの人数・スキルで実際の所要時間は変動する。
        /// </summary>
        [Tooltip("研究の基本所要時間（秒）。サイエンティストの能力で変動")]
        [Min(1f)]
        public float BaseResearchTime = 120f;

        // ---- 研究進捗（ランタイム状態） ----

        /// <summary>現在の研究状態</summary>
        [Tooltip("現在の研究状態")]
        public ResearchState CurrentState = ResearchState.Locked;

        /// <summary>現在の研究進捗（0.0～BaseResearchTime）</summary>
        public float CurrentProgress;

        /// <summary>研究進捗率（0.0～1.0）</summary>
        public float ProgressRatio => BaseResearchTime > 0 ? Mathf.Clamp01(CurrentProgress / BaseResearchTime) : 0f;

        // ---- アンロック内容 ----

        /// <summary>
        /// この研究完了でアンロックされる施設タイプ。
        /// 施設タイプのアンロックの場合に設定する。
        /// </summary>
        [Tooltip("アンロックされる施設タイプ（該当する場合）")]
        public FacilityType UnlockedFacilityType;

        /// <summary>
        /// この研究完了でアンロックされるアトラクションID。
        /// アトラクションのアンロックの場合に設定する。
        /// 空欄の場合はアトラクション以外のアンロック。
        /// </summary>
        [Tooltip("アンロックされるアトラクションID（該当する場合）")]
        public string UnlockedAttractionId;

        /// <summary>
        /// この研究完了でアンロックされるアップグレードの対象アトラクションID。
        /// アップグレード研究の場合に設定する。
        /// </summary>
        [Tooltip("アップグレード対象のアトラクションID（アップグレード研究の場合）")]
        public string UpgradeTargetAttractionId;

        /// <summary>アップグレード研究の場合、解放されるアップグレードレベル</summary>
        [Tooltip("解放されるアップグレードレベル（1～3）")]
        public int UpgradeLevel;

        // ---- UIアイコン ----

        /// <summary>研究ツリーUI上のアイコン</summary>
        [Tooltip("研究ツリーUI上で表示するアイコン")]
        public Sprite Icon;

        // ---- ヘルパーメソッド ----

        /// <summary>この研究が開始可能かどうかを判定する（前提条件・状態チェック）</summary>
        public bool CanStart(Dictionary<string, ResearchItem> allResearch)
        {
            if (CurrentState != ResearchState.Available) return false;

            // 前提条件チェック
            foreach (string prereqId in RequiredPrerequisites)
            {
                if (!allResearch.TryGetValue(prereqId, out var prereq))
                    return false;
                if (prereq.CurrentState != ResearchState.Completed)
                    return false;
            }

            return true;
        }

        /// <summary>
        /// 研究進捗を加算する。
        /// </summary>
        /// <param name="deltaProgress">加算する進捗量</param>
        /// <returns>研究が完了したらtrue</returns>
        public bool AddProgress(float deltaProgress)
        {
            if (CurrentState != ResearchState.InProgress) return false;

            CurrentProgress += deltaProgress;
            if (CurrentProgress >= BaseResearchTime)
            {
                CurrentProgress = BaseResearchTime;
                CurrentState = ResearchState.Completed;
                return true;
            }
            return false;
        }

        /// <summary>研究を開始状態にする</summary>
        public void StartResearch()
        {
            CurrentState = ResearchState.InProgress;
            CurrentProgress = 0f;
        }

        /// <summary>研究をリセットする（デバッグ用）</summary>
        public void Reset()
        {
            CurrentState = ResearchState.Locked;
            CurrentProgress = 0f;
        }

        /// <summary>文字列表現</summary>
        public override string ToString()
        {
            return $"[{ResearchId}] {NameJP} ({CurrentState}, {ProgressRatio:P0})";
        }
    }
}
