using System.Globalization;
using System.Text.Json.Serialization;

namespace BiomSharp.Imaging.Tests.Models;

internal sealed record WsqMetadata(
    int Width,
    int Height,
    byte Black,
    byte White,
    byte Encoder,
    short Implementer,
    byte LowFilterLength,
    byte HighFilterLength,
    long SizeExcludingCommentsInBytes,
    int[] BlockCounts)
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? CompressionRatio { get; init; }

    public static WsqMetadata Create(WsqInspection inspection, long? imageSizeInBytes = null)
    {
        if (imageSizeInBytes < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(imageSizeInBytes));
        }
        if (imageSizeInBytes.HasValue && inspection.SizeWithoutComments <= 0)
        {
            throw new ArgumentException("WSQ size excluding comments must be positive.", nameof(inspection));
        }

        WsqFrame header = inspection.Header;
        return new WsqMetadata(header.Width, header.Height, header.Black, header.White,
            header.Encoder, header.Implementer, header.LowFilterLength, header.HighFilterLength,
            inspection.SizeWithoutComments, inspection.BlockCounts)
        {
            CompressionRatio = imageSizeInBytes.HasValue
                ? ((decimal)imageSizeInBytes.Value / inspection.SizeWithoutComments)
                    .ToString("0.##", CultureInfo.InvariantCulture) + ":1"
                : null
        };
    }
}
