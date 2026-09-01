using System;

namespace OpenLipSync.Inference.OVRCompat;

public sealed class NullOvrLipSyncBackend : IOvrLipSyncBackend
{
    private bool _initialized;
    public string? LastError { get; private set; }

    public Result Initialize(int sampleRate, int bufferSize)
    {
        _initialized = true;
        LastError = null;
        return Result.Success;
    }

    public void Shutdown()
    {
        _initialized = false;
        LastError = null;
    }

    public void Dispose() { }

    public Result CreateContext(ref int context, ContextProviders provider, int sampleRate = 0, bool enableAcceleration = false)
    {
        if (!_initialized) return Result.CannotCreateContext;
        context = 1; // non-zero handle
        return Result.Success;
    }

    public Result CreateContextWithModelFile(ref int context, ContextProviders provider, string modelPath, int sampleRate = 0, bool enableAcceleration = false)
        => CreateContext(ref context, provider, sampleRate, enableAcceleration);

    public Result DestroyContext(int context) => _initialized ? Result.Success : Result.Unknown;

    public Result ResetContext(int context) => _initialized ? Result.Success : Result.Unknown;

    public Result SendSignal(int context, Signals signal, int arg1) => _initialized ? Result.Success : Result.Unknown;

    public Result ProcessFrameFloat(int context, ReadOnlySpan<float> audio, bool stereo, ref Frame frame)
    {
        if (!_initialized) return Result.Unknown;
        frame.frameNumber++;
        frame.frameDelay = 0;
        Array.Clear(frame.Visemes, 0, frame.Visemes.Length);
        frame.laughterScore = 0f;
        return Result.Success;
    }

    public Result ProcessFrameShort(int context, ReadOnlySpan<short> audio, bool stereo, ref Frame frame)
    {
        if (!_initialized) return Result.Unknown;
        frame.frameNumber++;
        frame.frameDelay = 0;
        Array.Clear(frame.Visemes, 0, frame.Visemes.Length);
        frame.laughterScore = 0f;
        return Result.Success;
    }
}