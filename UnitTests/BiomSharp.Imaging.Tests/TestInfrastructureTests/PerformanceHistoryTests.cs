using BiomSharp.Imaging.Tests.Performance;
using BiomSharp.Imaging.Wsq;
using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.Json;

namespace BiomSharp.Imaging.Tests.TestInfrastructureTests;

[Trait("Category", "PerformanceInfrastructure")]
public sealed class PerformanceHistoryTests : IDisposable
{
    private readonly string _directory = Path.Combine(TestResources.UnitTestsDirectory,
        "TestResults", $"performance-history-{Guid.NewGuid():N}");

    [Fact]
    public void Single_case_run_compares_with_its_latest_result_and_preserves_other_cases()
    {
        // Arrange
        PerformanceRun first = Run("first");
        PerformanceRun unrelated = Run("unrelated");
        PerformanceRun latest = Run("latest");
        PerformanceHistoryStore.Append(_directory, first, Result("a.pgm", 100, 1000));
        PerformanceHistoryStore.Append(_directory, first, Result("b.pgm", 200, 2000));
        PerformanceHistoryStore.Append(_directory, unrelated, Result("b.pgm", 180, 1800));
        PerformanceHistoryStore.Append(_directory, latest, Result("a.pgm", 90, 900));
        var current = Result("a.pgm", 72, 720);

        // Act
        PerformanceComparison comparison = PerformanceHistoryStore.Append(_directory, Run("single"), current);
        PerformanceHistory history = PerformanceHistoryStore.Load(Path.Combine(_directory, "history.json"));

        // Assert
        Assert.Equal("Compared", comparison.Status);
        Assert.Equal("latest", comparison.PreviousRunId);
        Assert.Equal(-18d, comparison.MedianMilliseconds!.Delta);
        Assert.Equal(-20d, comparison.MedianMilliseconds.PercentChange);
        Assert.Equal(-180d, comparison.MedianAllocatedBytes!.Delta);
        Assert.Equal(-20d, comparison.MedianAllocatedBytes.PercentChange);
        Assert.True(comparison.OutputBytesIdentical);
        Assert.Equal(4, history.Runs.Count);
        Assert.Equal(5, history.Runs.Sum(run => run.ResultsEncoding.Count));
        Assert.Single(history.Runs.Single(run => run.Id == "single").ResultsEncoding);
        Assert.Equal(2, history.Runs.Single(run => run.Id == "first").ResultsEncoding.Count);
        string report = File.ReadAllText(Path.Combine(_directory, "comparison.md"));
        Assert.Contains("Compared with run `latest`", report);
        Assert.Contains("| Median elapsed (ms) | 90 | 72 | -18 | -20 |", report);
        Assert.Contains("| Median allocated (bytes) | 900 | 720 | -180 | -20 |", report);
        Assert.DoesNotContain("## Encoding: b.pgm", report);
    }

    [Theory]
    [InlineData("Machine")]
    [InlineData("Framework")]
    [InlineData("Debug")]
    [InlineData("Debugger")]
    [InlineData("Input")]
    [InlineData("Parameters")]
    [InlineData("MeasurementVersion")]
    [InlineData("FailedValidation")]
    public void Comparison_skips_newer_incompatible_or_failed_results(string mismatch)
    {
        // Arrange
        PerformanceRun compatible = Run("compatible");
        PerformanceHistoryStore.Append(_directory, compatible, Result("a.pgm", 100, 1000));
        PerformanceRun incompatible = Run("incompatible");
        PerformanceResult prior = Result("a.pgm", 50, 500);
        switch (mismatch)
        {
            case "Machine":
                incompatible = incompatible with { Environment = incompatible.Environment with { Machine = "different" } };
                break;
            case "Framework":
                incompatible = incompatible with { Environment = incompatible.Environment with { Framework = "different" } };
                break;
            case "Debug":
                incompatible = incompatible with { Environment = incompatible.Environment with { Configuration = "Debug" } };
                break;
            case "Debugger":
                incompatible = incompatible with { Environment = incompatible.Environment with { DebuggerAttached = true } };
                break;
            case "Input":
                prior = prior with { InputSha256 = "different" };
                break;
            case "Parameters":
                prior = prior with { Parameters = new WsqParameters { PackedDHT = true } };
                break;
            case "MeasurementVersion":
                incompatible = incompatible with { MeasurementVersion = 2 };
                break;
            case "FailedValidation":
                prior = prior with { ValidationPassed = false, ValidationFailure = "Output validation failed." };
                break;
        }
        PerformanceHistoryStore.Append(_directory, incompatible, prior);

        // Act
        PerformanceComparison comparison = PerformanceHistoryStore.Append(_directory, Run("current"),
            Result("a.pgm", 80, 800));

        // Assert
        Assert.Equal("Compared", comparison.Status);
        Assert.Equal("compatible", comparison.PreviousRunId);
        Assert.Equal(100d, comparison.MedianMilliseconds!.Previous);
        Assert.Equal(3, PerformanceHistoryStore.Load(Path.Combine(_directory, "history.json")).Runs.Count);
    }

