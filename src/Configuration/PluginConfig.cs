using System;
using System.Runtime.CompilerServices;
using IPA.Config.Stores;
using UnityEngine;


[assembly: InternalsVisibleTo(GeneratedStore.AssemblyVisibilityTarget)]
namespace LipSync.Configuration
{
    public delegate void OnChangedHandler();
    internal class PluginConfig
    {
        public static PluginConfig Instance { get; set; } = null!;

        public static event OnChangedHandler OnChanged = null!;

        public virtual bool AutoSetup { get; set; } = true;
        public virtual string MicInput { get; set; } = string.Empty; // Must be 'virtual' if you want BSIPA to detect a value change and save the config automatically.
        public virtual float VisemeScale { get; set; } = 1.0f;

        /// <summary>
        /// This is called whenever BSIPA reads the config from disk (including when file changes are detected).
        /// </summary>
        public virtual void OnReload()
        {
            // Do stuff after config is read from disk.
        }

        /// <summary>
        /// Call this to force BSIPA to update the config file. This is also called by BSIPA if it detects the file was modified.
        /// </summary>
        public virtual void Changed()
        {
            OnChanged?.Invoke();
        }

        /// <summary>
        /// Call this to have BSIPA copy the values from <paramref name="other"/> into this config.
        /// </summary>
        public virtual void CopyFrom(PluginConfig other)
        {
            // This instance's members populated from other
        }
    }
}