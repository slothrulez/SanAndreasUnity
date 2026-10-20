using System;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Kochi.EditorTools
{
    // Builds a standalone player containing only the Kochi prototype scene.
    // Command line: Unity -quit -projectPath . -executeMethod Kochi.EditorTools.KochiBuild.BuildLinux [-kochiBuildPath <dir>]
    public static class KochiBuild
    {
        public const string ScenePath = "Assets/Kochi/Scenes/KochiPrototype.unity";
        public const string ProductName = "Kochi Prototype";

        [MenuItem("Kochi/Build Prototype (Linux)")]
        public static void BuildLinux() => ExitIfCommandLine(Build(BuildTarget.StandaloneLinux64, "Builds/KochiLinux/KochiPrototype.x86_64"));

        [MenuItem("Kochi/Build Prototype (Windows)")]
        public static void BuildWindows() => ExitIfCommandLine(Build(BuildTarget.StandaloneWindows64, "Builds/KochiWindows/KochiPrototype.exe"));

        [MenuItem("Kochi/Build Prototype (WebGL)")]
        public static void BuildWebGL()
        {
            // Uncompressed output can be served by any static file server.
            var originalCompression = PlayerSettings.WebGL.compressionFormat;
            var originalLinkerTarget = PlayerSettings.WebGL.linkerTarget;
            PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Disabled;
            PlayerSettings.WebGL.linkerTarget = WebGLLinkerTarget.Wasm;
            bool succeeded;
            try
            {
                succeeded = Build(BuildTarget.WebGL, "Builds/KochiWebGL");
            }
            finally
            {
                PlayerSettings.WebGL.compressionFormat = originalCompression;
                PlayerSettings.WebGL.linkerTarget = originalLinkerTarget;
            }
            ExitIfCommandLine(succeeded);
        }

        static bool Build(BuildTarget target, string defaultPath)
        {
            string path = GetArg("-kochiBuildPath") ?? defaultPath;
            string originalProductName = PlayerSettings.productName;
            PlayerSettings.productName = ProductName;
            var options = new BuildPlayerOptions
            {
                scenes = new[] { ScenePath },
                locationPathName = path,
                target = target,
                options = BuildOptions.None,
            };
            BuildReport report;
            try
            {
                report = BuildPipeline.BuildPlayer(options);
            }
            finally
            {
                PlayerSettings.productName = originalProductName;
            }
            Debug.Log($"[KochiBuild] {report.summary.result} -> {path} ({report.summary.totalErrors} errors)");
            return report.summary.result == BuildResult.Succeeded;
        }

        static void ExitIfCommandLine(bool succeeded)
        {
            if (Application.isBatchMode || Environment.GetCommandLineArgs().Contains("-quit"))
                EditorApplication.Exit(succeeded ? 0 : 1);
        }

        static string GetArg(string name)
        {
            string[] args = Environment.GetCommandLineArgs();
            int i = Array.IndexOf(args, name);
            return i >= 0 && i + 1 < args.Length ? args[i + 1] : null;
        }
    }
}