    [Fact]
    public void Equal_timestamps_use_the_latest_persisted_case_and_allow_codec_changes()
    {
        // Arrange
        PerformanceResult first = Result("a.pgm", 100, 1000);
        PerformanceHistoryStore.Append(_directory, Run("first"), first);
        PerformanceHistoryStore.Append(_directory, Run("latest"), first with
        {
            SamplesMilliseconds = Enumerable.Repeat(90d, 5).ToArray()
        });
        PerformanceRun changed = Run("changed") with { SourceRevision = "new-revision", CodecAssemblySha256 = "new-codec" };

        // Act
        PerformanceComparison comparison = PerformanceHistoryStore.Append(_directory, changed, Result("a.pgm", 80, 800));

        // Assert
        Assert.Equal("Compared", comparison.Status);
        Assert.Equal("latest", comparison.PreviousRunId);
        Assert.Equal(90d, comparison.MedianMilliseconds!.Previous);
    }

    [Theory]
    [InlineData("Debug", false)]
    [InlineData("Release", true)]
    public void Debug_run_is_saved_without_claiming_comparability(string configuration, bool debugger)
    {
        // Arrange
        PerformanceRun oldRun = Run("old");
        PerformanceHistoryStore.Append(_directory, oldRun, Result("a.pgm", 100, 1000));
        PerformanceRun current = Run("current");
        current = current with { Environment = current.Environment with { Configuration = configuration, DebuggerAttached = debugger } };

        // Act
        PerformanceComparison comparison = PerformanceHistoryStore.Append(_directory, current, Result("a.pgm", 50, 500));

        // Assert
        Assert.Equal("NonComparableConfiguration", comparison.Status);
        Assert.Null(comparison.PreviousRunId);
        Assert.Null(comparison.MedianMilliseconds);
        Assert.Equal(2, PerformanceHistoryStore.Load(Path.Combine(_directory, "history.json")).Runs.Count);
    }

    [Fact]
    public void First_result_has_no_fabricated_comparison_and_preserves_both_operation_groups()
    {
        // Arrange
        PerformanceRun run = Run("first");
        PerformanceResult encoding = Result("a.pgm", 100, 1000);
        PerformanceResult decoding = Result("a.pgm", 80, 800) with { Operation = "Decoding", EncodingMetrics = null };

        // Act
        PerformanceComparison comparison = PerformanceHistoryStore.Append(_directory, run, encoding);
        PerformanceHistoryStore.Append(_directory, run, decoding);

        // Assert
        Assert.Equal("NoComparablePreviousResult", comparison.Status);
        Assert.Null(comparison.MedianAllocatedBytes);
        using JsonDocument json = JsonDocument.Parse(File.ReadAllText(Path.Combine(_directory, "history.json")));
        Assert.Equal(3, json.RootElement.GetProperty("SchemaVersion").GetInt32());
        JsonElement storedRun = json.RootElement.GetProperty("Runs")[0];
        Assert.Equal(1, storedRun.GetProperty("ResultsEncoding").GetArrayLength());
        Assert.Equal(1, storedRun.GetProperty("ResultsDecoding").GetArrayLength());
        Assert.False(storedRun.TryGetProperty("Results", out _));
        JsonElement result = storedRun.GetProperty("ResultsEncoding")[0];
        Assert.Equal(5, result.GetProperty("SamplesMilliseconds").GetArrayLength());
        Assert.Equal(5, result.GetProperty("EncodingMetrics").GetProperty("Samples").GetArrayLength());
    }

