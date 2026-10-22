using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace TurtleGame.Editor
{
    /// <summary>Updates already-open beach scenes after script reload, without regenerating Terrain.</summary>
    [InitializeOnLoad]
    public static class MoonBeachVisualRepair
    {
        static MoonBeachVisualRepair()
        {
            EditorApplication.delayCall += RepairOpenBeach;
            EditorSceneManager.sceneOpened += OnSceneOpened;
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
        }

        private static void OnSceneOpened(Scene scene, OpenSceneMode mode) => RepairOpenBeach();

        private static void OnPlayModeChanged(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.EnteredEditMode)
                RepairOpenBeach();
            else if (state == PlayModeStateChange.ExitingEditMode)
                RepairOpenBeach();
        }

        [MenuItem("Turtle Game/Actualizar tortuga en playa abierta")]
        public static void RepairOpenBeach()
        {
            if (EditorApplication.isPlaying)
                return;
            var modelAsset = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Fbx/tortuguita.fbx");
            if (modelAsset == null)
                return;
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                Scene scene = SceneManager.GetSceneAt(i);
                if (!scene.isLoaded || scene.path != MoonBeachBuilder.ScenePath)
                    continue;
                bool changed = false;
                foreach (var root in scene.GetRootGameObjects())
                {
                    foreach (var controller in root.GetComponentsInChildren<TurtlePlayerController>(true))
                    {
                        Transform visual = controller.Visual;
                        if (visual == null)
                            continue;
                        var placeholder = visual.Find("PlaceholderCube");
                        if (placeholder != null && placeholder.gameObject.activeSelf)
                        {
                            Undo.RecordObject(placeholder.gameObject, "Hide turtle placeholder");
                            placeholder.gameObject.SetActive(false);
                            PrefabUtility.RecordPrefabInstancePropertyModifications(placeholder.gameObject);
                            changed = true;
                        }
                        if (visual.Find("tortuguita") != null)
                            continue;
                        var model = (GameObject)PrefabUtility.InstantiatePrefab(modelAsset, scene);
                        Undo.RegisterCreatedObjectUndo(model, "Add turtle model");
                        model.name = "tortuguita";
                        model.transform.SetParent(visual, false);
                        model.transform.localPosition = new Vector3(-0.3535492f, 0.05198106f, 0.44814384f);
                        model.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
                        changed = true;
                    }
                }
                if (changed)
                {
                    EditorSceneManager.MarkSceneDirty(scene);
                    Debug.Log("MoonBeach: modelo de tortuga actualizado en la escena abierta. Guarda con Ctrl+S.");
                }
            }
        }
    }
}
