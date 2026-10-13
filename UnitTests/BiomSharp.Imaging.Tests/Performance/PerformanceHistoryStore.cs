using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Text.Json;

namespace BiomSharp.Imaging.Tests.Performance;

internal static class PerformanceHistoryStore
{
    internal const string HistoryFileName = "history.json";
    internal const string ReportFileName = "comparison.md";
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    internal static PerformanceComparison Append(string directory, PerformanceRun run, PerformanceResult result,
        string? baselinePath = null)
    {
        Directory.CreateDirectory(directory);
        using FileStream fileLock = AcquireLock(Path.Combine(directory, "history.lock"));
        string path = Path.Combine(directory, HistoryFileName);
        PerformanceHistory history = File.Exists(path) || baselinePath == null ? Load(path) : LoadBaseline(baselinePath);
        PerformanceRun? storedRun = history.Runs.SingleOrDefault(item => item.Id == run.Id);
        if (storedRun != null && (storedRun.Environment != run.Environment ||
            storedRun.MeasurementVersion != run.MeasurementVersion || storedRun.CodecAssemblySha256 != run.CodecAssemblySha256))
        {
            throw new InvalidDataException($"Run metadata changed for {run.Id}.");
        }
        if (storedRun != null && storedRun.Results.Any(item => item.SameCase(result)))
        {
            throw new InvalidDataException($"Duplicate result in run {run.Id}: {result.Operation} {result.Filename} {result.Setting}.");
        }

        result.Comparison = Compare(history, run, result);
        if (storedRun == null)
        {
            storedRun = run with { ResultsEncoding = new(), ResultsDecoding = new() };
            history.Runs.Add(storedRun);
        }
        switch (result.Operation)
        {
            case "Encoding":
                storedRun.ResultsEncoding.Add(result);
                break;
            case "Decoding":
                storedRun.ResultsDecoding.Add(result);
                break;
            default:
                throw new InvalidDataException($"Unknown performance operation: {result.Operation}.");
        }
        Validate(history);
        WriteAtomically(path, JsonSerializer.Serialize(history, JsonOptions));
        WriteAtomically(Path.Combine(directory, ReportFileName), CreateReport(storedRun));
        return result.Comparison;
    }

    internal static PerformanceHistory Load(string path)
    {
        if (!File.Exists(path))
        {
            return new PerformanceHistory();
        }
        string json = File.ReadAllText(path);
        using JsonDocument document = JsonDocument.Parse(json);
        if (document.RootElement.GetProperty("SchemaVersion").GetInt32() != 3 ||
            document.RootElement.GetProperty("Runs").ValueKind != JsonValueKind.Array)
        {
            throw new InvalidDataException($"Unsupported performance history schema: {path}.");
        }
        PerformanceHistory history = JsonSerializer.Deserialize<PerformanceHistory>(json, JsonOptions)
            ?? throw new InvalidDataException($"Empty performance history: {path}.");
        Validate(history);
        return history;
    }

    private static PerformanceHistory LoadBaseline(string path)
    {
        if (!File.Exists(path))
        {
            throw new FileNotFoundException("The initial performance baseline is required to initialize missing history.", path);
        }
        PerformanceHistory baseline = Load(path);
        if (baseline.Runs.Count == 0 || !baseline.Runs.SelectMany(run => run.Results).Any() ||
            baseline.Runs.SelectMany(run => run.Results).Any(result => !result.ValidationPassed) ||
            baseline.Runs.SelectMany(run => run.ResultsEncoding).Any(result => result.EncodingMetrics == null))
        {
            throw new InvalidDataException($"The initial baseline must contain validated results and encoding memory metrics: {path}.");
        }
        return baseline;
    }

    private static void Validate(PerformanceHistory history)
    {
        if (history.Runs == null || history.Runs.Select(run => run.Id).Distinct().Count() != history.Runs.Count)
        {
            throw new InvalidDataException("Performance history has missing runs or duplicate run IDs.");
        }
        foreach (PerformanceRun run in history.Runs)
        {
            if (string.IsNullOrWhiteSpace(run.Id) || run.Environment == null ||
                run.ResultsEncoding == null || run.ResultsDecoding == null || run.MeasuredIterations <= 0)
            {
                throw new InvalidDataException("Performance history contains incomplete run metadata.");
            }
            foreach (PerformanceResult result in run.Results)
            {
                if (result.Parameters == null || result.SamplesMilliseconds == null ||
                    result.SamplesMilliseconds.Length != run.MeasuredIterations ||
                    result.SamplesMilliseconds.Any(value => !double.IsFinite(value) || value < 0) ||
                    (result.EncodingMetrics != null && result.EncodingMetrics.Samples.Length != run.MeasuredIterations))
                {
                    throw new InvalidDataException($"Invalid performance samples in run {run.Id}.");
                }
            }
            if (run.ResultsEncoding.Any(result => result.Operation != "Encoding") ||
                run.ResultsDecoding.Any(result => result.Operation != "Decoding"))
            {
                throw new InvalidDataException($"Incorrect operation group in run {run.Id}.");
            }
        }
    }

