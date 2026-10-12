using System.Text.Json.Serialization;
using BiomSharp.Imaging.Wsq;

namespace BiomSharp.Imaging.Tests.Models;

internal sealed class TestReportEncoding : TestReportBase
{
 public TestReportEncoding(
    string Image,
    float BitRate,
    string Filter,
    string Runtime,
    string OS,
    string Architecture,
    string? BiomSharpVersion,
    WsqMetadata EncodedMetadata,
    WsqMetadata ReferenceMetadata,
    EncodingTestComparison Comparison,
    string CertificationLimitation) 
        : base (Image,
    BitRate,
    Filter,
    Runtime,
    OS,
    Architecture,
    BiomSharpVersion,
    EncodedMetadata,
    ReferenceMetadata,
    Comparison,
    CertificationLimitation)
    {

    }
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
        WsqInspection actual,
        WsqInspection reference,
        int width,
        int height,
        long imageSizeInBytes)
    {
        return EncodingTestReport.Create(imageName, parameters, actual, reference, width, height, imageSizeInBytes);
    }
}
