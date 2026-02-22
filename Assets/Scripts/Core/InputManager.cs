// ============================================================
// ThemeParkGame - InputManager
// タッチ入力管理システム（スマホ向け）
// ============================================================

using System;
using UnityEngine;
using UnityEngine.EventSystems;

namespace ThemeParkGame.Core
{
    /// <summary>
    /// スマートフォン向けタッチ入力を管理するマネージャー。
    /// タップ・ドラッグ・ピンチ操作を検出し、カメラ制御や
    /// 施設選択などのゲーム操作に変換する。
    /// </summary>
    public class InputManager : MonoBehaviour
    {
        public static InputManager Instance { get; private set; }

        [Header("Touch Settings")]
        [SerializeField] private float tapThreshold = 0.3f;
        [SerializeField] private float dragThreshold = 10f;
        [SerializeField] private float pinchZoomSpeed = 0.05f;
        [SerializeField] private float cameraPanSpeed = 0.5f;

        // カメラ制御
        [Header("Camera Bounds")]
        [SerializeField] private float minZoom = 5f;
        [SerializeField] private float maxZoom = 50f;
        [SerializeField] private float minCameraX = -100f;
        [SerializeField] private float maxCameraX = 100f;
        [SerializeField] private float minCameraZ = -100f;
        [SerializeField] private float maxCameraZ = 100f;

        // タッチ状態
        private float touchStartTime;
        private Vector2 touchStartPosition;
        private bool isDragging;
        private float previousPinchDistance;

        // カメラ参照
        private Camera mainCamera;

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
        }

        private void Update()
        {
            if (GameManager.Instance != null && GameManager.Instance.IsPaused) return;

#if UNITY_EDITOR
            HandleMouseInput();
#else
            HandleTouchInput();
#endif
        }

        /// <summary>タッチ入力を処理する（モバイル向け）</summary>
        private void HandleTouchInput()
        {
            if (Input.touchCount == 0) return;

            // UI上のタッチは無視
            if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject(Input.GetTouch(0).fingerId))
                return;

            if (Input.touchCount == 1)
            {
                HandleSingleTouch(Input.GetTouch(0));
            }
            else if (Input.touchCount == 2)
            {
                HandlePinchZoom(Input.GetTouch(0), Input.GetTouch(1));
            }
        }

        /// <summary>シングルタッチ（タップ/ドラッグ）</summary>
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
                    {
                        ProcessTap(touch.position);
                    }
                    break;
            }
        }

        /// <summary>ピンチズーム操作</summary>
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
                ApplyCameraZoom(-zoomDelta);
                OnPinchZoom?.Invoke(zoomDelta);
            }
        }

        /// <summary>マウス入力を処理する（エディタ向け）</summary>
        private void HandleMouseInput()
        {
            if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
                return;

            // 左クリック（タップ）
            if (Input.GetMouseButtonDown(0))
            {
                touchStartTime = Time.unscaledTime;
                touchStartPosition = Input.mousePosition;
                isDragging = false;
            }
            else if (Input.GetMouseButton(0))
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
                if (!isDragging && (Time.unscaledTime - touchStartTime) < tapThreshold)
                {
                    ProcessTap(Input.mousePosition);
                }
            }

            // マウスホイール（ズーム）
            float scroll = Input.GetAxis("Mouse ScrollWheel");
            if (Mathf.Abs(scroll) > 0.01f)
            {
                ApplyCameraZoom(-scroll * 10f);
            }
        }

        /// <summary>タップ位置のRaycastで対象を特定する</summary>
        private void ProcessTap(Vector2 screenPosition)
        {
            if (mainCamera == null) return;

            Ray ray = mainCamera.ScreenPointToRay(screenPosition);
            if (Physics.Raycast(ray, out RaycastHit hit, 200f))
            {
                OnTap?.Invoke(hit.point);
                OnObjectTapped?.Invoke(hit.collider.gameObject);

                // アトラクション/施設を選択
                var facility = hit.collider.GetComponent<ThemeParkGame.Attraction.FacilityBase>();
                if (facility != null)
                {
                    GameEvents.FireFacilitySelected(facility.FacilityId);
                    return;
                }

                // 来場者を選択
                var visitorAI = hit.collider.GetComponent<ThemeParkGame.Visitor.VisitorAI>();
                if (visitorAI != null)
                {
                    GameEvents.FireVisitorSelected(visitorAI.VisitorId);
                    return;
                }
            }
        }

        /// <summary>カメラパン移動を適用する</summary>
        private void HandleCameraPan(Vector2 delta)
        {
            if (mainCamera == null) return;

            Vector3 move = new Vector3(-delta.x * cameraPanSpeed, 0f, -delta.y * cameraPanSpeed);
            Vector3 newPos = mainCamera.transform.position + move;

            newPos.x = Mathf.Clamp(newPos.x, minCameraX, maxCameraX);
            newPos.z = Mathf.Clamp(newPos.z, minCameraZ, maxCameraZ);

            mainCamera.transform.position = newPos;
        }

        /// <summary>カメラズームを適用する</summary>
        private void ApplyCameraZoom(float delta)
        {
            if (mainCamera == null) return;

            if (mainCamera.orthographic)
            {
                mainCamera.orthographicSize = Mathf.Clamp(
                    mainCamera.orthographicSize + delta, minZoom, maxZoom);
            }
            else
            {
                Vector3 pos = mainCamera.transform.position;
                pos.y = Mathf.Clamp(pos.y + delta, minZoom, maxZoom);
                mainCamera.transform.position = pos;
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
    }
}
