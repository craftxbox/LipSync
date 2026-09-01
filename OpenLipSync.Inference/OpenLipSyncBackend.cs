using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using Newtonsoft.Json;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using OpenLipSync.Inference.Audio;
using OpenLipSync.Inference.OVRCompat;
using System;
using System.Linq;
using System.Threading;
using System.IO;
using LipSync;

namespace OpenLipSync.Inference;

/// <summary>
/// OpenLipSync backend implementation that processes audio through a trained TCN model
/// to generate real-time viseme predictions compatible with OVR LipSync interface.
/// </summary>
public sealed class OpenLipSyncBackend : IOvrLipSyncBackend
{
    private readonly object _lock = new();
    private readonly ConcurrentDictionary<int, AudioContext> _contexts = new();
    private InferenceSession? _onnxSession;
    private ModelConfig? _modelConfig;
    private AudioProcessingConfig? _audioConfig;
    private string? _defaultModelPath;
    private int _nextContextId = 1;
    private int _inputSampleRate;
    private bool _initialized;
    private bool _disposed;
    private string? _lastError;
    
    // Cached model properties to avoid per-frame checks
    private bool _isMultiLabel;
    private int _numVisemes = Frame.VisemeCount;

    public bool IsInitialized => _initialized;
    public int SampleRate => _inputSampleRate;
    public string? DefaultModelPath { get => _defaultModelPath; set => _defaultModelPath = value; }
    public string? LastError => _lastError;

    /// <summary>
    /// Initialize the backend with the specified audio parameters.
    /// Automatically loads the ONNX model and configuration.
    /// </summary>
    public Result Initialize(int sampleRate, int bufferSize)
    {
        if (_disposed) return Result.Unknown;
        if (_initialized) return Result.Success;

        try
        {
            _inputSampleRate = sampleRate;

            // Load ONNX model and configuration using precedence (no preferred path here)
            if (!LoadModel(null))
            {
                return Result.Unknown; // Model not found or failed to load
            }

            _initialized = true;
            return Result.Success;
        }
        catch (Exception ex)
        {
            Plugin.Log?.Warn($"OpenLipSync initialization failed: {ex.Message}");
            _lastError = $"Initialization failed: {ex.Message}";
            return Result.CannotCreateContext;
        }
    }

    public void Shutdown()
    {
        if (!_initialized) return;

        lock (_lock)
        {
            // Dispose all contexts
            foreach (var context in _contexts.Values)
            {
                context.Dispose();
            }
            _contexts.Clear();

            // Dispose model
            _onnxSession?.Dispose();
            _onnxSession = null;

            _initialized = false;
        }
    }

    public Result CreateContext(ref int context, ContextProviders provider, int sampleRate = 0, bool enableAcceleration = false)
    {
        if (!_initialized) return Result.Unknown;

        if (_audioConfig == null)
        {
            if (!LoadModel(null)) return Result.CannotCreateContext;
        }

        if (_audioConfig == null) return Result.CannotCreateContext;

        try
        {
            var audioContext = new AudioContext(_inputSampleRate, _audioConfig, _numVisemes);
            Interlocked.Increment(ref _nextContextId);
            context = _nextContextId;
            _contexts[context] = audioContext;

            return Result.Success;
        }
        catch
        {
            _lastError = "Failed to create audio context";
            return Result.CannotCreateContext;
        }
    }

    public Result CreateContextWithModelFile(ref int context, ContextProviders provider, string modelPath, int sampleRate = 0, bool enableAcceleration = false)
    {
        if (!_initialized) return Result.Unknown;

        // Load/replace model based on explicit per-context override (affects backend session in this implementation)
        if (!LoadModel(modelPath)) return Result.CannotCreateContext;

        return CreateContext(ref context, provider, sampleRate, enableAcceleration);
    }

    public Result DestroyContext(int context)
    {
        if (!_initialized) return Result.Unknown;

        if (_contexts.TryRemove(context, out var audioContext))
        {
            audioContext.Dispose();
            return Result.Success;
        }

        return Result.InvalidParam;
    }

    public Result ResetContext(int context)
    {
        if (!_initialized) return Result.Unknown;

        if (_contexts.TryGetValue(context, out var audioContext))
        {
            audioContext.Reset();
            return Result.Success;
        }

        return Result.InvalidParam;
    }

    public Result SendSignal(int context, Signals signal, int arg1)
    {
        if (!_initialized) return Result.Unknown;

        if (_contexts.TryGetValue(context, out var audioContext))
        {
            return audioContext.SendSignal(signal, arg1);
        }

        return Result.InvalidParam;
    }

