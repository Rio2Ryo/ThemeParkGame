// ============================================================
// ThemeParkGame - VisitorVisualController
// 来場者の状態に応じたプロシージャルアニメーション制御
// モデル/Animator不要でカプセルの色・動き・スケールで状態を表現
// ============================================================

using UnityEngine;
using ThemeParkGame.Core;
using ThemeParkGame.Park;

namespace ThemeParkGame.Visitor
{
    /// <summary>
    /// 来場者カプセルに状態別のプロシージャルアニメーションを適用する。
    /// - 色の変化で状態を視覚的に伝達
    /// - 上下のボブ(bob)で歩行を表現
    /// - スケールパルスで興奮/搭乗を表現
    /// - 減速フェードで退園を表現
    /// </summary>
    public class VisitorVisualController : MonoBehaviour
    {
        // ---- 外部参照 ----

        private Renderer _bodyRenderer;
        private Transform _bodyTransform;
        private Material _material;
        private Vector3 _baseLocalPos;
        private Vector3 _baseLocalScale;

        // ---- 現在の状態 ----

        private VisitorBehaviorState _currentState = VisitorBehaviorState.Idle;
        private float _stateTime;
        private float _animPhase; // アニメーション位相（個体差用）

        // ---- 混雑度影響 ----
        private float _localCongestion;
        private float _congestionCheckTimer;
        private const float CongestionCheckInterval = 0.8f;

        // ---- 状態別カラーテーブル ----

        private static readonly Color ColorIdle          = new Color(0.3f, 0.6f, 1.0f);   // 青
        private static readonly Color ColorWalking       = new Color(0.3f, 0.8f, 0.5f);   // 緑
        private static readonly Color ColorWaitingQueue  = new Color(1.0f, 0.85f, 0.2f);  // 黄
        private static readonly Color ColorRiding        = new Color(1.0f, 0.45f, 0.1f);  // オレンジ
        private static readonly Color ColorEating        = new Color(0.9f, 0.6f, 0.8f);   // ピンク
        private static readonly Color ColorToilet        = new Color(0.6f, 0.5f, 0.9f);   // 紫
        private static readonly Color ColorResting       = new Color(0.5f, 0.8f, 0.9f);   // 水色
        private static readonly Color ColorLeaving       = new Color(0.5f, 0.5f, 0.5f);   // グレー
        private static readonly Color ColorVomiting      = new Color(0.4f, 0.8f, 0.2f);   // 黄緑
        private static readonly Color ColorTalking       = new Color(1.0f, 1.0f, 0.6f);   // 明黄

        // ---- 初期化 ----

        /// <summary>
        /// Bodyオブジェクトのレンダラーを設定する。
        /// RuntimeGameSetup.CreateVisitorPrefabから呼ばれる。
        /// </summary>
        public void Setup(Renderer bodyRenderer)
        {
            _bodyRenderer = bodyRenderer;
            if (_bodyRenderer != null)
            {
                _bodyTransform = _bodyRenderer.transform;
                _material = _bodyRenderer.material;
                _baseLocalPos = _bodyTransform.localPosition;
                _baseLocalScale = _bodyTransform.localScale;
            }

            // 個体差アニメ位相（全員が同じタイミングでボブしない）
            _animPhase = Random.Range(0f, Mathf.PI * 2f);
        }

        // ---- 状態変更 ----

        /// <summary>
        /// FSM状態が変わった時にVisitorAIから呼ばれる。
        /// </summary>
        public void OnStateChanged(VisitorBehaviorState newState)
        {
            _currentState = newState;
            _stateTime = 0f;
            ApplyStateColor(newState);
        }

        /// <summary>初期化時にリセットする</summary>
        public void ResetVisual()
        {
            _currentState = VisitorBehaviorState.Idle;
            _stateTime = 0f;

            if (_bodyTransform != null)
            {
                _bodyTransform.localPosition = _baseLocalPos;
                _bodyTransform.localScale = _baseLocalScale;
            }

            ApplyStateColor(VisitorBehaviorState.Idle);
        }

        // ---- Update ----

