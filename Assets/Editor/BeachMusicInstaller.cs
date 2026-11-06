using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace TurtleGame.Editor
{
    public static class BeachMusicInstaller
    {
        public const string ClipPath = "Assets/Audio/Dreamy Night.mp3";

        [MenuItem("Turtle Game/Instalar música de playa y disco rayado")]
        public static void Install()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Install beach music from Edit Mode.");
            var scene = SceneManager.GetActiveScene();
            if (scene.path != MoonBeachBuilder.ScenePath)
            {
                if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                    return;
                scene = EditorSceneManager.OpenScene(MoonBeachBuilder.ScenePath);
            }
            var sequence = Object.FindFirstObjectByType<RoadCrossingSequence>();
            if (sequence == null)
                throw new InvalidOperationException("Install the road crossing before the beach music.");

            var importer = AssetImporter.GetAtPath(ClipPath) as AudioImporter;
            if (importer == null)
                throw new InvalidOperationException("Import Dreamy Night.mp3 into Assets/Audio first.");
            var settings = importer.defaultSampleSettings;
            // Sample-accurate rewinds need decoded audio rather than streaming seeks.
            settings.loadType = AudioClipLoadType.DecompressOnLoad;
            settings.compressionFormat = AudioCompressionFormat.Vorbis;
            settings.quality = 0.8f;
            settings.preloadAudioData = true;
            importer.defaultSampleSettings = settings;
            importer.forceToMono = false;
            importer.SaveAndReimport();

            var music = Object.FindFirstObjectByType<BeachMusic>();
            if (music == null)
                music = new GameObject("Beach music - Dreamy Night").AddComponent<BeachMusic>();
            var guide = Object.FindFirstObjectByType<MoonFairyGuide>();
            if (guide == null)
                throw new InvalidOperationException("The beach needs a moon guide to trigger the record scratch.");
            var musicSettings = new SerializedObject(music);
            musicSettings.FindProperty("moonGuide").objectReferenceValue = guide;
            musicSettings.ApplyModifiedPropertiesWithoutUndo();
            var source = music.GetComponent<AudioSource>();
            source.clip = AssetDatabase.LoadAssetAtPath<AudioClip>(ClipPath);
            source.playOnAwake = false;
            source.loop = true;
            source.spatialBlend = 0f;
            source.dopplerLevel = 0f;
            source.volume = 0f;
            music.GetComponent<AudioDistortionFilter>().enabled = false;
            music.GetComponent<AudioLowPassFilter>().enabled = false;
            var serialized = new SerializedObject(sequence);
            serialized.FindProperty("beachMusic").objectReferenceValue = music;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            Debug.Log("BEACH_MUSIC_INSTALLED: Dreamy Night; " + source.clip.length.ToString("F1") +
                " seconds; a brief scratch breaks the calm at the moon's final stop; the engine plays alone.");
        }

        public static void InstallAndVerify()
        {
            Install();
            RoadCrossingVerification.Run();
        }
    }
}
