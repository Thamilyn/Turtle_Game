using System;
using System.Collections;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

namespace TurtleGame.Editor
{
    /// <summary>Runs against an isolated player in Play Mode; leaves the open scene unchanged.</summary>
    [InitializeOnLoad]
    public static class TurtleWalkAnimationVerification
    {
        private const string PendingKey = "TurtleGame.WalkVerificationPending";
        private static IEnumerator checks;
        private static TurtlePlayerController player;
        private static int lastFrame;
        private static int passed;
        private static double deadline;

        static TurtleWalkAnimationVerification()
        {
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
        }

        [MenuItem("Turtle Game/Verificar animacion de caminar")]
        public static void Run()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Run verification from Edit Mode.");
            if (Application.isBatchMode)
                EditorSceneManager.OpenScene(MoonBeachBuilder.ScenePath);
            SessionState.SetBool(PendingKey, true);
            EditorApplication.isPlaying = true;
        }

        private static void OnPlayModeChanged(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.ExitingPlayMode && checks != null)
            {
                Finish(false, "Verification interrupted.");
                return;
            }
            if (state != PlayModeStateChange.EnteredPlayMode || !SessionState.GetBool(PendingKey, false))
                return;
            SessionState.SetBool(PendingKey, false);
            passed = 0;
            lastFrame = -1;
            deadline = EditorApplication.timeSinceStartup + 120;
            Time.captureDeltaTime = 1f / 60f;
            checks = Verify();
            EditorApplication.update += Tick;
        }

        private static void Tick()
        {
            if (EditorApplication.timeSinceStartup > deadline)
            {
                Finish(false, "Verification timed out.");
                return;
            }
            if (Time.frameCount == lastFrame)
                return;
            lastFrame = Time.frameCount;
            try
            {
                if (!checks.MoveNext()) Finish(true, passed + " checks passed.");
            }
            catch (Exception exception)
            {
                Finish(false, exception.ToString());
            }
        }

        private static IEnumerator Verify()
        {
            const float dt = 1f / 60f;
            var floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            floor.name = "Walk verification floor";
            floor.transform.position = new Vector3(1000f, -0.5f, 0f);
            floor.transform.localScale = new Vector3(40f, 1f, 40f);
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/TurtlePlayer.prefab");
            var root = Object.Instantiate(prefab, new Vector3(1000f, 0.05f, 0f), Quaternion.identity);
            player = root.GetComponent<TurtlePlayerController>();
            player.ReadKeyboard = false;
            var animation = root.GetComponent<TurtleWalkAnimation>();
            Assert(animation != null, "Player prefab includes walk animation");
            player.Visual.Find("PlaceholderCube").gameObject.SetActive(false);
            var model = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Fbx/tortuguita.fbx"), player.Visual);
            model.transform.localPosition = new Vector3(-0.3535492f, 0.05198106f, 0.44814384f);
            model.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
            var limbs = new[] { model.transform.Find("aleta iz"), model.transform.Find("aleta der"),
                model.transform.Find("pata iz"), model.transform.Find("pata iz001") };
            var rotations = new Quaternion[4];
            var positions = new Vector3[4];
            for (int i = 0; i < 4; i++)
            {
                rotations[i] = limbs[i].localRotation;
                positions[i] = limbs[i].localPosition;
            }
            Vector3 restModel = model.transform.localPosition;
            Transform head = model.transform.Find("cabeza");
            Transform eye = model.transform.Find("ojoiz");
            Vector3 eyeInHead = head.InverseTransformPoint(eye.position);
            for (int i = 0; i < 25; i++) { player.Move(Vector2.zero, dt); yield return null; }
            Assert(player.IsGrounded, "Verification player lands on floor");
            for (int i = 0; i < 4; i++)
                Assert(Quaternion.Angle(rotations[i], limbs[i].localRotation) < 0.01f, "Idle limb " + i);
            CapturePreview(model, "idle");

            var maximumAngle = new float[4];
            Vector3 start = player.transform.position;
            for (int frame = 0; frame < 90; frame++)
            {
                player.Move(Vector2.up, dt);
                yield return null;
                for (int i = 0; i < 4; i++)
                    maximumAngle[i] = Mathf.Max(maximumAngle[i], Quaternion.Angle(rotations[i], limbs[i].localRotation));
                Assert(Vector3.Distance(eyeInHead, head.InverseTransformPoint(eye.position)) < 0.0001f,
                    "Eyes follow head " + frame);
                if (frame == 32 || frame == 44) CapturePreview(model, "walk-" + frame);
            }
            for (int i = 0; i < 4; i++) Assert(maximumAngle[i] > 15f, "Limb swings " + i);
            Quaternion leftOffset = limbs[0].localRotation * Quaternion.Inverse(rotations[0]);
            Quaternion rightOffset = limbs[1].localRotation * Quaternion.Inverse(rotations[1]);
            Assert(Mathf.Abs(leftOffset.eulerAngles.z - rightOffset.eulerAngles.z) > 1f,
                "Opposite front flippers alternate lift");
            Assert(player.transform.position.z - start.z > 4f, "Animation preserves movement speed");
            Assert(Quaternion.Angle(player.transform.rotation, Quaternion.identity) < 0.01f,
                "Physics root stays upright");
            Assert(Quaternion.Angle(player.Visual.rotation, Quaternion.identity) < 0.01f,
                "Visual keeps movement heading");

            Time.timeScale = 0f;
            Quaternion paused = limbs[0].localRotation;
            for (int i = 0; i < 5; i++) yield return null;
            Assert(Quaternion.Angle(paused, limbs[0].localRotation) < 0.01f, "Pause freezes pose");
            Time.timeScale = 1f;
            for (int i = 0; i < 40; i++) { player.Move(Vector2.zero, dt); yield return null; }
            for (int i = 0; i < 4; i++)
            {
                Assert(Quaternion.Angle(rotations[i], limbs[i].localRotation) < 0.01f, "Stopped rotation " + i);
                Assert(Vector3.Distance(positions[i], limbs[i].localPosition) < 0.0001f, "Stopped position " + i);
            }
            Assert(Vector3.Distance(restModel, model.transform.localPosition) < 0.0001f, "Body returns to rest");

            var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wall.transform.position = player.transform.position + new Vector3(0f, 1f, 0.9f);
            wall.transform.localScale = new Vector3(4f, 3f, 0.3f);
            Physics.SyncTransforms();
            for (int i = 0; i < 60; i++) { player.Move(Vector2.up, dt); yield return null; }
            Assert(Quaternion.Angle(rotations[0], limbs[0].localRotation) < 0.01f,
                "Blocked movement settles to idle");
            wall.SetActive(false);
            for (int i = 0; i < 20; i++) { player.Move(Vector2.up, dt); yield return null; }
            player.enabled = false;
            yield return null;
            Assert(Quaternion.Angle(rotations[0], limbs[0].localRotation) < 0.01f,
                "Disabling controller restores pose");
            player.enabled = true;
            player.ReadKeyboard = false;
            var character = player.GetComponent<CharacterController>();
            character.enabled = false;
            player.transform.position += Vector3.up * 8f;
            character.enabled = true;
            for (int i = 0; i < 15; i++) { player.Move(Vector2.up, dt); yield return null; }
            Assert(!player.IsGrounded && Quaternion.Angle(rotations[0], limbs[0].localRotation) < 0.01f,
                "Airborne movement does not walk");
            animation.enabled = false;
            Assert(Vector3.Distance(restModel, model.transform.localPosition) < 0.0001f,
                "Disabling animation restores model");

            // A replacement with no matching limbs is safe; the imported mesh is never modified.
            model.SetActive(false);
            Object.Destroy(model);
            var replacement = GameObject.CreatePrimitive(PrimitiveType.Cube);
            replacement.transform.SetParent(player.Visual, false);
            animation.enabled = true;
            Quaternion replacementRotation = replacement.transform.localRotation;
            for (int i = 0; i < 10; i++) { player.Move(Vector2.right, dt); yield return null; }
            Assert(Quaternion.Angle(replacementRotation, replacement.transform.localRotation) < 0.01f,
                "Unrelated replacement model stays untouched");
        }

        private static void CapturePreview(GameObject model, string label)
        {
            foreach (var t in model.GetComponentsInChildren<Transform>()) t.gameObject.layer = 31;
            Bounds bounds = model.GetComponentsInChildren<Renderer>()[0].bounds;
            foreach (var renderer in model.GetComponentsInChildren<Renderer>()) bounds.Encapsulate(renderer.bounds);
            var cameraObject = new GameObject("Walk preview camera");
            var camera = cameraObject.AddComponent<Camera>();
            camera.cullingMask = 1 << 31;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.15f, 0.18f, 0.23f);
            camera.orthographic = true;
            camera.orthographicSize = 0.38f;
            camera.transform.position = bounds.center + new Vector3(0.75f, 0.65f, 0.9f);
            camera.transform.LookAt(bounds.center);
            var lightObject = new GameObject("Walk preview light");
            var light = lightObject.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 2.5f;
            light.cullingMask = 1 << 31;
            light.transform.rotation = Quaternion.Euler(45f, -30f, 0f);
            var target = new RenderTexture(800, 600, 24);
            var pixels = new Texture2D(800, 600, TextureFormat.RGB24, false);
            RenderTexture previous = RenderTexture.active;
            try
            {
                camera.targetTexture = target;
                camera.Render();
                RenderTexture.active = target;
                pixels.ReadPixels(new Rect(0, 0, 800, 600), 0, 0);
                pixels.Apply();
                Directory.CreateDirectory("Logs");
                File.WriteAllBytes("Logs/turtle-" + label + ".png", pixels.EncodeToPNG());
            }
            finally
            {
                camera.targetTexture = null;
                RenderTexture.active = previous;
                target.Release();
                Object.Destroy(target);
                Object.Destroy(pixels);
                Object.Destroy(cameraObject);
                Object.Destroy(lightObject);
            }
        }

        private static void Assert(bool condition, string description)
        {
            if (!condition) throw new InvalidOperationException("FAIL: " + description);
            passed++;
        }

        private static void Finish(bool success, string message)
        {
            EditorApplication.update -= Tick;
            checks = null;
            Time.captureDeltaTime = 0f;
            Time.timeScale = 1f;
            string result = (success ? "TURTLE_WALK_OK: " : "TURTLE_WALK_FAILED: ") + message;
            Directory.CreateDirectory("Logs");
            File.WriteAllText("Logs/walk-animation-verification-result.txt", result);
            if (success) Debug.Log(result); else Debug.LogError(result);
            if (Application.isBatchMode) EditorApplication.Exit(success ? 0 : 1);
            else EditorApplication.isPlaying = false;
        }
    }
}
