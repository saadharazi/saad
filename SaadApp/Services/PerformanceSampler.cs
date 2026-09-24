using System.Diagnostics;

namespace SaadApp.Services;

/// <summary>
/// قياس استهلاك التطبيق الفعلي: نسبة المعالج والذاكرة.
/// </summary>
public sealed class PerformanceSampler
{
    private readonly Process _process = Process.GetCurrentProcess();
    private TimeSpan _lastCpuTime;
    private long _lastTimestamp;

    public PerformanceSampler()
    {
        _lastCpuTime = _process.TotalProcessorTime;
        _lastTimestamp = Stopwatch.GetTimestamp();
    }

    /// <summary>يرجع (نسبة المعالج %، الذاكرة بالميجابايت) منذ آخر قياس.</summary>
    public (double CpuPercent, double MemoryMb) Sample()
    {
        _process.Refresh();
        TimeSpan cpu = _process.TotalProcessorTime;
        double elapsedMs = Stopwatch.GetElapsedTime(_lastTimestamp).TotalMilliseconds;

        double percent = elapsedMs > 0
            ? (cpu - _lastCpuTime).TotalMilliseconds / (elapsedMs * Environment.ProcessorCount) * 100
            : 0;

        _lastCpuTime = cpu;
        _lastTimestamp = Stopwatch.GetTimestamp();

        return (Math.Clamp(percent, 0, 100), _process.WorkingSet64 / (1024.0 * 1024.0));
    }
}
