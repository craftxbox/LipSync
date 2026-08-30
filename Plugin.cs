using IPA;
using IPA.Config;
using IPA.Config.Stores;
using UnityEngine;
using LipSync.Configuration;
using IPALogger = IPA.Logging.Logger;

namespace LipSync
{
    [Plugin(RuntimeOptions.SingleStartInit)]
    public class Plugin
    {
        internal static Plugin Instance { get; private set; }
        internal static IPALogger Log { get; private set; }

        [Init]
        public Plugin(IPALogger logger, Config conf)
        {
            Instance = this;
            Log = logger;
            PluginConfig.Instance = conf.Generated<PluginConfig>();
            Log.Debug("Config loaded");
        }

        [OnStart]
        public void OnApplicationStart()
        {
            Log.Debug("OnApplicationStart");
            new GameObject("LipSyncExtensionController").AddComponent<LipSyncExtensionController>();
        }

        [OnExit]
        public void OnApplicationQuit()
        {
            Log.Debug("OnApplicationQuit");

        }
    }
}
