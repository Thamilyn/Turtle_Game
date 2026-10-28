using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace TurtleGame.Editor
{
    public static class MoonBeachBuilder
    {
        public const string ScenePath = "Assets/Scenes/MoonBeach.unity";
        private const string Folder = "Assets/Environment/MoonBeach";
        private static Terrain terrain;

        [MenuItem("Turtle Game/Crear playa nocturna con Terrain")]
        public static void CreateFromMenu()
        {
            if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                Create();
        }

        public static void Create()
        {
            if (File.Exists(ScenePath) || Directory.Exists(Folder))
                throw new System.InvalidOperationException("MoonBeach already exists. Edit the existing scene and Terrain; generation never overwrites them.");

            Directory.CreateDirectory(Folder);
            AssetDatabase.Refresh();
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var data = new TerrainData
            {
                name = "Beach Terrain", heightmapResolution = 513,
                alphamapResolution = 256, size = new Vector3(100f, 12f, 160f)
            };
            var heights = new float[513, 513];
            for (int z = 0; z < 513; z++)
            {
                for (int x = 0; x < 513; x++)
                {
                    float wx = x / 512f * 100f - 50f;
                    float wz = z / 512f * 160f - 20f;
                    float path = PathX(wz);
                    float distance = Mathf.Abs(wx - path);
                    float dunes = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(3f, 15f, distance));
                    float noise = Mathf.PerlinNoise((wx + 100f) * 0.045f, (wz + 40f) * 0.045f);
                    float shore = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(-12f, 12f, wz));
                    float roadFlatten = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(108f, 120f, wz));
                    float height = 0.35f + shore * 1.1f + dunes * (1.4f + noise * 4.5f) * shore * roadFlatten;
                    heights[z, x] = height / 12f;
                }
            }
            data.SetHeights(0, 0, heights);
            AssetDatabase.CreateAsset(data, Folder + "/BeachTerrain.asset");
            var dry = Layer("DrySand", new Color(0.66f, 0.58f, 0.43f), new Color(0.78f, 0.70f, 0.53f));
            var wet = Layer("WetSand", new Color(0.29f, 0.32f, 0.33f), new Color(0.43f, 0.44f, 0.40f));
            data.terrainLayers = new[] { dry, wet };
            var weights = new float[256, 256, 2];
            for (int z = 0; z < 256; z++)
            {
                float dryWeight = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(-5f, 15f, z / 255f * 160f - 20f));
                for (int x = 0; x < 256; x++)
                {
                    weights[z, x, 0] = dryWeight;
                    weights[z, x, 1] = 1f - dryWeight;
                }
            }
            data.SetAlphamaps(0, 0, weights);
            terrain = Terrain.CreateTerrainGameObject(data).GetComponent<Terrain>();
            terrain.name = "Beach - sculpt and paint here";
            terrain.transform.position = new Vector3(-50f, 0f, -20f);
            terrain.materialTemplate = new Material(Shader.Find("Universal Render Pipeline/Terrain/Lit"));
            AssetDatabase.CreateAsset(terrain.materialTemplate, Folder + "/Terrain.mat");
            terrain.heightmapPixelError = 3f;

            Material rock = MaterialAsset("Stone", new Color(0.29f, 0.32f, 0.38f));
            Material wood = MaterialAsset("Driftwood", new Color(0.29f, 0.23f, 0.19f));
            Material asphalt = MaterialAsset("Asphalt", new Color(0.07f, 0.08f, 0.11f));
            Material metal = MaterialAsset("LampMetal", new Color(0.12f, 0.14f, 0.18f));
            Material sea = MaterialAsset("Ocean", new Color(0.035f, 0.11f, 0.19f));
            sea.SetFloat("_Smoothness", 0.88f);
            Shape("Ocean - visual placeholder", PrimitiveType.Cube, new Vector3(0f, 0.48f, -55f), new Vector3(220f, 0.12f, 80f), sea, false);
            Shape("Coastal road", PrimitiveType.Cube, new Vector3(0f, 1.48f, 124f), new Vector3(100f, 0.12f, 9f), asphalt, true);

            var random = new System.Random(42);
            for (int i = 0; i < 52; i++)
            {
                float z = 8f + (float)random.NextDouble() * 100f;
                float side = i % 2 == 0 ? -1f : 1f;
                float x = PathX(z) + side * (6f + (float)random.NextDouble() * 28f);
                float size = 0.45f + (float)random.NextDouble() * 1.2f;
                var stone = Shape("Dune rock " + i, PrimitiveType.Sphere, Ground(x, z) + Vector3.up * size * 0.2f,
                    new Vector3(size * 1.7f, size, size * 1.2f), rock, true);
                stone.transform.rotation = Quaternion.Euler(i * 13f, i * 47f, i * 7f);
            }
            Shape("Route obstacle - rock", PrimitiveType.Sphere, Ground(PathX(28f), 28f) + Vector3.up * 0.6f, new Vector3(3f, 1.7f, 2.4f), rock, true);
            var log = Shape("Route obstacle - driftwood", PrimitiveType.Cylinder, Ground(PathX(59f), 59f) + Vector3.up * 0.45f, new Vector3(0.8f, 2.6f, 0.8f), wood, true);
            log.transform.rotation = Quaternion.Euler(0f, 22f, 90f);
            Shape("Route obstacle - split passage", PrimitiveType.Sphere, Ground(PathX(85f), 85f) + Vector3.up * 0.7f, new Vector3(4f, 2f, 3f), rock, true);

            RenderSettings.skybox = null;
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.2f, 0.24f, 0.32f);
            RenderSettings.ambientIntensity = 1f;
            RenderSettings.reflectionIntensity = 0f;
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogColor = new Color(0.045f, 0.065f, 0.115f);
            RenderSettings.fogDensity = 0.009f;
            // Broad moonlight keeps the route readable while the floating moon guides the player.
            var moon = new GameObject("Moonlight").AddComponent<Light>();
            moon.type = LightType.Directional;
            moon.color = new Color(0.58f, 0.71f, 1f);
            moon.intensity = 0.55f;
            moon.shadows = LightShadows.Soft;
            moon.transform.rotation = Quaternion.Euler(28f, -130f, 0f);

            Material glow = MaterialAsset("DistantGlow", new Color(1f, 0.83f, 0.58f));
            glow.EnableKeyword("_EMISSION");
            glow.SetColor("_EmissionColor", new Color(1f, 0.8f, 0.48f) * 5f);
            for (int i = -1; i <= 1; i++)
            {
                float x = i * 18f;
                Shape("Road lamp pole", PrimitiveType.Cylinder, new Vector3(x, 5.5f, 130f), new Vector3(0.18f, 4f, 0.18f), metal, true);
                Shape("Distant light source", PrimitiveType.Sphere, new Vector3(x, 9.5f, 130f), new Vector3(1.3f, 0.55f, 1.3f), glow, false);
                var lamp = new GameObject("Warm road light").AddComponent<Light>();
                lamp.type = LightType.Point;
                lamp.transform.position = new Vector3(x, 8.8f, 129f);
                lamp.color = new Color(1f, 0.81f, 0.55f);
                lamp.intensity = 9f;
                lamp.range = 28f;
            }

            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/TurtlePlayer.prefab");
            if (prefab == null)
                throw new System.InvalidOperationException("Missing Assets/Prefabs/TurtlePlayer.prefab");
            var player = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
            player.transform.position = Ground(PathX(5f), 5f) + Vector3.up * 0.12f;
            var modelAsset = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Fbx/tortuguita.fbx");
            if (modelAsset != null)
            {
                Transform visual = player.GetComponent<TurtlePlayerController>().Visual;
                for (int i = visual.childCount - 1; i >= 0; i--)
                    Object.DestroyImmediate(visual.GetChild(i).gameObject);
                var model = (GameObject)PrefabUtility.InstantiatePrefab(modelAsset, scene);
                model.transform.SetParent(visual, false);
                model.transform.localPosition = new Vector3(-0.3535492f, 0.05198106f, 0.44814384f);
                model.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
            }
            var cameraObject = new GameObject("Main Camera");
            cameraObject.tag = "MainCamera";
            var camera = cameraObject.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = RenderSettings.fogColor;
            camera.fieldOfView = 60f;
            camera.farClipPlane = 260f;
            cameraObject.AddComponent<AudioListener>();
            cameraObject.AddComponent<TurtleFollowCamera>().Target = player.transform;

            cameraObject.AddComponent<RoadLightApproach>();
            cameraObject.AddComponent<MoonFairyGuide>();
            cameraObject.transform.position = player.transform.position + new Vector3(0f, 0.621f, -1.281f);
            cameraObject.transform.LookAt(player.transform.position + Vector3.up * 0.43298f);

            EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.SaveAssets();
            Debug.Log("MOON_BEACH_OK: " + ScenePath);
        }

        private static float PathX(float z) => Mathf.Sin(z * 0.055f) * 5f;
        private static Vector3 Ground(float x, float z) => new Vector3(x, terrain.SampleHeight(new Vector3(x, 0f, z)), z);

        private static TerrainLayer Layer(string name, Color dark, Color light)
        {
            var texture = new Texture2D(128, 128) { name = name, wrapMode = TextureWrapMode.Repeat };
            var pixels = new Color[128 * 128];
            for (int z = 0; z < 128; z++)
                for (int x = 0; x < 128; x++)
                    pixels[z * 128 + x] = Color.Lerp(dark, light, Mathf.PerlinNoise(x * 0.19f, z * 0.19f));
            texture.SetPixels(pixels);
            texture.Apply();
            AssetDatabase.CreateAsset(texture, Folder + "/" + name + "Texture.asset");
            var layer = new TerrainLayer { diffuseTexture = texture, tileSize = new Vector2(5f, 5f), smoothness = 0.1f };
            AssetDatabase.CreateAsset(layer, Folder + "/" + name + ".terrainlayer");
            return layer;
        }

        private static Material MaterialAsset(string name, Color color)
        {
            var material = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = name, color = color };
            AssetDatabase.CreateAsset(material, Folder + "/" + name + ".mat");
            return material;
        }

        private static GameObject Shape(string name, PrimitiveType type, Vector3 position, Vector3 scale, Material material, bool collider)
        {
            var item = GameObject.CreatePrimitive(type);
            item.name = name;
            item.transform.position = position;
            item.transform.localScale = scale;
            item.GetComponent<Renderer>().sharedMaterial = material;
            if (!collider)
                Object.DestroyImmediate(item.GetComponent<Collider>());
            return item;
        }
    }
}
