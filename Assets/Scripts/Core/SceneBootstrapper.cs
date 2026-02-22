// ============================================================
// ThemeParkGame - Scene Bootstrapper
// ランタイム用のブートストラッパー
// シーンに配置しておくと、必要なManagerが存在しない場合に自動生成する
// ============================================================

using UnityEngine;

namespace ThemeParkGame.Core
{
    /// <summary>
    /// シーン起動時にGameManagerの存在を確認し、
    /// 無ければ自動生成するランタイムブートストラッパー。
    /// エディタでのテストプレイ時にManagerが未配置でもゲームが起動できる。
    /// </summary>
    [DefaultExecutionOrder(-100)]
    public class SceneBootstrapper : MonoBehaviour
    {
        [Header("Bootstrap Settings")]
        [Tooltip("Managerが存在しない場合に自動生成するか")]
        [SerializeField] private bool autoCreateManagers = true;

        [Tooltip("起動時にログを出力するか")]
        [SerializeField] private bool verboseLogging = true;

        private void Awake()
        {
            if (verboseLogging)
                Debug.Log("[SceneBootstrapper] シーン起動チェック開始");

            if (!autoCreateManagers)
            {
                if (verboseLogging)
                    Debug.Log("[SceneBootstrapper] 自動生成OFF - スキップ");
                return;
            }

            EnsureGameManager();
            EnsureAudioManager();
            EnsureInputManager();
            EnsureEventSystem();

            if (verboseLogging)
                Debug.Log("[SceneBootstrapper] シーン起動チェック完了");
        }

        private void EnsureGameManager()
        {
            if (GameManager.Instance != null)
            {
                if (verboseLogging)
                    Debug.Log("[SceneBootstrapper] GameManager 検出済み");
                return;
            }

            var go = new GameObject("GameManager");
            go.AddComponent<GameManager>();
            if (verboseLogging)
                Debug.Log("[SceneBootstrapper] GameManager を自動生成しました");
        }

        private void EnsureAudioManager()
        {
            if (AudioManager.Instance != null)
            {
                if (verboseLogging)
                    Debug.Log("[SceneBootstrapper] AudioManager 検出済み");
                return;
            }

            var go = new GameObject("AudioManager");
            go.AddComponent<AudioManager>();
            if (verboseLogging)
                Debug.Log("[SceneBootstrapper] AudioManager を自動生成しました");
        }

        private void EnsureInputManager()
        {
            if (InputManager.Instance != null)
            {
                if (verboseLogging)
                    Debug.Log("[SceneBootstrapper] InputManager 検出済み");
                return;
            }

            var go = new GameObject("InputManager");
            go.AddComponent<InputManager>();
            if (verboseLogging)
                Debug.Log("[SceneBootstrapper] InputManager を自動生成しました");
        }

        private void EnsureEventSystem()
        {
            var existingES = FindObjectOfType<UnityEngine.EventSystems.EventSystem>();
            if (existingES != null)
            {
                if (verboseLogging)
                    Debug.Log("[SceneBootstrapper] EventSystem 検出済み");
                return;
            }

            var go = new GameObject("EventSystem");
            go.AddComponent<UnityEngine.EventSystems.EventSystem>();
            go.AddComponent<UnityEngine.EventSystems.StandaloneInputModule>();
            if (verboseLogging)
                Debug.Log("[SceneBootstrapper] EventSystem を自動生成しました");
        }
    }
}
