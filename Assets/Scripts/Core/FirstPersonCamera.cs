// ============================================================
// ThemeParkGame - FirstPersonCamera
// 来場者目線のファーストパーソンビューカメラ
// 選択した来場者に追従し、パーク内を一人称視点で体験できる
// ============================================================

using UnityEngine;
using ThemeParkGame.Visitor;

namespace ThemeParkGame.Core
{
    /// <summary>
    /// 来場者の目線で一人称視点を提供するカメラコントローラー。
    ///
    /// 【ゲームデザイン】
    /// プレイヤーが実際に来場者としてパークを体験できるモード。
    /// 来場者を選択して「搭乗」すると、その来場者の目線でパークを見渡せる。
    /// マウスドラッグで自由に見回し、来場者が歩くと歩行ボブが発生する。
    ///
    /// 機能:
    /// - 選択した来場者の頭上位置に追従
    /// - マウスドラッグ/タッチスワイプで視点回転（Yaw/Pitch）
    /// - 歩行中のヘッドボブアニメーション
    /// - 搭乗中のカメラ揺れ（アトラクション体験感）
    /// - 滑らかな開始/終了トランジション
    /// - ESCキーまたはUI操作で通常視点に復帰
    /// </summary>
    public class FirstPersonCamera : MonoBehaviour
    {
        // ---- 設定 ----

        [Header("追従設定")]
        [SerializeField] private float eyeHeight = 1.6f;
        [SerializeField] private float followSmooth = 12f;

        [Header("視点操作")]
        [SerializeField] private float mouseSensitivity = 2.5f;
        [SerializeField] private float touchSensitivity = 0.3f;
        [SerializeField] private float pitchMin = -60f;
        [SerializeField] private float pitchMax = 70f;

        [Header("ヘッドボブ")]
        [SerializeField] private float bobFrequency = 8f;
        [SerializeField] private float bobAmplitudeY = 0.06f;
        [SerializeField] private float bobAmplitudeX = 0.03f;

        [Header("搭乗揺れ")]
        [SerializeField] private float rideShakeIntensity = 0.15f;
        [SerializeField] private float rideShakeSpeed = 6f;

        [Header("トランジション")]
        [SerializeField] private float transitionDuration = 0.6f;

        // ---- 状態 ----

        private bool _isActive;
        private VisitorAI _targetVisitor;
        private Camera _camera;

        // 保存された元カメラ状態
        private Vector3 _originalPosition;
        private Quaternion _originalRotation;
        private float _originalFov;

        // 視点回転
        private float _yaw;
        private float _pitch;

        // ヘッドボブ
        private float _bobTimer;
        private Vector3 _lastVisitorPos;
        private bool _isVisitorMoving;

        // トランジション
        private float _transitionTimer;
        private bool _transitioning;
        private Vector3 _transitionStartPos;
        private Quaternion _transitionStartRot;
        private float _transitionStartFov;

        // タッチ入力
        private Vector2 _touchPrevPos;
        private bool _touchActive;

        // ---- プロパティ ----

        /// <summary>ファーストパーソンモードが有効か</summary>
        public bool IsActive => _isActive;

        /// <summary>追従中の来場者</summary>
        public VisitorAI TargetVisitor => _targetVisitor;

        // ---- 初期化 ----

        private void Awake()
        {
            _camera = GetComponent<Camera>();
        }

        private void OnEnable()
        {
            GameEvents.OnCameraFollowRequested += OnFollowRequested;
            GameEvents.OnViewModeChanged += OnViewModeChanged;
        }

        private void OnDisable()
        {
            GameEvents.OnCameraFollowRequested -= OnFollowRequested;
            GameEvents.OnViewModeChanged -= OnViewModeChanged;
        }

        // ---- イベントハンドラ ----

        private void OnFollowRequested(int targetId)
        {
            if (GameManager.Instance == null || GameManager.Instance.VisitorManager == null) return;

            var visitor = GameManager.Instance.VisitorManager.FindVisitorById(targetId);
            if (visitor != null && visitor.IsActive)
            {
                EnterFirstPerson(visitor);
            }
        }

        private void OnViewModeChanged(ViewMode mode)
        {
            if (mode != ViewMode.FirstPerson && _isActive)
            {
                ExitFirstPerson();
            }
        }

        // ---- 開始/終了 ----

