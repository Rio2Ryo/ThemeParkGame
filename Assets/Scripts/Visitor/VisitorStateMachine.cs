// ============================================================
// ThemeParkGame - VisitorStateMachine
// 来場者ライフサイクルFSM（Waiting / Enjoying / Leaving）
// 既存のVisitorBehaviorState（ミクロ状態）を3つのマクロフェーズに分類し、
// 来場者がアトラクションに並んで楽しんで帰るまでのサイクルを管理する
// ============================================================

using UnityEngine;
using ThemeParkGame.Core;

namespace ThemeParkGame.Visitor
{
    /// <summary>
    /// 来場者の高レベルライフサイクルを管理するFSM。
    ///
    /// 【ゲームデザイン】
    /// VisitorAIは15+のミクロ行動状態を持つが、プレイヤーが俯瞰的に把握するには
    /// 「待ち」「体験中」「退園中」の3フェーズで十分。
    /// このコンポーネントはミクロ状態をマクロフェーズにマッピングし、
    /// フェーズ遷移の統計・イベント通知・ビジュアルフィードバックを提供する。
    ///
    /// Waiting  : 入園後、体験を探している状態（移動・行列待ち・散策・マップ確認）
    /// Enjoying : 体験中（搭乗・食事・飲料・トイレ・休憩・ショー・会話・嘔吐）
    /// Leaving  : 退園中
    ///
    /// フェーズ遷移時にGameEvents.OnVisitorLifecycleChangedを発火する。
    /// </summary>
    public class VisitorStateMachine : MonoBehaviour
    {
        // ---- 状態 ----

        private VisitorLifecyclePhase _currentPhase = VisitorLifecyclePhase.Waiting;
        private VisitorBehaviorState _lastBehaviorState = VisitorBehaviorState.Idle;
        private bool _isInitialized;
        private int _visitorId;

        // ---- 統計 ----

        private float _waitingTime;
        private float _enjoyingTime;
        private float _leavingTime;
        private int _waitToEnjoyCount;   // Waiting→Enjoyingの遷移回数（体験開始回数）
        private int _enjoyToWaitCount;   // Enjoying→Waitingの遷移回数（体験完了回数）
        private float _phaseStartTime;   // 現在フェーズの開始時刻

        // ---- ビジュアル ----

        private Renderer _headMarker;
        private Material _headMarkerMaterial;

        private static readonly Color PhaseColorWaiting  = new Color(0.3f, 0.7f, 1.0f); // 水色
        private static readonly Color PhaseColorEnjoying = new Color(1.0f, 0.6f, 0.1f); // オレンジ
        private static readonly Color PhaseColorLeaving  = new Color(0.5f, 0.5f, 0.5f); // グレー

        // ---- プロパティ ----

        /// <summary>現在のライフサイクルフェーズ</summary>
        public VisitorLifecyclePhase CurrentPhase => _currentPhase;

        /// <summary>Waitingフェーズに費やした累積時間（秒）</summary>
        public float WaitingTime => _waitingTime;

        /// <summary>Enjoyingフェーズに費やした累積時間（秒）</summary>
        public float EnjoyingTime => _enjoyingTime;

        /// <summary>Leavingフェーズに費やした累積時間（秒）</summary>
        public float LeavingTime => _leavingTime;

        /// <summary>体験開始回数（Waiting→Enjoying遷移回数）</summary>
        public int ExperienceStartCount => _waitToEnjoyCount;

        /// <summary>体験完了回数（Enjoying→Waiting遷移回数）</summary>
        public int ExperienceCompleteCount => _enjoyToWaitCount;

        /// <summary>現在フェーズの経過時間（秒）</summary>
        public float CurrentPhaseElapsed => Time.time - _phaseStartTime;

        /// <summary>体験効率（Enjoying時間 / 総滞在時間、0-1）</summary>
        public float EnjoymentRatio
        {
            get
            {
                float total = _waitingTime + _enjoyingTime + _leavingTime;
                return total > 0f ? _enjoyingTime / total : 0f;
            }
        }

        // ---- 初期化 ----

