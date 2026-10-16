using System.Diagnostics;

namespace BiomSharp.Imaging.Tests.Performance;

internal readonly struct PerformanceMeasurement
{
    private readonly Process process;
    private readonly MemorySnapshot before;
    private readonly int gen0, gen1, gen2;
    private readonly double cpuBefore;
    private readonly long allocatedBefore, started;

    internal PerformanceMeasurement(Process process)
    {
        this.process = process;
        before = MemorySnapshot.Capture(process);
        gen0 = GC.CollectionCount(0);
        gen1 = GC.CollectionCount(1);
        gen2 = GC.CollectionCount(2);
        cpuBefore = process.TotalProcessorTime.TotalMilliseconds;
        allocatedBefore = GC.GetAllocatedBytesForCurrentThread();
        started = Stopwatch.GetTimestamp();
    }

    internal (double Milliseconds, PerformanceSampleMetrics Metrics) Complete()
    {
        long stopped = Stopwatch.GetTimestamp();
        long allocatedBytes = GC.GetAllocatedBytesForCurrentThread() - allocatedBefore;
        double cpuMilliseconds = process.TotalProcessorTime.TotalMilliseconds - cpuBefore;
        int gen0Collections = GC.CollectionCount(0) - gen0;
        int gen1Collections = GC.CollectionCount(1) - gen1;
        int gen2Collections = GC.CollectionCount(2) - gen2;
        MemorySnapshot after = MemorySnapshot.Capture(process);
        return ((stopped - started) * 1000d / Stopwatch.Frequency,
            new PerformanceSampleMetrics(allocatedBytes, cpuMilliseconds, gen0Collections, gen1Collections,
                gen2Collections, before, after, 0, null));
    }
}

internal sealed record MemorySnapshot(long ManagedHeapBytes, long WorkingSetBytes, long PrivateMemoryBytes)
{
    internal static MemorySnapshot Capture(Process process)
    {
        process.Refresh();
        return new MemorySnapshot(GC.GetTotalMemory(forceFullCollection: false),
            process.WorkingSet64, process.PrivateMemorySize64);
    }
}

internal sealed record PerformanceSampleMetrics(long AllocatedBytes, double ProcessCpuMilliseconds,
    int Gen0Collections, int Gen1Collections, int Gen2Collections, MemorySnapshot Before, MemorySnapshot After,
    int OutputBytes, string? OutputSha256);

internal sealed record PerformanceMetrics(PerformanceSampleMetrics[] Samples)
{
    public double MedianAllocatedBytes => PerformanceResult.Median(Samples.Select(sample => (double)sample.AllocatedBytes));
    public long MaximumAllocatedBytes => Samples.Max(sample => sample.AllocatedBytes);
    public double MedianProcessCpuMilliseconds => PerformanceResult.Median(Samples.Select(sample => sample.ProcessCpuMilliseconds));
    public double MedianManagedHeapBytesAfter => PerformanceResult.Median(Samples.Select(sample => (double)sample.After.ManagedHeapBytes));
    public double MedianWorkingSetBytesAfter => PerformanceResult.Median(Samples.Select(sample => (double)sample.After.WorkingSetBytes));
    public double MedianPrivateMemoryBytesAfter => PerformanceResult.Median(Samples.Select(sample => (double)sample.After.PrivateMemoryBytes));
    public double MedianOutputBytes => PerformanceResult.Median(Samples.Select(sample => (double)sample.OutputBytes));
    public int Gen0Collections => Samples.Sum(sample => sample.Gen0Collections);
    public int Gen1Collections => Samples.Sum(sample => sample.Gen1Collections);
    public int Gen2Collections => Samples.Sum(sample => sample.Gen2Collections);
}
