using BeatSaberMarkupLanguage.Settings;
using UnityEngine;

namespace LipSync.UI
{

    internal static class SettingsMenuManager
    {

        private static Settings Instance { get; set; } = null!;

        private const string MenuName = nameof(LipSync);
        private const string ResourcePath = nameof(LipSync) + ".UI.Settings.bsml";

        public static void AddSettingsMenu()
        {
            if (Instance == null)
            {
                Instance = new GameObject(nameof(Settings)).AddComponent<Settings>();
                UnityEngine.Object.DontDestroyOnLoad(Instance.gameObject);
            }

            RemoveSettingsMenu();

            BSMLSettings.Instance.AddSettingsMenu(MenuName, ResourcePath, Instance);
        }

        public static void RemoveSettingsMenu()
        {
            BSMLSettings.Instance.RemoveSettingsMenu(Instance);
        }
    }
}