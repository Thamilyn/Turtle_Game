using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace TurtleGame.Editor
{
    /// <summary>Upgrades the three route obstacles without regenerating the beach.</summary>
    public static class RouteObstacleInstaller
    {
        private const string SurvivalFolder = "Assets/Polygon-Lite Survival Collection/";
        private const string WoodMaterialPath = "Assets/Materials/RouteDriftwood_Survival.mat";
        private static readonly string[] Names =
        {
            "Route obstacle - rock", "Route obstacle - driftwood", "Route obstacle - split passage"
        };
        private static readonly string[] PrefabPaths =
        {
            "Assets/PolyOne/Rocks Stylized/Prefabs/SM_Rocks_06.prefab",
            SurvivalFolder + "Prefabs/SM_Log.prefab",
            "Assets/PolyOne/Rocks Stylized/Prefabs/SM_Rocks_03.prefab"
        };

        [MenuItem("Turtle Game/Reemplazar obstáculos del camino")]
        public static void Install()
        {
            Scene scene = OpenBeach();
            if (!scene.IsValid())
                return;
            ReplaceObstacles(scene);
            Verify(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            Debug.Log("ROUTE_OBSTACLES_OK: two PolyOne rocks and one Survival Collection log saved.");
        }

        public static void ReplaceObstacles(Scene scene)
        {
            GameObject[] obstacles = FindObstacles(scene);
            GameObject[] prefabs = PrefabPaths.Select(path => AssetDatabase.LoadAssetAtPath<GameObject>(path)).ToArray();
            if (prefabs.Any(prefab => prefab == null || prefab.GetComponent<MeshFilter>()?.sharedMesh == null))
                throw new InvalidOperationException("A route obstacle prefab or mesh is missing.");
            Terrain terrain = FindTerrain(scene);
            Material wood = LoadWoodMaterial();
            Material stone = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/DuneRocks_PolyOne.mat");
            if (stone == null)
                throw new InvalidOperationException("Install the PolyOne dune rocks first.");

            Undo.IncrementCurrentGroup();
            int group = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Replace route obstacles");
            bool changed = false;
            for (int i = 0; i < obstacles.Length; i++)
            {
                GameObject original = obstacles[i];
                // Keep the original object as an anchor so references and route positions survive.
                Transform existingVisual = FindVisual(original, i);
                if (existingVisual != null &&
                    PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(existingVisual.gameObject) == PrefabPaths[i])
                {
                    if (i != 1 && existingVisual.name != prefabs[i].name)
                    {
                        Undo.RecordObject(existingVisual.gameObject, "Identify SM_Rocks obstacle");
                        existingVisual.name = prefabs[i].name;
                        PrefabUtility.RecordPrefabInstancePropertyModifications(existingVisual.gameObject);
                        changed = true;
                    }
                    continue;
                }
                changed = true;
                Renderer oldRenderer = existingVisual != null ? existingVisual.GetComponent<Renderer>() : original.GetComponent<Renderer>();
                Bounds oldBounds = oldRenderer.bounds;
                Vector3 anchor = existingVisual != null ? oldBounds.center : original.transform.position;
                Vector3 logDirection = original.transform.up;
                float logLength = original.transform.lossyScale.y * 2f;
                Undo.RecordObject(original.transform, "Normalize obstacle anchor");
                original.transform.rotation = Quaternion.identity;
                original.transform.localScale = Vector3.one;
                if (existingVisual != null)
                    Undo.DestroyObjectImmediate(existingVisual.gameObject);

                var visual = (GameObject)PrefabUtility.InstantiatePrefab(prefabs[i], scene);
                Undo.RegisterCreatedObjectUndo(visual, "Create obstacle visual");
                visual.name = i == 1 ? "Pack visual" : prefabs[i].name;
                visual.transform.SetParent(original.transform, false);
                visual.layer = original.layer;
                var filter = visual.GetComponent<MeshFilter>();
                var renderer = visual.GetComponent<MeshRenderer>();
                renderer.sharedMaterial = i == 1 ? wood : stone;

                if (i == 1)
                {
                    Vector3 size = filter.sharedMesh.bounds.size;
                    Vector3 axis = size.x >= size.y && size.x >= size.z ? Vector3.right :
                        size.y >= size.z ? Vector3.up : Vector3.forward;
                    visual.transform.localRotation = Quaternion.FromToRotation(axis, logDirection);
                    visual.transform.localScale = Vector3.one * (logLength / Mathf.Max(size.x, size.y, size.z));
                }
                else
                {
                    visual.transform.localRotation = Quaternion.Euler(0f, i == 0 ? 27f : -18f, 0f);
                    Bounds bounds = renderer.bounds;
                    if (i == 2)
                    {
                        // Keep the rounded rock's proportions inside the previous obstacle's footprint.
                        float scale = Mathf.Min(oldBounds.size.x / bounds.size.x,
                            oldBounds.size.z / bounds.size.z, oldBounds.size.y * 1.1f / bounds.size.y);
                        visual.transform.localScale = Vector3.one * scale;
                    }
                    else
                    {
                        original.transform.localScale = new Vector3(oldBounds.size.x / bounds.size.x,
                            oldBounds.size.y / bounds.size.y, oldBounds.size.z / bounds.size.z);
                    }
                }

                Bounds fitted = renderer.bounds;
                visual.transform.position += new Vector3(anchor.x - fitted.center.x, 0f, anchor.z - fitted.center.z);
                float ground = terrain.SampleHeight(anchor) + terrain.transform.position.y;
                float burial = i == 1 ? 0.07f : 0.15f;
                visual.transform.position += Vector3.up * (ground - renderer.bounds.min.y - burial);

                var collider = visual.GetComponent<MeshCollider>();
                if (collider == null)
                    collider = Undo.AddComponent<MeshCollider>(visual);
                collider.sharedMesh = filter.sharedMesh;
                collider.convex = false;
                collider.isTrigger = false;
                collider.enabled = true;
                foreach (Collider placeholder in original.GetComponents<Collider>())
                    Undo.DestroyObjectImmediate(placeholder);
                if (original.TryGetComponent<MeshRenderer>(out var placeholderRenderer))
                    Undo.DestroyObjectImmediate(placeholderRenderer);
                if (original.TryGetComponent<MeshFilter>(out var placeholderFilter))
                    Undo.DestroyObjectImmediate(placeholderFilter);
                PrefabUtility.RecordPrefabInstancePropertyModifications(visual);
                PrefabUtility.RecordPrefabInstancePropertyModifications(visual.transform);
                PrefabUtility.RecordPrefabInstancePropertyModifications(renderer);
                PrefabUtility.RecordPrefabInstancePropertyModifications(collider);
            }
            Undo.CollapseUndoOperations(group);
            if (changed)
                EditorSceneManager.MarkSceneDirty(scene);
        }

        private static Material LoadWoodMaterial()
        {
            Material material = AssetDatabase.LoadAssetAtPath<Material>(WoodMaterialPath);
            if (material != null)
                return material;
            Material source = AssetDatabase.LoadAssetAtPath<Material>(SurvivalFolder + "Materials/MainMaterialFree.mat");
            Texture texture = AssetDatabase.LoadAssetAtPath<Texture>(SurvivalFolder + "Textures/Polygon_Texture.png");
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (source == null || texture == null || shader == null)
                throw new InvalidOperationException("Missing Survival Collection material, texture or URP shader.");
            material = new Material(shader) { name = "RouteDriftwood_Survival", enableInstancing = true };
            material.SetTexture("_BaseMap", texture);
            material.SetColor("_BaseColor", Color.white);
            material.SetFloat("_Smoothness", 0.15f);
            material.SetFloat("_Metallic", 0f);
            AssetDatabase.CreateAsset(material, WoodMaterialPath);
            return material;
        }

        private static Scene OpenBeach()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Replace or verify obstacles from Edit Mode.");
            Scene scene = SceneManager.GetActiveScene();
            if (scene.path == MoonBeachBuilder.ScenePath)
                return scene;
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return default;
            return EditorSceneManager.OpenScene(MoonBeachBuilder.ScenePath);
        }

        private static GameObject[] FindObstacles(Scene scene)
        {
            Transform[] transforms = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<Transform>(true)).ToArray();
            return Names.Select(name => transforms.Single(item => item.name == name).gameObject).ToArray();
        }

        private static Terrain FindTerrain(Scene scene) => scene.GetRootGameObjects()
            .SelectMany(root => root.GetComponentsInChildren<Terrain>()).First();

        private static Transform FindVisual(GameObject root, int index) =>
            root.transform.Find(Path.GetFileNameWithoutExtension(PrefabPaths[index])) ?? root.transform.Find("Pack visual") ??
            root.transform.Cast<Transform>().FirstOrDefault(child => child.GetComponent<MeshFilter>() != null);

        private static void Verify(Scene scene)
        {
            Physics.SyncTransforms();
            GameObject[] obstacles = FindObstacles(scene);
            Terrain terrain = FindTerrain(scene);
            for (int i = 0; i < obstacles.Length; i++)
            {
                GameObject root = obstacles[i];
                Transform visual = FindVisual(root, i);
                if (visual == null || PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(visual.gameObject) != PrefabPaths[i])
                    throw new InvalidOperationException("Incorrect obstacle prefab: " + root.name);
                var renderer = visual.GetComponent<MeshRenderer>();
                var collider = visual.GetComponent<MeshCollider>();
                if (root.GetComponent<Renderer>() != null || root.GetComponent<Collider>() != null ||
                    collider == null || !collider.enabled || collider.isTrigger ||
                    collider.sharedMesh != visual.GetComponent<MeshFilter>().sharedMesh)
                    throw new InvalidOperationException("Incorrect obstacle collision: " + root.name);
                Material material = renderer.sharedMaterial;
                if (material == null || material.shader.name != "Universal Render Pipeline/Lit" || material.GetTexture("_BaseMap") == null)
                    throw new InvalidOperationException("Missing textured URP material: " + root.name);
                Bounds bounds = renderer.bounds;
                float ground = terrain.SampleHeight(bounds.center) + terrain.transform.position.y;
                if (bounds.min.y > ground + 0.02f || bounds.max.y < ground + 0.3f)
                    throw new InvalidOperationException("Obstacle floats or is buried: " + root.name);
                Vector3 origin = new Vector3(bounds.center.x, ground + 0.3f, bounds.min.z - 2f);
                if (!collider.Raycast(new Ray(origin, Vector3.forward), out _, bounds.size.z + 4f))
                    throw new InvalidOperationException("Obstacle does not block the approach: " + root.name);
                Debug.Log("ROUTE_OBSTACLE_VERIFIED: " + root.name + "; bounds " + bounds + "; prefab " + PrefabPaths[i]);
            }
        }

        [MenuItem("Turtle Game/Verificar obstáculos y capturar vistas")]
        public static void VerifySavedScene()
        {
            Scene scene = OpenBeach();
            if (!scene.IsValid())
                return;
            Verify(scene);
            GameObject[] obstacles = FindObstacles(scene);
            int[] ids = obstacles.Select((root, index) => FindVisual(root, index).GetInstanceID()).ToArray();
            ReplaceObstacles(scene);
            if (!ids.SequenceEqual(FindObstacles(scene).Select((root, index) => FindVisual(root, index).GetInstanceID())))
                throw new InvalidOperationException("Repeated installation changed the obstacles.");
            Directory.CreateDirectory("Logs");
            var camera = new GameObject("Obstacle preview camera").AddComponent<Camera>();
            camera.CopyFrom(Camera.main);
            camera.fieldOfView = 55f;
            try
            {
                for (int i = 0; i < obstacles.Length; i++)
                {
                    Bounds bounds = obstacles[i].GetComponentInChildren<Renderer>().bounds;
                    Vector3 center = bounds.center;
                    camera.transform.position = center + new Vector3(3.8f, 2.5f, -5.8f);
                    camera.transform.LookAt(center);
                    Capture(camera, "Logs/route-obstacle-" + i + ".png");
                }
            }
            finally
            {
                Object.DestroyImmediate(camera.gameObject);
            }
            File.WriteAllText("Logs/route-obstacles-verification-result.txt",
                "PASS: three pack obstacles; textured URP materials; mesh collisions block approach; terrain contact; idempotent installation.\n");
            Debug.Log("ROUTE_OBSTACLES_VERIFICATION_PASS");
        }

        private static void Capture(Camera camera, string path)
        {
            var texture = new RenderTexture(1440, 900, 24);
            RenderTexture previous = RenderTexture.active;
            camera.targetTexture = texture;
            camera.Render();
            RenderTexture.active = texture;
            var image = new Texture2D(texture.width, texture.height, TextureFormat.RGB24, false);
            image.ReadPixels(new Rect(0, 0, texture.width, texture.height), 0, 0);
            image.Apply();
            File.WriteAllBytes(path, image.EncodeToPNG());
            camera.targetTexture = null;
            RenderTexture.active = previous;
            Object.DestroyImmediate(image);
            Object.DestroyImmediate(texture);
        }
    }
}