    public Result ProcessFrameFloat(int context, ReadOnlySpan<float> audio, bool stereo, ref Frame frame)
    {
        if (!_initialized || _onnxSession == null) { _lastError = "Backend not initialized or model not loaded"; return Result.Unknown; }

        if (!_contexts.TryGetValue(context, out var audioContext))
        {
            _lastError = $"Invalid context: {context}";
            return Result.InvalidParam;
        }

        try
        {
            // Convert stereo to mono if needed; avoid allocation when already mono
            if (stereo)
            {
                var monoAudio = ConvertStereoToMono(audio);
                audioContext.ProcessAudio(monoAudio.AsSpan());
            }
            else
            {
                audioContext.ProcessAudio(audio);
            }

            // Try to get mel features and run inference
            while (audioContext.TryGetNextMelFrame(out var melFeatures))
            {
                // Run inference into reusable buffer and update smoothed results
                RunInferenceInto(melFeatures, audioContext.GetInferenceBuffer());
                audioContext.UpdateLatestResults(audioContext.GetInferenceBuffer());
            }

            // Update frame with latest results
            audioContext.UpdateFrame(ref frame);

            return Result.Success;
        }
        catch (Exception ex)
        {
            Plugin.Log?.Warn($"ProcessFrameFloat error: {ex.Message}");
            _lastError = $"ProcessFrameFloat: {ex.Message}";
            return Result.Unknown;
        }
    }

    public Result ProcessFrameShort(int context, ReadOnlySpan<short> audio, bool stereo, ref Frame frame)
    {
        if (!_initialized) { _lastError = "Backend not initialized"; return Result.Unknown; }

        // Convert short samples to float
        var floatAudio = new float[audio.Length];
        for (int i = 0; i < audio.Length; i++)
        {
            floatAudio[i] = audio[i] / 32768f;
        }

        return ProcessFrameFloat(context, floatAudio, stereo, ref frame);
    }

    private bool LoadModel(string? preferredModelPath)
    {
        try
        {
            string? modelPath = null;
            string? configPath = null;
            var searchLog = new List<string>();

            // 1) Preferred (per-context) override
            ResolveFrom(preferredModelPath, ref modelPath, ref configPath, searchLog, label: "preferred");

            // 2) Backend default path
            if (modelPath == null)
                ResolveFrom(_defaultModelPath, ref modelPath, ref configPath, searchLog, label: "backend default");

            // 3) Environment override
            if (modelPath == null)
                ResolveFrom(Environment.GetEnvironmentVariable("OPENLIPSYNC_MODEL_PATH"), ref modelPath, ref configPath, searchLog, label: "env OPENLIPSYNC_MODEL_PATH");

            // 4) Workspace discovery
            if (modelPath == null)
            {
                var standard = new[] { "model.onnx", "model/model.onnx", "export/model.onnx" };
                foreach (var path in standard)
                {
                    if (File.Exists(path))
                    {
                        modelPath = path;
                        var dir = Path.GetDirectoryName(path) ?? "";
                        var potentialConfigPath = Path.Combine(dir, "config.json");
                        if (File.Exists(potentialConfigPath)) configPath = potentialConfigPath;
                        break;
                    }
                    else
                    {
                        searchLog.Add($"workspace: not found '{path}'");
                    }
                }
            }

            if (modelPath == null)
            {
                Plugin.Log?.Warn("ONNX model file not found");
                _lastError = "Model not found. Search steps:\n" + string.Join("\n", searchLog);
                return false;
            }

            // Load model configuration
            if (configPath != null)
            {
                var configJson = File.ReadAllText(configPath);
                _modelConfig = JsonConvert.DeserializeObject<ModelConfig>(configJson);
                
                if (_modelConfig != null)
                {
                    _audioConfig = AudioProcessingConfig.FromModelConfig(_modelConfig);
                    
                    Plugin.Log?.Warn($"Loaded config: {_audioConfig.SampleRate}Hz, {_audioConfig.NMels} mels, {_audioConfig.Fps}fps, {_audioConfig.HopLengthSamples} hop samples");
                    
                    // Log the sample rate from config
                    Plugin.Log?.Warn($"Model expects {_audioConfig.SampleRate}Hz (from config)");
                    
                    // Cache flags and constants for runtime
                    _isMultiLabel = _modelConfig.Training?.MultiLabel ?? false;
                    _numVisemes = _modelConfig.Model?.NumVisemes ?? Frame.VisemeCount;
                }
                else
                {
                    _lastError = $"Failed to parse config.json at '{configPath}'";
                }
            }
            else
            {
                _lastError = "No config.json found next to model";
            }

            // Create ONNX session with CPU provider
            var sessionOptions = new SessionOptions();
            sessionOptions.LogSeverityLevel = OrtLoggingLevel.ORT_LOGGING_LEVEL_WARNING;
            sessionOptions.GraphOptimizationLevel = GraphOptimizationLevel.ORT_ENABLE_ALL;
            sessionOptions.ExecutionMode = ExecutionMode.ORT_SEQUENTIAL;
            sessionOptions.InterOpNumThreads = 1;
            sessionOptions.IntraOpNumThreads = 1;

            try
            {
                _onnxSession = new InferenceSession(modelPath, sessionOptions);
            }
            catch (Exception exCreate)
            {
                _lastError = $"Failed to create ONNX session for '{modelPath}': {exCreate.Message}";
                throw;
            }

            Plugin.Log?.Warn($"Loaded OpenLipSync model: {modelPath}");
            return true;
        }
        catch (Exception ex)
        {
            Plugin.Log?.Warn($"Failed to load model: {ex.Message}");
            if (string.IsNullOrEmpty(_lastError)) _lastError = $"LoadModel exception: {ex.Message}";
            return false;
        }

        static void ResolveFrom(string? pathOrDir, ref string? modelPath, ref string? configPath, List<string> searchLog, string label)
        {
            if (modelPath != null) return;
            if (string.IsNullOrWhiteSpace(pathOrDir)) return;

            try
            {
                if (File.Exists(pathOrDir))
                {
                    modelPath = pathOrDir;
                    var dir = Path.GetDirectoryName(pathOrDir) ?? "";
                    var cfg = Path.Combine(dir, "config.json");
                    if (File.Exists(cfg)) configPath = cfg;
                    searchLog.Add($"{label}: file '{pathOrDir}'");
                    return;
                }

                if (Directory.Exists(pathOrDir))
                {
                    var candidates = new[]
                    {
                        Path.Combine(pathOrDir, "model.onnx"),
                        Path.Combine(pathOrDir, "model", "model.onnx"),
                        Path.Combine(pathOrDir, "export", "model.onnx"),
                    };
                    foreach (var p in candidates)
                    {
                        if (File.Exists(p))
                        {
                            modelPath = p;
                            var dir = Path.GetDirectoryName(p) ?? "";
                            var cfg = Path.Combine(dir, "config.json");
                            if (File.Exists(cfg)) configPath = cfg;
                            searchLog.Add($"{label}: found '{p}'");
                            return;
                        }
                        else
                        {
                            searchLog.Add($"{label}: not found '{p}'");
                        }
                    }
                }
                else
                {
                    searchLog.Add($"{label}: path does not exist '{pathOrDir}'");
                }
            }
            catch (Exception ex)
            {
                searchLog.Add($"{label}: exception '{ex.Message}'");
            }
        }
    }

