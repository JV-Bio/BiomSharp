using BiomSharp.Imaging.Wsq;
using System.Text.Json;

namespace BiomSharp.Imaging.Tests.Performance;

internal sealed record PerformanceResult(string Operation, string Filename, string Setting, float BitRate,
    int Width, int Height, string InputSha256, double[] SamplesMilliseconds,
    WsqParameters Parameters, DateTimeOffset RecordedUtc, bool ValidationPassed, string? ValidationFailure,
    EncodingMetrics? EncodingMetrics)
{
    public double MedianMilliseconds => Median(SamplesMilliseconds);
    public double MaximumMilliseconds => SamplesMilliseconds.Max();
    public PerformanceComparison? Comparison { get; set; }

    internal bool SameCase(PerformanceResult other) =>
        Operation == other.Operation && Filename == other.Filename && Setting == other.Setting &&
        BitRate == other.BitRate && Width == other.Width && Height == other.Height &&
        InputSha256 == other.InputSha256 &&
        JsonSerializer.Serialize(Parameters) == JsonSerializer.Serialize(other.Parameters);

    internal static double Median(IEnumerable<double> values)
    {
        double[] ordered = values.OrderBy(value => value).ToArray();
        if (ordered.Length == 0)
        {
            throw new InvalidDataException("Cannot calculate a median without samples.");
        }
        int middle = ordered.Length / 2;
        return ordered.Length % 2 == 0 ? (ordered[middle - 1] + ordered[middle]) / 2 : ordered[middle];
    }
}

internal sealed record PerformanceEnvironment(string Machine, string Framework, string OS, string Architecture,
    string? Processor, int ProcessorCount, string Configuration, bool DebuggerAttached,
    bool ServerGC, string GCLatencyMode, string RuntimeSettings)
{
    internal bool IsComparable => Configuration == "Release" && !DebuggerAttached;
}

internal sealed record PerformanceRun(string Id, DateTimeOffset StartedUtc, string? SourceRevision,
    string? CodecVersion, string CodecAssemblySha256, PerformanceEnvironment Environment)
{
    public int MeasurementVersion { get; init; } = 1;
    public int WarmUpIterations { get; init; } = 1;
    public int MeasuredIterations { get; init; } = WsqPerformanceTestData.MeasuredIterations;
    public double MaximumMilliseconds { get; init; } = WsqPerformanceTestData.MaximumMilliseconds;
    public List<PerformanceResult> ResultsEncoding { get; init; } = new();
    public List<PerformanceResult> ResultsDecoding { get; init; } = new();

    internal IEnumerable<PerformanceResult> Results => ResultsEncoding.Concat(ResultsDecoding);

    internal bool CompatibleWith(PerformanceRun other) =>
        Environment.IsComparable && other.Environment.IsComparable && Environment == other.Environment &&
        MeasurementVersion == other.MeasurementVersion && WarmUpIterations == other.WarmUpIterations &&
        MeasuredIterations == other.MeasuredIterations && MaximumMilliseconds == other.MaximumMilliseconds;
}

internal sealed class PerformanceHistory
{
    public int SchemaVersion { get; init; } = 3;
    public List<PerformanceRun> Runs { get; init; } = new();
}

internal sealed record MetricChange(double Previous, double Current)
{
    public double Delta => Current - Previous;
    public double? PercentChange => Previous == 0 ? null : Delta / Previous * 100;
    public string Direction => Current < Previous ? "Lower" : Current > Previous ? "Higher" : "Unchanged";
}

internal sealed record PerformanceComparison(string Status, string? PreviousRunId = null,
    DateTimeOffset? PreviousRecordedUtc = null, MetricChange? MedianMilliseconds = null,
    MetricChange? MaximumMilliseconds = null, MetricChange? MedianAllocatedBytes = null,
    MetricChange? MaximumAllocatedBytes = null, MetricChange? MedianProcessCpuMilliseconds = null,
    MetricChange? MedianManagedHeapBytesAfter = null, MetricChange? MedianWorkingSetBytesAfter = null,
    MetricChange? MedianPrivateMemoryBytesAfter = null, MetricChange? MedianOutputBytes = null,
    MetricChange? Gen0Collections = null, MetricChange? Gen1Collections = null, MetricChange? Gen2Collections = null,
    bool? OutputBytesIdentical = null);
