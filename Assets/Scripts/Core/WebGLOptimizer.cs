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
            QualitySettings.lodBias = 0.7f;           // LODを早めに切替
            QualitySettings.maximumLODLevel = 1;       // 最高精度LODをスキップ
            QualitySettings.skinWeights = SkinWeights.TwoBones; // ボーン計算を軽量化
            QualitySettings.asyncUploadTimeSlice = 2;  // テクスチャアップロード最小化
            QualitySettings.asyncUploadBufferSize = 4; // アップロードバッファ4MB

            // 動的バッチングは有効にする（小さいメッシュの結合）
            // staticBatchingはProjectSettingsで有効、dynamicはランタイム設定不可

            // シェーダーの最大LOD（Toonシェーダー LOD 200を使用）
            Shader.globalMaximumLOD = 200;

            Debug.Log("[WebGLOptimizer] WebGL最適化設定を適用しました (v5.0)");
#else
            // エディタ/スタンドアロンではフレームレート60固定
            Application.targetFrameRate = 60;
            // エディタでは影を有効にする
            QualitySettings.shadows = ShadowQuality.HardOnly;
            QualitySettings.shadowDistance = 80f;
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

        /// <summary>
        /// 条件付き警告ログ。WebGLリリースビルドではスキップされる。
        /// Debug.LogWarningの代替として使用する。
        /// </summary>
        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        [System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        public static void LogWarning(string message)
        {
            Debug.LogWarning(message);
        }

        /// <summary>
        /// エラーログ。リリースビルドでも出力される（重大なエラーは常に記録すべき）。
        /// Debug.LogErrorの代替として使用する。WebGLではスタックトレースを省略する。
        /// </summary>
        public static void LogError(string message)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            // WebGLリリースではconsole.errorのみ（スタックトレースは既に無効化済み）
            Debug.LogError(message);
#else
            Debug.LogError(message);
#endif
        }

        /// <summary>
        /// テクスチャメモリ使用量を削減する。
        /// ゲーム起動後に呼び出し、不要なテクスチャを解放する。
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void OptimizeMemory()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            // 未使用アセットを解放
            Resources.UnloadUnusedAssets();

            // GC実行（起動時の1回のみ）
            System.GC.Collect();
            System.GC.WaitForPendingFinalizers();

            Debug.Log("[WebGLOptimizer] メモリ最適化完了");
#endif
        }
    }
}
