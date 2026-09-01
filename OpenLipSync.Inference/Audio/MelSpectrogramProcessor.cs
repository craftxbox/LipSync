using System;
using System.Numerics;

namespace OpenLipSync.Inference.Audio;

/// <summary>
/// Real-time mel spectrogram processor optimized for OpenLipSync TCN model.
/// Converts audio frames to mel-scale spectrograms using configurable parameters.
/// </summary>
public sealed class MelSpectrogramProcessor : IDisposable
{
    // Audio processing parameters (loaded from model config)
    private readonly int _sampleRate;
    private readonly int _hopLength;
    private readonly int _windowLength;
    private readonly int _nFft;
    private readonly int _nMels;
    private readonly float _fMin;
    private readonly float _fMax;
    
    private readonly float[] _window;
    private readonly float[] _fftBuffer;
    private readonly Complex[] _fftInput;
    private readonly Complex[] _fftOutput;
    private readonly float[,] _melFilterBank;
    private readonly float[] _powerSpectrum; // reused per hop
    private readonly float[] _melSpectrum;   // reused per hop (returned)
    private readonly float[] _windowBuffer;
    private readonly FFTProcessor _fft;
    
    // State for overlapping windows
    private readonly float[] _previousSamples;
    private bool _disposed;

    public MelSpectrogramProcessor(AudioProcessingConfig config)
    {
        _sampleRate = config.SampleRate;
        _hopLength = config.HopLengthSamples;
        _windowLength = config.WindowLengthSamples;
        _nFft = config.NFft;
        _nMels = config.NMels;
        _fMin = config.FMin;
        _fMax = config.FMax;
        
        _window = CreateHannWindow(_windowLength);
        _fftBuffer = new float[_nFft];
        _fftInput = new Complex[_nFft];
        _fftOutput = new Complex[_nFft];
        _windowBuffer = new float[_windowLength];
        _previousSamples = new float[_windowLength - _hopLength];
        _melFilterBank = CreateMelFilterBank();
        _powerSpectrum = new float[_nFft / 2 + 1];
        _melSpectrum = new float[_nMels];
        _fft = new FFTProcessor(_nFft);
    }

    public int SampleRate => _sampleRate;
    public int HopLength => _hopLength;
    public int WindowLength => _windowLength;
    public int MelBands => _nMels;

    /// <summary>
    /// Process a hop of audio samples to produce mel spectrogram features.
    /// </summary>
    /// <param name="hopSamples">Audio samples (must be exactly HOP_LENGTH samples)</param>
    /// <returns>Mel spectrogram features (N_MELS bands)</returns>
    public float[] ProcessHop(ReadOnlySpan<float> hopSamples)
    {
        if (_disposed) throw new ObjectDisposedException(nameof(MelSpectrogramProcessor));
        if (hopSamples.Length != _hopLength)
            throw new ArgumentException($"Expected {_hopLength} samples, got {hopSamples.Length}", nameof(hopSamples));

        // Build windowed frame from previous samples + new hop
        // Copy previous samples (overlap from last frame)
        _previousSamples.AsSpan().CopyTo(_windowBuffer.AsSpan(0, _previousSamples.Length));
        
        // Copy new hop samples
        hopSamples.CopyTo(_windowBuffer.AsSpan(_previousSamples.Length, _hopLength));

        // Update previous samples for next hop
        _windowBuffer.AsSpan(_hopLength, _previousSamples.Length)
            .CopyTo(_previousSamples.AsSpan());

        // Apply Hann window
        for (int i = 0; i < _windowLength; i++)
        {
            _windowBuffer[i] *= _window[i];
        }

        // Zero-pad to FFT size
        Array.Clear(_fftBuffer,0, _fftBuffer.Length);
        _windowBuffer.AsSpan().CopyTo(_fftBuffer.AsSpan(0, _windowLength));

        // Convert to complex for FFT
        for (int i = 0; i < _nFft; i++)
        {
            _fftInput[i] = new Complex(_fftBuffer[i], 0);
        }

        // Compute FFT
        _fft.Forward(_fftInput.AsSpan(), _fftOutput.AsSpan());

        // Compute power spectrum (magnitude squared) into reused buffer
        for (int i = 0; i < _powerSpectrum.Length; i++)
        {
            _powerSpectrum[i] = (float)(_fftOutput[i].Magnitude * _fftOutput[i].Magnitude);
        }

        // Apply mel filter bank into reused mel buffer
        for (int mel = 0; mel < _nMels; mel++)
        {
            float sum = 0f;
            for (int bin = 0; bin < _powerSpectrum.Length; bin++)
            {
                sum += _powerSpectrum[bin] * _melFilterBank[mel, bin];
            }
            _melSpectrum[mel] = sum;
        }

        // Convert to log scale (dB) from power spectrum.
        // Use small floor like torchaudio/librosa to avoid log(0).
        for (int i = 0; i < _nMels; i++)
        {
            _melSpectrum[i] = 10f * MathF.Log10(MathF.Max(_melSpectrum[i], 1e-10f));
        }

        return _melSpectrum;
    }