    private static PerformanceComparison Compare(PerformanceHistory history, PerformanceRun run, PerformanceResult result)
    {
        if (!result.ValidationPassed)
        {
            return new PerformanceComparison("FailedValidation");
        }
        if (!run.Environment.IsComparable)
        {
            return new PerformanceComparison("NonComparableConfiguration");
        }
        var previous = history.Runs.AsEnumerable().Reverse()
            .Where(candidate => candidate.Id != run.Id && candidate.CompatibleWith(run))
            .SelectMany(candidate => candidate.Results.Select(item => new { Run = candidate, Result = item }))
            .Where(candidate => candidate.Result.ValidationPassed && candidate.Result.SameCase(result) &&
                (candidate.Result.EncodingMetrics == null) == (result.EncodingMetrics == null))
            .OrderByDescending(candidate => candidate.Result.RecordedUtc)
            .FirstOrDefault();
        if (previous == null)
        {
            return new PerformanceComparison("NoComparablePreviousResult");
        }

        EncodingMetrics? oldMetrics = previous.Result.EncodingMetrics;
        EncodingMetrics? newMetrics = result.EncodingMetrics;
        return new PerformanceComparison("Compared", previous.Run.Id, previous.Result.RecordedUtc,
            new MetricChange(previous.Result.MedianMilliseconds, result.MedianMilliseconds),
            new MetricChange(previous.Result.MaximumMilliseconds, result.MaximumMilliseconds),
            Change(oldMetrics?.MedianAllocatedBytes, newMetrics?.MedianAllocatedBytes),
            Change(oldMetrics?.MaximumAllocatedBytes, newMetrics?.MaximumAllocatedBytes),
            Change(oldMetrics?.MedianProcessCpuMilliseconds, newMetrics?.MedianProcessCpuMilliseconds),
            Change(oldMetrics?.MedianManagedHeapBytesAfter, newMetrics?.MedianManagedHeapBytesAfter),
            Change(oldMetrics?.MedianWorkingSetBytesAfter, newMetrics?.MedianWorkingSetBytesAfter),
            Change(oldMetrics?.MedianPrivateMemoryBytesAfter, newMetrics?.MedianPrivateMemoryBytesAfter),
            Change(oldMetrics?.MedianOutputBytes, newMetrics?.MedianOutputBytes),
            Change(oldMetrics?.Gen0Collections, newMetrics?.Gen0Collections),
            Change(oldMetrics?.Gen1Collections, newMetrics?.Gen1Collections),
            Change(oldMetrics?.Gen2Collections, newMetrics?.Gen2Collections),
            oldMetrics == null || newMetrics == null ? null :
                oldMetrics.Samples.Select(sample => sample.OutputSha256)
                    .SequenceEqual(newMetrics.Samples.Select(sample => sample.OutputSha256)));
    }

    private static MetricChange? Change(double? previous, double? current) =>
        previous.HasValue && current.HasValue ? new MetricChange(previous.Value, current.Value) : null;

    private static FileStream AcquireLock(string path)
    {
        var timer = Stopwatch.StartNew();
        while (true)
        {
            try
            {
                return new FileStream(path, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
            }
            catch (IOException error) when ((error.HResult & 0xffff) is 32 or 33)
            {
                if (timer.Elapsed >= TimeSpan.FromSeconds(10))
                {
                    throw new IOException($"Timed out waiting for the performance history lock: {path}.", error);
                }
                Thread.Sleep(50);
            }
        }
    }

    private static void WriteAtomically(string path, string contents)
    {
        string temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            File.WriteAllText(temporary, contents);
            if (File.Exists(path))
            {
                File.Replace(temporary, path, destinationBackupFileName: null);
            }
            else
            {
                File.Move(temporary, path);
            }
        }
        finally
        {
            if (File.Exists(temporary))
            {
                File.Delete(temporary);
            }
        }
    }