    [Fact]
    public void Failed_timing_result_is_retained_and_not_used_as_a_reference()
    {
        // Arrange
        PerformanceHistoryStore.Append(_directory, Run("valid"), Result("a.pgm", 100, 1000));
        PerformanceResult failure = Result("a.pgm", 1500, 2000) with
        {
            ValidationPassed = false,
            ValidationFailure = "Encoding took 1500 ms; limit: 1000 ms."
        };

        // Act
        PerformanceComparison failed = PerformanceHistoryStore.Append(_directory, Run("failed"), failure);
        string failedReport = File.ReadAllText(Path.Combine(_directory, "comparison.md"));
        PerformanceComparison next = PerformanceHistoryStore.Append(_directory, Run("next"), Result("a.pgm", 90, 900));

        // Assert
        Assert.Equal("FailedValidation", failed.Status);
        Assert.Contains(failure.ValidationFailure, failedReport);
        Assert.Equal("valid", next.PreviousRunId);
        PerformanceRun saved = PerformanceHistoryStore.Load(Path.Combine(_directory, "history.json"))
            .Runs.Single(run => run.Id == "failed");
        Assert.False(saved.ResultsEncoding[0].ValidationPassed);
        Assert.Equal(1500d, saved.ResultsEncoding[0].MaximumMilliseconds);
    }

    [Fact]
    public void Zero_previous_measurement_has_no_percentage_and_output_changes_are_visible()
    {
        // Arrange
        PerformanceHistoryStore.Append(_directory, Run("old"), Result("a.pgm", 0, 0));
        PerformanceResult current = Result("a.pgm", 10, 100);
        current = current with
        {
            EncodingMetrics = new EncodingMetrics(current.EncodingMetrics!.Samples
                .Select(sample => sample with { OutputSha256 = "changed" }).ToArray())
        };

        // Act
        PerformanceComparison comparison = PerformanceHistoryStore.Append(_directory, Run("new"), current);

        // Assert
        Assert.Null(comparison.MedianMilliseconds!.PercentChange);
        Assert.Null(comparison.MedianAllocatedBytes!.PercentChange);
        Assert.Equal(10d, comparison.MedianMilliseconds.Delta);
        Assert.False(comparison.OutputBytesIdentical);
        Assert.Contains("| Median elapsed (ms) | 0 | 10 | 10 | N/A |",
            File.ReadAllText(Path.Combine(_directory, "comparison.md")));
    }

    [Theory]
    [InlineData("{broken")]
    [InlineData("{\"SchemaVersion\":2,\"Runs\":[]}")]
    public void Invalid_history_is_reported_without_replacing_the_file(string contents)
    {
        // Arrange
        Directory.CreateDirectory(_directory);
        string path = Path.Combine(_directory, "history.json");
        File.WriteAllText(path, contents);

        // Act
        Exception? error = Record.Exception(() =>
            PerformanceHistoryStore.Append(_directory, Run("current"), Result("a.pgm", 100, 1000)));

        // Assert
        Assert.NotNull(error);
        Assert.Equal(contents, File.ReadAllText(path));
        Assert.False(File.Exists(Path.Combine(_directory, "comparison.md")));
    }

    [Fact]
    public async Task Concurrent_writers_preserve_all_results()
    {
        // Arrange
        var runs = Enumerable.Range(0, 4).Select(index => Run($"run-{index}")).ToArray();

        // Act
        await Task.WhenAll(runs.Select(run => Task.Run(() =>
            PerformanceHistoryStore.Append(_directory, run, Result($"{run.Id}.pgm", 100, 1000)))));

        // Assert
        PerformanceHistory history = PerformanceHistoryStore.Load(Path.Combine(_directory, "history.json"));
        Assert.Equal(4, history.Runs.Count);
        Assert.All(history.Runs, run => Assert.Single(run.ResultsEncoding));
        Assert.Empty(Directory.GetFiles(_directory, "*.tmp"));
    }