        private void Update()
        {
            if (_bodyTransform == null) return;

            float dt = Time.deltaTime;
            _stateTime += dt;

            // 混雑度チェック（歩行状態時のみ）
            _congestionCheckTimer -= dt;
            if (_congestionCheckTimer <= 0f)
            {
                _congestionCheckTimer = CongestionCheckInterval;
                UpdateLocalCongestion();
            }

            float t = _stateTime + _animPhase;

            switch (_currentState)
            {
                case VisitorBehaviorState.Idle:
                    AnimateIdle(t);
                    break;

                case VisitorBehaviorState.WalkingToAttraction:
                case VisitorBehaviorState.WalkingToShop:
                case VisitorBehaviorState.WalkingToToilet:
                    AnimateWalking(t);
                    break;

                case VisitorBehaviorState.WaitingInQueue:
                    AnimateWaiting(t);
                    break;

                case VisitorBehaviorState.RidingAttraction:
                    AnimateRiding(t);
                    break;

                case VisitorBehaviorState.Eating:
                case VisitorBehaviorState.Drinking:
                    AnimateEating(t);
                    break;

                case VisitorBehaviorState.UsingToilet:
                    AnimateToilet(t);
                    break;

                case VisitorBehaviorState.Resting:
                case VisitorBehaviorState.LookingAtMap:
                case VisitorBehaviorState.WatchingEntertainment:
                    AnimateResting(t);
                    break;

                case VisitorBehaviorState.LeavingPark:
                    AnimateLeaving(t, dt);
                    break;

                case VisitorBehaviorState.Vomiting:
                    AnimateVomiting(t);
                    break;

                case VisitorBehaviorState.TalkingToPlayer:
                    AnimateTalking(t);
                    break;
            }
        }

        // ---- アニメーション実装 ----

        /// <summary>Idle: 呼吸のような微小上下動</summary>
        private void AnimateIdle(float t)
        {
            float bob = Mathf.Sin(t * 1.5f) * 0.02f;
            _bodyTransform.localPosition = _baseLocalPos + new Vector3(0f, bob, 0f);
            _bodyTransform.localScale = _baseLocalScale;
        }

        /// <summary>歩行: 大きめのボブ + 左右揺れ。混雑時はボブが小さく・揺れが増す</summary>
        private void AnimateWalking(float t)
        {
            // 混雑度に応じてアニメーション変化
            // 空き時: 大きなボブ、少ない揺れ → 軽快な歩行
            // 混雑時: 小さなボブ、大きな揺れ → もたつく歩行
            float congestionFactor = _localCongestion;

            float bobSpeed = Mathf.Lerp(6f, 3f, congestionFactor);
            float bobHeight = Mathf.Lerp(0.06f, 0.02f, congestionFactor);
            float bob = Mathf.Abs(Mathf.Sin(t * bobSpeed)) * bobHeight;
            _bodyTransform.localPosition = _baseLocalPos + new Vector3(0f, bob, 0f);

            // 混雑時は左右揺れが大きく、微妙なX振れも追加
            float swayAmount = Mathf.Lerp(2f, 5f, congestionFactor);
            float sway = Mathf.Sin(t * 3f) * swayAmount;
            float xShift = Mathf.Sin(t * 1.5f + _animPhase) * congestionFactor * 0.03f;
            _bodyTransform.localPosition += new Vector3(xShift, 0f, 0f);
            _bodyTransform.localRotation = Quaternion.Euler(0f, 0f, sway);

            // 混雑時は体が少し縮む（圧迫感表現）
            float scaleY = Mathf.Lerp(1f, 0.92f, congestionFactor);
            _bodyTransform.localScale = Vector3.Scale(_baseLocalScale, new Vector3(1f, scaleY, 1f));
        }

        /// <summary>行列待ち: 左右によたよた + 上下ゆっくり</summary>
        private void AnimateWaiting(float t)
        {
            float bob = Mathf.Sin(t * 1.2f) * 0.015f;
            float sway = Mathf.Sin(t * 0.8f) * 3f;
            _bodyTransform.localPosition = _baseLocalPos + new Vector3(0f, bob, 0f);
            _bodyTransform.localRotation = Quaternion.Euler(0f, 0f, sway);
        }

        /// <summary>搭乗中: 興奮パルス (スケール揺れ + 色変動)</summary>
        private void AnimateRiding(float t)
        {
            float pulse = 1f + Mathf.Sin(t * 8f) * 0.08f;
            _bodyTransform.localScale = _baseLocalScale * pulse;

            float bounce = Mathf.Abs(Mathf.Sin(t * 5f)) * 0.1f;
            _bodyTransform.localPosition = _baseLocalPos + new Vector3(0f, bounce, 0f);

            // 色を明暗パルス
            if (_material != null)
            {
                float bright = 0.85f + Mathf.Sin(t * 4f) * 0.15f;
                _material.color = ColorRiding * bright;
            }
        }

        /// <summary>食事/飲料: 小刻みに頭を上下</summary>
        private void AnimateEating(float t)
        {
            float nod = Mathf.Sin(t * 4f) * 0.03f;
            _bodyTransform.localPosition = _baseLocalPos + new Vector3(0f, nod, 0f);
            _bodyTransform.localRotation = Quaternion.identity;
            _bodyTransform.localScale = _baseLocalScale;
        }

