using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace TurtleGame.Editor
{
    /// <summary>Replaces beach placeholders while preserving their layout and the rest of the scene.</summary>
    public static class DuneRockInstaller
    {
        private const string PrefabFolder = "Assets/PolyOne/Rocks Stylized/Prefabs/";
        private const string MaterialPath = "Assets/Materials/DuneRocks_PolyOne.mat";
        private const int Seed = 73621;

        [MenuItem("Turtle Game/Reemplazar Dune Rocks por PolyOne")]
        public static void Install()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Replace rocks from Edit Mode.");

            Scene scene = SceneManager.GetActiveScene();
            if (scene.path != MoonBeachBuilder.ScenePath)
            {
                if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                    return;
                scene = EditorSceneManager.OpenScene(MoonBeachBuilder.ScenePath);
            }

            ReplaceDuneRocks(scene);
            Verify(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log("DUNE_ROCKS_OK: PolyOne rocks saved in " + scene.path);
        }

        public static void ReplaceDuneRocks(Scene scene)
        {
            GameObject[] originals = FindRocks(scene);
            if (originals.Length == 0)
                throw new InvalidOperationException("No Dune rock placeholders found.");

            var prefabs = new GameObject[11];
            for (int i = 0; i < prefabs.Length; i++)
            {
                string path = PrefabFolder + "SM_Rocks_" + (i + 1).ToString("00") + ".prefab";
                prefabs[i] = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (prefabs[i] == null || prefabs[i].GetComponent<MeshFilter>()?.sharedMesh == null)
                    throw new InvalidOperationException("Missing rock mesh: " + path);
            }

            Material material = LoadBeachMaterial();
            // Running the menu again must not change an already installed arrangement.
            if (originals.All(rock => prefabs.Contains(PrefabUtility.GetCorrespondingObjectFromSource(rock))))
            {
                foreach (GameObject rock in originals)
                {
                    var renderer = rock.GetComponent<MeshRenderer>();
                    if (renderer.sharedMaterial == material)
                        continue;
                    Undo.RecordObject(renderer, "Apply matte beach stone");
                    renderer.sharedMaterial = material;
                    PrefabUtility.RecordPrefabInstancePropertyModifications(renderer);
                    EditorSceneManager.MarkSceneDirty(scene);
                }
                return;
            }

            Terrain terrain = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<Terrain>())
                .FirstOrDefault();
            if (terrain == null)
                throw new InvalidOperationException("MoonBeach Terrain is missing.");

            var random = new System.Random(Seed);
            int[] variants = Enumerable.Range(0, prefabs.Length).ToArray();
            Undo.IncrementCurrentGroup();
            int undoGroup = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Replace dune rocks with PolyOne");
            for (int i = 0; i < originals.Length; i++)
            {
                // Shuffled bags use every silhouette before repeating one.
                if (i % variants.Length == 0)
                {
                    for (int j = variants.Length - 1; j > 0; j--)
                    {
                        int other = random.Next(j + 1);
                        int value = variants[j];
                        variants[j] = variants[other];
                        variants[other] = value;
                    }
                }

                GameObject original = originals[i];
                Vector3 anchor = original.transform.position;
                Bounds oldBounds = original.GetComponent<Renderer>().bounds;
                float width = Mathf.Max(oldBounds.size.x, oldBounds.size.z);
                width = Mathf.Clamp(width * Range(random, 0.88f, 1.12f), 0.65f, 2.8f);
                GameObject prefab = prefabs[variants[i % variants.Length]];
                var rock = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
                Undo.RegisterCreatedObjectUndo(rock, "Create stylized rock");
                rock.name = "Dune rock " + RockIndex(original) + " - " + prefab.name;
                rock.transform.SetParent(original.transform.parent, true);
                rock.transform.SetSiblingIndex(original.transform.GetSiblingIndex());
                rock.layer = original.layer;
                rock.SetActive(original.activeSelf);

                Vector3 terrainPoint = anchor - terrain.transform.position;
                Vector3 normal = terrain.terrainData.GetInterpolatedNormal(
                    terrainPoint.x / terrain.terrainData.size.x, terrainPoint.z / terrain.terrainData.size.z);
                Quaternion slope = Quaternion.RotateTowards(Quaternion.identity,
                    Quaternion.FromToRotation(Vector3.up, terrain.transform.TransformDirection(normal)), 18f);
                rock.transform.rotation = slope * Quaternion.Euler(Range(random, -5f, 5f),
                    Range(random, 0f, 360f), Range(random, -5f, 5f));

                var renderer = rock.GetComponent<MeshRenderer>();
                renderer.sharedMaterial = material;
                Bounds bounds = renderer.bounds;
                rock.transform.localScale *= width / Mathf.Max(bounds.size.x, bounds.size.z);
                bounds = renderer.bounds;
                // Center offset pivots at the old location, then nest the base slightly into the sand.
                rock.transform.position += new Vector3(anchor.x - bounds.center.x, 0f, anchor.z - bounds.center.z);
                float ground = terrain.SampleHeight(anchor) + terrain.transform.position.y;
                float burial = Mathf.Clamp(bounds.size.y * Range(random, 0.1f, 0.18f), 0.06f, 0.28f);
                rock.transform.position += Vector3.up * (ground - renderer.bounds.min.y - burial);

                var collider = Undo.AddComponent<MeshCollider>(rock);
                collider.sharedMesh = rock.GetComponent<MeshFilter>().sharedMesh;
                collider.convex = false;
                PrefabUtility.RecordPrefabInstancePropertyModifications(rock.transform);
                PrefabUtility.RecordPrefabInstancePropertyModifications(rock);
                PrefabUtility.RecordPrefabInstancePropertyModifications(renderer);
                Undo.DestroyObjectImmediate(original);
            }
            Undo.CollapseUndoOperations(undoGroup);
            EditorSceneManager.MarkSceneDirty(scene);
        }

        private static GameObject[] FindRocks(Scene scene) => scene.GetRootGameObjects()
            .SelectMany(root => root.GetComponentsInChildren<Transform>(true))
            .Where(item => item.name.StartsWith("Dune rock ", StringComparison.Ordinal))
            .Select(item => item.gameObject).OrderBy(RockIndex).ToArray();

        private static int RockIndex(GameObject rock) => int.Parse(rock.name.Substring("Dune rock ".Length).Split(' ')[0]);
        private static float Range(System.Random random, float min, float max) => min + (float)random.NextDouble() * (max - min);

        private static Material LoadBeachMaterial()
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
            if (material != null)
                return material;
            var source = AssetDatabase.LoadAssetAtPath<Material>("Assets/PolyOne/Rocks Stylized/Materials/Rocks Stylized_M.mat");
            if (source == null)
                throw new InvalidOperationException("Missing PolyOne rock material.");
            material = new Material(source) { name = "DuneRocks_PolyOne", enableInstancing = true };
            // Keep the pack's painted texture, with a dry, matte finish under the moonlight.
            material.SetFloat("_Smoothness", 0.18f);
            material.SetFloat("_QueueOffset", 0f);
            material.renderQueue = 2000;
            AssetDatabase.CreateAsset(material, MaterialPath);
            AssetDatabase.SaveAssets();
            return material;
        }

        private static void Verify(Scene scene)
        {
            GameObject[] rocks = FindRocks(scene);
            var variants = new HashSet<string>();
            Terrain terrain = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<Terrain>()).First();
            foreach (GameObject rock in rocks)
            {
                string path = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(rock);
                if (!path.StartsWith(PrefabFolder, StringComparison.Ordinal))
                    throw new InvalidOperationException("Rock is not a PolyOne prefab: " + rock.name);
                variants.Add(path);
                var renderer = rock.GetComponent<MeshRenderer>();
                Material material = renderer.sharedMaterial;
                if (material == null || material.shader.name != "Universal Render Pipeline/Lit" || material.GetTexture("_BaseMap") == null)
                    throw new InvalidOperationException("Rock material is missing its URP shader or texture.");
                if (rock.GetComponent<MeshCollider>()?.sharedMesh != rock.GetComponent<MeshFilter>().sharedMesh)
                    throw new InvalidOperationException("Rock collision does not match the mesh: " + rock.name);
                float surface = terrain.SampleHeight(renderer.bounds.center) + terrain.transform.position.y;
                if (renderer.bounds.min.y > surface + 0.02f || renderer.bounds.max.y < surface)
                    throw new InvalidOperationException("Rock floats or is fully buried: " + rock.name);
            }
            if (variants.Count != Mathf.Min(11, rocks.Length))
                throw new InvalidOperationException("Not all rock variants were used.");
            Debug.Log("DUNE_ROCKS_VERIFIED: " + rocks.Length + " grounded prefab instances, " + variants.Count + " variants, textured URP materials and mesh collision.");
        }

        // Checks repeatability and captures views without saving camera changes.
        [MenuItem("Turtle Game/Verificar rocas PolyOne y capturar vistas")]
        public static void VerifySavedScene()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Verify rocks from Edit Mode.");
            Scene scene = SceneManager.GetActiveScene();
            if (scene.path != MoonBeachBuilder.ScenePath)
            {
                if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                    return;
                scene = EditorSceneManager.OpenScene(MoonBeachBuilder.ScenePath);
            }
            Verify(scene);
            GameObject[] rocks = FindRocks(scene);
            int[] instanceIds = rocks.Select(rock => rock.GetInstanceID()).ToArray();
            ReplaceDuneRocks(scene);
            if (!instanceIds.SequenceEqual(FindRocks(scene).Select(rock => rock.GetInstanceID())))
                throw new InvalidOperationException("Repeated installation changed the rocks.");

            Directory.CreateDirectory("Logs");
            var preview = new GameObject("Rock preview camera").AddComponent<Camera>();
            preview.CopyFrom(Camera.main);
            preview.fieldOfView = 55f;
            Capture(preview, new Vector3(0f, 38f, -17f), new Vector3(0f, 2f, 51f), "Logs/dune-rocks-overview.png");
            GameObject subject = rocks.OrderBy(rock => Mathf.Abs(rock.transform.position.z - 35f)).First();
            Vector3 center = subject.GetComponent<Renderer>().bounds.center;
            Capture(preview, center + new Vector3(3.8f, 2.4f, -4f), center, "Logs/dune-rocks-detail.png");
            Object.DestroyImmediate(preview.gameObject);
            File.WriteAllText("Logs/dune-rocks-verification-result.txt", "PASS: " + rocks.Length + " rocks; all 11 PolyOne variants; URP textures; fitted mesh colliders; terrain contact; idempotent installation.\n");
        }

        private static void Capture(Camera camera, Vector3 position, Vector3 target, string path)
        {
            camera.transform.position = position;
            camera.transform.LookAt(target);
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
