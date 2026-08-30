using UnityEngine;
using UnityEngine.SceneManagement;
using CustomAvatar.Player;
using LipSync.UI;
using BeatSaberMarkupLanguage.Util;
using CustomAvatar.Avatar;

namespace LipSync
{
    public class LipSyncExtensionController : MonoBehaviour
    {
        public static LipSyncExtensionController Instance { get; private set; }
        private PlayerAvatarManager _playerAvatarManager;

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
        }

        private void Update()
        {
            if (_playerAvatarManager == null)
            {
                var sceneContext = SceneManager.GetActiveScene().name;
                _playerAvatarManager = FindObjectOfType<PlayerAvatarManager>();
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
            OVRLipSync OLSInterfaceComponent = GetComponent<OVRLipSync>();
            if (OLSInterfaceComponent == null)
            {
                gameObject.AddComponent<OVRLipSync>();
            }
        }

        private void OnDisable()
        {
            OVRLipSync OLSInterfaceComponent = GetComponent<OVRLipSync>();
            if (OLSInterfaceComponent != null)
            {
                Destroy(OLSInterfaceComponent);
            }

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