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
    public static class WindowsBuildBuilder
    {
        private const string PendingKey = "TurtleGame.PendingWindowsBuild";

        static WindowsBuildBuilder() => EditorApplication.update += ContinueBuild;

        [MenuItem("Turtle Game/Build Windows 64 bits")]
        public static void Build()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Stop Play Mode before building.");
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            SessionState.SetBool(PendingKey, true);
            if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.StandaloneWindows64 &&
                !EditorUserBuildSettings.SwitchActiveBuildTargetAsync(BuildTargetGroup.Standalone,
                    BuildTarget.StandaloneWindows64))
            {
                SessionState.SetBool(PendingKey, false);
                throw new InvalidOperationException("Could not switch to Windows 64-bit.");
            }
        }

        private static void ContinueBuild()
        {
            if (!SessionState.GetBool(PendingKey, false) || EditorApplication.isCompiling ||
                EditorApplication.isUpdating || EditorUserBuildSettings.activeBuildTarget != BuildTarget.StandaloneWindows64)
                return;
            SessionState.SetBool(PendingKey, false);
            BuildBatch();
        }

        public static void BuildBatch()
        {
            Directory.CreateDirectory("Logs");
            File.WriteAllText("Logs/windows-build-result.txt", "RUNNING");
            bool succeeded = false;
            try
            {
                string output = Path.GetFullPath("Builds/SeaOfLights-Windows-" + DateTime.Now.ToString("yyyyMMdd-HHmmss"));
                BuildReport report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
                {
                    scenes = new[]
                    {
                        "Assets/Scenes/Sea_Of_Lights.unity",
                        "Assets/Scenes/Ending_Scene.unity",
                        "Assets/Scenes/MoonBeach.unity"
                    },
                    locationPathName = Path.Combine(output, "SeaOfLights.exe"),
                    target = BuildTarget.StandaloneWindows64,
                    options = BuildOptions.None
                });
                if (report.summary.result != BuildResult.Succeeded || report.summary.totalErrors != 0)
                    throw new InvalidOperationException("Windows build failed: " + report.summary.result +
                        "; errors: " + report.summary.totalErrors);
                foreach (string required in new[] { "SeaOfLights.exe", "UnityPlayer.dll", "SeaOfLights_Data/globalgamemanagers" })
                    if (!File.Exists(Path.Combine(output, required)))
                        throw new FileNotFoundException("Missing Windows build file", required);
                string archive = output + ".zip";
                using (var zip = ZipFile.Open(archive, ZipArchiveMode.Create))
                {
                    foreach (string file in Directory.GetFiles(output, "*", SearchOption.AllDirectories))
                    {
                        string relative = Path.GetRelativePath(output, file).Replace('\\', '/');
                        if (relative.Contains("BurstDebugInformation_DoNotShip/")) continue;
                        zip.CreateEntryFromFile(file, relative, System.IO.Compression.CompressionLevel.Optimal);
                    }
                }
                string result = "PASS\nBuild: " + output + "\nZIP: " + archive +
                    "\nSize: " + report.summary.totalSize + " bytes\nDuration: " + report.summary.totalTime;
                File.WriteAllText("Logs/windows-build-result.txt", result);
                Debug.Log(result);
                succeeded = true;
            }
            catch (Exception exception)
            {
                File.WriteAllText("Logs/windows-build-result.txt", "FAIL\n" + exception);
                Debug.LogException(exception);
            }
            finally
            {
                if (Application.isBatchMode) EditorApplication.Exit(succeeded ? 0 : 1);
            }
        }
    }
}
