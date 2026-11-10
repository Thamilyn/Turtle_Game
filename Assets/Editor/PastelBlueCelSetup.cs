using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Object = UnityEngine.Object;

namespace TurtleGame.Editor
{
    public static class PastelBlueCelSetup
    {
        private const string MaterialPath = "Assets/Materials/PastelBlueCel.mat";
        private const string FeatureName = "Cozy World Style";
        private static readonly string[] RendererPaths =
        {
            "Assets/Settings/PC_Renderer.asset",
            "Assets/Settings/Mobile_Renderer.asset"
        };

        [MenuItem("Turtle Game/Estilo cozy global/Instalar o actualizar")]
        public static void Install()
        {
            var shader = Shader.Find("TurtleGame/Cozy World Style");
            if (shader == null)
                throw new InvalidOperationException("Cozy World Style shader could not be imported.");

            var material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
            if (material == null)
            {
                material = new Material(shader) { name = "CozyWorldStyle" };
                AssetDatabase.CreateAsset(material, MaterialPath);
            }

            foreach (string path in RendererPaths)
            {
                var renderer = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(path);
                if (renderer == null)
                    throw new InvalidOperationException("Missing URP renderer: " + path);

                var feature = renderer.rendererFeatures.OfType<FullScreenPassRendererFeature>()
                    .FirstOrDefault(candidate => candidate.name == FeatureName || candidate.name == "Pastel Blue Cel" || candidate.name == "Cozy Anime Cel" || candidate.name == "Soft Cozy Cel");
                if (feature == null)
                {
                    feature = ScriptableObject.CreateInstance<FullScreenPassRendererFeature>();
                    feature.name = FeatureName;
                    Undo.RegisterCreatedObjectUndo(feature, "Install cozy anime cel style");
                    AssetDatabase.AddObjectToAsset(feature, renderer);
                    Undo.RecordObject(renderer, "Install cozy anime cel style");
                    renderer.rendererFeatures.Add(feature);
                }

                Undo.RecordObject(feature, "Configure cozy anime cel style");
                feature.name = FeatureName;
                feature.passMaterial = material;
                feature.injectionPoint = FullScreenPassRendererFeature.InjectionPoint.AfterRenderingPostProcessing;
                feature.fetchColorBuffer = true;
                feature.requirements = ScriptableRenderPassInput.Depth;
                feature.passIndex = 0;
                feature.bindDepthStencilAttachment = false;
                feature.SetActive(true);
                EditorUtility.SetDirty(feature);

                // Keep URP's recovery map aligned with the saved feature sub-assets.
                var serializedRenderer = new SerializedObject(renderer);
                var map = serializedRenderer.FindProperty("m_RendererFeatureMap");
                map.arraySize = renderer.rendererFeatures.Count;
                for (int i = 0; i < renderer.rendererFeatures.Count; i++)
                {
                    AssetDatabase.TryGetGUIDAndLocalFileIdentifier(renderer.rendererFeatures[i], out _, out long localId);
                    map.GetArrayElementAtIndex(i).longValue = localId;
                }
                serializedRenderer.ApplyModifiedPropertiesWithoutUndo();
                renderer.SetDirty();
                EditorUtility.SetDirty(renderer);
            }

            AssetDatabase.SaveAssets();
            SceneView.RepaintAll();
            Debug.Log("Cozy World Style installed on PC and Mobile. Adjust Assets/Materials/PastelBlueCel.mat.");
        }

