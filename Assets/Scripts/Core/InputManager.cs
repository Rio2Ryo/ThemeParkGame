// ============================================================
// ThemeParkGame - InputManager
// 入力管理システム（マウス＋タッチ対応、WebGL/モバイル両対応）
// カメラ操作: ドラッグパン / スクロールズーム / 右ドラッグ回転
// エッジスクロール / キーボードショートカット対応
// ============================================================

using System;
using UnityEngine;
using UnityEngine.EventSystems;

namespace ThemeParkGame.Core
{
    /// <summary>
    /// マウス＋タッチ入力を管理するマネージャー。
    /// 左ドラッグ: パン移動 / 右ドラッグ: カメラ回転 / ホイール: ズーム
    /// WASD/矢印キー: パン移動 / QE: 回転 / エッジスクロール対応
    /// </summary>
    public class InputManager : MonoBehaviour
    {
        public static InputManager Instance { get; private set; }

        [Header("Camera Pan")]
        [SerializeField] private float cameraPanSpeed = 0.5f;
        [SerializeField] private float keyboardPanSpeed = 30f;
        [SerializeField] private float edgeScrollSpeed = 20f;
        [SerializeField] private float edgeScrollMargin = 10f;
        [SerializeField] private bool enableEdgeScroll = true;

        [Header("Camera Zoom")]
        [SerializeField] private float scrollZoomSpeed = 5f;
        [SerializeField] private float zoomSmoothTime = 0.12f;
        [SerializeField] private float minZoom = 5f;
        [SerializeField] private float maxZoom = 50f;
        [SerializeField] private float pinchZoomSpeed = 0.05f;

        [Header("Camera Rotate")]
        [SerializeField] private float rotateSpeed = 3f;
        [SerializeField] private float keyboardRotateSpeed = 90f;

        [Header("Camera Bounds")]
        [SerializeField] private float minCameraX = -100f;
        [SerializeField] private float maxCameraX = 100f;
        [SerializeField] private float minCameraZ = -100f;
        [SerializeField] private float maxCameraZ = 100f;

        [Header("Touch Settings")]
        [SerializeField] private float tapThreshold = 0.3f;
        [SerializeField] private float dragThreshold = 10f;

        // カメラ参照
        private Camera mainCamera;

        // マウス/タッチ状態
        private float touchStartTime;
        private Vector2 touchStartPosition;
        private bool isDragging;
        private float previousPinchDistance;

        // ズームスムージング
        private float targetZoomY;
        private float zoomVelocity;

        // イベント
        public event Action<Vector3> OnTap;
        public event Action<Vector2> OnDrag;
        public event Action<float> OnPinchZoom;
        public event Action<GameObject> OnObjectTapped;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        private void Start()
        {
            mainCamera = Camera.main;
            if (mainCamera != null)
            {
                targetZoomY = mainCamera.orthographic
                    ? mainCamera.orthographicSize
                    : mainCamera.transform.position.y;
            }
        }

        private void Update()
        {
            if (GameManager.Instance != null && GameManager.Instance.IsPaused) return;
            if (GameManager.Instance != null &&
                GameManager.Instance.CurrentState == GameState.FirstPersonMode)
                return;

            if (Input.touchCount > 0)
            {
                HandleTouchInput();
            }
            else
            {
                HandleMouseInput();
                HandleKeyboardInput();
                HandleEdgeScroll();
            }

            // スムーズズーム適用
            ApplySmoothZoom();
        }

        // ================================================================
        // マウス入力
        // ================================================================

        private void HandleMouseInput()
        {
            bool overUI = EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();

            // 左クリック: タップ / 左ドラッグ: パン
            if (Input.GetMouseButtonDown(0) && !overUI)
            {
                touchStartTime = Time.unscaledTime;
                touchStartPosition = Input.mousePosition;
                isDragging = false;
            }
            else if (Input.GetMouseButton(0) && !overUI)
            {
                if (Vector2.Distance(Input.mousePosition, touchStartPosition) > dragThreshold)
                {
                    isDragging = true;
                    Vector2 delta = new Vector2(Input.GetAxis("Mouse X"), Input.GetAxis("Mouse Y"));
                    HandleCameraPan(-delta * 20f);
                }
            }
            else if (Input.GetMouseButtonUp(0))
            {
                if (!isDragging && (Time.unscaledTime - touchStartTime) < tapThreshold && !overUI)
                {
                    ProcessTap(Input.mousePosition);
                }
            }

            // 右ドラッグ: カメラ回転
            if (Input.GetMouseButton(1) && !overUI)
            {
                float rotX = Input.GetAxis("Mouse X") * rotateSpeed;
                ApplyCameraRotation(rotX);
            }

            // ミドルドラッグ: パン（代替操作）
            if (Input.GetMouseButton(2) && !overUI)
            {
                Vector2 delta = new Vector2(Input.GetAxis("Mouse X"), Input.GetAxis("Mouse Y"));
                HandleCameraPan(-delta * 20f);
            }

            // マウスホイール: ズーム
            if (!overUI)
            {
                float scroll = Input.GetAxis("Mouse ScrollWheel");
                if (Mathf.Abs(scroll) > 0.01f)
                {
                    targetZoomY += -scroll * scrollZoomSpeed;
                    targetZoomY = Mathf.Clamp(targetZoomY, minZoom, maxZoom);
                }
            }
        }

