// ============================================================
// ThemeParkGame - EmotionBubble
// 来場者の頭上に表示される感情バブルの制御
// 状態・パラメータに応じたバブルタイプ選択と表示管理
// ============================================================

using System;
using UnityEngine;
using ThemeParkGame.Core;

namespace ThemeParkGame.Visitor
{
    /// <summary>
    /// 来場者の感情バブルの優先度付きエントリ。
    /// 複数の感情が競合する場合、優先度が高いものが表示される。
    /// </summary>
    [Serializable]
    public struct EmotionBubbleEntry
    {
        public EmotionBubbleType BubbleType;
        public EmotionBubbleColor Color;
        public int Priority;

        public EmotionBubbleEntry(EmotionBubbleType type, EmotionBubbleColor color, int priority)
        {
            BubbleType = type;
            Color = color;
            Priority = priority;
        }
    }

    /// <summary>
    /// 来場者の頭上に表示される感情バブルを制御するMonoBehaviour。
    ///
    /// 【ゲームデザイン】
    /// 感情バブルはプレイヤーが来場者の状態を視覚的に把握する主要手段。
    /// 原作「Sim Theme Park」に倣い、色分けされたバブルで直感的に理解できる。
    /// - 緑: 迷子・何かを探している（施設配置の改善が必要）
    /// - 黄: 空腹・喉の渇き（ショップ不足のサイン）
    /// - 水色: 現在の行動状態（情報表示）
    /// - 灰: 不満・苦情（問題発生のアラート）
    /// - 白: 評価（パークの品質フィードバック）
    /// - 青: 最高評価（成功のサイン）
    ///
    /// 複数の感情が同時に発生する場合、緊急性の高いものが優先表示される。
    /// </summary>
    public class EmotionBubble : MonoBehaviour
    {
        [Header("表示設定")]
        [SerializeField] private float displayDuration = 3f;
        [SerializeField] private float fadeDuration = 0.5f;
        [SerializeField] private float bubbleHeightOffset = 2.5f;
        [SerializeField] private float bobAmplitude = 0.1f;
        [SerializeField] private float bobFrequency = 2f;
        [SerializeField] private float cooldownBetweenBubbles = 2f;

        [Header("参照")]
        [SerializeField] private SpriteRenderer bubbleRenderer;
        [SerializeField] private Transform bubbleTransform;

        // 現在表示中のバブル情報
        private EmotionBubbleType currentBubbleType;
        private EmotionBubbleColor currentColor;
        private bool isDisplaying;
        private float displayTimer;
        private float fadeTimer;
        private float cooldownTimer;
        private bool isFadingOut;

        // 対象の来場者への参照
        private Transform visitorTransform;

        // ---- 色定義 ----

        /// <summary>感情バブルの色をColorに変換する</summary>
        private static readonly Color ColorGreen = new Color(0.2f, 0.9f, 0.3f, 1f);
        private static readonly Color ColorYellow = new Color(1f, 0.9f, 0.2f, 1f);
        private static readonly Color ColorLightBlue = new Color(0.4f, 0.8f, 1f, 1f);
        private static readonly Color ColorGray = new Color(0.6f, 0.6f, 0.6f, 1f);
        private static readonly Color ColorWhite = new Color(1f, 1f, 1f, 1f);
        private static readonly Color ColorBlue = new Color(0.2f, 0.4f, 1f, 1f);

        // ---- プロパティ ----

        /// <summary>現在表示中か</summary>
        public bool IsDisplaying => isDisplaying;

        /// <summary>現在のバブルタイプ</summary>
        public EmotionBubbleType CurrentType => currentBubbleType;

        /// <summary>クールダウン中か</summary>
        public bool IsOnCooldown => cooldownTimer > 0f;

        // ---- Unity ライフサイクル ----

        private void Awake()
        {
            visitorTransform = transform.parent;
            if (bubbleTransform == null)
            {
                bubbleTransform = transform;
            }

            HideBubble();
        }

        private void Update()
        {
            // クールダウン処理
            if (cooldownTimer > 0f)
            {
                cooldownTimer -= Time.deltaTime;
            }

            if (!isDisplaying) return;

            // フェードアウト中
            if (isFadingOut)
            {
                fadeTimer -= Time.deltaTime;
                if (fadeTimer <= 0f)
                {
                    HideBubble();
                    return;
                }
                float alpha = fadeTimer / fadeDuration;
                ApplyAlpha(alpha);
                return;
            }

            // 表示タイマー
            displayTimer -= Time.deltaTime;
            if (displayTimer <= 0f)
            {
                StartFadeOut();
                return;
            }

            // バブルの上下ボブアニメーション
            UpdateBobAnimation();

            // バブル位置を来場者の頭上に追従
            UpdatePosition();
        }

        private void OnDisable()
        {
            HideBubble();
        }

        // ---- 公開メソッド ----