    [Fact]
    public void Encoding_measurement_records_allocations_memory_and_output_outside_the_result_allocation()
    {
        // Arrange
        var bitmap = SimpleBitmap.Gray8(Enumerable.Range(0, 4096)
            .Select(index => (byte)((index * 13 + index / 64 * 7) % 256)).ToArray(), 64, 64);
        var codec = new WsqCodec();
        codec.FromRaw(bitmap);
        var parameters = new WsqParameters();
        using var process = Process.GetCurrentProcess();

        // Act
        EncodingPerformanceSample sample = EncodingPerformanceSample.Measure(codec, parameters, process);

        // Assert
        Assert.NotNull(sample.Bytes);
        Assert.NotEmpty(sample.Bytes!);
        Assert.True(sample.Milliseconds > 0);
        Assert.True(sample.Metrics.AllocatedBytes >= sample.Bytes!.Length);
        Assert.Equal(sample.Bytes.Length, sample.Metrics.OutputBytes);
        Assert.Equal(Convert.ToHexString(SHA256.HashData(sample.Bytes)), sample.Metrics.OutputSha256);
        Assert.True(sample.Metrics.Before.ManagedHeapBytes > 0);
        Assert.True(sample.Metrics.After.WorkingSetBytes > 0);
        Assert.True(sample.Metrics.After.PrivateMemoryBytes > 0);
        Assert.True(sample.Metrics.ProcessCpuMilliseconds >= 0);
        Assert.True(sample.Metrics.Gen0Collections >= 0);
        Assert.True(sample.Metrics.Gen1Collections >= 0);
        Assert.True(sample.Metrics.Gen2Collections >= 0);
    }

    [Theory]
    [InlineData("directory")]
    [InlineData("history.json")]
    [InlineData("comparison.md")]
    [InlineData("history.lock")]
    public void Missing_artifacts_are_recreated_and_only_missing_history_is_seeded(string missing)
    {
        // Arrange
        string baseline = SaveBaseline();
        byte[] originalBaseline = File.ReadAllBytes(baseline);
        string resultsDirectory = Path.Combine(_directory, "results");
        PerformanceHistoryStore.Append(resultsDirectory, Run("previous"), Result("a.pgm", 90, 900), baseline);
        if (missing == "directory")
        {
            Directory.Delete(resultsDirectory, recursive: true);
        }
        else
        {
            File.Delete(Path.Combine(resultsDirectory, missing));
        }
        bool historyWasDeleted = missing is "directory" or "history.json";

        // Act
        PerformanceComparison comparison = PerformanceHistoryStore.Append(resultsDirectory, Run("current"),
            Result("a.pgm", 80, 800), baseline);

        // Assert
        Assert.Equal("Compared", comparison.Status);
        Assert.Equal(historyWasDeleted ? "baseline" : "previous", comparison.PreviousRunId);
        Assert.Equal(historyWasDeleted ? 100d : 90d, comparison.MedianMilliseconds!.Previous);
        Assert.Equal(historyWasDeleted ? 1000d : 900d, comparison.MedianAllocatedBytes!.Previous);
        PerformanceHistory history = PerformanceHistoryStore.Load(Path.Combine(resultsDirectory, "history.json"));
        Assert.Equal(historyWasDeleted ? 2 : 3, history.Runs.Count);
        Assert.Single(history.Runs.Where(run => run.Id == "baseline"));
        Assert.Equal(originalBaseline, File.ReadAllBytes(baseline));
        Assert.True(File.Exists(Path.Combine(resultsDirectory, "history.lock")));
        Assert.Contains($"Compared with run `{comparison.PreviousRunId}`",
            File.ReadAllText(Path.Combine(resultsDirectory, "comparison.md")));
    }

