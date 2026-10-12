using System.Text.Json;
using BiomSharp.Imaging.Pgm;
using BiomSharp.Imaging.Tests.Fixtures;
using BiomSharp.Imaging.Tests.Models;
using BiomSharp.Imaging.Wsq;

namespace BiomSharp.Imaging.Tests.TestInfrastructureTests;

public class DecodingTestReportTests
{
    [Theory]
    [InlineData(0, 0, 100, 0, 0)]
    [InlineData(1, 1, 75, 0.25, 0.25)]
    [InlineData(255, 1, 75, 63.75, 16256.25)]
    public void Report_compares_pixels_without_claiming_certification(
        byte changedPixel, int mismatches, decimal identicalPercent, decimal meanDifference, decimal meanSquaredError)
    {
        var test = CreateTest(SimpleBitmap.Gray8(new byte[] { changedPixel, 10, 20, 30 }, 2, 2));
        try
        {
            new FixtureFullProcess().WriteComparisonReport(test);
            using var json = ReadReport(test);
            var root = json.RootElement;
            Assert.False(root.GetProperty("CertificationEvaluated").GetBoolean());
            Assert.Contains("does not claim NIST/FBI certification", root.GetProperty("CertificationLimitation").GetString());
            Assert.Equal("Decoding", root.GetProperty("CodecOperation").GetString());
            var comparison = root.GetProperty("DecodedComparisons").GetProperty("BiomSharp").GetProperty("Pixels");
            Assert.Equal("BiomSharp", comparison.GetProperty("DecoderExecutable").GetString());
            Assert.Equal("Original uncompressed PGM", comparison.GetProperty("Baseline").GetString());
            Assert.Equal(4, comparison.GetProperty("PixelCount").GetInt32());
            Assert.Equal(mismatches, comparison.GetProperty("Mismatches").GetInt32());
            Assert.Equal(identicalPercent, comparison.GetProperty("IdenticalPixelPercent").GetDecimal());
            Assert.Equal((int)changedPixel, comparison.GetProperty("MaximumAbsoluteDifference").GetInt32());
            Assert.Equal(meanDifference, comparison.GetProperty("MeanAbsoluteDifference").GetDecimal());
            Assert.Equal(meanSquaredError, comparison.GetProperty("MeanSquaredError").GetDecimal());
            Assert.Single(Directory.GetFiles(test.CaseId.CaseDirectory));
        }
        finally { DeleteArtifacts(test); }
    }

    [Fact]
    public void Report_compares_native_pixels_and_artifact_writer_saves_native_binary_Pgm()
    {
        var test = CreateTest(SimpleBitmap.Gray8(new byte[] { 1, 10, 20, 30 }, 2, 2));
        byte[] nativePixels = { 255, 9, 20, 30 };
        test.AddPgmContainer(ExecutableType.NativDswq, new DataContainerPgm("dwsq", "example.pgm",
            SimpleBitmap.Gray8(nativePixels, 2, 2)) { DecoderSha256 = "native-hash" });
        var fixture = new FixtureFullProcess();
        try
        {
            fixture.WriteComparisonReport(test);
            using var json = ReadReport(test);
            var root = json.RootElement;
            var native = root.GetProperty("DecodedComparisons").GetProperty("NativDswq").GetProperty("Pixels");
            Assert.Equal("dwsq", native.GetProperty("DecoderExecutable").GetString());
            Assert.Equal("native-hash", native.GetProperty("DecoderSha256").GetString());
            Assert.Equal(2, native.GetProperty("Mismatches").GetInt32());
            Assert.Equal(50m, native.GetProperty("IdenticalPixelPercent").GetDecimal());
            Assert.Equal(255, native.GetProperty("MaximumAbsoluteDifference").GetInt32());
            Assert.Equal(64m, native.GetProperty("MeanAbsoluteDifference").GetDecimal());
            Assert.Equal(16256.5m, native.GetProperty("MeanSquaredError").GetDecimal());
            var decoder = root.GetProperty("DecoderComparisons").GetProperty("Results").GetProperty("BiomSharp");
            Assert.Equal(2, decoder.GetProperty("Mismatches").GetInt32());
            Assert.Equal(50m, decoder.GetProperty("IdenticalPixelPercent").GetDecimal());
            Assert.Equal(254, decoder.GetProperty("MaximumAbsoluteDifference").GetInt32());
            Assert.Equal(63.75m, decoder.GetProperty("MeanAbsoluteDifference").GetDecimal());
            Assert.Equal(16129.25m, decoder.GetProperty("MeanSquaredError").GetDecimal());
            Assert.False(decoder.GetProperty("IsWsqRequirementsDecodingPassed").GetBoolean());

            Assert.Single(Directory.GetFiles(test.CaseId.CaseDirectory));
            fixture.WriteFilesToCaseDirectory(test);
            var pgm = new PgmCodec();
            using var stream = File.OpenRead(Path.Combine(test.CaseId.CaseDirectory, "example.NativDswq.decoded-15.pgm"));
            pgm.Read(stream, out PgmParameters? parameters);
            Assert.Equal(PgmFormatType.BIN, parameters!.Format);
            var saved = Assert.IsType<SimpleBitmap>(pgm.ToRaw());
            Assert.Equal(2, saved.Width);
            Assert.Equal(2, saved.Height);
            Assert.Equal(nativePixels, saved.Pixels);
        }
        finally { DeleteArtifacts(test); }
    }

