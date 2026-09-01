using System;
using OpenLipSync.Inference.Audio;
using OpenLipSync.Inference.OVRCompat;

namespace OpenLipSync.Inference;

/// <summary>
/// Audio processing context for a single audio stream.
/// Handles resampling, buffering, and mel spectrogram computation.
/// </summary>
internal sealed class AudioContext : IDisposable
{
    private readonly AudioRingBuffer _ringBuffer;
    private readonly MelSpectrogramProcessor _melProcessor;
    private readonly AudioResampler? _resampler;
    private readonly float[] _latestVisemeResults;
    private readonly float[] _probabilityBuffer; // reused buffer sized to model visemes
    private float _smoothing = 0.7f;
    private int _frameNumber;
    private bool _disposed;

    public AudioContext(int inputSampleRate, AudioProcessingConfig audioConfig, int modelVisemeCount)
    {
        _ringBuffer = new AudioRingBuffer(audioConfig.SampleRate * 3); // 3 seconds at target sample rate
        _melProcessor = new MelSpectrogramProcessor(audioConfig);
        _resampler = inputSampleRate != audioConfig.SampleRate
            ? new AudioResampler(inputSampleRate, audioConfig.SampleRate)
            : null;
        var modelVisemeCount1 = modelVisemeCount > 0 ? modelVisemeCount : Frame.VisemeCount;
        _latestVisemeResults = new float[modelVisemeCount1];
        _probabilityBuffer = new float[modelVisemeCount1];
        if (_latestVisemeResults.Length > 0) _latestVisemeResults[0] = 1f;
        _frameNumber = 0;
    }

    public void ProcessAudio(ReadOnlySpan<float> audioSamples)
    {
        if (_disposed) return;

        // Resample if needed
        if (_resampler is null)
        {
            _ringBuffer.Write(audioSamples);
        }
        else
        {
            var resampled = _resampler.Resample(audioSamples);
            _ringBuffer.Write(resampled.AsSpan());
        }
    }

    public bool TryGetNextMelFrame(out float[] melFeatures)
    {
        return _melProcessor.TryProcessNextHop(_ringBuffer, out melFeatures);
    }

    public float[] GetInferenceBuffer()
    {
        return _probabilityBuffer;
    }

    public void UpdateLatestResults(float[] visemeProbs)
    {
        if (_disposed || visemeProbs.Length == 0) return;

        // Apply smoothing over overlapping indices; ignore extras
        int n = Math.Min(_latestVisemeResults.Length, visemeProbs.Length);
        for (int i = 0; i < n; i++)
        {
            _latestVisemeResults[i] = _latestVisemeResults[i] * _smoothing + visemeProbs[i] * (1f - _smoothing);
        }
    }

    public void UpdateFrame(ref Frame frame)
    {
        if (_disposed) return;

        frame.frameNumber = ++_frameNumber;
        frame.frameDelay = 0;

        // Copy model visemes into OVR frame with truncate/pad
        int copyCount = Math.Min(_latestVisemeResults.Length, frame.Visemes.Length);
        Array.Copy(_latestVisemeResults, frame.Visemes, copyCount);
        if (copyCount < frame.Visemes.Length)
        {
            Array.Clear(frame.Visemes, copyCount, frame.Visemes.Length - copyCount);
        }

        // No laughter score from model
        frame.laughterScore = 0f;
    }

    public Result SendSignal(Signals signal, int arg1)
    {
        if (_disposed) return Result.Unknown;

        switch (signal)
        {
            case Signals.VisemeSmoothing:
                _smoothing = Math.Clamp(arg1 / 100f, 0f, 1f);
                return Result.Success;
            
            default:
                return Result.Success; // Ignore unsupported signals
        }
    }

    public void Reset()
    {
        if (_disposed) return;

        _ringBuffer.Clear();
        Array.Clear(_latestVisemeResults, 0, _latestVisemeResults.Length);
        if (_latestVisemeResults.Length > 0) _latestVisemeResults[0] = 1f;
        _frameNumber = 0;
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            _ringBuffer.Dispose();
            _melProcessor.Dispose();
            _disposed = true;
        }
    }
}