    private static string CreateReport(PerformanceRun run)
    {
        var text = new StringBuilder();
        text.AppendLine("# WSQ performance comparison");
        text.AppendLine();
        text.AppendLine($"Run: `{run.Id}`; started: {run.StartedUtc:O}; configuration: {run.Environment.Configuration}; debugger: {run.Environment.DebuggerAttached}.");
        text.AppendLine($"Source revision: `{run.SourceRevision ?? "not supplied"}`; codec SHA-256: `{run.CodecAssemblySha256}`.");
        text.AppendLine($"Recorded cases in this run: {run.ResultsEncoding.Count} encoding, {run.ResultsDecoding.Count} decoding. Unexecuted cases remain in history.json.");
        text.AppendLine();
        text.AppendLine("Negative changes mean lower measurements, not proof of a statistically significant improvement. Comparisons use the latest compatible, validated result for each case.");
        text.AppendLine("Allocated bytes cover the synchronous encoding thread. CPU time and GC counts are process-wide. Memory snapshots are process-wide context, not per-operation usage or peak memory; GC and retained buffers affect them.");
        foreach (PerformanceResult result in run.Results.OrderBy(item => item.Operation).ThenBy(item => item.Filename).ThenBy(item => item.BitRate))
        {
            PerformanceComparison comparison = result.Comparison
                ?? throw new InvalidDataException("A recorded result is missing its comparison.");
            text.AppendLine();
            text.AppendLine($"## {result.Operation}: {result.Filename} / {result.Setting} ({result.BitRate.ToString(CultureInfo.InvariantCulture)} bpp)");
            text.AppendLine();
            text.AppendLine($"Status: **{comparison.Status}**. Output/timing validation: {(result.ValidationPassed ? "passed" : "failed")}.");
            if (result.ValidationFailure != null)
            {
                text.AppendLine("```text");
                text.AppendLine(result.ValidationFailure);
                text.AppendLine("```");
            }
            if (comparison.PreviousRunId != null)
            {
                text.AppendLine($"Compared with run `{comparison.PreviousRunId}`, case recorded at {comparison.PreviousRecordedUtc:O}.");
            }
            text.AppendLine();
            text.AppendLine("| Metric | Previous | Current | Change | Change % |");
            text.AppendLine("|---|---:|---:|---:|---:|");
            AddMetric(text, "Median elapsed (ms)", result.MedianMilliseconds, comparison.MedianMilliseconds);
            AddMetric(text, "Maximum elapsed (ms)", result.MaximumMilliseconds, comparison.MaximumMilliseconds);
            if (result.EncodingMetrics is EncodingMetrics metrics)
            {
                AddMetric(text, "Median allocated (bytes)", metrics.MedianAllocatedBytes, comparison.MedianAllocatedBytes);
                AddMetric(text, "Maximum allocated (bytes)", metrics.MaximumAllocatedBytes, comparison.MaximumAllocatedBytes);
                AddMetric(text, "Median process CPU (ms)", metrics.MedianProcessCpuMilliseconds, comparison.MedianProcessCpuMilliseconds);
                AddMetric(text, "Median managed heap after (bytes; context)", metrics.MedianManagedHeapBytesAfter, comparison.MedianManagedHeapBytesAfter);
                AddMetric(text, "Median working set after (bytes; context)", metrics.MedianWorkingSetBytesAfter, comparison.MedianWorkingSetBytesAfter);
                AddMetric(text, "Median private memory after (bytes; context)", metrics.MedianPrivateMemoryBytesAfter, comparison.MedianPrivateMemoryBytesAfter);
                AddMetric(text, "Median output size (bytes)", metrics.MedianOutputBytes, comparison.MedianOutputBytes);
                AddMetric(text, "Gen 0 collections (all samples)", metrics.Gen0Collections, comparison.Gen0Collections);
                AddMetric(text, "Gen 1 collections (all samples)", metrics.Gen1Collections, comparison.Gen1Collections);
                AddMetric(text, "Gen 2 collections (all samples)", metrics.Gen2Collections, comparison.Gen2Collections);
                text.AppendLine();
                text.AppendLine($"Encoded output hashes identical to previous: {comparison.OutputBytesIdentical?.ToString() ?? "not compared"}.");
            }
        }
        return text.ToString();
    }

    private static void AddMetric(StringBuilder text, string name, double current, MetricChange? change)
    {
        static string Format(double? value) => value?.ToString("0.###", CultureInfo.InvariantCulture) ?? "N/A";
        text.AppendLine($"| {name} | {Format(change?.Previous)} | {Format(current)} | {Format(change?.Delta)} | {Format(change?.PercentChange)} |");
    }
}