    /// <summary>
    /// Check if we can process a hop from the ring buffer.
    /// </summary>
    public bool CanProcessHop(AudioRingBuffer ringBuffer)
    {
        return ringBuffer.AvailableSamples >= _hopLength;
    }

    /// <summary>
    /// Process next hop from ring buffer if available.
    /// </summary>
    public bool TryProcessNextHop(AudioRingBuffer ringBuffer, out float[] melFeatures)
    {
        if (!CanProcessHop(ringBuffer)) { melFeatures = Array.Empty<float>(); return false; }

        Span<float> hopBuffer = stackalloc float[_hopLength];
        int read = ringBuffer.Read(hopBuffer, _hopLength);
        
        if (read == _hopLength) { melFeatures = ProcessHop(hopBuffer); return true; }

        melFeatures = [];
        return false;
    }

    private static float[] CreateHannWindow(int length)
    {
        var window = new float[length];
        for (int i = 0; i < length; i++)
        {
            window[i] = 0.5f * (1f - MathF.Cos(2f * MathF.PI * i / (length - 1)));
        }
        return window;
    }

    private float[,] CreateMelFilterBank()
    {
        var filterBank = new float[_nMels, _nFft / 2 + 1];
        
        // Convert mel scale to frequency
        float melMin = HzToMel(_fMin);
        float melMax = HzToMel(_fMax);
        
        // Create mel-spaced filter centers
        var melPoints = new float[_nMels + 2];
        for (int i = 0; i < melPoints.Length; i++)
        {
            melPoints[i] = melMin + (melMax - melMin) * i / (_nMels + 1);
        }
        
        // Convert back to Hz
        var hzPoints = new float[melPoints.Length];
        for (int i = 0; i < hzPoints.Length; i++)
        {
            hzPoints[i] = MelToHz(melPoints[i]);
        }
        
        // Convert Hz to FFT bin indices
        var binPoints = new float[hzPoints.Length];
        for (int i = 0; i < hzPoints.Length; i++)
        {
            binPoints[i] = (_nFft + 1) * hzPoints[i] / _sampleRate;
        }
        
        // Build triangular filters
        for (int mel = 0; mel < _nMels; mel++)
        {
            float left = binPoints[mel];
            float center = binPoints[mel + 1];
            float right = binPoints[mel + 2];
            
            for (int bin = 0; bin < _nFft / 2 + 1; bin++)
            {
                if (bin >= left && bin <= center)
                {
                    filterBank[mel, bin] = (bin - left) / (center - left);
                }
                else if (bin > center && bin <= right)
                {
                    filterBank[mel, bin] = (right - bin) / (right - center);
                }
                else
                {
                    filterBank[mel, bin] = 0f;
                }
            }
        }
        
        return filterBank;
    }

    private static float HzToMel(float hz)
    {
        return 2595f * MathF.Log10(1f + hz / 700f);
    }

    private static float MelToHz(float mel)
    {
        return 700f * (MathF.Pow(10f, mel / 2595f) - 1f);
    }

    private static void NormalizePerUtterance(Span<float> features)
    {
        // Calculate mean and standard deviation
        float sum = 0f;
        for (int i = 0; i < features.Length; i++)
        {
            sum += features[i];
        }
        float mean = sum / features.Length;

        float sumSquares = 0f;
        for (int i = 0; i < features.Length; i++)
        {
            float diff = features[i] - mean;
            sumSquares += diff * diff;
        }
        float std = MathF.Sqrt(sumSquares / features.Length);

        // Avoid division by zero
        if (std < 1e-8f) std = 1e-8f;

        // Normalize
        for (int i = 0; i < features.Length; i++)
        {
            features[i] = (features[i] - mean) / std;
        }
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            _fft?.Dispose();
            _disposed = true;
        }
    }
}