    private void RunInferenceInto(float[] melFeatures, float[] destination)
    {
        if (_onnxSession == null || _audioConfig == null) { Array.Clear(destination,0,destination.Length); return; }

        try
        {
            // Prepare input tensor: [batch=1, time=1, features=n_mels]
            var inputTensor = new DenseTensor<float>(melFeatures, new[] { 1, 1, melFeatures.Length });

            // Run inference
            using var results = _onnxSession.Run(new[] { NamedOnnxValue.CreateFromTensor("audio_features", inputTensor) });

            // Extract output tensor: [batch=1, time=1, classes=num_visemes]
            var outputTensor = results.First().AsTensor<float>();

            // Compute probabilities directly into destination (no intermediate arrays)
            int numVisemes = Math.Min(destination.Length, _numVisemes);
            if (_isMultiLabel)
            {
                // Sigmoid in-place
                for (int i = 0; i < numVisemes; i++)
                {
                    float x = outputTensor[0, 0, i];
                    x = Math.Clamp(x, -50f, 50f);
                    destination[i] = 1f / (1f + MathF.Exp(-x));
                }
            }
            else
            {
                // Softmax in-place
                float maxLogit = float.MinValue;
                for (int i = 0; i < numVisemes; i++)
                {
                    float v = outputTensor[0, 0, i];
                    if (v > maxLogit) maxLogit = v;
                }
                float sum = 0f;
                for (int i = 0; i < numVisemes; i++)
                {
                    float e = MathF.Exp(outputTensor[0, 0, i] - maxLogit);
                    destination[i] = e;
                    sum += e;
                }
                if (sum > 0f)
                {
                    float inv = 1f / sum;
                    for (int i = 0; i < numVisemes; i++) destination[i] *= inv;
                }
                else
                {
                    Array.Clear(destination, 0, numVisemes);
                }
            }
            if (numVisemes < destination.Length) Array.Clear(destination, numVisemes, destination.Length - numVisemes);
        }
        catch (Exception ex)
        {
            Plugin.Log?.Warn($"Inference error: {ex.Message}");
            Array.Clear(destination,0,destination.Length);
        }
    }

    // Removed allocating Softmax/Sigmoid helpers; compute directly into destination in RunInferenceInto

    private static float[] ConvertStereoToMono(ReadOnlySpan<float> stereoAudio)
    {
        var monoAudio = new float[stereoAudio.Length / 2];
        for (int i = 0; i < monoAudio.Length; i++)
        {
            monoAudio[i] = (stereoAudio[i * 2] + stereoAudio[i * 2 + 1]) * 0.5f;
        }
        return monoAudio;
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            Shutdown();
            _disposed = true;
        }
    }
}