        /// <summary>トイレ: 小刻みに震える</summary>
        private void AnimateToilet(float t)
        {
            float shake = Mathf.Sin(t * 15f) * 0.01f;
            _bodyTransform.localPosition = _baseLocalPos + new Vector3(shake, 0f, shake);
            _bodyTransform.localScale = _baseLocalScale;
        }

        /// <summary>休憩: ゆっくり沈む + 呼吸</summary>
        private void AnimateResting(float t)
        {
            float breath = Mathf.Sin(t * 0.8f) * 0.015f;
            float sink = -0.05f; // 座っている感
            _bodyTransform.localPosition = _baseLocalPos + new Vector3(0f, sink + breath, 0f);
            _bodyTransform.localScale = Vector3.Scale(_baseLocalScale, new Vector3(1.05f, 0.9f, 1.05f));
        }

        /// <summary>退園: ボブしながら徐々に色が薄くなる</summary>
        private void AnimateLeaving(float t, float dt)
        {
            float bob = Mathf.Abs(Mathf.Sin(t * 5f)) * 0.04f;
            _bodyTransform.localPosition = _baseLocalPos + new Vector3(0f, bob, 0f);
            _bodyTransform.localScale = _baseLocalScale;

            // 徐々に透明に（10秒で完全グレー化）
            if (_material != null)
            {
                float fade = Mathf.Clamp01(_stateTime / 10f);
                Color c = Color.Lerp(ColorLeaving, new Color(0.3f, 0.3f, 0.3f, 0.4f), fade);
                _material.color = c;
            }
        }

        /// <summary>嘔吐: 激しい前後揺れ</summary>
        private void AnimateVomiting(float t)
        {
            float lurch = Mathf.Sin(t * 10f) * 8f;
            float bob = Mathf.Sin(t * 6f) * 0.04f;
            _bodyTransform.localPosition = _baseLocalPos + new Vector3(0f, bob - 0.05f, 0f);
            _bodyTransform.localRotation = Quaternion.Euler(lurch, 0f, 0f);
            _bodyTransform.localScale = _baseLocalScale;
        }

        /// <summary>会話中: 頭の揺れ(うなずき)</summary>
        private void AnimateTalking(float t)
        {
            float nod = Mathf.Sin(t * 2.5f) * 4f;
            _bodyTransform.localPosition = _baseLocalPos;
            _bodyTransform.localRotation = Quaternion.Euler(nod, 0f, 0f);
            _bodyTransform.localScale = _baseLocalScale;
        }

        // ---- 混雑度取得 ----

        /// <summary>現在位置の通路混雑度を取得する</summary>
        private void UpdateLocalCongestion()
        {
            bool isWalking = _currentState == VisitorBehaviorState.WalkingToAttraction ||
                             _currentState == VisitorBehaviorState.WalkingToShop ||
                             _currentState == VisitorBehaviorState.WalkingToToilet ||
                             _currentState == VisitorBehaviorState.LeavingPark;
            if (!isWalking)
            {
                _localCongestion = Mathf.Lerp(_localCongestion, 0f, 0.3f);
                return;
            }

            var pathSystem = Object.FindObjectOfType<PathwaySystem>();
            if (pathSystem != null)
            {
                float target = pathSystem.GetCongestionAt(transform.position);
                _localCongestion = Mathf.Lerp(_localCongestion, target, 0.4f);
            }
            else
            {
                _localCongestion = 0f;
            }
        }

        // ---- カラー適用 ----

        private void ApplyStateColor(VisitorBehaviorState state)
        {
            if (_material == null) return;

            switch (state)
            {
                case VisitorBehaviorState.Idle:
                    _material.color = ColorIdle;
                    break;
                case VisitorBehaviorState.WalkingToAttraction:
                case VisitorBehaviorState.WalkingToShop:
                case VisitorBehaviorState.WalkingToToilet:
                    _material.color = ColorWalking;
                    break;
                case VisitorBehaviorState.WaitingInQueue:
                    _material.color = ColorWaitingQueue;
                    break;
                case VisitorBehaviorState.RidingAttraction:
                    _material.color = ColorRiding;
                    break;
                case VisitorBehaviorState.Eating:
                case VisitorBehaviorState.Drinking:
                    _material.color = ColorEating;
                    break;
                case VisitorBehaviorState.UsingToilet:
                    _material.color = ColorToilet;
                    break;
                case VisitorBehaviorState.Resting:
                case VisitorBehaviorState.LookingAtMap:
                case VisitorBehaviorState.WatchingEntertainment:
                    _material.color = ColorResting;
                    break;
                case VisitorBehaviorState.LeavingPark:
                    _material.color = ColorLeaving;
                    break;
                case VisitorBehaviorState.Vomiting:
                    _material.color = ColorVomiting;
                    break;
                case VisitorBehaviorState.TalkingToPlayer:
                    _material.color = ColorTalking;
                    break;
            }
        }
    }
}
