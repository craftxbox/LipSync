using System;
using System.Runtime.CompilerServices;

namespace OpenLipSync.Inference.OVRCompat;

public sealed class OVRLipSyncContext : IDisposable
{
    private readonly OVRLipSyncInterface _ovr;
    private int _context;
    private Frame _frame = new Frame();

    public bool IsInitialized => _context != 0;
    public float Smoothing { get; private set; } = -1f; // 0..1

    public OVRLipSyncContext(OVRLipSyncInterface ovr)
    {
        _ovr = ovr ?? throw new ArgumentNullException(nameof(ovr));
        var res = _ovr.CreateContext(ref _context, ContextProviders.Enhanced_with_Laughter, _ovr.SampleRate, enableAcceleration: true);
        if (res != Result.Success) _context = 0;
    }

    public void Update(float smoothing)
    {
        if (Math.Abs(smoothing - Smoothing) < 1e-6f) return;
        Smoothing = smoothing;

        if (_context == 0) return;

        int smoothInt = Clamp((int)(Math.Clamp(Smoothing, 0f, 1f) * 100f), 0, 100);
        _ = _ovr.SendSignal(_context, Signals.VisemeSmoothing, smoothInt);
    }

    public void Dispose()
    {
        if (_context != 0)
        {
            _ovr.DestroyContext(_context);
            _context = 0;
        }
    }

    // Analyze one frame of audio and write 16 outputs: 15 visemes + laughter
    public void Analyze(float[] audioData, float[] analysis, Action? onDone)
    {
        if (_context == 0 || audioData == null || analysis == null) { onDone?.Invoke(); return; }

        var res = _ovr.ProcessFrame(_context, audioData, _frame, stereo: false);
        if (res == Result.Success)
        {
            int n = Math.Min(Frame.VisemeCount, analysis.Length);
            for (int i = 0; i < n; i++)
                analysis[i] = _frame.Visemes[i];

            if (analysis.Length > Frame.VisemeCount)
                analysis[Frame.VisemeCount] = _frame.laughterScore;
        }
        else
        {
            // Preserve previous frame to avoid flicker on transient failures
            int n = Math.Min(Frame.VisemeCount, analysis.Length);
            for (int i = 0; i < n; i++)
                analysis[i] = _frame.Visemes[i];

            if (analysis.Length > Frame.VisemeCount)
                analysis[Frame.VisemeCount] = _frame.laughterScore;
        }

        onDone?.Invoke();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int Clamp(int v, int min, int max) => v < min ? min : (v > max ? max : v);
}