        /// <summary>
        /// VisitorAI.Initialize()から呼ばれる。
        /// </summary>
        public void Initialize(int visitorId)
        {
            _visitorId = visitorId;
            _currentPhase = VisitorLifecyclePhase.Waiting;
            _lastBehaviorState = VisitorBehaviorState.Idle;
            _waitingTime = 0f;
            _enjoyingTime = 0f;
            _leavingTime = 0f;
            _waitToEnjoyCount = 0;
            _enjoyToWaitCount = 0;
            _phaseStartTime = Time.time;
            _isInitialized = true;

            ApplyPhaseVisual(_currentPhase);
        }

        /// <summary>
        /// VisitorAI.ResetVisitor()から呼ばれる。
        /// </summary>
        public void ResetStateMachine()
        {
            _isInitialized = false;
            _currentPhase = VisitorLifecyclePhase.Waiting;
            _lastBehaviorState = VisitorBehaviorState.Idle;
            _waitingTime = 0f;
            _enjoyingTime = 0f;
            _leavingTime = 0f;
            _waitToEnjoyCount = 0;
            _enjoyToWaitCount = 0;
        }

        // ---- ビジュアル設定 ----

        /// <summary>
        /// 頭上マーカー（フェーズ表示球）を設定する。
        /// RuntimeGameSetup.CreateVisitorPrefabから呼ばれる。
        /// </summary>
        public void SetupHeadMarker(Renderer marker)
        {
            _headMarker = marker;
            if (_headMarker != null)
            {
                _headMarkerMaterial = _headMarker.material;
            }
        }

        // ---- ミクロ→マクロ状態マッピング ----

        /// <summary>
        /// VisitorBehaviorStateからVisitorLifecyclePhaseへのマッピング。
        ///
        /// Waiting:
        ///   Idle, WalkingToAttraction, WalkingToShop, WalkingToToilet,
        ///   WaitingInQueue, LookingAtMap
        ///
        /// Enjoying:
        ///   RidingAttraction, Eating, Drinking, UsingToilet,
        ///   Resting, WatchingEntertainment, TalkingToPlayer, Vomiting
        ///
        /// Leaving:
        ///   LeavingPark
        /// </summary>
        public static VisitorLifecyclePhase MapBehaviorToPhase(VisitorBehaviorState behavior)
        {
            switch (behavior)
            {
                // ---- Waiting: 移動・探索・待機 ----
                case VisitorBehaviorState.Idle:
                case VisitorBehaviorState.WalkingToAttraction:
                case VisitorBehaviorState.WalkingToShop:
                case VisitorBehaviorState.WalkingToToilet:
                case VisitorBehaviorState.WaitingInQueue:
                case VisitorBehaviorState.LookingAtMap:
                    return VisitorLifecyclePhase.Waiting;

                // ---- Enjoying: 体験・アクティビティ ----
                case VisitorBehaviorState.RidingAttraction:
                case VisitorBehaviorState.Eating:
                case VisitorBehaviorState.Drinking:
                case VisitorBehaviorState.UsingToilet:
                case VisitorBehaviorState.Resting:
                case VisitorBehaviorState.WatchingEntertainment:
                case VisitorBehaviorState.TalkingToPlayer:
                case VisitorBehaviorState.Vomiting:
                    return VisitorLifecyclePhase.Enjoying;

                // ---- Leaving: 退園 ----
                case VisitorBehaviorState.LeavingPark:
                    return VisitorLifecyclePhase.Leaving;

                default:
                    return VisitorLifecyclePhase.Waiting;
            }
        }

        // ---- 状態更新 ----

        /// <summary>
        /// VisitorAIのFSM状態が変化した時に呼ばれる。
        /// マクロフェーズの遷移を判定し、必要に応じてイベントを発火する。
        /// </summary>
        public void OnBehaviorStateChanged(VisitorBehaviorState newBehavior)
        {
            if (!_isInitialized) return;

            _lastBehaviorState = newBehavior;
            VisitorLifecyclePhase newPhase = MapBehaviorToPhase(newBehavior);

            if (newPhase != _currentPhase)
            {
                TransitionPhase(newPhase);
            }
        }

