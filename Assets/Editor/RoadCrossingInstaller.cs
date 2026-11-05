using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace TurtleGame.Editor
{
    /// <summary>Adds the encounter to the existing beach without regenerating its environment.</summary>
    public static class RoadCrossingInstaller
    {
        private const string CarFolder = "Assets/PROMETEO - Car Controller/";

        [MenuItem("Turtle Game/Instalar automóvil y final en carretera")]
        public static void Install()
        {
            var scene = SceneManager.GetActiveScene();
            if (scene.path != MoonBeachBuilder.ScenePath)
            {
                if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                    return;
                scene = EditorSceneManager.OpenScene(MoonBeachBuilder.ScenePath);
            }
            if (Object.FindFirstObjectByType<RoadCrossingSequence>() != null)
            {
                Debug.Log("ROAD_CROSSING_ALREADY_INSTALLED");
                return;
            }
            var player = Object.FindFirstObjectByType<TurtlePlayerController>();
            var road = GameObject.Find("Coastal road").GetComponent<BoxCollider>();
            var root = new GameObject("Road crossing - car and heartbeat");
            var sequence = root.AddComponent<RoadCrossingSequence>();
            var source = AssetDatabase.LoadAssetAtPath<GameObject>(CarFolder + "Prefabs/Prometheus.prefab");
            var car = (GameObject)PrefabUtility.InstantiatePrefab(source, scene);
            PrefabUtility.UnpackPrefabInstance(car, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            car.name = "Passing automobile";
            car.transform.SetParent(root.transform);
            foreach (var behaviour in car.GetComponentsInChildren<MonoBehaviour>(true))
                Object.DestroyImmediate(behaviour);
            foreach (var collider in car.GetComponentsInChildren<Collider>(true))
                Object.DestroyImmediate(collider);
            foreach (var body in car.GetComponentsInChildren<Rigidbody>(true))
                Object.DestroyImmediate(body);
            Transform effects = car.transform.Find("Effects");
            if (effects != null)
                Object.DestroyImmediate(effects.gameObject);

            var materials = new Dictionary<Material, Material>();
            foreach (var renderer in car.GetComponentsInChildren<Renderer>(true))
            {
                var converted = renderer.sharedMaterials.Select(original => ConvertMaterial(original, materials)).ToArray();
                renderer.sharedMaterials = converted;
            }
            // Put the tires on the road even if the source model's pivot changes.
            Bounds bounds = car.GetComponentInChildren<Renderer>().bounds;
            foreach (var renderer in car.GetComponentsInChildren<Renderer>())
                bounds.Encapsulate(renderer.bounds);
            var visual = new GameObject("Car visual").transform;
            visual.SetParent(car.transform, false);
            foreach (Transform child in car.transform.Cast<Transform>().ToArray())
                if (child != visual)
                    child.SetParent(visual, true);
            visual.localPosition = Vector3.up * -bounds.min.y;
            var wheels = visual.GetComponentsInChildren<MeshRenderer>()
                .Where(renderer => renderer.name.Contains("Wheel")).Select(renderer => renderer.transform).ToArray();

            for (int side = -1; side <= 1; side += 2)
            {
                var lamp = new GameObject("Car headlight").AddComponent<Light>();
                lamp.transform.SetParent(car.transform, false);
                lamp.transform.localPosition = new Vector3(side * 0.62f, 0.65f, bounds.max.z - 0.1f);
                lamp.transform.localRotation = Quaternion.Euler(8f, 0f, 0f);
                lamp.type = LightType.Spot;
                lamp.color = new Color(1f, 0.9f, 0.7f);
                lamp.intensity = 8f;
                lamp.range = 22f;
                lamp.spotAngle = 65f;
            }
            var engine = car.AddComponent<AudioSource>();
            engine.clip = AssetDatabase.LoadAssetAtPath<AudioClip>(CarFolder + "Sounds/CarEngine.wav");
            engine.playOnAwake = false;
            engine.loop = true;
            engine.spatialBlend = 1f;
            engine.minDistance = 5f;
            engine.maxDistance = 45f;
            engine.volume = 0.8f;
            engine.pitch = 1.2f;
            car.transform.SetPositionAndRotation(new Vector3(-25f, road.bounds.max.y, road.bounds.min.z + 1f),
                Quaternion.LookRotation(Vector3.right));
            car.SetActive(false);

            var canvas = new GameObject("Road heartbeat overlay", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
            canvas.transform.SetParent(root.transform, false);
            canvas.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.GetComponent<Canvas>().sortingOrder = 32760;
            var scaler = canvas.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            var image = new GameObject("Black and red pulse", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image))
                .GetComponent<Image>();
            image.transform.SetParent(canvas.transform, false);
            image.rectTransform.anchorMin = Vector2.zero;
            image.rectTransform.anchorMax = Vector2.one;
            image.rectTransform.offsetMin = Vector2.zero;
            image.rectTransform.offsetMax = Vector2.zero;
            image.color = Color.black;
            image.raycastTarget = false;
            image.gameObject.SetActive(false);

            var serialized = new SerializedObject(sequence);
            serialized.FindProperty("player").objectReferenceValue = player;
            serialized.FindProperty("roadSurface").objectReferenceValue = road;
            serialized.FindProperty("car").objectReferenceValue = car.transform;
            serialized.FindProperty("engine").objectReferenceValue = engine;
            serialized.FindProperty("screenOverlay").objectReferenceValue = image;
            serialized.FindProperty("viewingCamera").objectReferenceValue = Camera.main;
            var wheelArray = serialized.FindProperty("wheels");
            wheelArray.arraySize = wheels.Length;
            for (int i = 0; i < wheels.Length; i++)
                wheelArray.GetArrayElementAtIndex(i).objectReferenceValue = wheels[i];
            serialized.ApplyModifiedPropertiesWithoutUndo();
            if (!EditorBuildSettings.scenes.Any(entry => entry.enabled && entry.path == "Assets/Scenes/Ending_Scene.unity"))
                throw new System.InvalidOperationException("Ending_Scene must be enabled in Build Settings.");
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            Debug.Log("ROAD_CROSSING_INSTALLED: " + wheels.Length + " animated wheels; URP materials; Ending_Scene.");
        }

        private static Material ConvertMaterial(Material original, Dictionary<Material, Material> materials)
        {
            if (original == null)
                return null;
            if (materials.TryGetValue(original, out Material existing))
                return existing;
            string path = "Assets/Materials/RoadCar_" + original.name + ".mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                material.SetTexture("_BaseMap", original.mainTexture);
                material.SetColor("_BaseColor", original.color);
                material.SetFloat("_Smoothness", 0.25f);
                if (original.IsKeywordEnabled("_EMISSION"))
                {
                    material.EnableKeyword("_EMISSION");
                    material.SetTexture("_EmissionMap", original.GetTexture("_EmissionMap"));
                    material.SetColor("_EmissionColor", original.GetColor("_EmissionColor"));
                }
                AssetDatabase.CreateAsset(material, path);
            }
            materials.Add(original, material);
            return material;
        }
    }
}