    [Fact]
    public void Existing_history_does_not_read_or_reinsert_the_baseline()
    {
        // Arrange
        string baseline = SaveBaseline();
        string resultsDirectory = Path.Combine(_directory, "results");
        PerformanceHistoryStore.Append(resultsDirectory, Run("previous"), Result("a.pgm", 90, 900), baseline);
        File.WriteAllText(baseline, "{invalid baseline that must not be read");

        // Act
        PerformanceComparison comparison = PerformanceHistoryStore.Append(resultsDirectory, Run("current"),
            Result("a.pgm", 80, 800), baseline);

        // Assert
        Assert.Equal("previous", comparison.PreviousRunId);
        Assert.Equal(3, PerformanceHistoryStore.Load(Path.Combine(resultsDirectory, "history.json")).Runs.Count);
        Assert.Equal("{invalid baseline that must not be read", File.ReadAllText(baseline));
    }

    [Theory]
    [InlineData("missing")]
    [InlineData("malformed")]
    [InlineData("empty")]
    [InlineData("missing-metrics")]
    public void Invalid_initial_baseline_fails_without_creating_empty_history(string condition)
    {
        // Arrange
        string baseline = SaveBaseline();
        if (condition == "missing")
        {
            File.Delete(baseline);
        }
        else if (condition == "malformed")
        {
            File.WriteAllText(baseline, "{broken");
        }
        else if (condition == "empty")
        {
            File.WriteAllText(baseline, "{\"SchemaVersion\":3,\"Runs\":[]}");
        }
        else
        {
            var seed = new PerformanceHistory();
            seed.Runs.Add(Run("baseline") with
            {
                ResultsEncoding = new() { Result("a.pgm", 100, 1000) with { EncodingMetrics = null } }
            });
            File.WriteAllText(baseline, JsonSerializer.Serialize(seed));
        }
        string resultsDirectory = Path.Combine(_directory, "results");

        // Act
        Exception? error = Record.Exception(() => PerformanceHistoryStore.Append(resultsDirectory, Run("current"),
            Result("a.pgm", 80, 800), baseline));

        // Assert
        Assert.NotNull(error);
        Assert.False(File.Exists(Path.Combine(resultsDirectory, "history.json")));
        Assert.False(File.Exists(Path.Combine(resultsDirectory, "comparison.md")));
    }

    [Fact]
    public void Corrupt_existing_history_is_not_replaced_with_baseline_data()
    {
        // Arrange
        string baseline = SaveBaseline();
        string resultsDirectory = Path.Combine(_directory, "results");
        Directory.CreateDirectory(resultsDirectory);
        string path = Path.Combine(resultsDirectory, "history.json");
        File.WriteAllText(path, "{broken existing history");

        // Act
        Exception? error = Record.Exception(() => PerformanceHistoryStore.Append(resultsDirectory, Run("current"),
            Result("a.pgm", 80, 800), baseline));

        // Assert
        Assert.NotNull(error);
        Assert.Equal("{broken existing history", File.ReadAllText(path));
    }

    [Fact]
    public async Task Concurrent_first_runs_seed_the_baseline_only_once()
    {
        // Arrange
        string baseline = SaveBaseline();
        byte[] originalBaseline = File.ReadAllBytes(baseline);
        string resultsDirectory = Path.Combine(_directory, "results");
        PerformanceRun[] runs = Enumerable.Range(0, 4).Select(index => Run($"run-{index}")).ToArray();

        // Act
        await Task.WhenAll(runs.Select(run => Task.Run(() => PerformanceHistoryStore.Append(resultsDirectory, run,
            Result("a.pgm", 80, 800), baseline))));

        // Assert
        PerformanceHistory history = PerformanceHistoryStore.Load(Path.Combine(resultsDirectory, "history.json"));
        Assert.Equal(5, history.Runs.Count);
        Assert.Single(history.Runs.Where(run => run.Id == "baseline"));
        Assert.Equal(originalBaseline, File.ReadAllBytes(baseline));
    }

