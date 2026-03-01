// ============================================================
// ThemeParkGame - GameCameraController
// PS1「新テーマパーク」風アイソメトリックカメラコントローラー
// Orthographic投影 + 26.565°/45°固定角度で2:1アイソメトリックを実現
// ============================================================

using UnityEngine;

namespace ThemeParkGame.Core
{
    /// <summary>
    /// PS1「新テーマパーク」風のアイソメトリックカメラコントローラー。
    ///
    /// 【カメラ設定】
    /// - Orthographic投影で2:1アイソメトリックビューを実現
    /// - 固定角度: Pitch 26.565° / Yaw 45° (arctan(0.5) = 26.565°)
    /// - ズーム: マウスホイールでorthographicSizeを5〜25の範囲で調整
    /// - パン: WASD/矢印キーでXZ平面上をアイソメトリック方向に移動
    ///
    /// 【FirstPersonCamera連携】
    /// FirstPersonCameraが有効になると自動的に無効化され、
    /// 復帰時にアイソメトリック設定を復元する。
    /// </summary>
    public class GameCameraController : MonoBehaviour
    {
        // ---- 設定 ----

        [Header("アイソメトリック設定")]
        [SerializeField] private float defaultOrthographicSize = 12f;
        [SerializeField] private float minOrthographicSize = 5f;
        [SerializeField] private float maxOrthographicSize = 25f;

        [Header("パン設定")]
        [SerializeField] private float basePanSpeed = 15f;

        [Header("ズーム設定")]
        [SerializeField] private float zoomSpeed = 3f;
        [SerializeField] private float zoomSmooth = 8f;

        // ---- 固定アイソメトリック角度 ----
        // arctan(0.5) ≈ 26.565° で完璧な2:1アイソメトリックを実現
        private static readonly Quaternion IsometricRotation = Quaternion.Euler(26.565f, 45f, 0f);
        private static readonly Color SkyBlueBackground = new Color(0.53f, 0.81f, 0.92f);

        // ---- 状態 ----

        private Camera _camera;
        private FirstPersonCamera _firstPersonCamera;
        private float _targetOrthographicSize;
        private bool _wasFirstPersonActive;

        // ---- プロパティ ----

        /// <summary>アイソメトリックモードが有効か</summary>
        public bool IsIsometricMode { get; set; } = true;

        /// <summary>現在のズームレベル（orthographicSize）</summary>
        public float CurrentZoom => _camera != null ? _camera.orthographicSize : defaultOrthographicSize;

        // ---- 初期化 ----

        private void Awake()
        {
            _camera = GetComponent<Camera>();
            if (_camera == null)
            {
                WebGLOptimizer.LogError("[GameCameraController] Camera コンポーネントが見つかりません");
                enabled = false;
                return;
            }

            // Orthographic投影に設定
            _camera.orthographic = true;
            _camera.orthographicSize = defaultOrthographicSize;
            _targetOrthographicSize = defaultOrthographicSize;

            // アイソメトリック角度を設定
            transform.rotation = IsometricRotation;

            // 背景色を設定
            _camera.clearFlags = CameraClearFlags.SolidColor;
            _camera.backgroundColor = SkyBlueBackground;

            // FirstPersonCameraコンポーネントを取得
            _firstPersonCamera = GetComponent<FirstPersonCamera>();

            WebGLOptimizer.LogVerbose("[GameCameraController] アイソメトリックカメラ初期化完了");
        }

        // ---- Update ----

        private void Update()
        {
            // FirstPersonCameraが有効な場合は自身を無効化
            if (_firstPersonCamera != null && _firstPersonCamera.IsActive)
            {
                if (!_wasFirstPersonActive)
                {
                    _wasFirstPersonActive = true;
                    IsIsometricMode = false;
                    WebGLOptimizer.LogVerbose("[GameCameraController] FirstPersonモード検出 — アイソメトリック無効化");
                }
                return;
            }

            // FirstPersonモードから復帰した場合
            if (_wasFirstPersonActive)
            {
                _wasFirstPersonActive = false;
                RestoreIsometricSettings();
            }

            if (!IsIsometricMode) return;

            HandlePanInput();
            HandleZoomInput();
            UpdateZoom();
        }

        // ---- パン操作 ----

        private void HandlePanInput()
        {
            float h = 0f;
            float v = 0f;

            // WASD / 矢印キー入力
            if (Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow))    v += 1f;
            if (Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow))  v -= 1f;
            if (Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow))  h -= 1f;
            if (Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow)) h += 1f;

            if (Mathf.Approximately(h, 0f) && Mathf.Approximately(v, 0f)) return;

            // パン速度はズームレベルに比例
            float speed = basePanSpeed * (_camera.orthographicSize / defaultOrthographicSize) * Time.deltaTime;

            // アイソメトリック方向に合わせたXZ平面移動
            // 45°回転しているので、カメラのright/forwardをXZ平面に投影
            Vector3 right = transform.right;
            right.y = 0f;
            right.Normalize();

            Vector3 forward = transform.forward;
            forward.y = 0f;
            forward.Normalize();

            Vector3 move = (right * h + forward * v) * speed;
            transform.position += move;
        }

        // ---- ズーム操作 ----

        private void HandleZoomInput()
        {
            float scroll = Input.GetAxis("Mouse ScrollWheel");
            if (!Mathf.Approximately(scroll, 0f))
            {
                _targetOrthographicSize -= scroll * zoomSpeed;
                _targetOrthographicSize = Mathf.Clamp(_targetOrthographicSize, minOrthographicSize, maxOrthographicSize);
            }
        }

        private void UpdateZoom()
        {
            if (_camera == null) return;

            if (!Mathf.Approximately(_camera.orthographicSize, _targetOrthographicSize))
            {
                _camera.orthographicSize = Mathf.Lerp(
                    _camera.orthographicSize,
                    _targetOrthographicSize,
                    zoomSmooth * Time.deltaTime
                );
            }
        }

        // ---- アイソメトリック設定復元 ----

        /// <summary>
        /// FirstPersonモードから復帰した際にアイソメトリック設定を復元する。
        /// </summary>
        private void RestoreIsometricSettings()
        {
            if (_camera == null) return;

            // Orthographic投影に戻す
            _camera.orthographic = true;
            _camera.orthographicSize = _targetOrthographicSize;

            // アイソメトリック角度を復元
            transform.rotation = IsometricRotation;

            // 背景色を復元
            _camera.clearFlags = CameraClearFlags.SolidColor;
            _camera.backgroundColor = SkyBlueBackground;

            IsIsometricMode = true;

            WebGLOptimizer.LogVerbose("[GameCameraController] アイソメトリック設定を復元");
        }

        // ---- 公開メソッド ----

        /// <summary>カメラを指定ワールド座標にフォーカスする</summary>
        public void FocusOn(Vector3 worldPosition)
        {
            // アイソメトリック視点でのカメラオフセットを計算
            Vector3 offset = IsometricRotation * Vector3.back * 20f;
            transform.position = worldPosition + offset;
            WebGLOptimizer.LogVerbose($"[GameCameraController] フォーカス移動: {worldPosition}");
        }

        /// <summary>ズームレベルを直接設定する</summary>
        public void SetZoom(float orthographicSize)
        {
            _targetOrthographicSize = Mathf.Clamp(orthographicSize, minOrthographicSize, maxOrthographicSize);
            if (_camera != null)
            {
                _camera.orthographicSize = _targetOrthographicSize;
            }
        }
    }
}
