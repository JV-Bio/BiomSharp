using System.Text.Json;
using BiomSharp.Imaging.Tests.Models;
using BiomSharp.Imaging.Wsq;

namespace BiomSharp.Imaging.Tests.TestInfrastructureTests;

public class EncodingTestReportTests
{
    [Theory]
    [InlineData(107256L, 107213L, 1520064L, "14.17:1", "14.18:1")]
    [InlineData(2147483648L, 2147483649L, 10737418240L, "5:1", "5:1")]
    [InlineData(200L, 100L, 1000L, "5:1", "10:1")]
    public void Report_groups_metadata_by_file(
        long actualSize, long referenceSize, long imageSize, string encodedRatio, string referenceRatio)
    {
        var actual = new WsqInspection(new WsqFrame(1508, 1008, 0, 255, 2, 0, 7, 9),
            actualSize, 0.44m, new decimal[WsqRequirementsEncoding.ComparedSubbandCount],
            new decimal[WsqRequirementsEncoding.ComparedSubbandCount],
            new[] { 1, 2, 3 }, new[] { 1, 1, 1 });
        var reference = actual with
        {
            Header = new WsqFrame(1508, 1008, 1, 254, 2, -27436, 8, 8),
            SizeWithoutComments = referenceSize,
            BlockCounts = new[] { 2, 1, 0 }
        };

        var parameters = new WsqParameters();
        EncodingTestReport report = EncodingTestReport.Create(
            "a001.pgm", parameters, actual, reference, 1508, 1008, imageSize);
        using JsonDocument json = JsonDocument.Parse(JsonSerializer.Serialize(report));
        JsonElement root = json.RootElement;

        Assert.Equal(new[] { "Runtime", "OS", "Architecture", "BiomSharpVersion", "InputValues", "EncodedMetadata", "ReferenceMetadata" },
            root.EnumerateObject().Take(7).Select(property => property.Name).ToArray());
        JsonElement input = root.GetProperty("InputValues");
        Assert.Equal(new[] { "Image", "ImageSizeInBytes", "BitRate", "Filter" },
            input.EnumerateObject().Select(property => property.Name).ToArray());
        Assert.Equal("a001.pgm", input.GetProperty("Image").GetString());
        Assert.Equal(imageSize, input.GetProperty("ImageSizeInBytes").GetInt64());
        Assert.Equal(parameters.BitRate, input.GetProperty("BitRate").GetSingle());
        Assert.Equal(parameters.Filter.ToString(), input.GetProperty("Filter").GetString());
        AssertMetadata(root.GetProperty("EncodedMetadata"), actual, encodedRatio);
        AssertMetadata(root.GetProperty("ReferenceMetadata"), reference, referenceRatio);
        foreach (string oldProperty in new[]
        {
            "ActualMetadata", "ActualHeader", "ReferenceHeader",
            "ActualBytesExcludingComments", "ReferenceBytesExcludingComments",
            "ActualBlockCounts", "ReferenceBlockCounts", "CompressionRatio",
            "Image", "ImageSizeInBytes", "BitRate", "Filter"
        })
        {
            Assert.False(root.TryGetProperty(oldProperty, out _));
        }
        Assert.Equal(
            JsonSerializer.Serialize(EncodingTestComparison.Compare(reference, actual)),
            root.GetProperty("Comparison").GetRawText());
    }

    [Fact]
    public void Report_preserves_input_dimension_checks_when_both_wsq_frames_agree()
    {
        var inspection = new WsqInspection(new WsqFrame(100, 100, 0, 255, 2, 0, 7, 9),
            1000, 0.44m, new decimal[WsqRequirementsEncoding.ComparedSubbandCount],
            new decimal[WsqRequirementsEncoding.ComparedSubbandCount], new[] { 0 }, new[] { 1, 0, 0 });

        var report = EncodingTestReport.Create("example.pgm", new WsqParameters(),
            inspection, inspection, 200, 100, 20000);

        Assert.Contains("Encoded dimensions differ from the input PGM.", report.Comparison.Failures);
        Assert.Contains("Reference dimensions differ from the input PGM.", report.Comparison.Failures);
    }

    private static void AssertMetadata(JsonElement metadata, WsqInspection inspection, string compressionRatio)
    {
        Assert.Equal(11, metadata.EnumerateObject().Count());
        Assert.Equal(compressionRatio, metadata.GetProperty("CompressionRatio").GetString());
        Assert.Equal(inspection.Header.Width, metadata.GetProperty("Width").GetInt32());
        Assert.Equal(inspection.Header.Height, metadata.GetProperty("Height").GetInt32());
        Assert.Equal(inspection.Header.Black, metadata.GetProperty("Black").GetByte());
        Assert.Equal(inspection.Header.White, metadata.GetProperty("White").GetByte());
        Assert.Equal(inspection.Header.Encoder, metadata.GetProperty("Encoder").GetByte());
        Assert.Equal(inspection.Header.Implementer, metadata.GetProperty("Implementer").GetInt16());
        Assert.Equal(inspection.Header.LowFilterLength, metadata.GetProperty("LowFilterLength").GetByte());
        Assert.Equal(inspection.Header.HighFilterLength, metadata.GetProperty("HighFilterLength").GetByte());
        Assert.Equal(inspection.SizeWithoutComments, metadata.GetProperty("SizeExcludingCommentsInBytes").GetInt64());
        Assert.False(metadata.TryGetProperty("BytesExcludingComments", out _));
        Assert.Equal(inspection.BlockCounts,
            metadata.GetProperty("BlockCounts").EnumerateArray().Select(value => value.GetInt32()).ToArray());
    }
}
