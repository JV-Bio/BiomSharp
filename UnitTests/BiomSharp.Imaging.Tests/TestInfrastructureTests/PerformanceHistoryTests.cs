using BiomSharp.Imaging.Tests.Performance;
using BiomSharp.Imaging.Wsq;
using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;

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
        Assert.Equal(new FastestElapsedComparison(100, 80), comparison.OverallFastestElapsed);
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
        Assert.Null(comparison.OverallFastestElapsed);
        Assert.Contains("| Overall Fastest elapsed (ms) | N/A | N/A | N/A | N/A |",
            File.ReadAllText(Path.Combine(_directory, "comparison.md")));
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
        Assert.Equal(new FastestElapsedComparison(null, 100), comparison.OverallFastestElapsed);
        string report = File.ReadAllText(Path.Combine(_directory, "comparison.md"));
        Assert.Contains("| Overall Fastest elapsed (ms) | N/A | 100 | N/A | N/A |", report);
        Assert.Contains("| Overall Fastest elapsed (ms) | N/A | 80 | N/A | N/A |", report);
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
        Assert.Null(failed.OverallFastestElapsed);
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
            EncodingMetrics = new PerformanceMetrics(current.EncodingMetrics!.Samples
                .Select(sample => sample with { OutputSha256 = "changed" }).ToArray())
        };

        // Act
        PerformanceComparison comparison = PerformanceHistoryStore.Append(_directory, Run("new"), current);

        // Assert
        Assert.Null(comparison.MedianMilliseconds!.PercentChange);
        Assert.Null(comparison.MedianAllocatedBytes!.PercentChange);
        Assert.Equal(10d, comparison.MedianMilliseconds.Delta);
        Assert.False(comparison.OutputBytesIdentical);
        Assert.Equal(new FastestElapsedComparison(0, 10), comparison.OverallFastestElapsed);
        Assert.Equal(0d, comparison.OverallFastestElapsed!.BestMilliseconds);
        Assert.Contains("| Overall Fastest elapsed (ms) | 0 | 10 | 10 | N/A |",
            File.ReadAllText(Path.Combine(_directory, "comparison.md")));
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
        Assert.Equal(new FastestElapsedComparison(example.SamplesMilliseconds.Min(), example.SamplesMilliseconds.Min()),
            comparison.OverallFastestElapsed);
        PerformanceRun persisted = PerformanceHistoryStore.Load(Path.Combine(_directory, "history.json")).Runs[0];
        Assert.Equal(JsonSerializer.Serialize(seed), JsonSerializer.Serialize(persisted));
        Assert.Equal(originalBaseline, File.ReadAllBytes(baseline));
    }

    [Theory]
    [InlineData("Encoding", 70d, 70d)]
    [InlineData("Encoding", 90d, 80d)]
    [InlineData("Encoding", 80d, 80d)]
    [InlineData("Decoding", 70d, 70d)]
    [InlineData("Decoding", 90d, 80d)]
    [InlineData("Decoding", 80d, 80d)]
    public void Overall_fastest_uses_all_historical_samples_not_the_latest_median(
        string operation, double currentMinimum, double expectedRecord)
    {
        // Arrange
        PerformanceResult oldest = Result("a.pgm", 100, 1000) with
        {
            Operation = operation,
            EncodingMetrics = operation == "Encoding" ? Result("a.pgm", 100, 1000).EncodingMetrics : null,
            SamplesMilliseconds = new[] { 100d, 110d, 80d, 120d, 115d }
        };
        PerformanceHistoryStore.Append(_directory, Run("oldest"), oldest);
        PerformanceHistoryStore.Append(_directory, Run("latest"), oldest with
        {
            SamplesMilliseconds = Enumerable.Repeat(90d, 5).ToArray(),
            RecordedUtc = DateTimeOffset.UtcNow
        });
        PerformanceResult current = oldest with
        {
            SamplesMilliseconds = new[] { currentMinimum, 120d, 130d, 140d, 150d },
            RecordedUtc = DateTimeOffset.UtcNow
        };

        // Act
        PerformanceComparison comparison = PerformanceHistoryStore.Append(_directory, Run("current"), current);
        PerformanceHistory saved = PerformanceHistoryStore.Load(Path.Combine(_directory, "history.json"));

        // Assert
        Assert.Equal("latest", comparison.PreviousRunId);
        Assert.Equal(90d, comparison.MedianMilliseconds!.Previous);
        Assert.Equal(130d, comparison.MedianMilliseconds.Current);
        Assert.Equal(new FastestElapsedComparison(80, currentMinimum), comparison.OverallFastestElapsed);
        Assert.Equal(expectedRecord, comparison.OverallFastestElapsed!.BestMilliseconds);
        Assert.Equal(comparison.OverallFastestElapsed,
            Assert.Single(saved.Runs.Single(run => run.Id == "current").Results).Comparison!.OverallFastestElapsed);
        string report = File.ReadAllText(Path.Combine(_directory, "comparison.md"));
        string expectedRow = currentMinimum == 70
            ? "| Overall Fastest elapsed (ms) | 80 | 70 | -10 | -12.5 |"
            : currentMinimum == 90
                ? "| Overall Fastest elapsed (ms) | 80 | 90 | 10 | 12.5 |"
                : "| Overall Fastest elapsed (ms) | 80 | 80 | 0 | 0 |";
        Assert.Contains(expectedRow, report);
        Assert.Contains("| Median elapsed (ms) | 90 | 130 | 40 | 44.444 |", report);
        Assert.Equal(1, report.Split("| Overall Fastest elapsed (ms) |").Length - 1);
    }

    [Fact]
    public void Overall_fastest_is_separate_for_each_file_bitrate_and_operation()
    {
        // Arrange
        PerformanceRun prior = Run("prior");
        PerformanceHistoryStore.Append(_directory, prior, Result("a.pgm", 100, 1000));
        PerformanceHistoryStore.Append(_directory, prior, Result("b.pgm", 1, 1000));
        PerformanceHistoryStore.Append(_directory, prior, Result("a.pgm", 2, 1000) with
        {
            Setting = "Minimum", BitRate = WsqParameters.BitRateMinValue,
            Parameters = new WsqParameters { BitRate = WsqParameters.BitRateMinValue }
        });
        PerformanceHistoryStore.Append(_directory, prior, Result("a.pgm", 3, 1000) with
        {
            Operation = "Decoding", EncodingMetrics = null
        });

        // Act
        PerformanceComparison comparison = PerformanceHistoryStore.Append(_directory, Run("current"),
            Result("a.pgm", 80, 1000));

        // Assert
        Assert.Equal(new FastestElapsedComparison(100, 80), comparison.OverallFastestElapsed);
        string report = File.ReadAllText(Path.Combine(_directory, "comparison.md"));
        Assert.Contains("| Overall Fastest elapsed (ms) | 100 | 80 | -20 | -20 |", report);
        Assert.DoesNotContain("## Encoding: b.pgm", report);
    }

    [Theory]
    [InlineData("Encoding", false, false)]
    [InlineData("Decoding", false, false)]
    [InlineData("Encoding", true, false)]
    [InlineData("Decoding", true, false)]
    [InlineData("Encoding", false, true)]
    [InlineData("Decoding", false, true)]
    public void Regenerating_legacy_report_recovers_records_without_rewriting_history_or_using_future_results(
        string operation, bool equalTimestamps, bool hasOldRecordComparison)
    {
        // Arrange
        DateTimeOffset timestamp = DateTimeOffset.UtcNow;
        PerformanceResult old = Result("a.pgm", 70, 1000) with
        {
            Operation = operation,
            EncodingMetrics = operation == "Encoding" ? Result("a.pgm", 70, 1000).EncodingMetrics : null,
            RecordedUtc = timestamp,
            Comparison = new PerformanceComparison("Baseline")
        };
        PerformanceResult current = old with
        {
            SamplesMilliseconds = Enumerable.Repeat(80d, 5).ToArray(),
            RecordedUtc = equalTimestamps ? timestamp : timestamp.AddMinutes(1),
            Comparison = new PerformanceComparison("Compared", "prior",
                MedianMilliseconds: new MetricChange(70, 80),
                OverallFastestElapsed: hasOldRecordComparison ? new FastestElapsedComparison(70, 70) : null)
        };
        PerformanceResult future = old with
        {
            SamplesMilliseconds = Enumerable.Repeat(1d, 5).ToArray(),
            RecordedUtc = equalTimestamps ? timestamp : timestamp.AddMinutes(2)
        };
        PerformanceRun WithResult(string id, PerformanceResult result) => Run(id) with
        {
            ResultsEncoding = operation == "Encoding" ? new() { result } : new(),
            ResultsDecoding = operation == "Decoding" ? new() { result } : new()
        };
        var history = new PerformanceHistory
        {
            Runs = new() { WithResult("prior", old), WithResult("current", current), WithResult("future", future) }
        };
        Directory.CreateDirectory(_directory);
        string path = Path.Combine(_directory, "history.json");
        string original = JsonSerializer.Serialize(history,
            new JsonSerializerOptions { DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull });
        File.WriteAllText(path, original);

        // Act
        PerformanceHistoryStore.RegenerateReport(_directory, "current");

        // Assert
        if (!hasOldRecordComparison)
        {
            Assert.DoesNotContain("OverallFastestElapsed", original);
        }
        Assert.Equal(original, File.ReadAllText(path));
        string report = File.ReadAllText(Path.Combine(_directory, "comparison.md"));
        Assert.Contains("Run: `current`", report);
        Assert.Contains("| Overall Fastest elapsed (ms) | 70 | 80 | 10 | 14.286 |", report);
        Assert.Contains("| Median elapsed (ms) | 70 | 80 | 10 | 14.286 |", report);
        Assert.Empty(Directory.GetFiles(_directory, "*.tmp"));
    }

    [Theory]
    [InlineData("Encoding")]
    [InlineData("Decoding")]
    public void Slower_runs_preserve_the_record_and_improvements_become_the_next_reference(string operation)
    {
        // Arrange
        PerformanceResult Case(double duration) => Result("a.pgm", duration, 1000) with
        {
            Operation = operation,
            EncodingMetrics = operation == "Encoding" ? Result("a.pgm", duration, 1000).EncodingMetrics : null
        };
        PerformanceHistoryStore.Append(_directory, Run("record"), Case(70));

        // Act
        PerformanceComparison slower = PerformanceHistoryStore.Append(_directory, Run("slower"), Case(80));
        string slowerReport = File.ReadAllText(Path.Combine(_directory, "comparison.md"));
        PerformanceComparison faster = PerformanceHistoryStore.Append(_directory, Run("faster"), Case(65));
        string fasterReport = File.ReadAllText(Path.Combine(_directory, "comparison.md"));
        PerformanceComparison next = PerformanceHistoryStore.Append(_directory, Run("next"), Case(75));
        PerformanceHistory saved = PerformanceHistoryStore.Load(Path.Combine(_directory, "history.json"));

        // Assert
        Assert.Equal(new FastestElapsedComparison(70, 80), slower.OverallFastestElapsed);
        Assert.Equal(70d, slower.OverallFastestElapsed!.BestMilliseconds);
        Assert.Contains("| Overall Fastest elapsed (ms) | 70 | 80 | 10 | 14.286 |", slowerReport);
        Assert.Equal(new FastestElapsedComparison(70, 65), faster.OverallFastestElapsed);
        Assert.Equal(65d, faster.OverallFastestElapsed!.BestMilliseconds);
        Assert.Contains("| Overall Fastest elapsed (ms) | 70 | 65 | -5 | -7.143 |", fasterReport);
        Assert.Equal(new FastestElapsedComparison(65, 75), next.OverallFastestElapsed);
        Assert.Equal(65d, next.OverallFastestElapsed!.BestMilliseconds);
        Assert.Equal(new[] { 70d, 70d, 65d, 65d },
            saved.Runs.Select(run => Assert.Single(run.Results).Comparison!.OverallFastestElapsed!.BestMilliseconds));
    }

    [Fact]
    public void Decoding_measurement_records_the_same_metrics_and_hashes_decoded_pixels()
    {
        // Arrange
        var image = SimpleBitmap.Gray8(Enumerable.Range(0, 4096)
            .Select(index => (byte)((index * 13 + index / 64 * 7) % 256)).ToArray(), 64, 64);
        var encoder = new WsqCodec();
        encoder.FromRaw(image);
        byte[] encoded = encoder.Encode(new WsqParameters())!;
        var decoder = new WsqCodec();
        using var process = Process.GetCurrentProcess();

        // Act
        DecodingPerformanceSample sample = DecodingPerformanceSample.Measure(decoder, encoded, process);

        // Assert
        WsqPerformanceTestData.AssertImage(image, sample.Bitmap);
        Assert.True(sample.Milliseconds > 0);
        Assert.True(sample.Metrics.AllocatedBytes >= image.Pixels.Length);
        Assert.Equal(image.Pixels.Length, sample.Metrics.OutputBytes);
        Assert.Equal(Convert.ToHexString(SHA256.HashData(sample.Bitmap!.Pixels)), sample.Metrics.OutputSha256);
        Assert.NotEqual(Convert.ToHexString(SHA256.HashData(encoded)), sample.Metrics.OutputSha256);
        Assert.True(sample.Metrics.Before.ManagedHeapBytes > 0);
        Assert.True(sample.Metrics.After.WorkingSetBytes > 0);
        Assert.True(sample.Metrics.After.PrivateMemoryBytes > 0);
        Assert.True(sample.Metrics.ProcessCpuMilliseconds >= 0);
        Assert.True(sample.Metrics.Gen0Collections >= 0);
        Assert.True(sample.Metrics.Gen1Collections >= 0);
        Assert.True(sample.Metrics.Gen2Collections >= 0);
    }

    [Fact]
    public void Measurement_excludes_snapshot_and_result_allocations()
    {
        // Arrange
        using var process = Process.GetCurrentProcess();
        _ = new PerformanceMeasurement(process).Complete();

        // Act
        var measurement = new PerformanceMeasurement(process);
        var buffer = new byte[1024];
        var (_, metrics) = measurement.Complete();
        GC.KeepAlive(buffer);

        // Assert
        Assert.InRange(metrics.AllocatedBytes, 1024, 1088);
    }

    [Fact]
    public void Decoding_compares_legacy_timings_and_all_metrics_on_subsequent_runs()
    {
        // Arrange
        PerformanceResult DecodeResult(double milliseconds, long allocated) => Result("a.pgm", milliseconds, allocated) with
        {
            Operation = "Decoding", EncodingMetrics = null,
            DecodingMetrics = Result("a.pgm", milliseconds, allocated).EncodingMetrics
        };
        PerformanceHistoryStore.Append(_directory, Run("legacy"),
            DecodeResult(1, 1000) with { DecodingMetrics = null });
        PerformanceHistoryStore.Append(_directory, Run("encoding"), Result("a.pgm", 1, 1000));
        PerformanceResult first = DecodeResult(100, 1000);

        // Act
        PerformanceComparison initial = PerformanceHistoryStore.Append(_directory, Run("first"), first);
        string initialReport = File.ReadAllText(Path.Combine(_directory, "comparison.md"));
        PerformanceComparison next = PerformanceHistoryStore.Append(_directory, Run("next"), DecodeResult(80, 800));
        PerformanceHistory history = PerformanceHistoryStore.Load(Path.Combine(_directory, "history.json"));

        // Assert
        Assert.Equal("Compared", initial.Status);
        Assert.Equal("legacy", initial.PreviousRunId);
        Assert.Equal(new MetricChange(1, 100), initial.MedianMilliseconds);
        Assert.Equal(new MetricChange(1, 100), initial.MaximumMilliseconds);
        Assert.Equal(new FastestElapsedComparison(1, 100), initial.OverallFastestElapsed);
        Assert.Null(initial.MedianAllocatedBytes);
        Assert.Null(initial.OutputBytesIdentical);
        Assert.Contains("previous decoding result contains timings only", initialReport);
        Assert.Contains("| Median allocated (bytes) | N/A | 1000 | N/A | N/A |", initialReport);
        Assert.Equal("first", next.PreviousRunId);
        Assert.Equal(-200d, next.MedianAllocatedBytes!.Delta);
        Assert.Equal(-20d, next.MedianAllocatedBytes.PercentChange);
        Assert.NotNull(next.MaximumAllocatedBytes);
        Assert.NotNull(next.MedianProcessCpuMilliseconds);
        Assert.NotNull(next.MedianManagedHeapBytesAfter);
        Assert.NotNull(next.MedianWorkingSetBytesAfter);
        Assert.NotNull(next.MedianPrivateMemoryBytesAfter);
        Assert.NotNull(next.MedianOutputBytes);
        Assert.NotNull(next.Gen0Collections);
        Assert.NotNull(next.Gen1Collections);
        Assert.NotNull(next.Gen2Collections);
        Assert.True(next.OutputBytesIdentical);
        PerformanceResult saved = Assert.Single(history.Runs.Last().ResultsDecoding);
        Assert.Null(saved.EncodingMetrics);
        Assert.Equal(800d, saved.DecodingMetrics!.MedianAllocatedBytes);
        Assert.Equal(5, saved.DecodingMetrics.Samples.Length);
        string report = File.ReadAllText(Path.Combine(_directory, "comparison.md"));
        Assert.Contains("| Median allocated (bytes) | 1000 | 800 | -200 | -20 |", report);
        Assert.Contains("Decoded pixel hashes identical to previous: True", report);
        using JsonDocument json = JsonDocument.Parse(File.ReadAllText(Path.Combine(_directory, "history.json")));
        Assert.Equal(5, json.RootElement.GetProperty("Runs")[3].GetProperty("ResultsDecoding")[0]
            .GetProperty("DecodingMetrics").GetProperty("Samples").GetArrayLength());
    }

    [Theory]
    [InlineData("Failed")]
    [InlineData("Debug")]
    [InlineData("Input")]
    public void Decoding_full_metrics_exclude_ineligible_references(string reason)
    {
        // Arrange
        PerformanceResult first = Result("a.pgm", 100, 1000) with
        {
            Operation = "Decoding", EncodingMetrics = null,
            DecodingMetrics = Result("a.pgm", 100, 1000).EncodingMetrics
        };
        PerformanceHistoryStore.Append(_directory, Run("first"), first);
        PerformanceResult ineligible = first with
        {
            RecordedUtc = DateTimeOffset.UtcNow, SamplesMilliseconds = Enumerable.Repeat(1d, 5).ToArray(),
            ValidationPassed = reason != "Failed", InputSha256 = reason == "Input" ? "other" : first.InputSha256
        };
        PerformanceRun run = Run("ineligible");
        if (reason == "Debug")
        {
            run = run with { Environment = run.Environment with { Configuration = "Debug" } };
        }
        PerformanceHistoryStore.Append(_directory, run, ineligible);
        PerformanceResult current = first with
        {
            RecordedUtc = DateTimeOffset.UtcNow,
            DecodingMetrics = new PerformanceMetrics(first.DecodingMetrics!.Samples
                .Select(sample => sample with { OutputSha256 = "changed-pixels" }).ToArray())
        };

        // Act
        PerformanceComparison comparison = PerformanceHistoryStore.Append(_directory, Run("current"), current);

        // Assert
        Assert.Equal("first", comparison.PreviousRunId);
        Assert.Equal(100d, comparison.OverallFastestElapsed!.Previous);
        Assert.False(comparison.OutputBytesIdentical);
        Assert.Contains("Decoded pixel hashes identical to previous: False",
            File.ReadAllText(Path.Combine(_directory, "comparison.md")));
    }

    [Fact]
    public void Invalid_decoding_metric_sample_count_is_rejected()
    {
        // Arrange
        PerformanceResult result = Result("a.pgm", 100, 1000) with
        {
            Operation = "Decoding", EncodingMetrics = null,
            DecodingMetrics = new PerformanceMetrics(Array.Empty<PerformanceSampleMetrics>())
        };

        // Act
        Exception? error = Record.Exception(() => PerformanceHistoryStore.Append(_directory, Run("invalid"), result));

        // Assert
        Assert.IsType<InvalidDataException>(error);
        Assert.False(File.Exists(Path.Combine(_directory, "history.json")));
    }

    [Fact]
    public void Missing_history_seeds_both_operations_and_compares_available_decoding_baseline_data()
    {
        // Arrange
        string baseline = Path.Combine(TestResources.UnitTestsDirectory, "BiomSharp.Imaging.Tests",
            "Performance", "Baselines", "initial.json");
        byte[] originalBaseline = File.ReadAllBytes(baseline);
        PerformanceRun seed = Assert.Single(PerformanceHistoryStore.Load(baseline).Runs);
        PerformanceResult example = seed.ResultsDecoding[0];
        var currentRun = seed with { Id = "current", StartedUtc = DateTimeOffset.UtcNow };
        PerformanceResult current = example with
        {
            RecordedUtc = DateTimeOffset.UtcNow,
            SamplesMilliseconds = example.SamplesMilliseconds.Select(value => value / 2).ToArray(),
            DecodingMetrics = Result("sample", 1, 1000).EncodingMetrics
        };

        // Act
        PerformanceComparison comparison = PerformanceHistoryStore.Append(_directory, currentRun, current, baseline);
        PerformanceHistory history = PerformanceHistoryStore.Load(Path.Combine(_directory, "history.json"));

        // Assert
        Assert.Equal("Compared", comparison.Status);
        Assert.Equal(seed.Id, comparison.PreviousRunId);
        Assert.Equal(example.MedianMilliseconds, comparison.MedianMilliseconds!.Previous);
        Assert.Equal(-50d, comparison.MedianMilliseconds.PercentChange);
        Assert.Equal(example.MaximumMilliseconds, comparison.MaximumMilliseconds!.Previous);
        Assert.Equal(example.SamplesMilliseconds.Min(), comparison.OverallFastestElapsed!.Previous);
        Assert.Null(comparison.MedianAllocatedBytes);
        Assert.Null(comparison.MedianProcessCpuMilliseconds);
        Assert.Null(comparison.OutputBytesIdentical);
        Assert.Equal(30, history.Runs[0].ResultsEncoding.Count);
        Assert.Equal(30, history.Runs[0].ResultsDecoding.Count);
        Assert.Equal(JsonSerializer.Serialize(seed), JsonSerializer.Serialize(history.Runs[0]));
        Assert.Equal(originalBaseline, File.ReadAllBytes(baseline));
    }

    [Fact]
    public void Refreshing_comparisons_repairs_existing_decoding_report_without_changing_measurements()
    {
        // Arrange
        PerformanceResult prior = Result("a.pgm", 100, 1000) with
        {
            Operation = "Decoding", EncodingMetrics = null
        };
        PerformanceHistoryStore.Append(_directory, Run("prior"), prior);
        PerformanceResult current = prior with
        {
            RecordedUtc = DateTimeOffset.UtcNow,
            SamplesMilliseconds = Enumerable.Repeat(80d, 5).ToArray(),
            DecodingMetrics = Result("a.pgm", 80, 800).EncodingMetrics
        };
        PerformanceHistoryStore.Append(_directory, Run("current"), current);
        string historyPath = Path.Combine(_directory, "history.json");
        PerformanceHistory history = PerformanceHistoryStore.Load(historyPath);
        history.Runs[1].ResultsDecoding[0].Comparison = new PerformanceComparison("NoComparablePreviousResult");
        File.WriteAllText(historyPath, JsonSerializer.Serialize(history));

        // Act
        PerformanceHistoryStore.RegenerateReport(_directory, "current", refreshComparisons: true);
        PerformanceHistory refreshed = PerformanceHistoryStore.Load(historyPath);

        // Assert
        Assert.Equal(2, refreshed.Runs.Count);
        Assert.Equal(JsonSerializer.Serialize(history.Runs[0]), JsonSerializer.Serialize(refreshed.Runs[0]));
        PerformanceResult result = refreshed.Runs[1].ResultsDecoding[0];
        Assert.Equal(current.SamplesMilliseconds, result.SamplesMilliseconds);
        Assert.Equal(JsonSerializer.Serialize(current.DecodingMetrics), JsonSerializer.Serialize(result.DecodingMetrics));
        Assert.Equal("Compared", result.Comparison!.Status);
        Assert.Equal("prior", result.Comparison.PreviousRunId);
        Assert.Contains("| Median elapsed (ms) | 100 | 80 | -20 | -20 |",
            File.ReadAllText(Path.Combine(_directory, "comparison.md")));
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
        var sample = new PerformanceSampleMetrics(allocated, 10, 0, 0, 0, snapshot, snapshot, 100, "output-hash");
        return new PerformanceResult("Encoding", filename, "Default", 0.75f, 64, 64, "input-hash",
            Enumerable.Repeat(milliseconds, 5).ToArray(), new WsqParameters(), DateTimeOffset.UtcNow, true, null,
            new PerformanceMetrics(Enumerable.Repeat(sample, 5).ToArray()));
    }

    public void Dispose()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }
}
