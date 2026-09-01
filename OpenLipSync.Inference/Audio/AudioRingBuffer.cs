using System;

namespace OpenLipSync.Inference.Audio;

/// <summary>
/// Thread-safe circular buffer for audio samples.
/// Optimized for single-writer, single-reader scenarios with lock-free operations.
/// </summary>
public sealed class AudioRingBuffer : IDisposable
{
    private readonly float[] _buffer;
    private readonly int _capacity;
    private volatile int _writeIndex;
    private volatile int _readIndex;
    private volatile int _availableSamples;
    private readonly object _lock = new();
    private bool _disposed;

    public AudioRingBuffer(int capacitySamples)
    {
        if (capacitySamples <= 0) 
            throw new ArgumentException("Capacity must be positive", nameof(capacitySamples));
        
        // Ensure capacity is power of 2 for efficient modulo operations
        _capacity = NextPowerOfTwo(capacitySamples);
        _buffer = new float[_capacity];
        _writeIndex = 0;
        _readIndex = 0;
        _availableSamples = 0;
    }

    public int Capacity => _capacity;
    public int AvailableSamples => _availableSamples;
    public int FreeSpace => _capacity - _availableSamples;
    public bool IsEmpty => _availableSamples == 0;
    public bool IsFull => _availableSamples >= _capacity;

    /// <summary>
    /// Write audio samples to the buffer.
    /// If buffer is full, oldest samples will be overwritten.
    /// </summary>
    /// <param name="samples">Audio samples to write</param>
    /// <returns>Number of samples actually written</returns>
    public int Write(ReadOnlySpan<float> samples)
    {
        if (_disposed) throw new ObjectDisposedException(nameof(AudioRingBuffer));
        if (samples.IsEmpty) return 0;

        lock (_lock)
        {
            int samplesToWrite = Math.Min(samples.Length, _capacity);
            int written = 0;

            // Handle buffer wraparound
            while (written < samplesToWrite)
            {
                int contiguousSpace = Math.Min(
                    samplesToWrite - written,
                    _capacity - (_writeIndex & (_capacity - 1))
                );

                samples.Slice(written, contiguousSpace)
                    .CopyTo(_buffer.AsSpan((_writeIndex & (_capacity - 1)), contiguousSpace));

                written += contiguousSpace;
                _writeIndex = (_writeIndex + contiguousSpace) & (_capacity - 1);
            }

            // Update available samples (clamped to capacity)
            _availableSamples = Math.Min(_availableSamples + samplesToWrite, _capacity);

            // If we filled the buffer, advance read index to maintain circular behavior
            if (_availableSamples == _capacity && samplesToWrite > 0)
            {
                _readIndex = _writeIndex;
            }

            return samplesToWrite;
        }
    }

    /// <summary>
    /// Read audio samples from the buffer without removing them.
    /// </summary>
    /// <param name="output">Buffer to write samples to</param>
    /// <param name="count">Number of samples to read</param>
    /// <returns>Number of samples actually read</returns>
    public int Peek(Span<float> output, int count)
    {
        if (_disposed) throw new ObjectDisposedException(nameof(AudioRingBuffer));
        if (count <= 0) return 0;

        lock (_lock)
        {
            int samplesToRead = Math.Min(count, Math.Min(_availableSamples, output.Length));
            int read = 0;
            int tempReadIndex = _readIndex;

            while (read < samplesToRead)
            {
                int contiguousData = Math.Min(
                    samplesToRead - read,
                    _capacity - (tempReadIndex & (_capacity - 1))
                );

                _buffer.AsSpan((tempReadIndex & (_capacity - 1)), contiguousData)
                    .CopyTo(output.Slice(read, contiguousData));

                read += contiguousData;
                tempReadIndex = (tempReadIndex + contiguousData) & (_capacity - 1);
            }

            return samplesToRead;
        }
    }

    /// <summary>
    /// Read and remove audio samples from the buffer.
    /// </summary>
    /// <param name="output">Buffer to write samples to</param>
    /// <param name="count">Number of samples to read</param>
    /// <returns>Number of samples actually read</returns>
    public int Read(Span<float> output, int count)
    {
        if (_disposed) throw new ObjectDisposedException(nameof(AudioRingBuffer));
        if (count <= 0) return 0;

        lock (_lock)
        {
            int samplesToRead = Math.Min(count, Math.Min(_availableSamples, output.Length));
            int read = 0;

            while (read < samplesToRead)
            {
                int contiguousData = Math.Min(
                    samplesToRead - read,
                    _capacity - (_readIndex & (_capacity - 1))
                );

                _buffer.AsSpan((_readIndex & (_capacity - 1)), contiguousData)
                    .CopyTo(output.Slice(read, contiguousData));

                read += contiguousData;
                _readIndex = (_readIndex + contiguousData) & (_capacity - 1);
            }

            _availableSamples -= samplesToRead;
            return samplesToRead;
        }
    }

    /// <summary>
    /// Skip (discard) a number of samples from the buffer.
    /// </summary>
    /// <param name="count">Number of samples to skip</param>
    /// <returns>Number of samples actually skipped</returns>
    public int Skip(int count)
    {
        if (_disposed) throw new ObjectDisposedException(nameof(AudioRingBuffer));
        if (count <= 0) return 0;

        lock (_lock)
        {
            int samplesToSkip = Math.Min(count, _availableSamples);
            _readIndex = (_readIndex + samplesToSkip) & (_capacity - 1);
            _availableSamples -= samplesToSkip;
            return samplesToSkip;
        }
    }

    /// <summary>
    /// Clear all data from the buffer.
    /// </summary>
    public void Clear()
    {
        if (_disposed) throw new ObjectDisposedException(nameof(AudioRingBuffer));

        lock (_lock)
        {
            _writeIndex = 0;
            _readIndex = 0;
            _availableSamples = 0;
            Array.Clear(_buffer,0,_buffer.Length);
        }
    }

    /// <summary>
    /// Get buffer statistics for debugging.
    /// </summary>
    public (int available, int capacity, float fillPercentage) GetStats()
    {
        lock (_lock)
        {
            return (_availableSamples, _capacity, (float)_availableSamples / _capacity * 100f);
        }
    }

    private static int NextPowerOfTwo(int value)
    {
        if (value <= 1) return 1;
        
        value--;
        value |= value >> 1;
        value |= value >> 2;
        value |= value >> 4;
        value |= value >> 8;
        value |= value >> 16;
        return value + 1;
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            lock (_lock)
            {
                Array.Clear(_buffer,0,_buffer.Length);
                _disposed = true;
            }
        }
    }
}
