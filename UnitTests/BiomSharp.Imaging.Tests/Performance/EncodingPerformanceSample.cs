using BiomSharp.Imaging.Wsq;
using System.Diagnostics;
using System.Security.Cryptography;

namespace BiomSharp.Imaging.Tests.Performance;

internal sealed record EncodingPerformanceSample(byte[]? Bytes, double Milliseconds, EncodingSampleMetrics Metrics)
{
    internal static EncodingPerformanceSample Measure(WsqCodec codec, WsqParameters parameters, Process process)
    {
        MemorySnapshot before = MemorySnapshot.Capture(process);
        int gen0 = GC.CollectionCount(0);
        int gen1 = GC.CollectionCount(1);
        int gen2 = GC.CollectionCount(2);
        double cpuBefore = process.TotalProcessorTime.TotalMilliseconds;
        long allocatedBefore = GC.GetAllocatedBytesForCurrentThread();
        long started = Stopwatch.GetTimestamp();

        byte[]? bytes = codec.Encode(parameters);

        long stopped = Stopwatch.GetTimestamp();
        long allocatedBytes = GC.GetAllocatedBytesForCurrentThread() - allocatedBefore;
        double cpuMilliseconds = process.TotalProcessorTime.TotalMilliseconds - cpuBefore;
        int gen0Collections = GC.CollectionCount(0) - gen0;
        int gen1Collections = GC.CollectionCount(1) - gen1;
        int gen2Collections = GC.CollectionCount(2) - gen2;
        MemorySnapshot after = MemorySnapshot.Capture(process);

        return new EncodingPerformanceSample(bytes, (stopped - started) * 1000d / Stopwatch.Frequency,
            new EncodingSampleMetrics(allocatedBytes, cpuMilliseconds, gen0Collections, gen1Collections,
                gen2Collections, before, after, bytes?.Length ?? 0,
                bytes == null ? null : Convert.ToHexString(SHA256.HashData(bytes))));
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

internal sealed record EncodingSampleMetrics(long AllocatedBytes, double ProcessCpuMilliseconds,
    int Gen0Collections, int Gen1Collections, int Gen2Collections, MemorySnapshot Before, MemorySnapshot After,
    int OutputBytes, string? OutputSha256);

internal sealed record EncodingMetrics(EncodingSampleMetrics[] Samples)
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
