using System;
using System.IO;
using System.IO.Compression;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace TurtleGame.Editor
{
    [InitializeOnLoad]
    public static class WebBuildBuilder
    {
        private const string PendingKey = "TurtleGame.PendingWebBuild";
        private static readonly string[] Scenes =
        {
            "Assets/Scenes/Sea_Of_Lights.unity",
            "Assets/Scenes/Ending_Scene.unity",
            "Assets/Scenes/MoonBeach.unity"
        };

        static WebBuildBuilder()
        {
            EditorApplication.update += ContinueBuild;
        }

        [MenuItem("Turtle Game/Build Web para itch.io")]
        public static void Build()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Stop Play Mode before building.");
            if (!BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.WebGL, BuildTarget.WebGL))
                throw new InvalidOperationException("Install Web Build Support for Unity 6000.3.23f1 first.");
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            foreach (string scene in Scenes)
                if (!File.Exists(scene)) throw new FileNotFoundException("Missing build scene", scene);
            SessionState.SetBool(PendingKey, true);
            if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.WebGL &&
                !EditorUserBuildSettings.SwitchActiveBuildTargetAsync(BuildTargetGroup.WebGL, BuildTarget.WebGL))
            {
                SessionState.SetBool(PendingKey, false);
                throw new InvalidOperationException("Could not switch to Web.");
            }
        }

        private static void ContinueBuild()
        {
            if (!SessionState.GetBool(PendingKey, false) || EditorApplication.isCompiling ||
                EditorApplication.isUpdating || EditorUserBuildSettings.activeBuildTarget != BuildTarget.WebGL)
                return;
            SessionState.SetBool(PendingKey, false);
            BuildNow();
        }

        public static void BuildBatch()
        {
            if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.WebGL)
                throw new InvalidOperationException("Launch Unity with -buildTarget WebGL.");
            BuildNow();
        }

        private static void BuildNow()
        {
            Directory.CreateDirectory("Logs");
            string stamp = DateTime.Now.ToString("yyyyMMdd-HHmmss");
            string output = Path.GetFullPath("Builds/SeaOfLights-Web-" + stamp);
            var previousCompression = PlayerSettings.WebGL.compressionFormat;
            bool previousFallback = PlayerSettings.WebGL.decompressionFallback;
            bool succeeded = false;
            try
            {
                PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Gzip;
                PlayerSettings.WebGL.decompressionFallback = true;
                BuildReport report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
                {
                    scenes = Scenes,
                    locationPathName = output,
                    target = BuildTarget.WebGL,
                    options = BuildOptions.None
                });
                if (report.summary.result != BuildResult.Succeeded)
                    throw new InvalidOperationException("Web build failed: " + report.summary.result +
                        "; errors: " + report.summary.totalErrors);
                if (!File.Exists(Path.Combine(output, "index.html")) ||
                    !File.Exists(Path.Combine(output, "StreamingAssets/GameExit/index.html")))
                    throw new InvalidOperationException("Build is missing its entry or game exit page.");
                string archive = output + "-itchio.zip";
                ZipFile.CreateFromDirectory(output, archive, System.IO.Compression.CompressionLevel.Optimal, false);
                string result = "PASS\nBuild: " + output + "\nZIP: " + archive +
                    "\nSize: " + report.summary.totalSize + " bytes\nDuration: " + report.summary.totalTime;
                File.WriteAllText("Logs/web-build-result.txt", result);
                Debug.Log(result);
                succeeded = true;
            }
            catch (Exception exception)
            {
                File.WriteAllText("Logs/web-build-result.txt", "FAIL\n" + exception);
                Debug.LogException(exception);
            }
            finally
            {
                PlayerSettings.WebGL.compressionFormat = previousCompression;
                PlayerSettings.WebGL.decompressionFallback = previousFallback;
                if (Application.isBatchMode) EditorApplication.Exit(succeeded ? 0 : 1);
            }
        }
    }
}
