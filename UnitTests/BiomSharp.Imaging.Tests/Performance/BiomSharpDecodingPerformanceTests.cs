using BiomSharp.Imaging.Wsq;
using System.Diagnostics;
using Xunit.Abstractions;

namespace BiomSharp.Imaging.Tests.Performance;

[Trait("Category", "Performance")]
[Collection(WsqPerformanceCollection.Name)]
public class BiomSharpDecodingPerformanceTests
{
    private readonly ITestOutputHelper _output;

    public BiomSharpDecodingPerformanceTests(ITestOutputHelper output) => _output = output;

    [Theory]
    [MemberData(nameof(WsqPerformanceTestData.Cases), MemberType = typeof(WsqPerformanceTestData))]
    public void Decode_Wsq_at_BitRate_completes_within_1000_ms(string filename, PerformanceBitRate setting)
    {
        // Arrange
        var (bitmap, inputSha256) = WsqPerformanceTestData.ReadImage(filename);
        WsqParameters parameters = WsqPerformanceTestData.CreateParameters(setting);
        var encoder = new WsqCodec();
        encoder.FromRaw(bitmap);
        byte[] encoded = encoder.Encode(parameters)
            ?? throw new InvalidDataException($"WSQ encoder returned no output for {filename}.");
        var warmUp = new WsqCodec();
        warmUp.Decode(encoded);
        var samples = new (SimpleBitmap? Bitmap, double Milliseconds)[WsqPerformanceTestData.MeasuredIterations];

        // Act
        for (int i = 0; i < samples.Length; i++)
        {
            var codec = new WsqCodec();
            var timer = Stopwatch.StartNew();
            codec.Decode(encoded);
            timer.Stop();
            samples[i] = (codec.ToRaw(), timer.Elapsed.TotalMilliseconds);
        }

        // Assert
        WsqPerformanceTestData.AssertAndRecordResult("Decoding", filename, setting, parameters, bitmap, inputSha256,
            samples.Select(sample => sample.Milliseconds).ToArray(), _output, () => Assert.All(samples, sample =>
        {
            WsqPerformanceTestData.AssertImage(bitmap, sample.Bitmap);
            Assert.True(sample.Milliseconds <= WsqPerformanceTestData.MaximumMilliseconds,
                $"Decoding {filename} at {parameters.BitRate} took {sample.Milliseconds:F3} ms; limit: 1000 ms.");
        }));
    }
}
