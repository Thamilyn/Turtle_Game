using UnityEngine;
#if UNITY_WEBGL && !UNITY_EDITOR
using System.Runtime.InteropServices;
#endif

namespace TurtleGame
{
    /// <summary>Shared exit action for the main, pause and ending menus.</summary>
    public static class GameExit
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        [DllImport("__Internal")]
        private static extern void TurtleGame_ExitWebGame();
#endif

        public static void Quit()
        {
            PlayerPrefs.Save();
#if UNITY_EDITOR
            Debug.Log("Quit Game: native builds close the application; Web builds unload only the game and keep the itch.io page open.");
#elif UNITY_WEBGL
            Time.timeScale = 0f;
            AudioListener.pause = true;
            TurtleGame_ExitWebGame();
#else
            Application.Quit();
#endif
        }
    }
}
