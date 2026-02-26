// ============================================================
// ThemeParkGame - WebGL Builder (Editor Only)
// CI/CD 用の軽量 WebGL ビルドスクリプト
// GitHub Actions から -executeMethod で呼び出される
// ============================================================

using System;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace ThemeParkGame.Editor
{
    public static class WebGLBuilder
    {
        /// <summary>
        /// CI/CD から呼び出される WebGL ビルドメソッド。
        /// -executeMethod ThemeParkGame.Editor.WebGLBuilder.Build
        /// </summary>
        public static void Build()
        {
            Debug.Log("[WebGLBuilder] Starting WebGL build...");

            // ビルド対象シーンの取得（EditorBuildSettings に登録されたシーン）
            string[] scenes = EditorBuildSettings.scenes
                .Where(s => s.enabled)
                .Select(s => s.path)
                .ToArray();

            if (scenes.Length == 0)
            {
                Debug.LogWarning("[WebGLBuilder] No scenes in build settings. Using default scene.");
                scenes = new[] { "Assets/Scenes/MainScene.unity" };
            }

            Debug.Log($"[WebGLBuilder] Scenes: {string.Join(", ", scenes)}");

            // ビルド出力先
            string buildPath = GetBuildPath();
            Debug.Log($"[WebGLBuilder] Output: {buildPath}");

            // プレイヤー設定を最適化
            ApplyWebGLOptimizations();

            // ビルドオプション
            var buildOptions = new BuildPlayerOptions
            {
                scenes = scenes,
                locationPathName = buildPath,
                target = BuildTarget.WebGL,
                options = BuildOptions.None // Development Build 無効
            };

            // ビルド実行
            BuildReport report = BuildPipeline.BuildPlayer(buildOptions);
            BuildSummary summary = report.summary;

            Debug.Log($"[WebGLBuilder] Result: {summary.result}");
            Debug.Log($"[WebGLBuilder] Total size: {summary.totalSize / (1024 * 1024)} MB");
            Debug.Log($"[WebGLBuilder] Total time: {summary.totalTime}");
            Debug.Log($"[WebGLBuilder] Warnings: {summary.totalWarnings}");
            Debug.Log($"[WebGLBuilder] Errors: {summary.totalErrors}");

            if (summary.result != BuildResult.Succeeded)
            {
                Debug.LogError($"[WebGLBuilder] Build failed: {summary.result}");
                EditorApplication.Exit(1);
                return;
            }

            Debug.Log("[WebGLBuilder] Build succeeded!");
            EditorApplication.Exit(0);
        }

        /// <summary>WebGL ビルド用にプレイヤー設定を最適化する</summary>
        private static void ApplyWebGLOptimizations()
        {
            // Scripting Backend: IL2CPP (WebGL では必須)
            PlayerSettings.SetScriptingBackend(
                BuildTargetGroup.WebGL, ScriptingImplementation.IL2CPP);

            // Managed Stripping Level: High (ビルドサイズ削減)
            PlayerSettings.SetManagedStrippingLevel(
                BuildTargetGroup.WebGL, ManagedStrippingLevel.High);

            // Strip Engine Code: 有効
            PlayerSettings.stripEngineCode = true;

            // IL2CPP Compiler Configuration: Release
            PlayerSettings.SetIl2CppCompilerConfiguration(
                BuildTargetGroup.WebGL, Il2CppCompilerConfiguration.Release);

            // WebGL 固有設定
            PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Disabled;
            PlayerSettings.WebGL.exceptionSupport = WebGLExceptionSupport.ExplicitlyThrownExceptionsOnly;
            PlayerSettings.WebGL.memorySize = 512;
            PlayerSettings.WebGL.linkerTarget = WebGLLinkerTarget.Wasm;
            PlayerSettings.WebGL.threadsSupport = false;
            PlayerSettings.WebGL.decompressionFallback = false;

#if UNITY_2021_2_OR_NEWER
            PlayerSettings.WebGL.powerPreference = WebGLPowerPreference.HighPerformance;
#endif

            Debug.Log("[WebGLBuilder] Applied WebGL optimizations:");
            Debug.Log("  - Scripting Backend: IL2CPP");
            Debug.Log("  - Stripping Level: High");
            Debug.Log("  - Strip Engine Code: ON");
            Debug.Log("  - Compression: Disabled");
            Debug.Log("  - Exception Support: ExplicitlyThrownOnly");
            Debug.Log("  - Memory Size: 512 MB");
            Debug.Log("  - Linker Target: Wasm");
            Debug.Log("  - Decompression Fallback: ON");
        }

        /// <summary>コマンドライン引数からビルド出力先を取得する</summary>
        private static string GetBuildPath()
        {
            string[] args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++)
            {
                if (args[i] == "-buildPath")
                    return args[i + 1];
            }

            // デフォルトパス (game-ci の buildsPath/WebGL/ThemeParkGame と整合)
            return "build/WebGL/ThemeParkGame";
        }
    }
}
