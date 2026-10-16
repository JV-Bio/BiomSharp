using BiomSharp.Imaging.Wsq;
using System.Diagnostics;
using System.Security.Cryptography;

namespace BiomSharp.Imaging.Tests.Performance;

internal sealed record DecodingPerformanceSample(SimpleBitmap? Bitmap, double Milliseconds,
    PerformanceSampleMetrics Metrics)
{
    internal static DecodingPerformanceSample Measure(WsqCodec codec, byte[] encoded, Process process)
    {
        var measurement = new PerformanceMeasurement(process);
        codec.Decode(encoded);
        var (milliseconds, metrics) = measurement.Complete();
        SimpleBitmap? bitmap = codec.ToRaw();
        return new DecodingPerformanceSample(bitmap, milliseconds, metrics with
        {
            OutputBytes = bitmap?.Pixels.Length ?? 0,
            OutputSha256 = bitmap == null ? null : Convert.ToHexString(SHA256.HashData(bitmap.Pixels))
        });
    }
}
