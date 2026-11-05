using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace TurtleGame.Editor
{
    /// <summary>Applies BOXOPHOBIC's night cubemap to the existing beach.</summary>
    public static class NightSkySetup
    {
        private const string MaterialPath = "Assets/Materials/BlueNightSky.mat";
        private const string CubemapPath = "Assets/BOXOPHOBIC/Skybox Cubemap Extended/Demo/Textures/Polyverse Skies - Night Sky.exr";

        public static Material CreateNightMaterial()
        {
            var shader = Shader.Find("Skybox/Cubemap Extended");
            var cubemap = AssetDatabase.LoadAssetAtPath<Cubemap>(CubemapPath);
            if (shader == null || cubemap == null)
                throw new InvalidOperationException("Import the BOXOPHOBIC skybox shader and night cubemap first.");

            var material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
            if (material == null)
            {
                material = new Material(shader) { name = "BlueNightSky" };
                AssetDatabase.CreateAsset(material, MaterialPath);
                material.SetTexture("_Tex", cubemap);
                material.SetFloat("_BlueNight", 1f);
                material.EnableKeyword("_BLUENIGHT_ON");
                material.SetColor("_NightZenithColor", new Color(0.035f, 0.07f, 0.18f, 1f));
                material.SetColor("_NightHorizonColor", new Color(0.08f, 0.16f, 0.32f, 1f));
                material.SetFloat("_NightStarIntensity", 3f);
                material.SetFloat("_Exposure", 1f);
                material.SetFloat("_EnableRotation", 0f);
                material.DisableKeyword("_ENABLEROTATION_ON");
                // A narrow horizon haze blends into the beach without hiding the stars above it.
                material.SetFloat("_EnableFog", 1f);
                material.EnableKeyword("_ENABLEFOG_ON");
                material.SetFloat("_FogIntensity", 0.65f);
                material.SetFloat("_FogHeight", 0.12f);
                material.SetFloat("_FogSmoothness", 0.45f);
                material.SetFloat("_FogFill", 0f);
                material.SetFloat("_FogPosition", 0f);
                EditorUtility.SetDirty(material);
            }
            return material;
        }

        [MenuItem("Turtle Game/Cielo nocturno azul/Aplicar en MoonBeach")]
        public static void Install()
        {
            if (EditorApplication.isPlaying)
                throw new InvalidOperationException("Apply the night sky outside Play Mode.");

            Scene scene = SceneManager.GetActiveScene();
            if (scene.path != MoonBeachBuilder.ScenePath)
            {
                if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                    return;
                scene = EditorSceneManager.OpenScene(MoonBeachBuilder.ScenePath);
            }

            RenderSettings.skybox = CreateNightMaterial();
            // Keep the existing moonlight and route lighting; only replace the flat sky.
            foreach (var camera in scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<Camera>(true)))
            {
                if (camera.clearFlags != CameraClearFlags.SolidColor && camera.clearFlags != CameraClearFlags.Skybox)
                    continue;
                Undo.RecordObject(camera, "Apply blue night sky");
                camera.clearFlags = CameraClearFlags.Skybox;
            }
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            SceneView.RepaintAll();
            Debug.Log("BLUE_NIGHT_SKY_INSTALLED");
        }

        [MenuItem("Turtle Game/Cielo nocturno azul/Verificar y capturar")]
        public static void VerifyAndCapture()
        {
            Install();
            var material = RenderSettings.skybox;
            Camera camera = Camera.main;
            if (camera == null || camera.clearFlags != CameraClearFlags.Skybox)
                throw new InvalidOperationException("The beach camera must render the skybox.");

            var previousPipeline = QualitySettings.renderPipeline;
            bool previousAsync = ShaderUtil.allowAsyncCompilation;
            int previousMask = camera.cullingMask;
            Quaternion previousRotation = camera.transform.rotation;
            Vector3 previousPosition = camera.transform.position;
            try
            {
                ShaderUtil.allowAsyncCompilation = false;
                Directory.CreateDirectory("Logs");
                var player = Object.FindFirstObjectByType<TurtlePlayerController>();
                if (player == null)
                    throw new InvalidOperationException("The beach player is missing.");
                camera.transform.position = player.transform.position + new Vector3(0f, 0.621f, -1.281f);
                camera.transform.rotation = Quaternion.LookRotation(new Vector3(0f, 0.43298f - 0.621f, 1.281f));
                Quaternion gameplayRotation = camera.transform.rotation;

                foreach (string platform in new[] { "PC", "Mobile" })
                {
                    var pipeline = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(
                        "Assets/Settings/" + platform + "_RPAsset.asset");
                    if (pipeline == null)
                        throw new InvalidOperationException("Missing " + platform + " render pipeline.");
                    QualitySettings.renderPipeline = pipeline;
                    camera.cullingMask = previousMask;
                    camera.transform.rotation = gameplayRotation;
                    Capture(camera, "Logs/blue-night-" + platform + "-beach.png");
                    camera.cullingMask = 0;
                    camera.transform.rotation = Quaternion.Euler(-28f, 0f, 0f);
                    Color32[] pixels = Capture(camera, "Logs/blue-night-" + platform + "-sky.png");
                    VerifySky(pixels, platform);
                }

                var errors = ShaderUtil.GetShaderMessages(material.shader)
                    .Where(message => message.severity == UnityEditor.Rendering.ShaderCompilerMessageSeverity.Error);
                if (errors.Any())
                    throw new InvalidOperationException(string.Join("\n", errors.Select(message => message.message)));
                File.WriteAllText("Logs/blue-night-verification-result.txt",
                    "PASS: BOXOPHOBIC night cubemap; saved scene and skybox camera; PC and Mobile renders; blue sky and bright stars; shader compilation.\n");
                Debug.Log("BLUE_NIGHT_SKY_VERIFICATION_PASS");
            }
            finally
            {
                camera.cullingMask = previousMask;
                camera.transform.SetPositionAndRotation(previousPosition, previousRotation);
                QualitySettings.renderPipeline = previousPipeline;
                ShaderUtil.allowAsyncCompilation = previousAsync;
            }
        }

        private static Color32[] Capture(Camera camera, string path)
        {
            var target = new RenderTexture(1280, 720, 24);
            var image = new Texture2D(target.width, target.height, TextureFormat.RGB24, false);
            var previousActive = RenderTexture.active;
            var previousTarget = camera.targetTexture;
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

        private static void VerifySky(Color32[] pixels, string platform)
        {
            int blue = pixels.Count(pixel => pixel.b > pixel.r * 1.3f && pixel.b > pixel.g);
            int stars = pixels.Count(pixel => pixel.b > 180 && pixel.g > 100);
            int dark = pixels.Count(pixel => pixel.r < 55 && pixel.g < 85 && pixel.b < 150);
            Debug.Log($"BLUE_NIGHT_{platform}: blue={blue}, stars={stars}, dark={dark} / {pixels.Length}");
            if (blue < pixels.Length * 0.8f || stars < 20 || dark < pixels.Length * 0.5f)
                throw new InvalidOperationException(platform + " sky must be blue and dark, with visible bright stars.");
        }
    }
}