        // ================================================================
        // キーボード入力
        // ================================================================

        private void HandleKeyboardInput()
        {
            // WASD / 矢印キー: パン
            float h = 0f, v = 0f;
            if (Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow)) v += 1f;
            if (Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow)) v -= 1f;
            if (Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow)) h -= 1f;
            if (Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow)) h += 1f;

            if (h != 0f || v != 0f)
            {
                if (mainCamera == null) return;
                Vector3 forward = mainCamera.transform.forward;
                forward.y = 0f;
                forward.Normalize();
                Vector3 right = mainCamera.transform.right;
                right.y = 0f;
                right.Normalize();

                Vector3 move = (right * h + forward * v) * keyboardPanSpeed * Time.unscaledDeltaTime;
                Vector3 newPos = mainCamera.transform.position + move;
                newPos.x = Mathf.Clamp(newPos.x, minCameraX, maxCameraX);
                newPos.z = Mathf.Clamp(newPos.z, minCameraZ, maxCameraZ);
                mainCamera.transform.position = newPos;
            }

            // Q / E: 回転
            if (Input.GetKey(KeyCode.Q))
                ApplyCameraRotation(-keyboardRotateSpeed * Time.unscaledDeltaTime);
            if (Input.GetKey(KeyCode.E))
                ApplyCameraRotation(keyboardRotateSpeed * Time.unscaledDeltaTime);
        }

        // ================================================================
        // エッジスクロール
        // ================================================================

        private void HandleEdgeScroll()
        {
            if (!enableEdgeScroll || mainCamera == null) return;

            Vector3 mousePos = Input.mousePosition;
            float h = 0f, v = 0f;

            if (mousePos.x <= edgeScrollMargin) h -= 1f;
            else if (mousePos.x >= Screen.width - edgeScrollMargin) h += 1f;
            if (mousePos.y <= edgeScrollMargin) v -= 1f;
            else if (mousePos.y >= Screen.height - edgeScrollMargin) v += 1f;

            if (h != 0f || v != 0f)
            {
                Vector3 forward = mainCamera.transform.forward;
                forward.y = 0f;
                forward.Normalize();
                Vector3 right = mainCamera.transform.right;
                right.y = 0f;
                right.Normalize();

                Vector3 move = (right * h + forward * v) * edgeScrollSpeed * Time.unscaledDeltaTime;
                Vector3 newPos = mainCamera.transform.position + move;
                newPos.x = Mathf.Clamp(newPos.x, minCameraX, maxCameraX);
                newPos.z = Mathf.Clamp(newPos.z, minCameraZ, maxCameraZ);
                mainCamera.transform.position = newPos;
            }
        }

        // ================================================================
        // タッチ入力
        // ================================================================

        private void HandleTouchInput()
        {
            if (Input.touchCount == 0) return;
            if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject(Input.GetTouch(0).fingerId))
                return;

            if (Input.touchCount == 1)
                HandleSingleTouch(Input.GetTouch(0));
            else if (Input.touchCount == 2)
                HandlePinchZoom(Input.GetTouch(0), Input.GetTouch(1));
        }

        private void HandleSingleTouch(Touch touch)
        {
            switch (touch.phase)
            {
                case TouchPhase.Began:
                    touchStartTime = Time.unscaledTime;
                    touchStartPosition = touch.position;
                    isDragging = false;
                    break;

                case TouchPhase.Moved:
                    if (Vector2.Distance(touch.position, touchStartPosition) > dragThreshold)
                    {
                        isDragging = true;
                        HandleCameraPan(touch.deltaPosition);
                        OnDrag?.Invoke(touch.deltaPosition);
                    }
                    break;

                case TouchPhase.Ended:
                    if (!isDragging && (Time.unscaledTime - touchStartTime) < tapThreshold)
                        ProcessTap(touch.position);
                    break;
            }
        }

        private void HandlePinchZoom(Touch touch0, Touch touch1)
        {
            float currentDistance = Vector2.Distance(touch0.position, touch1.position);

            if (touch0.phase == TouchPhase.Began || touch1.phase == TouchPhase.Began)
            {
                previousPinchDistance = currentDistance;
                return;
            }

            float delta = currentDistance - previousPinchDistance;
            previousPinchDistance = currentDistance;

            if (Mathf.Abs(delta) > 1f)
            {
                float zoomDelta = delta * pinchZoomSpeed;
                targetZoomY = Mathf.Clamp(targetZoomY - zoomDelta, minZoom, maxZoom);
                OnPinchZoom?.Invoke(zoomDelta);
            }
        }

        // ================================================================
        // カメラ操作
        // ================================================================

        private void HandleCameraPan(Vector2 delta)
        {
            if (mainCamera == null) return;

            Vector3 right = mainCamera.transform.right;
            right.y = 0f;
            right.Normalize();
            Vector3 forward = mainCamera.transform.forward;
            forward.y = 0f;
            forward.Normalize();

            Vector3 move = (right * -delta.x + forward * -delta.y) * cameraPanSpeed;
            Vector3 newPos = mainCamera.transform.position + move;

            newPos.x = Mathf.Clamp(newPos.x, minCameraX, maxCameraX);
            newPos.z = Mathf.Clamp(newPos.z, minCameraZ, maxCameraZ);

            mainCamera.transform.position = newPos;
        }

        private void ApplyCameraRotation(float degrees)
        {
            if (mainCamera == null) return;

            // カメラの注視点（地面との交点）を中心に回転
            Vector3 camPos = mainCamera.transform.position;
            Vector3 lookDir = mainCamera.transform.forward;

            // 地面(Y=0)との交点を計算
            float t = -camPos.y / lookDir.y;
            Vector3 pivot = (t > 0f) ? camPos + lookDir * t : new Vector3(camPos.x, 0f, camPos.z);

            mainCamera.transform.RotateAround(pivot, Vector3.up, degrees);
        }

        private void ApplySmoothZoom()
        {
            if (mainCamera == null) return;

            if (mainCamera.orthographic)
            {
                mainCamera.orthographicSize = Mathf.SmoothDamp(
                    mainCamera.orthographicSize, targetZoomY, ref zoomVelocity, zoomSmoothTime);
            }
            else
            {
                Vector3 pos = mainCamera.transform.position;
                pos.y = Mathf.SmoothDamp(pos.y, targetZoomY, ref zoomVelocity, zoomSmoothTime);
                mainCamera.transform.position = pos;
            }
        }

        // ================================================================
        // タップ処理
        // ================================================================

        private void ProcessTap(Vector2 screenPosition)
        {
            if (mainCamera == null) return;

            Ray ray = mainCamera.ScreenPointToRay(screenPosition);
            if (Physics.Raycast(ray, out RaycastHit hit, 200f))
            {
                OnTap?.Invoke(hit.point);
                OnObjectTapped?.Invoke(hit.collider.gameObject);

                var facility = hit.collider.GetComponent<ThemeParkGame.Attraction.FacilityBase>();
                if (facility != null)
                {
                    GameEvents.FireFacilitySelected(facility.FacilityId);
                    return;
                }

                var visitorAI = hit.collider.GetComponent<ThemeParkGame.Visitor.VisitorAI>();
                if (visitorAI != null)
                {
                    GameEvents.FireVisitorSelected(visitorAI.VisitorId);
                    return;
                }
            }
        }

        /// <summary>カメラを指定位置にフォーカスする</summary>
        public void FocusCamera(Vector3 worldPosition)
        {
            if (mainCamera == null) return;
            Vector3 cameraPos = mainCamera.transform.position;
            cameraPos.x = worldPosition.x;
            cameraPos.z = worldPosition.z;
            mainCamera.transform.position = cameraPos;
        }

        // ================================================================
        // スワイプジェスチャー検出（MobileUIOptimizer連携）
        // ================================================================

        /// <summary>スワイプ検出イベント（MobileUIOptimizerから購読される）</summary>
        public event Action<Vector2, float> OnSwipeGesture;

        /// <summary>3本指タップイベント（メニュー展開用）</summary>
        public event Action OnThreeFingerTap;

        private void DetectSwipeGesture(Touch touch)
        {
            if (touch.phase != TouchPhase.Ended) return;
            float duration = Time.unscaledTime - touchStartTime;
            if (duration > 0.5f) return; // 長すぎるスワイプは無視

            Vector2 delta = touch.position - touchStartPosition;
            if (delta.magnitude < 50f) return; // 短すぎるスワイプは無視

            OnSwipeGesture?.Invoke(delta.normalized, delta.magnitude);
        }
    }
}