        /// <summary>
        /// 指定されたバブルタイプを表示する。
        /// クールダウン中や、より優先度の高いバブルが表示中の場合は無視される。
        /// </summary>
        public void ShowBubble(EmotionBubbleType type, EmotionBubbleColor color)
        {
            if (IsOnCooldown && isDisplaying) return;

            currentBubbleType = type;
            currentColor = color;
            isDisplaying = true;
            isFadingOut = false;
            displayTimer = displayDuration;

            ApplyBubbleVisuals(type, color);
            SetVisible(true);
            ApplyAlpha(1f);
        }

        /// <summary>
        /// 来場者の現在の状態・パラメータから最適な感情バブルを評価して表示する。
        /// 【ゲームデザイン】優先度順に評価し、最も緊急性の高い感情を表示する。
        /// </summary>
        /// <param name="state">現在の行動状態</param>
        /// <param name="parameters">現在のパラメータ</param>
        public void EvaluateAndShow(VisitorBehaviorState state, VisitorParameters parameters)
        {
            if (IsOnCooldown && isDisplaying) return;

            EmotionBubbleEntry? bestBubble = DetermineBestBubble(state, parameters);
            if (bestBubble.HasValue)
            {
                ShowBubble(bestBubble.Value.BubbleType, bestBubble.Value.Color);
            }
        }

        /// <summary>即座にバブルを非表示にする</summary>
        public void ForceHide()
        {
            HideBubble();
        }

        /// <summary>表示時間を延長する</summary>
        public void ExtendDisplay(float additionalTime)
        {
            if (isDisplaying && !isFadingOut)
            {
                displayTimer += additionalTime;
            }
        }

        // ---- バブル判定ロジック ----

        /// <summary>
        /// 状態とパラメータから表示すべきバブルを決定する。
        /// 優先度が高いほど先に評価される。nullを返した場合、表示するバブルがない。
        ///
        /// 【ゲームデザイン】優先順位:
        /// 1. 緊急不満（灰色）: 嘔吐、高すぎる、汚い等
        /// 2. 探索中（緑）: トイレ/食事/出口を探している
        /// 3. 欲求（黄色）: 空腹、渇き
        /// 4. 評価（白/青）: 体験後の感想
        /// 5. 状態表示（水色）: 現在の行動
        /// </summary>
        private EmotionBubbleEntry? DetermineBestBubble(VisitorBehaviorState state, VisitorParameters parameters)
        {
            // 優先度1（最高）: 嘔吐中
            if (state == VisitorBehaviorState.Vomiting)
            {
                return new EmotionBubbleEntry(EmotionBubbleType.FoodTastesBad, EmotionBubbleColor.Gray, 100);
            }

            // 優先度2: トイレを探している（緊急）
            if (state == VisitorBehaviorState.WalkingToToilet && parameters.ToiletNeed >= VisitorParameters.ToiletAccidentThreshold - 5f)
            {
                return new EmotionBubbleEntry(EmotionBubbleType.LookingForToilet, EmotionBubbleColor.Green, 95);
            }

            // 優先度3: お金がない
            if (!parameters.HasMoney)
            {
                return new EmotionBubbleEntry(EmotionBubbleType.NoMoney, EmotionBubbleColor.LightBlue, 90);
            }

            // 優先度4: 退園中
            if (state == VisitorBehaviorState.LeavingPark)
            {
                return new EmotionBubbleEntry(EmotionBubbleType.LookingForExit, EmotionBubbleColor.Green, 85);
            }

            // 優先度5: 不満表示
            if (parameters.Happiness < 30f)
            {
                // 幸福度が非常に低い → 原因に応じた不満バブル
                if (parameters.Nausea > 50f)
                    return new EmotionBubbleEntry(EmotionBubbleType.NotExcitingEnough, EmotionBubbleColor.Gray, 80);
            }

            // 優先度6: 探索系
            if (state == VisitorBehaviorState.WalkingToToilet)
            {
                return new EmotionBubbleEntry(EmotionBubbleType.LookingForToilet, EmotionBubbleColor.Green, 70);
            }

            if (state == VisitorBehaviorState.WalkingToShop && parameters.IsHungry)
            {
                return new EmotionBubbleEntry(EmotionBubbleType.LookingForFood, EmotionBubbleColor.Green, 70);
            }

            // 優先度7: 欲求系
            if (parameters.IsHungry && state == VisitorBehaviorState.Idle)
            {
                return new EmotionBubbleEntry(EmotionBubbleType.Hungry, EmotionBubbleColor.Yellow, 60);
            }

            if (parameters.IsThirsty && state == VisitorBehaviorState.Idle)
            {
                return new EmotionBubbleEntry(EmotionBubbleType.Thirsty, EmotionBubbleColor.Yellow, 60);
            }

            // 優先度8: 行列待ちが長い
            if (state == VisitorBehaviorState.WaitingInQueue)
            {
                return new EmotionBubbleEntry(EmotionBubbleType.LongWait, EmotionBubbleColor.Gray, 50);
            }

            // 優先度9: 評価系（高幸福度時）
            if (parameters.Happiness >= 85f)
            {
                return new EmotionBubbleEntry(EmotionBubbleType.LovingIt, EmotionBubbleColor.Blue, 40);
            }

            if (parameters.Happiness >= 65f && parameters.Excitement >= 50f)
            {
                return new EmotionBubbleEntry(EmotionBubbleType.Interesting, EmotionBubbleColor.White, 35);
            }

            // 優先度10: 退屈
            if (parameters.IsBored && state == VisitorBehaviorState.Idle)
            {
                return new EmotionBubbleEntry(EmotionBubbleType.Boring, EmotionBubbleColor.White, 30);
            }

            // 優先度11: 状態表示系
            if (state == VisitorBehaviorState.Eating)
            {
                return new EmotionBubbleEntry(EmotionBubbleType.CurrentlyEating, EmotionBubbleColor.LightBlue, 20);
            }

            if (state == VisitorBehaviorState.Resting)
            {
                return new EmotionBubbleEntry(EmotionBubbleType.Resting, EmotionBubbleColor.LightBlue, 20);
            }

            if (state == VisitorBehaviorState.LookingAtMap)
            {
                return new EmotionBubbleEntry(EmotionBubbleType.Lost, EmotionBubbleColor.Green, 15);
            }

            // 表示するバブルなし
            return null;
        }