    [Fact]
    public void Report_records_missing_native_reconstruction()
    {
        var test = CreateTest(SimpleBitmap.Gray8(new byte[4], 2, 2));
        test.RemovePgmContainer(ExecutableType.NativDswq);
        try
        {
            new FixtureFullProcess().WriteComparisonReport(test);
            using var json = ReadReport(test);
            Assert.Equal("BaselineUnavailable",
                json.RootElement.GetProperty("DecoderComparisons").GetProperty("Status").GetString());
        }
        finally { DeleteArtifacts(test); }
    }

    [Theory]
    [InlineData(1000, 0, 0, true)]
    [InlineData(1000, 1, 1, true)]
    [InlineData(999, 1, 1, false)]
    [InlineData(1000, 2, 1, false)]
    [InlineData(1000, 1, 2, false)]
    public void Decoder_agreement_uses_inclusive_unrounded_AA3_limits(
        int count, int mismatches, byte difference, bool passes)
    {
        byte[] pixels = new byte[count];
        Array.Fill(pixels, difference, 0, mismatches);
        var test = CreateTest(SimpleBitmap.Gray8(pixels, count, 1), SimpleBitmap.Gray8(new byte[count], count, 1));
        try
        {
            new FixtureFullProcess().WriteComparisonReport(test);
            using var json = ReadReport(test);
            var comparison = json.RootElement.GetProperty("DecoderComparisons");
            var requirements = comparison.GetProperty("WsqRequirementsDecoding");
            Assert.Equal(99.9m, requirements.GetProperty("MinimumIdenticalPixelPercent").GetDecimal());
            Assert.Equal(1, requirements.GetProperty("MaximumPixelDifference").GetInt32());
            Assert.Equal(passes, comparison.GetProperty("Results").GetProperty("BiomSharp")
                .GetProperty("IsWsqRequirementsDecodingPassed").GetBoolean());
        }
        finally { DeleteArtifacts(test); }
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Report_records_incompatible_dimensions_even_with_equal_pixel_counts(bool invalidNative)
    {
        var test = CreateTest(SimpleBitmap.Gray8(new byte[4], 2, 2));
        test.AddPgmContainer(invalidNative ? ExecutableType.NativDswq : ExecutableType.BiomSharp,
            new DataContainerPgm("Test", "different.pgm", SimpleBitmap.Gray8(new byte[4], 4, 1)));
        try
        {
            new FixtureFullProcess().WriteComparisonReport(test);
            using var json = ReadReport(test);
            var comparison = json.RootElement.GetProperty("DecoderComparisons").GetProperty("Results").GetProperty("BiomSharp");
            Assert.Equal("Unavailable", comparison.GetProperty("Status").GetString());
            Assert.NotEmpty(comparison.GetProperty("Failures").EnumerateArray());
            Assert.Equal(JsonValueKind.Null, comparison.GetProperty("IsWsqRequirementsDecodingPassed").ValueKind);
        }
        finally { DeleteArtifacts(test); }
    }

    [Fact]
    public void Invalid_pixel_buffer_is_rejected_when_creating_pgm_input()
    {
        Assert.Throws<InvalidOperationException>(() =>
            new DataContainerPgm("Test", "invalid.pgm", SimpleBitmap.Gray8(new byte[3], 2, 2)));
    }

    private static TestCaseData CreateTest(SimpleBitmap decoded, SimpleBitmap? original = null)
    {
        original ??= SimpleBitmap.Gray8(new byte[] { 0, 10, 20, 30 }, 2, 2);
        string directory = Path.Combine(TestResources.UnitTestsDirectory, "TestResults", $"report_{Guid.NewGuid():N}");
        var test = TestCaseData.Create(new TestCaseIdentity("example.pgm", "report", directory, "Decoding"),
            new WsqParameters { BitRate = 0.75f }, new DataContainerPgm("Original", "example.pgm", original), null);
        test.AddPgmContainer(ExecutableType.BiomSharp, new DataContainerPgm("BiomSharp", "example.pgm", decoded));
        test.AddPgmContainer(ExecutableType.NativDswq, new DataContainerPgm("dwsq", "example.pgm", original));
        return test;
    }

    private static JsonDocument ReadReport(TestCaseData test) =>
        JsonDocument.Parse(File.ReadAllText(Path.Combine(test.CaseId.CaseDirectory, "example.comparison-15.json")));

    private static void DeleteArtifacts(TestCaseData test)
    {
        if (!Directory.Exists(test.CaseId.CaseDirectory))
            return;
        foreach (string path in Directory.GetFiles(test.CaseId.CaseDirectory))
            File.Delete(path);
        Directory.Delete(test.CaseId.CaseDirectory);
    }
}
