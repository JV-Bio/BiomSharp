using BiomSharp.Imaging.Wsq;
using System.Diagnostics;
using Xunit.Abstractions;

namespace BiomSharp.Imaging.Tests.Performance;

[Trait("Category", "Performance")]
[Collection(WsqPerformanceCollection.Name)]
public class BiomSharpEncodingPerformanceTests
{
    private readonly ITestOutputHelper _output;

    public BiomSharpEncodingPerformanceTests(ITestOutputHelper output) => _output = output;

    [Theory]
    [MemberData(nameof(WsqPerformanceTestData.Cases), MemberType = typeof(WsqPerformanceTestData))]
    public void Encode_PgmFile_at_BitRate_completes_within_1000_ms(string filename, PerformanceBitRate setting)
    {
        // Arrange
        var (bitmap, inputSha256) = WsqPerformanceTestData.ReadImage(filename);
        WsqParameters parameters = WsqPerformanceTestData.CreateParameters(setting);
        using var process = Process.GetCurrentProcess();
        var warmUp = new WsqCodec();
        warmUp.FromRaw(bitmap);
        _ = EncodingPerformanceSample.Measure(warmUp, parameters, process);
        var samples = new EncodingPerformanceSample[WsqPerformanceTestData.MeasuredIterations];

        // Act
        for (int i = 0; i < samples.Length; i++)
        {
            var codec = new WsqCodec();
            codec.FromRaw(bitmap);
            samples[i] = EncodingPerformanceSample.Measure(codec, parameters, process);
        }

        // Assert
        WsqPerformanceTestData.AssertAndRecordResult("Encoding", filename, setting, parameters, bitmap, inputSha256,
            samples.Select(sample => sample.Milliseconds).ToArray(), _output, () => Assert.All(samples, sample =>
        {
            Assert.NotNull(sample.Bytes);
            Assert.NotEmpty(sample.Bytes!);
            var decoder = new WsqCodec();
            decoder.Decode(sample.Bytes!);
            WsqPerformanceTestData.AssertImage(bitmap, decoder.ToRaw());
            Assert.True(sample.Milliseconds <= WsqPerformanceTestData.MaximumMilliseconds,
                $"Encoding {filename} at {parameters.BitRate} took {sample.Milliseconds:F3} ms; limit: 1000 ms.");
        }), new EncodingMetrics(samples.Select(sample => sample.Metrics).ToArray()));
    }
}