        private void TransitionPhase(VisitorLifecyclePhase newPhase)
        {
            VisitorLifecyclePhase oldPhase = _currentPhase;

            // 遷移統計を記録
            if (oldPhase == VisitorLifecyclePhase.Waiting && newPhase == VisitorLifecyclePhase.Enjoying)
            {
                _waitToEnjoyCount++;
            }
            else if (oldPhase == VisitorLifecyclePhase.Enjoying && newPhase == VisitorLifecyclePhase.Waiting)
            {
                _enjoyToWaitCount++;
            }

            _currentPhase = newPhase;
            _phaseStartTime = Time.time;

            // ビジュアル更新
            ApplyPhaseVisual(newPhase);

            // イベント発火
            GameEvents.FireVisitorLifecycleChanged(_visitorId, oldPhase, newPhase);

            Debug.Log($"[VisitorStateMachine] Visitor {_visitorId}: {oldPhase} -> {newPhase} " +
                      $"(exp:{_waitToEnjoyCount}, done:{_enjoyToWaitCount})");
        }

        // ---- Update ----

        private void Update()
        {
            if (!_isInitialized) return;
            if (GameManager.Instance != null && GameManager.Instance.IsPaused) return;

            float dt = Time.deltaTime;

            // フェーズ別累積時間を加算
            switch (_currentPhase)
            {
                case VisitorLifecyclePhase.Waiting:
                    _waitingTime += dt;
                    break;
                case VisitorLifecyclePhase.Enjoying:
                    _enjoyingTime += dt;
                    break;
                case VisitorLifecyclePhase.Leaving:
                    _leavingTime += dt;
                    break;
            }

            // 頭上マーカーの呼吸アニメーション
            AnimateHeadMarker(dt);
        }

        // ---- ビジュアル ----

        /// <summary>フェーズに応じた頭上マーカーの色を適用する</summary>
        private void ApplyPhaseVisual(VisitorLifecyclePhase phase)
        {
            if (_headMarkerMaterial == null) return;

            switch (phase)
            {
                case VisitorLifecyclePhase.Waiting:
                    _headMarkerMaterial.color = PhaseColorWaiting;
                    break;
                case VisitorLifecyclePhase.Enjoying:
                    _headMarkerMaterial.color = PhaseColorEnjoying;
                    break;
                case VisitorLifecyclePhase.Leaving:
                    _headMarkerMaterial.color = PhaseColorLeaving;
                    break;
            }
        }

        /// <summary>頭上マーカーの呼吸パルスアニメーション</summary>
        private void AnimateHeadMarker(float dt)
        {
            if (_headMarker == null) return;

            // Enjoyingフェーズのみパルス。他は一定サイズ。
            float baseScale = 0.2f;
            if (_currentPhase == VisitorLifecyclePhase.Enjoying)
            {
                float pulse = 1f + Mathf.Sin(Time.time * 4f) * 0.25f;
                float s = baseScale * pulse;
                _headMarker.transform.localScale = new Vector3(s, s, s);
            }
            else
            {
                _headMarker.transform.localScale = new Vector3(baseScale, baseScale, baseScale);
            }
        }

        // ---- ユーティリティ ----

        /// <summary>フェーズの日本語ラベルを返す</summary>
        public static string GetPhaseLabel(VisitorLifecyclePhase phase)
        {
            switch (phase)
            {
                case VisitorLifecyclePhase.Waiting:  return "待機中";
                case VisitorLifecyclePhase.Enjoying: return "体験中";
                case VisitorLifecyclePhase.Leaving:  return "退園中";
                default: return phase.ToString();
            }
        }

        /// <summary>フェーズの表示色を返す</summary>
        public static Color GetPhaseColor(VisitorLifecyclePhase phase)
        {
            switch (phase)
            {
                case VisitorLifecyclePhase.Waiting:  return PhaseColorWaiting;
                case VisitorLifecyclePhase.Enjoying: return PhaseColorEnjoying;
                case VisitorLifecyclePhase.Leaving:  return PhaseColorLeaving;
                default: return Color.white;
            }
        }

        /// <summary>統計サマリ文字列を生成する</summary>
        public string GetStatsSummary()
        {
            float total = _waitingTime + _enjoyingTime + _leavingTime;
            float enjoyPct = total > 0f ? (_enjoyingTime / total) * 100f : 0f;
            float waitPct = total > 0f ? (_waitingTime / total) * 100f : 0f;

            return $"待機:{_waitingTime:F0}s({waitPct:F0}%) " +
                   $"体験:{_enjoyingTime:F0}s({enjoyPct:F0}%) " +
                   $"回数:{_waitToEnjoyCount}回";
        }
    }
}
