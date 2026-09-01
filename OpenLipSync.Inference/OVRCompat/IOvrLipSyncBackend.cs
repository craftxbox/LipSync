using System;

namespace OpenLipSync.Inference.OVRCompat;

public interface IOvrLipSyncBackend : IDisposable
{
    // Last error message for diagnostics when a call returns non-success
    string? LastError { get; }

    Result Initialize(int sampleRate, int bufferSize);
    void Shutdown();

    Result CreateContext(ref int context, ContextProviders provider, int sampleRate = 0, bool enableAcceleration = false);
    Result CreateContextWithModelFile(ref int context, ContextProviders provider, string modelPath, int sampleRate = 0, bool enableAcceleration = false);
    Result DestroyContext(int context);
    Result ResetContext(int context);

    Result SendSignal(int context, Signals signal, int arg1);

    Result ProcessFrameFloat(int context, ReadOnlySpan<float> audio, bool stereo, ref Frame frame);
    Result ProcessFrameShort(int context, ReadOnlySpan<short> audio, bool stereo, ref Frame frame);
}