        /// <summary>
        /// 指定来場者に追従するファーストパーソンモードを開始する。
        /// </summary>
        public void EnterFirstPerson(VisitorAI visitor)
        {
            if (visitor == null || !visitor.IsActive) return;
            if (_camera == null) _camera = GetComponent<Camera>();

            _targetVisitor = visitor;

            // 現在のカメラ状態を保存
            _originalPosition = transform.position;
            _originalRotation = transform.rotation;
            _originalFov = _camera.fieldOfView;

            // 初期視点角度: 来場者の向きに合わせる
            Vector3 fwd = visitor.transform.forward;
            _yaw = Mathf.Atan2(fwd.x, fwd.z) * Mathf.Rad2Deg;
            _pitch = 0f;

            _lastVisitorPos = visitor.transform.position;
            _bobTimer = 0f;
            _isVisitorMoving = false;

            // トランジション開始
            _transitionStartPos = transform.position;
            _transitionStartRot = transform.rotation;
            _transitionStartFov = _camera.fieldOfView;
            _transitionTimer = 0f;
            _transitioning = true;

            _isActive = true;

            // GameManagerに通知
            if (GameManager.Instance != null)
            {
                GameManager.Instance.SwitchViewMode(ViewMode.FirstPerson);
            }

            Debug.Log($"[FirstPersonCamera] Enter: Visitor {visitor.VisitorId} ({visitor.Profile?.VisitorName})");
        }

        /// <summary>
        /// ファーストパーソンモードを終了し、元の視点に復帰する。
        /// </summary>
        public void ExitFirstPerson()
        {
            if (!_isActive) return;

            _isActive = false;
            _targetVisitor = null;

            // トランジション開始（元の位置へ戻る）
            _transitionStartPos = transform.position;
            _transitionStartRot = transform.rotation;
            _transitionStartFov = _camera != null ? _camera.fieldOfView : 60f;
            _transitionTimer = 0f;
            _transitioning = true;

            // GameManagerに通知
            if (GameManager.Instance != null &&
                GameManager.Instance.CurrentState == GameState.FirstPersonMode)
            {
                GameManager.Instance.SwitchViewMode(ViewMode.GodView);
            }

            Debug.Log("[FirstPersonCamera] Exit: returning to God View");
        }

        // ---- Update ----

        private void LateUpdate()
        {
            if (_transitioning)
            {
                UpdateTransition();
                return;
            }

            if (!_isActive) return;

            // ターゲットが無効になった場合は自動終了
            if (_targetVisitor == null || !_targetVisitor.IsActive)
            {
                ExitFirstPerson();
                return;
            }

            // ESCキーで終了
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                ExitFirstPerson();
                return;
            }

            HandleLookInput();
            UpdateCameraPosition();
        }

        // ---- 視点操作 ----

        private void HandleLookInput()
        {
            float dx = 0f, dy = 0f;

            // マウス: 右ドラッグまたは左ドラッグ（UI上でない場合）
            if (Input.GetMouseButton(0) || Input.GetMouseButton(1))
            {
                // UI上のクリックは無視
                if (UnityEngine.EventSystems.EventSystem.current != null &&
                    UnityEngine.EventSystems.EventSystem.current.IsPointerOverGameObject())
                    return;

                dx = Input.GetAxis("Mouse X") * mouseSensitivity;
                dy = Input.GetAxis("Mouse Y") * mouseSensitivity;
            }

            // タッチ入力
            if (Input.touchCount == 1)
            {
                var touch = Input.GetTouch(0);
                if (UnityEngine.EventSystems.EventSystem.current != null &&
                    UnityEngine.EventSystems.EventSystem.current.IsPointerOverGameObject(touch.fingerId))
                    return;

                if (touch.phase == TouchPhase.Began)
                {
                    _touchPrevPos = touch.position;
                    _touchActive = true;
                }
                else if (touch.phase == TouchPhase.Moved && _touchActive)
                {
                    Vector2 delta = touch.position - _touchPrevPos;
                    dx = delta.x * touchSensitivity;
                    dy = delta.y * touchSensitivity;
                    _touchPrevPos = touch.position;
                }
                else if (touch.phase == TouchPhase.Ended || touch.phase == TouchPhase.Canceled)
                {
                    _touchActive = false;
                }
            }

            _yaw += dx;
            _pitch -= dy;
            _pitch = Mathf.Clamp(_pitch, pitchMin, pitchMax);
        }

        // ---- カメラ位置更新 ----

