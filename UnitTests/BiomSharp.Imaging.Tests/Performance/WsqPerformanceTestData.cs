using BiomSharp.Imaging.Pgm;
using BiomSharp.Imaging.Wsq;
using System.Diagnostics;
using System.Reflection;
using System.Runtime;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text.Json;
using Xunit.Abstractions;

namespace BiomSharp.Imaging.Tests.Performance;

public enum PerformanceBitRate
{
    Default,
    Minimum,
    Maximum
}

public static class WsqPerformanceTestData
{
    internal const int MeasuredIterations = 5;
    internal const double MaximumMilliseconds = 1000;

    public static IEnumerable<object[]> Cases()
    {
        for (int image = 1; image <= 10; image++)
        {
            foreach (PerformanceBitRate setting in Enum.GetValues<PerformanceBitRate>())
            {
                yield return new object[] { $"cmp{image:D5}.pgm", setting };
            }
        }
    }

    internal static WsqParameters CreateParameters(PerformanceBitRate setting) => setting switch
    {
        PerformanceBitRate.Default => new WsqParameters(),
        PerformanceBitRate.Minimum => new WsqParameters { BitRate = WsqParameters.BitRateMinValue },
        PerformanceBitRate.Maximum => new WsqParameters { BitRate = WsqParameters.BitRateMaxValue },
        _ => throw new ArgumentOutOfRangeException(nameof(setting), setting, "Unknown bitrate setting.")
    };

    internal static (SimpleBitmap Bitmap, string Sha256) ReadImage(string filename)
    {
        byte[] bytes = File.ReadAllBytes(Path.Combine(TestResources.DatasetDirectory, filename));
        var pgm = new PgmCodec();
        pgm.Decode(bytes);
        SimpleBitmap bitmap = pgm.ToRaw()
            ?? throw new InvalidDataException($"PGM reader returned no image for {filename}.");
        if (!bitmap.IsGray || bitmap.Width <= 0 || bitmap.Height <= 0 ||
            bitmap.Pixels.Length != checked(bitmap.Width * bitmap.Height))
        {
            throw new InvalidDataException($"Expected a complete 8-bit grayscale image: {filename}.");
        }
        return (bitmap, Convert.ToHexString(SHA256.HashData(bytes)));
    }

    internal static void AssertImage(SimpleBitmap expected, SimpleBitmap? actual)
    {
        Assert.NotNull(actual);
        Assert.Equal(expected.Width, actual!.Width);
        Assert.Equal(expected.Height, actual.Height);
        Assert.Equal(expected.BitDepth, actual.BitDepth);
        Assert.Equal(expected.Pixels.Length, actual.Pixels.Length);
    }

    internal static void AssertAndRecordResult(string operation, string filename, PerformanceBitRate setting,
        WsqParameters parameters, SimpleBitmap bitmap, string inputSha256, double[] milliseconds,
        ITestOutputHelper output, Action assertions, EncodingMetrics? encodingMetrics = null)
    {
        try
        {
            assertions();
        }
        catch (Exception error)
        {
            RecordResult(operation, filename, setting, parameters, bitmap, inputSha256, milliseconds,
                output, encodingMetrics, error.ToString());
            throw;
        }
        RecordResult(operation, filename, setting, parameters, bitmap, inputSha256, milliseconds,
            output, encodingMetrics, validationFailure: null);
    }

    private static void RecordResult(string operation, string filename, PerformanceBitRate setting,
        WsqParameters parameters, SimpleBitmap bitmap, string inputSha256, double[] milliseconds,
        ITestOutputHelper output, EncodingMetrics? encodingMetrics, string? validationFailure)
    {
        var result = new PerformanceResult(operation, filename, setting.ToString(), parameters.BitRate,
            bitmap.Width, bitmap.Height, inputSha256, milliseconds, parameters, DateTimeOffset.UtcNow,
            validationFailure == null, validationFailure, encodingMetrics);
        PerformanceComparison comparison = PerformanceHistoryStore.Append(Report.Directory, Report.Run, result,
            Report.BaselinePath);
        output.WriteLine($"{operation}: {filename}, {setting} ({parameters.BitRate} bpp), " +
            $"{bitmap.Width}x{bitmap.Height}; median {result.MedianMilliseconds:F3} ms; " +
            $"maximum {result.MaximumMilliseconds:F3} ms.");
        for (int i = 0; i < milliseconds.Length; i++)
        {
            output.WriteLine($"Sample {i + 1}: {milliseconds[i]:F3} ms.");
        }
        if (encodingMetrics != null)
        {
            output.WriteLine($"Median allocated bytes per encoding: {encodingMetrics.MedianAllocatedBytes:F0}.");
        }
        output.WriteLine($"Comparison: {comparison.Status}; previous run: {comparison.PreviousRunId ?? "none"}.");
        if (comparison.MedianMilliseconds != null)
        {
            output.WriteLine($"Median elapsed change: {comparison.MedianMilliseconds.Delta:F3} ms " +
                $"({comparison.MedianMilliseconds.PercentChange:F2}%).");
        }
        output.WriteLine($"Performance history: {Path.Combine(Report.Directory, PerformanceHistoryStore.HistoryFileName)}");
        output.WriteLine($"Comparison report: {Path.Combine(Report.Directory, PerformanceHistoryStore.ReportFileName)}");
    }

    private static PerformanceRun CreateRun()
    {
#if DEBUG
        const string configuration = "Debug";
#else
        const string configuration = "Release";
#endif
        string[] runtimeSettingNames =
        {
            "DOTNET_TieredCompilation", "DOTNET_TieredPGO", "DOTNET_ReadyToRun",
            "COMPlus_TieredCompilation", "COMPlus_TieredPGO", "COMPlus_ReadyToRun"
        };
        var environment = new PerformanceEnvironment(Environment.MachineName, RuntimeInformation.FrameworkDescription,
            RuntimeInformation.OSDescription, RuntimeInformation.ProcessArchitecture.ToString(),
            Environment.GetEnvironmentVariable("PROCESSOR_IDENTIFIER"), Environment.ProcessorCount,
            configuration, Debugger.IsAttached, GCSettings.IsServerGC, GCSettings.LatencyMode.ToString(),
            JsonSerializer.Serialize(runtimeSettingNames.ToDictionary(name => name, Environment.GetEnvironmentVariable)));
        DateTimeOffset startedUtc = DateTimeOffset.UtcNow;
        return new PerformanceRun($"{startedUtc:yyyyMMddTHHmmssfffZ}_{Guid.NewGuid():N}", startedUtc,
            Environment.GetEnvironmentVariable("BIOMSHARP_PERFORMANCE_REVISION"),
            typeof(WsqCodec).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion,
            Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(typeof(WsqCodec).Assembly.Location))), environment);
    }

    // Initialized only during execution, not xUnit theory discovery.
    private static class Report
    {
        internal static readonly string Directory = Path.Combine(TestResources.UnitTestsDirectory, "TestResults", "Performance");
        internal static readonly string BaselinePath = Path.Combine(TestResources.UnitTestsDirectory,
            "BiomSharp.Imaging.Tests", "Performance", "Baselines", "initial.json");
        internal static readonly PerformanceRun Run = CreateRun();
    }
}