        [MenuItem("Turtle Game/Estilo cozy global/Verificar y capturar")]
        public static void VerifyAndCapture()
        {
            if (EditorApplication.isPlaying)
                throw new InvalidOperationException("Run cozy cel verification outside Play Mode.");
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return;

            Install();
            var material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
            int[] featureIds = RendererPaths.Select(path => FindFeature(path).GetInstanceID()).ToArray();
            Install();
            if (!featureIds.SequenceEqual(RendererPaths.Select(path => FindFeature(path).GetInstanceID())))
                throw new InvalidOperationException("Repeated installation duplicated the cel feature.");

            foreach (string path in RendererPaths)
            {
                var feature = FindFeature(path);
                if (feature.passMaterial != material || !feature.isActive || !feature.fetchColorBuffer ||
                    feature.requirements != ScriptableRenderPassInput.Depth)
                    throw new InvalidOperationException("Incorrect fullscreen feature configuration: " + path);
            }

            var previousScenes = EditorSceneManager.GetSceneManagerSetup();
            bool asyncCompilation = ShaderUtil.allowAsyncCompilation;
            var previousPipeline = QualitySettings.renderPipeline;
            try
            {
                ShaderUtil.allowAsyncCompilation = false;
                EditorSceneManager.OpenScene("Assets/Scenes/MoonBeach.unity");
                Camera camera = Camera.main;
                var player = Object.FindFirstObjectByType<TurtlePlayerController>();
                if (camera == null || player == null)
                    throw new InvalidOperationException("Beach camera or player is missing.");

                Directory.CreateDirectory("Logs");
                camera.transform.position = player.transform.position + new Vector3(0f, 0.621f, -1.281f);
                camera.transform.rotation = Quaternion.LookRotation(new Vector3(0f, 0.43298f - 0.621f, 1.281f));

                foreach (string platform in new[] { "PC", "Mobile" })
                {
                    QualitySettings.renderPipeline = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(
                        "Assets/Settings/" + platform + "_RPAsset.asset");
                    var renderer = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(
                        "Assets/Settings/" + platform + "_Renderer.asset");
                    var feature = FindFeature("Assets/Settings/" + platform + "_Renderer.asset");
                    try
                    {
                        feature.SetActive(false);
                        renderer.SetDirty();
                        Color32[] before = Capture(camera, "Logs/cozy-anime-" + platform + "-before.png");
                        feature.SetActive(true);
                        renderer.SetDirty();
                        Color32[] after = Capture(camera, "Logs/cozy-anime-" + platform + "-after.png");
                        VerifyPixels(before, after, platform);
                        VerifySky(camera, renderer, feature, platform);
                    }
                    finally
                    {
                        feature.SetActive(true);
                        renderer.SetDirty();
                    }
                }

                var errors = ShaderUtil.GetShaderMessages(material.shader)
                    .Where(message => message.severity == UnityEditor.Rendering.ShaderCompilerMessageSeverity.Error).ToArray();
                if (errors.Length != 0)
                    throw new InvalidOperationException(string.Join("\n", errors.Select(message => message.message)));
                File.WriteAllText("Logs/cozy-anime-verification-result.txt",
                    "PASS: shader compilation; PC and Mobile render captures; preserved tones; unchanged sky; visible effect; idempotent installation.\n");
                Debug.Log("COZY_ANIME_CEL_VERIFICATION_PASS");
            }
            finally
            {
                QualitySettings.renderPipeline = previousPipeline;
                ShaderUtil.allowAsyncCompilation = asyncCompilation;
                EditorSceneManager.RestoreSceneManagerSetup(previousScenes);
            }
        }

        private static void VerifySky(Camera camera, UniversalRendererData renderer,
            FullScreenPassRendererFeature feature, string platform)
        {
            int previousMask = camera.cullingMask;
            try
            {
                // Render the actual skybox without geometry, so all pixels must
                // bypass the effect regardless of the palette settings.
                camera.cullingMask = 0;
                feature.SetActive(false);
                renderer.SetDirty();
                Color32[] before = Capture(camera, "Logs/cozy-sky-" + platform + "-before.png");
                feature.SetActive(true);
                renderer.SetDirty();
                Color32[] after = Capture(camera, "Logs/cozy-sky-" + platform + "-after.png");
                for (int i = 0; i < before.Length; i++)
                {
                    if (Math.Abs(before[i].r - after[i].r) > 1 ||
                        Math.Abs(before[i].g - after[i].g) > 1 ||
                        Math.Abs(before[i].b - after[i].b) > 1)
                        throw new InvalidOperationException(platform + " sky was altered by the world style.");
                }
            }
            finally
            {
                camera.cullingMask = previousMask;
                feature.SetActive(true);
                renderer.SetDirty();
            }
        }

        private static FullScreenPassRendererFeature FindFeature(string path)
        {
            return AssetDatabase.LoadAssetAtPath<UniversalRendererData>(path).rendererFeatures
                .OfType<FullScreenPassRendererFeature>().Single(feature => feature.name == FeatureName);
        }

        private static Color32[] Capture(Camera camera, string path)
        {
            var target = new RenderTexture(1280, 720, 24);
            var image = new Texture2D(target.width, target.height, TextureFormat.RGB24, false);
            RenderTexture previousActive = RenderTexture.active;
            RenderTexture previousTarget = camera.targetTexture;
            try
            {
                camera.targetTexture = target;
                camera.Render();
                RenderTexture.active = target;
                image.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0);
                image.Apply();
                File.WriteAllBytes(path, image.EncodeToPNG());
                return image.GetPixels32();
            }
            finally
            {
                camera.targetTexture = previousTarget;
                RenderTexture.active = previousActive;
                target.Release();
                Object.DestroyImmediate(target);
                Object.DestroyImmediate(image);
            }
        }

        private static void VerifyPixels(Color32[] before, Color32[] after, string platform)
        {
            int changed = 0;
            long totalDifference = 0;
            int magenta = 0;
            foreach (var pair in before.Zip(after, (a, b) => (before: a, after: b)))
            {
                int difference = Math.Abs(pair.before.r - pair.after.r) + Math.Abs(pair.before.g - pair.after.g) +
                    Math.Abs(pair.before.b - pair.after.b);
                totalDifference += difference;
                if (difference > 2)
                    changed++;
                if (pair.after.r > 240 && pair.after.g < 15 && pair.after.b > 240)
                    magenta++;
            }
            // A soft treatment must be visible without replacing the scene's tones.
            float meanDifference = totalDifference / (after.Length * 3f);
            if (changed < after.Length * 0.02f || meanDifference > 12f || magenta > after.Length * 0.01f)
                throw new InvalidOperationException($"{platform} render failed: changed={changed}, meanDifference={meanDifference}, magenta={magenta}.");
            Debug.Log($"COZY_ANIME_{platform}: changed={changed}, meanDifference={meanDifference}, magenta={magenta}.");
        }
    }
}
