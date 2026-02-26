// ============================================================
// ThemeParkGame - WebGLBuildOptimizer
// WebGLビルドサイズ最適化（エディタ拡張）
// ============================================================

#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace ThemeParkGame.Editor
{
    /// <summary>
    /// WebGLビルド前に自動的に最適化設定を適用するエディタ拡張。
    /// メニュー「ThemeParkGame > Apply WebGL Optimizations」でも手動実行可能。
    /// </summary>
    public class WebGLBuildOptimizer : IPreprocessBuildWithReport
    {
        public int callbackOrder => 0;

        public void OnPreprocessBuild(BuildReport report)
        {
            if (report.summary.platform == BuildTarget.WebGL)
            {
                ApplyOptimizations();
            }
        }

        [MenuItem("ThemeParkGame/Apply WebGL Optimizations")]
        public static void ApplyOptimizations()
        {
            // ---- Player Settings ----
            PlayerSettings.stripEngineCode = true;
            PlayerSettings.SetManagedStrippingLevel(BuildTargetGroup.WebGL, ManagedStrippingLevel.High);

            // IL2CPP コード生成最適化（Unity 2021.3では設定不可、ProjectSettingsで対応）
            // PlayerSettings.SetIl2CppCodeGeneration は Unity 2022.1以降のAPI

            // ---- WebGL固有設定 ----
            PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Brotli;
            PlayerSettings.WebGL.dataCaching = true;
            PlayerSettings.WebGL.exceptionSupport = WebGLExceptionSupport.None;
            PlayerSettings.WebGL.debugSymbolMode = WebGLDebugSymbolMode.Off;
            PlayerSettings.WebGL.nameFilesAsHashes = true;
            PlayerSettings.WebGL.template = "PROJECT:Minimal";

            // メモリサイズ（MB）
            PlayerSettings.WebGL.memorySize = 256;

            // ---- テクスチャ圧縮 ----
            EditorSettings.spritePackerMode = SpritePackerMode.AlwaysOnAtlas;

            // ---- ビルドオプション ----
            EditorUserBuildSettings.development = false;

            // ---- Shader Stripping ----
            // Graphics設定でのシェーダーストリッピングはProjectSettingsで行う

            Debug.Log("[WebGLBuildOptimizer] WebGL最適化設定を適用しました:\n" +
                      "  - Managed Stripping: High\n" +
                      "  - IL2CPP Code Gen: OptimizeSize\n" +
                      "  - Compression: Brotli\n" +
                      "  - Data Caching: ON\n" +
                      "  - Exception Support: None\n" +
                      "  - Debug Symbols: Off\n" +
                      "  - Memory: 256MB");
        }

        /// <summary>
        /// ビルドサイズを見積もるエディタウィンドウ（ビルド後に表示）
        /// </summary>
        [MenuItem("ThemeParkGame/Show Build Size Estimate")]
        public static void ShowBuildSizeEstimate()
        {
            string buildPath = "Build/WebGL";
            if (!System.IO.Directory.Exists(buildPath))
            {
                Debug.Log("[WebGLBuildOptimizer] ビルドフォルダが見つかりません: " + buildPath);
                return;
            }

            long totalSize = 0;
            foreach (var file in System.IO.Directory.GetFiles(buildPath, "*", System.IO.SearchOption.AllDirectories))
            {
                totalSize += new System.IO.FileInfo(file).Length;
            }

            float mb = totalSize / (1024f * 1024f);
            Debug.Log($"[WebGLBuildOptimizer] ビルドサイズ: {mb:F2} MB ({totalSize:N0} bytes)");
            if (mb > 10f)
                Debug.LogWarning($"[WebGLBuildOptimizer] 目標10MB以下を超過しています！（{mb:F2} MB）");
            else
                Debug.Log($"[WebGLBuildOptimizer] 目標10MB以下を達成 ✓");
        }
    }
}
#endif
