using System.Runtime.InteropServices;
using System.Text.Json.Serialization;
using BiomSharp.Imaging.Wsq;

namespace BiomSharp.Imaging.Tests.Models;

internal sealed record EncodingTestReport(
    string Runtime,
    string OS,
    string Architecture,
    string? BiomSharpVersion,
    EncodingInputValues InputValues,
    WsqMetadata EncodedMetadata,
    WsqMetadata ReferenceMetadata,
    EncodingTestComparison Comparison,
    string CertificationLimitation)
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? WsqImage { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? SequentialWsqImage { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? ReferenceWsqImage { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? Batch { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public DateTimeOffset? EncodingStartedUtc { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public DateTimeOffset? EncodingEndedUtc { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public double? EncodingMilliseconds { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public double? BatchMilliseconds { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public SequentialComparison? SequentialComparison { get; init; }

    public static EncodingTestReport Create(string imageName,
        WsqParameters parameters,
        WsqInspection encoded,
        WsqInspection reference,
        int width,
        int height,
        long imageSizeInBytes)
    {
        var comparison = EncodingTestComparison.Compare(reference, encoded);
        if (encoded.Header.Width != width || encoded.Header.Height != height)
        {
            comparison.Failures.Add("Encoded dimensions differ from the input PGM.");
        }
        if (reference.Header.Width != width || reference.Header.Height != height)
        {
            comparison.Failures.Add("Reference dimensions differ from the input PGM.");
        }

        return new EncodingTestReport(
            RuntimeInformation.FrameworkDescription, 
            RuntimeInformation.OSDescription,
            RuntimeInformation.ProcessArchitecture.ToString(), 
            typeof(WsqCodec).Assembly.GetName().Version?.ToString(),
            new EncodingInputValues(imageName, imageSizeInBytes, parameters.BitRate, parameters.Filter.ToString()),
            WsqMetadata.Create(encoded, imageSizeInBytes),
            WsqMetadata.Create(reference, imageSizeInBytes),
            comparison,
            "Local encoder evaluation only; Sf=0 is not an assigned implementation number.");
    }

}
