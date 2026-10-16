using BiomSharp.Imaging.Wsq;
using System.Diagnostics;
using System.Security.Cryptography;

namespace BiomSharp.Imaging.Tests.Performance;

internal sealed record EncodingPerformanceSample(byte[]? Bytes, double Milliseconds, PerformanceSampleMetrics Metrics)
{
    internal static EncodingPerformanceSample Measure(WsqCodec codec, WsqParameters parameters, Process process)
    {
        var measurement = new PerformanceMeasurement(process);
        byte[]? bytes = codec.Encode(parameters);
        var (milliseconds, metrics) = measurement.Complete();
        return new EncodingPerformanceSample(bytes, milliseconds, metrics with
        {
            OutputBytes = bytes?.Length ?? 0,
            OutputSha256 = bytes == null ? null : Convert.ToHexString(SHA256.HashData(bytes))
        });
    }
}
