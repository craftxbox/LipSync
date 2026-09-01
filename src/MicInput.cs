using UnityEngine;
using LipSync.Configuration;
using System.Threading;
using OpenLipSync.Inference;
using OpenLipSync.Inference.OVRCompat;

namespace LipSync;

[RequireComponent(typeof(AudioSource))]
public class MicInput : MonoBehaviour
{
    private AudioSource? _audioSource;
    private string SelectedDevice => PluginConfig.Instance.MicInput;
    private bool started = false;

    void Awake()
    {
        _audioSource = GetComponent<AudioSource>();
        if (!_audioSource) return;
        PluginConfig.OnChanged += OnPluginConfigChanged;
    }

    private void OnPluginConfigChanged()
    {
        if (started)
        {
            StopMic();
            Plugin.Log?.Debug("Selected new device: " + SelectedDevice);
            StartMic();
        }
    }

    void Update()
    {
        if (_audioSource == null) return;
        _audioSource.volume = 100;
    }

    public void StartMic()
    {
        if (_audioSource == null) return;

        int minFreq, maxFreq;
        Microphone.GetDeviceCaps(SelectedDevice, out minFreq, out maxFreq);

        if(minFreq > 48000 || maxFreq < 48000)
        {
            Plugin.Log?.Warn("Selected microphone doesn't claim to support 48kHz... Concerning, but continuing anyway.");
        }

        _audioSource.clip = Microphone.Start(SelectedDevice, true, 1, 48000);
        _audioSource.loop = true;
        _audioSource.mute = false;

        int timeout = 10000;

        while (Microphone.GetPosition(SelectedDevice) <= 0)
        {
            Thread.Sleep(100);
            timeout -= 100;
            if (timeout <= 0)
            {
                Plugin.Log?.Error("Timeout waiting for microphone to start recording.");
                return;
            }
        }

        _audioSource.Play();
        started = true;
    }

    void StopMic()
    {
        if (_audioSource == null) return;
        _audioSource.Stop();
        Microphone.End(SelectedDevice);

        var morphTarget = GetComponent<MorphTarget>();
        if (morphTarget) morphTarget.ResetVisemes();

        started = false;
    }

}