    [Fact]
    public void Seed_from_another_machine_is_preserved_but_not_compared()
    {
        // Arrange
        string baseline = SaveBaseline();
        PerformanceRun current = Run("current");
        current = current with { Environment = current.Environment with { Machine = "other-machine" } };

        // Act
        PerformanceComparison comparison = PerformanceHistoryStore.Append(Path.Combine(_directory, "results"),
            current, Result("a.pgm", 80, 800), baseline);

        // Assert
        Assert.Equal("NoComparablePreviousResult", comparison.Status);
        Assert.Null(comparison.MedianAllocatedBytes);
        Assert.Equal(2, PerformanceHistoryStore.Load(Path.Combine(_directory, "results", "history.json")).Runs.Count);
    }

    [Fact]
    public void Bundled_baseline_seeds_a_single_case_with_all_original_metrics()
    {
        // Arrange
        string baseline = Path.Combine(TestResources.UnitTestsDirectory, "BiomSharp.Imaging.Tests",
            "Performance", "Baselines", "initial.json");
        byte[] originalBaseline = File.ReadAllBytes(baseline);
        PerformanceRun seed = Assert.Single(PerformanceHistoryStore.Load(baseline).Runs);
        Assert.Equal(30, seed.ResultsEncoding.Count);
        Assert.Equal(30, seed.ResultsDecoding.Count);
        PerformanceResult example = seed.ResultsEncoding[0];
        var current = seed with { Id = Guid.NewGuid().ToString("N"), StartedUtc = DateTimeOffset.UtcNow };

        // Act
        PerformanceComparison comparison = PerformanceHistoryStore.Append(_directory, current,
            example with { RecordedUtc = DateTimeOffset.UtcNow }, baseline);

        // Assert
        Assert.Equal("Compared", comparison.Status);
        Assert.Equal(seed.Id, comparison.PreviousRunId);
        Assert.Equal(example.EncodingMetrics!.MedianAllocatedBytes, comparison.MedianAllocatedBytes!.Previous);
        Assert.Equal(example.EncodingMetrics.MedianManagedHeapBytesAfter, comparison.MedianManagedHeapBytesAfter!.Previous);
        Assert.Equal(example.EncodingMetrics.MedianWorkingSetBytesAfter, comparison.MedianWorkingSetBytesAfter!.Previous);
        Assert.Equal(0d, comparison.MedianAllocatedBytes.Delta);
        Assert.True(comparison.OutputBytesIdentical);
        PerformanceRun persisted = PerformanceHistoryStore.Load(Path.Combine(_directory, "history.json")).Runs[0];
        Assert.Equal(JsonSerializer.Serialize(seed), JsonSerializer.Serialize(persisted));
        Assert.Equal(originalBaseline, File.ReadAllBytes(baseline));
    }

    private string SaveBaseline()
    {
        Directory.CreateDirectory(_directory);
        string path = Path.Combine(_directory, "initial.json");
        var baseline = new PerformanceHistory();
        baseline.Runs.Add(Run("baseline") with
        {
            ResultsEncoding = new() { Result("a.pgm", 100, 1000) with { Comparison = new PerformanceComparison("Baseline") } }
        });
        File.WriteAllText(path, JsonSerializer.Serialize(baseline));
        return path;
    }

    private static PerformanceRun Run(string id) => new(id, DateTimeOffset.UtcNow, "revision", "codec-version",
        "codec-hash", new PerformanceEnvironment("machine", ".NET 6", "Windows", "X64", "processor", 8,
            "Release", false, false, "Interactive", "{}"));

    private static PerformanceResult Result(string filename, double milliseconds, long allocated)
    {
        var snapshot = new MemorySnapshot(10000, 20000, 30000);
        var sample = new EncodingSampleMetrics(allocated, 10, 0, 0, 0, snapshot, snapshot, 100, "output-hash");
        return new PerformanceResult("Encoding", filename, "Default", 0.75f, 64, 64, "input-hash",
            Enumerable.Repeat(milliseconds, 5).ToArray(), new WsqParameters(), DateTimeOffset.UtcNow, true, null,
            new EncodingMetrics(Enumerable.Repeat(sample, 5).ToArray()));
    }

    public void Dispose()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }
}
