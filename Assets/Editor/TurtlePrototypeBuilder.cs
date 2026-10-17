using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace TurtleGame.Editor
{
    public static class TurtlePrototypeBuilder
    {
        public const string ScenePath = "Assets/TurtleGame/Scenes/TurtlePrototype.unity";
        public const string PrefabPath = "Assets/TurtleGame/Prefabs/TurtlePlayer.prefab";

        [MenuItem("Turtle Game/Crear escena de prueba")]
        public static void CreateFromMenu()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return;
            if (File.Exists(ScenePath) || File.Exists(PrefabPath))
            {
                EditorUtility.DisplayDialog("El prototipo ya existe",
                    "Abre TurtlePrototype.unity. No se sobrescribiran tus assets existentes.", "Aceptar");
                return;
            }
            Create();
        }

        // Called once by the batch setup. Existing files are deliberately preserved.
        public static void Create()
        {
            if (File.Exists(ScenePath) || File.Exists(PrefabPath))
                throw new System.InvalidOperationException("Prototype assets already exist.");

            Directory.CreateDirectory("Assets/TurtleGame/Scenes");
            Directory.CreateDirectory("Assets/TurtleGame/Prefabs");
            Directory.CreateDirectory("Assets/TurtleGame/Materials");
            AssetDatabase.Refresh();
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            RenderSettings.ambientLight = new Color(0.48f, 0.55f, 0.65f);

            Material sand = MaterialAsset("Sand", new Color(0.68f, 0.57f, 0.38f));
            Material turtle = MaterialAsset("TurtlePlaceholder", new Color(0.29f, 0.63f, 0.36f));
            Material rock = MaterialAsset("Rock", new Color(0.34f, 0.38f, 0.41f));
            Material sea = MaterialAsset("Sea", new Color(0.08f, 0.38f, 0.54f));

            Cube("Beach", new Vector3(0f, -0.5f, 0f), new Vector3(30f, 1f, 30f), sand);
            var seaObject = Cube("Sea_VisualOnly", new Vector3(0f, 0.01f, 13f),
                new Vector3(30f, 0.02f, 4f), sea);
            Object.DestroyImmediate(seaObject.GetComponent<Collider>());
            Cube("Obstacle", new Vector3(3f, 0.6f, 4f), new Vector3(3f, 1.2f, 1.5f), rock);
            Cube("Boundary_Left", new Vector3(-15f, 0.5f, 0f), new Vector3(0.5f, 1f, 30f), rock);
            Cube("Boundary_Right", new Vector3(15f, 0.5f, 0f), new Vector3(0.5f, 1f, 30f), rock);
            Cube("Boundary_Back", new Vector3(0f, 0.5f, -15f), new Vector3(30f, 1f, 0.5f), rock);
            Cube("Boundary_Front", new Vector3(0f, 0.5f, 15f), new Vector3(30f, 1f, 0.5f), rock);

            var player = new GameObject("TurtlePlayer");
            var character = player.AddComponent<CharacterController>();
            character.height = 0.8f;
            character.radius = 0.35f;
            character.center = new Vector3(0f, 0.4f, 0f);
            character.skinWidth = 0.03f;
            character.stepOffset = 0.15f;
            character.slopeLimit = 45f;
            character.minMoveDistance = 0f;
            var visual = new GameObject("Visual").transform;
            visual.SetParent(player.transform, false);
            var placeholder = Cube("PlaceholderCube", Vector3.zero, Vector3.one * 0.8f, turtle);
            Object.DestroyImmediate(placeholder.GetComponent<Collider>());
            placeholder.transform.SetParent(visual, false);
            placeholder.transform.localPosition = new Vector3(0f, 0.4f, 0f);
            player.AddComponent<TurtlePlayerController>().Visual = visual;
            PrefabUtility.SaveAsPrefabAsset(player, PrefabPath);
            Object.DestroyImmediate(player);
            player = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath), scene);

            var cameraObject = new GameObject("Main Camera");
            cameraObject.tag = "MainCamera";
            var camera = cameraObject.AddComponent<Camera>();
            camera.backgroundColor = new Color(0.12f, 0.18f, 0.27f);
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.fieldOfView = 55f;
            cameraObject.AddComponent<AudioListener>();
            cameraObject.transform.position = player.transform.position + new Vector3(0f, 7f, -8f);
            cameraObject.transform.LookAt(player.transform.position + Vector3.up * 0.4f);
            cameraObject.AddComponent<TurtleFollowCamera>().Target = player.transform;

            var moon = new GameObject("Moonlight").AddComponent<Light>();
            moon.type = LightType.Directional;
            moon.color = new Color(0.75f, 0.84f, 1f);
            moon.intensity = 1.1f;
            moon.shadows = LightShadows.Soft;
            moon.transform.rotation = Quaternion.Euler(45f, -30f, 0f);
            new GameObject("Prototype Help").AddComponent<PrototypeHelp>();

            EditorSceneManager.SaveScene(scene, ScenePath);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
            AssetDatabase.SaveAssets();
            Debug.Log("TURTLE_SETUP_OK: " + ScenePath);
        }

        private static Material MaterialAsset(string name, Color color)
        {
            var material = new Material(Shader.Find("Standard")) { name = name, color = color };
            AssetDatabase.CreateAsset(material, "Assets/TurtleGame/Materials/" + name + ".mat");
            return material;
        }

        private static GameObject Cube(string name, Vector3 position, Vector3 scale, Material material)
        {
            var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cube.name = name;
            cube.transform.position = position;
            cube.transform.localScale = scale;
            cube.GetComponent<Renderer>().sharedMaterial = material;
            return cube;
        }
    }
}
