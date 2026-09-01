using UnityEngine;
using OpenLipSync.Inference.OVRCompat;
using OpenLipSync.Inference;

namespace LipSync;

[RequireComponent(typeof(AudioSource))]
public class MorphTarget : MonoBehaviour
{
    private AudioSource _audioSource = null!;
    public SkinnedMeshRenderer? SMR;
    private OpenLipSyncBackend? _backend => LipSyncExtensionController.Instance?.backend;
    private int _context => LipSyncExtensionController.Instance?.OLSContext ?? 0;
    public string[] BlendshapeTargets = new string[15];

    private Frame frame = new();

    void Awake()
    {
        _audioSource = GetComponent<AudioSource>();
    }

    void Update()
    {
        if (SMR == null) return;
        for (int i = 0; i < BlendshapeTargets.Length; i++)
        {
            int index = SMR.sharedMesh.GetBlendShapeIndex(BlendshapeTargets[i]);
            if (index >= 0)
            {
                SMR.SetBlendShapeWeight(index, frame.Visemes[i] * 100f);
            }
        }
    }

    void OnAudioFilterRead(float[] data, int channels)
    {
        if (_backend == null || _context == 0) return;

        _backend.ProcessFrameFloat(_context, data, channels == 2, ref frame);

        for (int i = 0; i < data.Length; i++)
        {
            data[i] = 0f; // silence the audio output
        }
    }

    public void ResetVisemes()
    {
        frame.Reset();
    }
}