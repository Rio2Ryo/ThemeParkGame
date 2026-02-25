// ============================================================
// ThemeParkGame - WebGLOptimizer
// WebGLビルド向けパフォーマンス最適化
// ============================================================

using UnityEngine;

namespace ThemeParkGame.Core
{
    /// <summary>
    /// WebGLビルド向けのパフォーマンス最適化を適用する。
    /// RuntimeInitializeOnLoadMethodで自動実行される。
    /// </summary>
    public static class WebGLOptimizer
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void ApplyOptimizations()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            // WebGLではフレームレートをブラウザのrequestAnimationFrameに合わせる
            Application.targetFrameRate = -1; // vsync on

            // ログのスタックトレースを無効化（文字列アロケーション削減）
            Application.SetStackTraceLogType(LogType.Log, StackTraceLogType.None);
            Application.SetStackTraceLogType(LogType.Warning, StackTraceLogType.None);

            // クオリティ設定をWebGL用に調整
            QualitySettings.shadows = ShadowQuality.Disable;
            QualitySettings.vSyncCount = 1;
            QualitySettings.antiAliasing = 0;
            QualitySettings.pixelLightCount = 1;
            QualitySettings.masterTextureLimit = 1; // テクスチャ半解像度
            QualitySettings.softParticles = false;
            QualitySettings.realtimeReflectionProbes = false;
            QualitySettings.billboardsFaceCameraPosition = false;

            // GC設定: インクリメンタルGCを推奨
            // （PlayerSettingsで設定すべきだが念のためログ出力）
            Debug.Log("[WebGLOptimizer] WebGL最適化設定を適用しました");
#else
            // エディタ/スタンドアロンではフレームレート60固定
            Application.targetFrameRate = 60;
#endif
        }

        /// <summary>
        /// 条件付きログ出力。WebGLリリースビルドではverboseログをスキップする。
        /// 頻繁に呼ばれる箇所ではDebug.Logの代わりにこちらを使用する。
        /// </summary>
        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        [System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        public static void LogVerbose(string message)
        {
            Debug.Log(message);
        }
    }
}
