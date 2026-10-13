using System.Text.Json;
using BiomSharp.Imaging.Tests.Fixtures;
using BiomSharp.Imaging.Tests.Models;
using BiomSharp.Imaging.Wsq;
using BiomSharp.Nist.Nfiq;

namespace BiomSharp.Imaging.Tests.TestInfrastructureTests;

/// <summary>
/// Checks report structure, configuration, metadata, and availability states.
/// Detailed pixel measurements and acceptance boundaries are covered by
/// <see cref="DecodingTestReportTests"/>.
/// </summary>
public class FullProcessReportTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), $"biomsharp-report-tests-{Guid.NewGuid():N}");

    [Theory]
    [InlineData(0.75f, "15")]
    [InlineData(1.125f, "10")]
    [InlineData(2.25f, "5")]
    public void Report_uses_effective_bitrate_for_configuration_and_filename(float bitRate, string ratio)
    {
        // Arrange
        var test = CreateTest(bitRate: bitRate);

        // Act
        using var json = WriteReport(test);

        // Assert
        Assert.Equal(bitRate, json.RootElement.GetProperty("WsqParameters").GetProperty("BitRate").GetSingle());
        Assert.Equal(ratio, json.RootElement.GetProperty("ConfiguredRatio").GetString());
        Assert.Equal($"example.comparison-{ratio}.json", Path.GetFileName(Assert.Single(Directory.GetFiles(_directory))));
    }

    [Fact]
    public void Report_records_case_environment_and_original_image_without_claiming_certification()
    {
        // Arrange
        var test = CreateTest();

        // Act
        using var json = WriteReport(test);

        // Assert
        var root = json.RootElement;

        Assert.Equal(test.CaseId.TestName, root.GetProperty("TestName").GetString());
        var environment = root.GetProperty("Environment");
        Assert.Equal(test.Environment.Runtime, environment.GetProperty("Runtime").GetString());
        Assert.Equal(test.Environment.OS, environment.GetProperty("OS").GetString());
        Assert.Equal(test.Environment.Architecture, environment.GetProperty("Architecture").GetString());
        Assert.Equal(test.Environment.BiomSharpVersion, environment.GetProperty("BiomSharpVersion").GetString());
        var original = root.GetProperty("OriginalImage");
        Assert.Equal("example.pgm", original.GetProperty("FileName").GetString());
        Assert.Equal(test.OriginalPgm.Data.Length, original.GetProperty("SizeInBytes").GetInt32());
        Assert.Equal(4, original.GetProperty("RawPixelSizeInBytes").GetInt32());
        Assert.Equal(2, original.GetProperty("Width").GetInt32());
        Assert.Equal(2, original.GetProperty("Height").GetInt32());
        Assert.Equal(8, original.GetProperty("BitDepth").GetInt32());
        Assert.False(root.GetProperty("CertificationEvaluated").GetBoolean());
        Assert.Contains("does not claim NIST/FBI certification", root.GetProperty("CertificationLimitation").GetString());
    }

    [Theory]
    [InlineData(true, true, "EncodingAndDecoding")]
    [InlineData(true, false, "Encoding")]
    [InlineData(false, true, "Decoding")]
    [InlineData(false, false, "None")]
    public void Report_identifies_operations_from_available_results(bool encoding, bool decoding, string operation)
    {
        // Arrange
        var test = CreateTest(256, 256);
        if (encoding)
            test = AddEncoding(test, includeReference: false);
        if (!decoding)
            test.DecodedResults.Clear();

        // Act
        using var json = WriteReport(test);

        // Assert
        Assert.Equal(operation, json.RootElement.GetProperty("CodecOperation").GetString());
        Assert.Equal(encoding ? 1 : 0, json.RootElement.GetProperty("EncodedComparisons").EnumerateObject().Count());
        Assert.Equal(decoding ? 1 : 0, json.RootElement.GetProperty("DecodedComparisons").EnumerateObject().Count());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Report_records_reference_availability_without_changing_configuration(bool includeReference)
    {
        // Arrange
        var test = AddEncoding(CreateTest(256, 256), includeReference);

        // Act
        using var json = WriteReport(test);

        // Assert
        var root = json.RootElement;
        var encoded = root.GetProperty("EncodedComparisons").GetProperty("BiomSharp");

        Assert.Equal("15", root.GetProperty("ConfiguredRatio").GetString());
        Assert.Equal(0.75f, root.GetProperty("WsqParameters").GetProperty("BitRate").GetSingle());
        if (includeReference)
        {
            Assert.Equal("Available", root.GetProperty("ReferenceStatus").GetString());
            Assert.Equal(JsonValueKind.Null, root.GetProperty("ReferenceLimitation").ValueKind);
            Assert.Equal("example.wsq", root.GetProperty("OriginalWsq").GetProperty("FileName").GetString());
            Assert.Equal(test.OriginalWsqData!.Data.Length, root.GetProperty("OriginalWsq").GetProperty("SizeInBytes").GetInt32());
            var comparison = encoded.GetProperty("ReferenceComparison");
            Assert.False(comparison.GetProperty("BytesIdentical").GetBoolean());
            Assert.Equal(7, comparison.GetProperty("SizeDifferenceInBytes").GetInt32());
            Assert.Equal(0m, comparison.GetProperty("Structure").GetProperty("SizeDifferencePercent").GetDecimal());
            Assert.Empty(comparison.GetProperty("Structure").GetProperty("Failures").EnumerateArray());
        }
        else
        {
            Assert.Equal("Unavailable", root.GetProperty("ReferenceStatus").GetString());
            Assert.Contains("no reference comparison", root.GetProperty("ReferenceLimitation").GetString());
            Assert.Equal(JsonValueKind.Null, root.GetProperty("OriginalWsq").ValueKind);
            Assert.Equal(JsonValueKind.Null, encoded.GetProperty("ReferenceComparison").ValueKind);
        }
    }

    [Fact]
    public void Report_distinguishes_file_and_pixel_ratios_with_and_without_comments()
    {
        // Arrange
        var test = AddEncoding(CreateTest(256, 256), includeReference: true);
        var wsq = test.EncodedResults[ExecutableType.BiomSharp];
        long originalSize = test.OriginalPgm.Data.LongLength;
        long rawSize = 256 * 256;
        long sizeWithoutComments = test.OriginalWsqData!.Data.LongLength;

        // Act
        using var json = WriteReport(test);

        // Assert
        var encoded = json.RootElement.GetProperty("EncodedComparisons").GetProperty("BiomSharp");

        Assert.Equal(sizeWithoutComments + 7, encoded.GetProperty("SizeInBytes").GetInt64());
        Assert.Equal((decimal)originalSize / wsq.Data.Length, encoded.GetProperty("FileCompressionRatio").GetDecimal());
        Assert.Equal((decimal)rawSize / wsq.Data.Length, encoded.GetProperty("RawPixelCompressionRatio").GetDecimal());
        Assert.Equal((decimal)originalSize / sizeWithoutComments, encoded.GetProperty("FileCompressionRatioExcludingComments").GetDecimal());
        Assert.Equal((decimal)rawSize / sizeWithoutComments, encoded.GetProperty("RawPixelCompressionRatioExcludingComments").GetDecimal());
        Assert.Equal(wsq.Data.Length * 8m / rawSize, encoded.GetProperty("AchievedBitsPerPixel").GetDecimal());
        var metadata = encoded.GetProperty("Metadata");
        Assert.Equal(256, metadata.GetProperty("Width").GetInt32());
        Assert.Equal(256, metadata.GetProperty("Height").GetInt32());
        Assert.Equal(sizeWithoutComments, metadata.GetProperty("SizeExcludingCommentsInBytes").GetInt64());
    }

    [Fact]
    public void Report_keeps_full_nfiq_results_per_image_and_decoder_comparisons_compact()
    {
        // Arrange
        var test = CreateTest();
        var originalAnalysis = new Nfiq2Analysis(80, allMeasures: true);
        originalAnalysis.SetQualityFeatures(new List<string> { "Feature" }, new[] { 1.5 });
        test.OriginalPgm.Nfiq2Data.UpdateValues("test-version", 0, originalAnalysis);
        test.DecodedResults[ExecutableType.BiomSharp].Nfiq2Data.UpdateValues("test-version", 0, new Nfiq2Analysis(75));
        AddNativeBaseline(test);
        test.DecodedResults[ExecutableType.NativDswq].Nfiq2Data.UpdateValues("test-version", 0, new Nfiq2Analysis(77));

        // Act
        using var json = WriteReport(test);

        // Assert
        var root = json.RootElement;
        var original = root.GetProperty("OriginalImage");
        Assert.Equal("Calculated", original.GetProperty("Nfiq2Status").GetString());
        Assert.Equal(80, original.GetProperty("Nfiq2").GetProperty("Nfiq2Analysis").GetProperty("Score").GetInt32());
        Assert.Equal(1.5, original.GetProperty("Nfiq2").GetProperty("Nfiq2Analysis")
            .GetProperty("QualityFeatures").GetProperty("Feature").GetDouble());
        var decoded = root.GetProperty("DecodedComparisons").GetProperty("BiomSharp");
        Assert.Equal("Calculated", decoded.GetProperty("Nfiq2Status").GetString());
        Assert.Equal(75, decoded.GetProperty("Nfiq2").GetProperty("Nfiq2Analysis").GetProperty("Score").GetInt32());
        Assert.Equal(-5, decoded.GetProperty("Nfiq2ScoreDifference").GetInt32());
        var native = root.GetProperty("DecodedComparisons").GetProperty("NativDswq");
        Assert.Equal(-3, native.GetProperty("Nfiq2ScoreDifference").GetInt32());
        Assert.Equal("dwsq", native.GetProperty("Pixels").GetProperty("DecoderExecutable").GetString());
        Assert.Equal("test-hash", native.GetProperty("Pixels").GetProperty("DecoderSha256").GetString());

        var comparisons = root.GetProperty("DecoderComparisons");
        Assert.Equal(new[] { "Status", "Limitation", "Baseline", "WsqRequirementsDecoding", "Results" },
            comparisons.EnumerateObject().Select(property => property.Name).ToArray());
        Assert.Equal("Available", comparisons.GetProperty("Status").GetString());
        Assert.Equal(JsonValueKind.Null, comparisons.GetProperty("Limitation").ValueKind);
        Assert.Equal("NativDswq", comparisons.GetProperty("Baseline").GetString());
        var requirements = comparisons.GetProperty("WsqRequirementsDecoding");
        Assert.Equal(99.9m, requirements.GetProperty("MinimumIdenticalPixelPercent").GetDecimal());
        Assert.Equal(1, requirements.GetProperty("MaximumPixelDifference").GetInt32());
        var result = Assert.Single(comparisons.GetProperty("Results").EnumerateObject());
        Assert.Equal("BiomSharp", result.Name);
        Assert.Equal(new[]
        {
            "Status", "Failures", "Mismatches", "IdenticalPixelPercent",
            "MaximumAbsoluteDifference", "MeanAbsoluteDifference", "MeanSquaredError", "IsWsqRequirementsDecodingPassed"
        }, result.Value.EnumerateObject().Select(property => property.Name).ToArray());
    }

    [Theory]
    [InlineData(true, 0, "NotCalculated")]
    [InlineData(true, 7, "Failed")]
    [InlineData(false, 0, "NotCalculated")]
    [InlineData(false, 7, "Failed")]
    public void Report_requires_both_nfiq_scores_before_calculating_a_difference(
        bool unavailableOriginal, int errorCode, string status)
    {
        // Arrange
        var test = CreateTest();
        var decodedPgm = test.DecodedResults[ExecutableType.BiomSharp];
        test.OriginalPgm.Nfiq2Data.UpdateValues("test-version", 0, new Nfiq2Analysis(80));
        decodedPgm.Nfiq2Data.UpdateValues("test-version", 0, new Nfiq2Analysis(75));
        (unavailableOriginal ? test.OriginalPgm : decodedPgm).Nfiq2Data.UpdateValues("test-version", errorCode, null);

        // Act
        using var json = WriteReport(test);

        // Assert
        var decoded = json.RootElement.GetProperty("DecodedComparisons").GetProperty("BiomSharp");
        var unavailable = unavailableOriginal ? json.RootElement.GetProperty("OriginalImage") : decoded;
        Assert.Equal(status, unavailable.GetProperty("Nfiq2Status").GetString());
        Assert.Equal(errorCode, unavailable.GetProperty("Nfiq2").GetProperty("ErrorCode").GetInt32());
        Assert.Equal("test-version", unavailable.GetProperty("Nfiq2").GetProperty("Version").GetString());
        Assert.Equal(JsonValueKind.Null, unavailable.GetProperty("Nfiq2").GetProperty("Nfiq2Analysis").ValueKind);
        Assert.Equal(JsonValueKind.Null, decoded.GetProperty("Nfiq2ScoreDifference").ValueKind);
    }

    [Theory]
    [InlineData(false, "BaselineUnavailable")]
    [InlineData(true, "NoOtherDecoderResults")]
    public void Report_explains_why_decoder_comparisons_are_unavailable(bool nativeOnly, string status)
    {
        // Arrange
        var test = CreateTest();
        if (nativeOnly)
        {
            test.DecodedResults.Clear();
            AddNativeBaseline(test);
        }

        // Act
        using var json = WriteReport(test);

        // Assert
        var comparisons = json.RootElement.GetProperty("DecoderComparisons");
        Assert.Equal(status, comparisons.GetProperty("Status").GetString());
        Assert.False(string.IsNullOrWhiteSpace(comparisons.GetProperty("Limitation").GetString()));
        Assert.Equal(nativeOnly ? "NativDswq" : null, comparisons.GetProperty("Baseline").GetString());
        Assert.Empty(comparisons.GetProperty("Results").EnumerateObject());
    }

    [Fact]
    public void Report_records_missing_decoded_image_without_asserting_or_claiming_a_result()
    {
        // Arrange
        var test = CreateTest();
        AddNativeBaseline(test);
        test.DecodedResults[ExecutableType.BiomSharp] = null!;

        // Act
        using var json = WriteReport(test);

        // Assert
        var decoded = json.RootElement.GetProperty("DecodedComparisons").GetProperty("BiomSharp");
        Assert.Equal("Unavailable", decoded.GetProperty("Nfiq2Status").GetString());
        Assert.Equal(JsonValueKind.Null, decoded.GetProperty("SizeInBytes").ValueKind);
        Assert.Equal(JsonValueKind.Null, decoded.GetProperty("Nfiq2").ValueKind);
        var comparison = json.RootElement.GetProperty("DecoderComparisons").GetProperty("Results").GetProperty("BiomSharp");
        foreach (var pixels in new[] { decoded.GetProperty("Pixels"), comparison })
        {
            Assert.Equal("Unavailable", pixels.GetProperty("Status").GetString());
            Assert.NotEmpty(pixels.GetProperty("Failures").EnumerateArray());
            Assert.Equal(JsonValueKind.Null, pixels.GetProperty("IdenticalPixelPercent").ValueKind);
            Assert.Equal(JsonValueKind.Null, pixels.GetProperty("MeanSquaredError").ValueKind);
        }
        Assert.Equal(JsonValueKind.Null, comparison.GetProperty("IsWsqRequirementsDecodingPassed").ValueKind);
    }

    [Fact]
    public void Report_includes_additional_decoders_and_records_failed_requirements_without_asserting()
    {
        // Arrange
        var test = CreateTest();
        AddNativeBaseline(test);
        var additional = (ExecutableType)123;
        test.AddPgmContainer(additional, new DataContainerPgm("AdditionalDecoder", "other.pgm",
            SimpleBitmap.Gray8(new byte[] { 255, 255, 255, 255 }, 2, 2)));

        // Act
        using var json = WriteReport(test);

        // Assert
        var results = json.RootElement.GetProperty("DecoderComparisons").GetProperty("Results");
        Assert.Equal(2, results.EnumerateObject().Count());
        var result = results.GetProperty(additional.ToString());
        Assert.Equal("Available", result.GetProperty("Status").GetString());
        Assert.Empty(result.GetProperty("Failures").EnumerateArray());
        Assert.False(result.GetProperty("IsWsqRequirementsDecodingPassed").GetBoolean());
        Assert.True(json.RootElement.GetProperty("DecodedComparisons").TryGetProperty(additional.ToString(), out _));
    }

    private TestCaseData CreateTest(int width = 2, int height = 2, float bitRate = 0.75f)
    {
        byte[] pixels = new byte[width * height];
        new Random(42).NextBytes(pixels);
        pixels[0] = 0;
        var original = new DataContainerPgm("Original file", "example.pgm", SimpleBitmap.Gray8(pixels, width, height));
        var test = TestCaseData.Create(
            new TestCaseIdentity("example.pgm", nameof(FullProcessReportTests), _directory, "EncodingAndDecoding"),
            new WsqParameters { BitRate = bitRate }, original, null);
        pixels[0] = 1;
        test.AddPgmContainer(ExecutableType.BiomSharp,
            new DataContainerPgm("BiomSharp", "example.pgm", SimpleBitmap.Gray8(pixels, width, height)));
        return test;
    }

    private static TestCaseData AddEncoding(TestCaseData test, bool includeReference)
    {
        var codec = new WsqCodec();
        codec.FromRaw(test.OriginalPgm.GetSimpleBitmap()!);
        byte[] reference = codec.Encode(test.Parameters)
            ?? throw new InvalidDataException("Test encoder returned no WSQ data.");
        byte[] comment = { 0xff, 0xa8, 0, 5, 65, 66, 67 };
        byte[] encoded = reference[..2].Concat(comment).Concat(reference[2..]).ToArray();
        test = test with { OriginalWsqData = includeReference ? new DataContainerWsq("example.wsq", reference) : null };
        test.AddWsqContainer(ExecutableType.BiomSharp, new DataContainerWsq("example.wsq", encoded));
        return test;
    }

    private static void AddNativeBaseline(TestCaseData test) =>
        test.AddPgmContainer(ExecutableType.NativDswq,
            new DataContainerPgm("dwsq", "example.pgm", test.OriginalPgm.Data) { DecoderSha256 = "test-hash" });

    private JsonDocument WriteReport(TestCaseData test)
    {
        new FixtureFullProcess().WriteComparisonReport(test);
        string path = Assert.Single(Directory.GetFileSystemEntries(_directory));
        Assert.Equal(".json", Path.GetExtension(path));
        return JsonDocument.Parse(File.ReadAllText(path));
    }

    public void Dispose()
    {
        if (Directory.Exists(_directory))
            Directory.Delete(_directory, recursive: true);
    }
}
