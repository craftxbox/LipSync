using UnityEngine;
using UnityEngine.SceneManagement;
using CustomAvatar.Player;
using LipSync.UI;
using BeatSaberMarkupLanguage.Util;
using CustomAvatar.Avatar;
using OpenLipSync.Inference;
using OpenLipSync.Inference.OVRCompat;
using System.IO;
using BeatSaberMarkupLanguage;
using System;
using System.Text;

namespace LipSync
{
    public class LipSyncExtensionController : MonoBehaviour
    {
        public static LipSyncExtensionController? Instance { get; private set; }
        private PlayerAvatarManager? _playerAvatarManager;

        public OpenLipSyncBackend backend { get; private set; } = new OpenLipSyncBackend();
        public int OLSContext = 0;
        private void Awake()
        {
            // For this particular MonoBehaviour, we only want one instance to exist at any time, so store a reference to it in a static property
            //   and destroy any that are created while one already exists.
            if (Instance != null)
            {
                Plugin.Log?.Warn($"Instance of {GetType().Name} already exists, destroying.");
                DestroyImmediate(this);
                return;
            }

            DontDestroyOnLoad(this); // Don't destroy this object on scene changes
            Instance = this;
            Plugin.Log?.Debug($"{name}: Awake()");

            MainMenuAwaiter.MainMenuInitializing += SettingsMenuManager.AddSettingsMenu;

            CheckAndUnpackModel();

            backend.DefaultModelPath = PluginFiles.ModelFile.FullName;
            backend.Initialize(48000, 256);

            Result result = backend.CreateContextWithModelFile(ref OLSContext, ContextProviders.Enhanced_with_Laughter, PluginFiles.ModelFile.FullName, 48000, false);
            if (result != Result.Success)
            {
                Plugin.Log?.Error($"Failed to create OpenLipSync context. Result: {result}");
            }
            backend.SendSignal(OLSContext, Signals.VisemeSmoothing, 50);
        }

        private void CheckAndUnpackModel()
        {
            if (!PluginFiles.DataDir.Exists) PluginFiles.DataDir.Create();

            var assembly = typeof(LipSyncExtensionController).Assembly;

            if (!PluginFiles.ModelFile.Exists)
            {
                Plugin.Log?.Debug($"Model file not found at {PluginFiles.ModelFile.FullName}, unpacking from resources...");
                var modelStream = assembly.GetManifestResourceStream(nameof(LipSync) + ".model.onnx");
                if (modelStream == null)
                {
                    Plugin.Log?.Error("Failed to load model resource from assembly.");
                    return;
                }
                byte[] modelBytes = new byte[modelStream.Length];
                modelStream.Read(modelBytes, 0, modelBytes.Length);
                modelStream.Close();
                File.WriteAllBytes(PluginFiles.ModelFile.FullName, modelBytes);
                Plugin.Log?.Debug($"Model file unpacked to {PluginFiles.ModelFile.FullName}");
            }

            if (!PluginFiles.ConfigFile.Exists)
            {
                Plugin.Log?.Debug($"Config file not found at {PluginFiles.ConfigFile.FullName}, unpacking from resources...");

                var config = Utilities.GetResourceContent(assembly, nameof(LipSync) + ".config.json");
                if (config == null)
                {
                    Plugin.Log?.Error("Failed to load config resource from assembly.");
                    return;
                }
                File.WriteAllText(PluginFiles.ConfigFile.FullName, config);
                Plugin.Log?.Debug($"Config file unpacked to {PluginFiles.ConfigFile.FullName}");
            }
        }

        private void Update()
        {
            if (_playerAvatarManager == null)
            {
                var sceneContext = SceneManager.GetActiveScene().name;
                _playerAvatarManager = FindFirstObjectByType<PlayerAvatarManager>();
                if (_playerAvatarManager == null)
                {
                    Plugin.Log?.Debug("PlayerAvatarManager not found, waiting for it to be created...");
                    return;
                }
                _playerAvatarManager.avatarChanged += OnAvatarChanged;
            }
        }

        private void OnAvatarChanged(SpawnedAvatar avatar)
        {
            Plugin.Log?.Debug($"LipSyncExtensionController: OnAvatarChanged() called with avatar: {avatar?.name ?? "null"}");
            if (avatar == null) return;
            if (!Configuration.PluginConfig.Instance.AutoSetup) return;
            if (avatar.TryGetComponent<LipSyncController>(out _)) return;
            avatar.gameObject.AddComponent<LipSyncController>();
        }

        private void OnEnable()
        {
            // OVRLipSync OLSInterfaceComponent = GetComponent<OVRLipSync>();
            // if (OLSInterfaceComponent == null)
            // {
            //     gameObject.AddComponent<OVRLipSync>();
            // }
        }

        private void OnDisable()
        {
            // OVRLipSync OLSInterfaceComponent = GetComponent<OVRLipSync>();
            // if (OLSInterfaceComponent != null)
            // {
            //     Destroy(OLSInterfaceComponent);
            // }

            MainMenuAwaiter.MainMenuInitializing -= SettingsMenuManager.AddSettingsMenu;
        }

        private void OnDestroy()
        {
            Plugin.Log?.Debug($"{name}: OnDestroy()");
            if (Instance == this)
                Instance = null; // This MonoBehaviour is being destroyed, so set the static instance property to null.
        }
    }
}