        // ---- 表示制御 ----

        /// <summary>バブルのビジュアルを適用する（スプライト・色の変更）</summary>
        private void ApplyBubbleVisuals(EmotionBubbleType type, EmotionBubbleColor color)
        {
            if (bubbleRenderer == null) return;

            Color tint = GetBubbleColor(color);
            bubbleRenderer.color = tint;

            // スプライト切り替えは将来SpriteAtlasから取得する想定
            // 現時点では色変更のみ
            // bubbleRenderer.sprite = EmotionSpriteAtlas.GetSprite(type);
        }

        /// <summary>EmotionBubbleColorからUnityのColorを取得する</summary>
        public static Color GetBubbleColor(EmotionBubbleColor color)
        {
            switch (color)
            {
                case EmotionBubbleColor.Green:     return ColorGreen;
                case EmotionBubbleColor.Yellow:    return ColorYellow;
                case EmotionBubbleColor.LightBlue: return ColorLightBlue;
                case EmotionBubbleColor.Gray:      return ColorGray;
                case EmotionBubbleColor.White:     return ColorWhite;
                case EmotionBubbleColor.Blue:      return ColorBlue;
                default: return ColorWhite;
            }
        }

        /// <summary>アルファ値を設定する（フェードイン/アウト用）</summary>
        private void ApplyAlpha(float alpha)
        {
            if (bubbleRenderer == null) return;
            Color c = bubbleRenderer.color;
            c.a = Mathf.Clamp01(alpha);
            bubbleRenderer.color = c;
        }

        /// <summary>バブルの表示/非表示を切り替える</summary>
        private void SetVisible(bool visible)
        {
            if (bubbleRenderer != null)
                bubbleRenderer.enabled = visible;

            if (bubbleTransform != null)
                bubbleTransform.gameObject.SetActive(visible);
        }

        /// <summary>フェードアウトを開始する</summary>
        private void StartFadeOut()
        {
            isFadingOut = true;
            fadeTimer = fadeDuration;
        }

        /// <summary>バブルを完全に非表示にしてリセットする</summary>
        private void HideBubble()
        {
            isDisplaying = false;
            isFadingOut = false;
            displayTimer = 0f;
            fadeTimer = 0f;
            cooldownTimer = cooldownBetweenBubbles;
            SetVisible(false);
        }

        /// <summary>バブルの上下ボブアニメーション</summary>
        private void UpdateBobAnimation()
        {
            if (bubbleTransform == null) return;

            float bobOffset = Mathf.Sin(Time.time * bobFrequency) * bobAmplitude;
            Vector3 localPos = bubbleTransform.localPosition;
            localPos.y = bubbleHeightOffset + bobOffset;
            bubbleTransform.localPosition = localPos;
        }

        /// <summary>バブル位置を来場者に追従させる</summary>
        private void UpdatePosition()
        {
            if (visitorTransform == null || bubbleTransform == null) return;

            // ワールド座標でバブルを来場者の上に配置
            // （親子関係がある場合はlocalPositionで制御済みなのでスキップ可）
            if (bubbleTransform.parent != visitorTransform)
            {
                Vector3 targetPos = visitorTransform.position;
                targetPos.y += bubbleHeightOffset;
                bubbleTransform.position = targetPos;
            }
        }
    }
}
