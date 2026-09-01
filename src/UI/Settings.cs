using BeatSaberMarkupLanguage.Attributes;
using LipSync.Configuration;
using UnityEngine;
using System.Collections.Generic;

namespace LipSync.UI
{
    internal class Settings : MonoBehaviour
    {

        public static Settings Instance { get; } = new Settings();

        [UIValue("autosetup")]
        public bool AutoSetup
        {
            get => PluginConfig.Instance.AutoSetup;
            set => PluginConfig.Instance.AutoSetup = value;
        }

        [UIValue("micinput")]
        public string MicInput
        {
            get => PluginConfig.Instance.MicInput;
            set => PluginConfig.Instance.MicInput = value;
        }

        [UIValue("micoptions")]
        public List<object> MicOptions
        {
            get
            {
                var options = new List<object>();
                foreach (var device in Microphone.devices)
                {
                    options.Add(device);
                }
                return options;
            }
        }

        [UIValue("visemescale")]
        public float VisemeScale
        {
            get => PluginConfig.Instance.VisemeScale;
            set => PluginConfig.Instance.VisemeScale = value;
        }

    }
}