        private void UpdateCameraPosition()
        {
            Vector3 visitorPos = _targetVisitor.transform.position;

            // 来場者の移動検出
            float moveDist = Vector3.Distance(visitorPos, _lastVisitorPos);
            _isVisitorMoving = moveDist > 0.01f;
            _lastVisitorPos = visitorPos;

            // 目線位置（頭上）
            Vector3 eyePos = visitorPos + Vector3.up * eyeHeight;

            // ヘッドボブ（歩行中のみ）
            Vector3 bobOffset = Vector3.zero;
            if (_isVisitorMoving && _targetVisitor.CurrentState != VisitorBehaviorState.RidingAttraction)
            {
                _bobTimer += Time.deltaTime * bobFrequency;
                bobOffset.y = Mathf.Sin(_bobTimer) * bobAmplitudeY;
                bobOffset.x = Mathf.Cos(_bobTimer * 0.5f) * bobAmplitudeX;
            }
            else if (_targetVisitor.CurrentState == VisitorBehaviorState.RidingAttraction)
            {
                // 搭乗中: ランダムな揺れ
                float t = Time.time * rideShakeSpeed;
                bobOffset.x = (Mathf.PerlinNoise(t, 0f) - 0.5f) * rideShakeIntensity;
                bobOffset.y = (Mathf.PerlinNoise(0f, t) - 0.5f) * rideShakeIntensity;
                bobOffset.z = (Mathf.PerlinNoise(t, t) - 0.5f) * rideShakeIntensity * 0.5f;
            }

            // スムーズ追従
            Vector3 targetPos = eyePos + bobOffset;
            transform.position = Vector3.Lerp(transform.position, targetPos, followSmooth * Time.deltaTime);

            // 視点回転
            Quaternion targetRot = Quaternion.Euler(_pitch, _yaw, 0f);
            transform.rotation = targetRot;

            // FOVは通常60、搭乗中は少し広く
            float targetFov = _targetVisitor.CurrentState == VisitorBehaviorState.RidingAttraction ? 75f : 60f;
            if (_camera != null)
            {
                _camera.fieldOfView = Mathf.Lerp(_camera.fieldOfView, targetFov, 4f * Time.deltaTime);
            }
        }

        // ---- トランジション ----

        private void UpdateTransition()
        {
            _transitionTimer += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(_transitionTimer / transitionDuration);
            float smooth = t * t * (3f - 2f * t); // SmoothStep

            if (_isActive && _targetVisitor != null)
            {
                // 入場トランジション: 元の位置 → 来場者目線
                Vector3 eyePos = _targetVisitor.transform.position + Vector3.up * eyeHeight;
                Quaternion targetRot = Quaternion.Euler(_pitch, _yaw, 0f);

                transform.position = Vector3.Lerp(_transitionStartPos, eyePos, smooth);
                transform.rotation = Quaternion.Slerp(_transitionStartRot, targetRot, smooth);
                if (_camera != null)
                    _camera.fieldOfView = Mathf.Lerp(_transitionStartFov, 60f, smooth);
            }
            else
            {
                // 退出トランジション: 現在位置 → 元の位置
                transform.position = Vector3.Lerp(_transitionStartPos, _originalPosition, smooth);
                transform.rotation = Quaternion.Slerp(_transitionStartRot, _originalRotation, smooth);
                if (_camera != null)
                    _camera.fieldOfView = Mathf.Lerp(_transitionStartFov, _originalFov, smooth);
            }

            if (t >= 1f)
            {
                _transitioning = false;
            }
        }

        // ---- ユーティリティ ----

        /// <summary>現在の状態ラベルを返す（HUD表示用）</summary>
        public string GetStatusLabel()
        {
            if (!_isActive || _targetVisitor == null) return "";

            string visitorName = _targetVisitor.Profile?.VisitorName ?? $"#{_targetVisitor.VisitorId}";
            string stateName = GetBehaviorLabel(_targetVisitor.CurrentState);
            return $"{visitorName} - {stateName}";
        }

        private static string GetBehaviorLabel(VisitorBehaviorState state)
        {
            switch (state)
            {
                case VisitorBehaviorState.Idle:                  return "散策中";
                case VisitorBehaviorState.WalkingToAttraction:   return "アトラクションへ移動中";
                case VisitorBehaviorState.WaitingInQueue:        return "行列に並んでいます";
                case VisitorBehaviorState.RidingAttraction:      return "搭乗中!";
                case VisitorBehaviorState.WalkingToShop:         return "ショップへ移動中";
                case VisitorBehaviorState.Eating:                return "食事中";
                case VisitorBehaviorState.Drinking:              return "飲み物を購入中";
                case VisitorBehaviorState.WalkingToToilet:       return "トイレを探しています";
                case VisitorBehaviorState.UsingToilet:           return "トイレ使用中";
                case VisitorBehaviorState.Resting:               return "ベンチで休憩中";
                case VisitorBehaviorState.WatchingEntertainment: return "ショーを鑑賞中";
                case VisitorBehaviorState.Vomiting:              return "気分が悪い...";
                case VisitorBehaviorState.LeavingPark:           return "帰宅中";
                default:                                         return state.ToString();
            }
        }
    }
}
