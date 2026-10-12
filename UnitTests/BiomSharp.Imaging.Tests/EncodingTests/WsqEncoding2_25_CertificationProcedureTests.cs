using BiomSharp.Imaging.Tests.Fixtures;
using BiomSharp.Imaging.Tests.TestCollections;
using BiomSharp.Imaging.Wsq;
using Xunit.Abstractions;

namespace BiomSharp.Imaging.Tests.EncodingTests;

/// <summary>
/// Encodes the 40 NIST reference PGMs at bit allocation target 2.25 (nominal 5:1)
/// with the prescribed 7/9 filter for the NIST WSQ certification procedure.
/// </summary>
/// <remarks>
/// Each case loads checksum-verified dataset inputs, encodes the original PGM with BiomSharp,
/// writes the WSQ and comparison report, then asserts encoding requirements against the
/// corresponding NIST reference WSQ. Together with the 0.75 class, this supplies 80 encoder cases.
/// Generated BiomSharp WSQs are submission outputs; reference copies are local artifacts.
/// Ev is 2, but Sf is currently 0: Implementer.cs labels the implementation number NOT-SPECIFIED.
/// Encoder.cs uses the fixed Implementer.Id rather than WsqParameters.ImplementerId.
/// An assigned platform-specific implementation number must be wired into encoding and the
/// outputs regenerated before submission. Passing these tests does not confer certification.
/// See <see href="https://www.nist.gov/programs-projects/wsq-certification-procedure"/>.
/// </remarks>
[Trait("Category", "Encoding")]
[Collection(EncodingDecodingCollection.Name)]
public class WsqEncoding2_25_CertificationProcedureTests
{
    public WsqEncoding2_25_CertificationProcedureTests(FixtureFullProcess fixture, ITestOutputHelper output)
    {
        _fixture = fixture;
        _fixture.TestOutput = output;
    }

    private readonly FixtureFullProcess _fixture;

    [Fact]
    public void Encode_a001_at_2_25_matches_Nist_reference()
    {
        // Arrange.
        WsqParameters wsqParameters = _fixture.GetEncoding2_25Parameters(WsqFilterType.Odd7x9);
        var test = _fixture.ArrangeTestCase("a001.pgm", wsqParameters);

        // Act.
        _fixture.EncodePgmData(test, test.OriginalPgm);

        // Assert.
        _fixture.WriteComparisonReport(test);
        _fixture.WriteFilesToCaseDirectory(test);
        _fixture.AssertTestCaseEncoding(test);
    }

    [Fact]
    public void Encode_a002_at_2_25_matches_Nist_reference()
    {
        // Arrange.
        WsqParameters wsqParameters = _fixture.GetEncoding2_25Parameters(WsqFilterType.Odd7x9);
        var test = _fixture.ArrangeTestCase("a002.pgm", wsqParameters);

        // Act.
        _fixture.EncodePgmData(test, test.OriginalPgm);

        // Assert.
        _fixture.WriteComparisonReport(test);
        _fixture.WriteFilesToCaseDirectory(test);
        _fixture.AssertTestCaseEncoding(test);
    }

    [Fact]
    public void Encode_a018_at_2_25_matches_Nist_reference()
    {
        // Arrange.
        WsqParameters wsqParameters = _fixture.GetEncoding2_25Parameters(WsqFilterType.Odd7x9);
        var test = _fixture.ArrangeTestCase("a018.pgm", wsqParameters);

        // Act.
        _fixture.EncodePgmData(test, test.OriginalPgm);

        // Assert.
        _fixture.WriteComparisonReport(test);
        _fixture.WriteFilesToCaseDirectory(test);
        _fixture.AssertTestCaseEncoding(test);
    }

    [Fact]
    public void Encode_a039_at_2_25_matches_Nist_reference()
    {
        // Arrange.
        WsqParameters wsqParameters = _fixture.GetEncoding2_25Parameters(WsqFilterType.Odd7x9);
        var test = _fixture.ArrangeTestCase("a039.pgm", wsqParameters);

        // Act.
        _fixture.EncodePgmData(test, test.OriginalPgm);

        // Assert.
        _fixture.WriteComparisonReport(test);
        _fixture.WriteFilesToCaseDirectory(test);
        _fixture.AssertTestCaseEncoding(test);
    }

    [Fact]
    public void Encode_a070_at_2_25_matches_Nist_reference()
    {
        // Arrange.
        WsqParameters wsqParameters = _fixture.GetEncoding2_25Parameters(WsqFilterType.Odd7x9);
        var test = _fixture.ArrangeTestCase("a070.pgm", wsqParameters);

        // Act.
        _fixture.EncodePgmData(test, test.OriginalPgm);

        // Assert.
        _fixture.WriteComparisonReport(test);
        _fixture.WriteFilesToCaseDirectory(test);
        _fixture.AssertTestCaseEncoding(test);
    }

    [Fact]
    public void Encode_a076_at_2_25_matches_Nist_reference()
    {
        // Arrange.
        WsqParameters wsqParameters = _fixture.GetEncoding2_25Parameters(WsqFilterType.Odd7x9);
        var test = _fixture.ArrangeTestCase("a076.pgm", wsqParameters);

        // Act.
        _fixture.EncodePgmData(test, test.OriginalPgm);

        // Assert.
        _fixture.WriteComparisonReport(test);
        _fixture.WriteFilesToCaseDirectory(test);
        _fixture.AssertTestCaseEncoding(test);
    }

    [Fact]
    public void Encode_a089_at_2_25_matches_Nist_reference()
    {
        // Arrange.
        WsqParameters wsqParameters = _fixture.GetEncoding2_25Parameters(WsqFilterType.Odd7x9);
        var test = _fixture.ArrangeTestCase("a089.pgm", wsqParameters);

        // Act.
        _fixture.EncodePgmData(test, test.OriginalPgm);

        // Assert.
        _fixture.WriteComparisonReport(test);
        _fixture.WriteFilesToCaseDirectory(test);
        _fixture.AssertTestCaseEncoding(test);
    }

    [Fact]
    public void Encode_a107_at_2_25_matches_Nist_reference()
    {
        // Arrange.
        WsqParameters wsqParameters = _fixture.GetEncoding2_25Parameters(WsqFilterType.Odd7x9);
        var test = _fixture.ArrangeTestCase("a107.pgm", wsqParameters);

        // Act.
        _fixture.EncodePgmData(test, test.OriginalPgm);

        // Assert.
        _fixture.WriteComparisonReport(test);
        _fixture.WriteFilesToCaseDirectory(test);
        _fixture.AssertTestCaseEncoding(test);
    }

    [Fact]
    public void Encode_a129_at_2_25_matches_Nist_reference()
    {
        // Arrange.
        WsqParameters wsqParameters = _fixture.GetEncoding2_25Parameters(WsqFilterType.Odd7x9);
        var test = _fixture.ArrangeTestCase("a129.pgm", wsqParameters);

        // Act.
        _fixture.EncodePgmData(test, test.OriginalPgm);

        // Assert.
        _fixture.WriteComparisonReport(test);
        _fixture.WriteFilesToCaseDirectory(test);
        _fixture.AssertTestCaseEncoding(test);
    }

    [Fact]
    public void Encode_a165_at_2_25_matches_Nist_reference()
    {
        // Arrange.
        WsqParameters wsqParameters = _fixture.GetEncoding2_25Parameters(WsqFilterType.Odd7x9);
        var test = _fixture.ArrangeTestCase("a165.pgm", wsqParameters);

        // Act.
        _fixture.EncodePgmData(test, test.OriginalPgm);

        // Assert.
        _fixture.WriteComparisonReport(test);
        _fixture.WriteFilesToCaseDirectory(test);
        _fixture.AssertTestCaseEncoding(test);
    }

    [Fact]
    public void Encode_b082_at_2_25_matches_Nist_reference()
    {
        // Arrange.
        WsqParameters wsqParameters = _fixture.GetEncoding2_25Parameters(WsqFilterType.Odd7x9);
        var test = _fixture.ArrangeTestCase("b082.pgm", wsqParameters);

        // Act.
        _fixture.EncodePgmData(test, test.OriginalPgm);

        // Assert.
        _fixture.WriteComparisonReport(test);
        _fixture.WriteFilesToCaseDirectory(test);
        _fixture.AssertTestCaseEncoding(test);
    }

    [Fact]
    public void Encode_b124_at_2_25_matches_Nist_reference()
    {
        // Arrange.
        WsqParameters wsqParameters = _fixture.GetEncoding2_25Parameters(WsqFilterType.Odd7x9);
        var test = _fixture.ArrangeTestCase("b124.pgm", wsqParameters);

        // Act.
        _fixture.EncodePgmData(test, test.OriginalPgm);

        // Assert.
        _fixture.WriteComparisonReport(test);
        _fixture.WriteFilesToCaseDirectory(test);
        _fixture.AssertTestCaseEncoding(test);
    }

    [Fact]
    public void Encode_b157_at_2_25_matches_Nist_reference()
    {
        // Arrange.
        WsqParameters wsqParameters = _fixture.GetEncoding2_25Parameters(WsqFilterType.Odd7x9);
        var test = _fixture.ArrangeTestCase("b157.pgm", wsqParameters);

        // Act.
        _fixture.EncodePgmData(test, test.OriginalPgm);

        // Assert.
        _fixture.WriteComparisonReport(test);
        _fixture.WriteFilesToCaseDirectory(test);
        _fixture.AssertTestCaseEncoding(test);
    }

    [Fact]
    public void Encode_b158_at_2_25_matches_Nist_reference()
    {
        // Arrange.
        WsqParameters wsqParameters = _fixture.GetEncoding2_25Parameters(WsqFilterType.Odd7x9);
        var test = _fixture.ArrangeTestCase("b158.pgm", wsqParameters);

        // Act.
        _fixture.EncodePgmData(test, test.OriginalPgm);

        // Assert.
        _fixture.WriteComparisonReport(test);
        _fixture.WriteFilesToCaseDirectory(test);
        _fixture.AssertTestCaseEncoding(test);
    }

    [Fact]
    public void Encode_b186_at_2_25_matches_Nist_reference()
    {
        // Arrange.
        WsqParameters wsqParameters = _fixture.GetEncoding2_25Parameters(WsqFilterType.Odd7x9);
        var test = _fixture.ArrangeTestCase("b186.pgm", wsqParameters);

        // Act.
        _fixture.EncodePgmData(test, test.OriginalPgm);

        // Assert.
        _fixture.WriteComparisonReport(test);
        _fixture.WriteFilesToCaseDirectory(test);
        _fixture.AssertTestCaseEncoding(test);
    }

    [Fact]
    public void Encode_cmp00001_at_2_25_matches_Nist_reference()
    {
        // Arrange.
        WsqParameters wsqParameters = _fixture.GetEncoding2_25Parameters(WsqFilterType.Odd7x9);
        var test = _fixture.ArrangeTestCase("cmp00001.pgm", wsqParameters);

        // Act.
        _fixture.EncodePgmData(test, test.OriginalPgm);

        // Assert.
        _fixture.WriteComparisonReport(test);
        _fixture.WriteFilesToCaseDirectory(test);
        _fixture.AssertTestCaseEncoding(test);
    }

    [Fact]
    public void Encode_cmp00002_at_2_25_matches_Nist_reference()
    {
        // Arrange.
        WsqParameters wsqParameters = _fixture.GetEncoding2_25Parameters(WsqFilterType.Odd7x9);
        var test = _fixture.ArrangeTestCase("cmp00002.pgm", wsqParameters);

        // Act.
        _fixture.EncodePgmData(test, test.OriginalPgm);

        // Assert.
        _fixture.WriteComparisonReport(test);
        _fixture.WriteFilesToCaseDirectory(test);
        _fixture.AssertTestCaseEncoding(test);
    }

    [Fact]
    public void Encode_cmp00003_at_2_25_matches_Nist_reference()
    {
        // Arrange.
        WsqParameters wsqParameters = _fixture.GetEncoding2_25Parameters(WsqFilterType.Odd7x9);
        var test = _fixture.ArrangeTestCase("cmp00003.pgm", wsqParameters);

        // Act.
        _fixture.EncodePgmData(test, test.OriginalPgm);

        // Assert.
        _fixture.WriteComparisonReport(test);
        _fixture.WriteFilesToCaseDirectory(test);
        _fixture.AssertTestCaseEncoding(test);
    }

    [Fact]
    public void Encode_cmp00004_at_2_25_matches_Nist_reference()
    {
        // Arrange.
        WsqParameters wsqParameters = _fixture.GetEncoding2_25Parameters(WsqFilterType.Odd7x9);
        var test = _fixture.ArrangeTestCase("cmp00004.pgm", wsqParameters);

        // Act.
        _fixture.EncodePgmData(test, test.OriginalPgm);

        // Assert.
        _fixture.WriteComparisonReport(test);
        _fixture.WriteFilesToCaseDirectory(test);
        _fixture.AssertTestCaseEncoding(test);
    }

    [Fact]
    public void Encode_cmp00005_at_2_25_matches_Nist_reference()
    {
        // Arrange.
        WsqParameters wsqParameters = _fixture.GetEncoding2_25Parameters(WsqFilterType.Odd7x9);
        var test = _fixture.ArrangeTestCase("cmp00005.pgm", wsqParameters);

        // Act.
        _fixture.EncodePgmData(test, test.OriginalPgm);

        // Assert.
        _fixture.WriteComparisonReport(test);
        _fixture.WriteFilesToCaseDirectory(test);
        _fixture.AssertTestCaseEncoding(test);
    }

    [Fact]
    public void Encode_cmp00006_at_2_25_matches_Nist_reference()
    {
        // Arrange.
        WsqParameters wsqParameters = _fixture.GetEncoding2_25Parameters(WsqFilterType.Odd7x9);
        var test = _fixture.ArrangeTestCase("cmp00006.pgm", wsqParameters);

        // Act.
        _fixture.EncodePgmData(test, test.OriginalPgm);

        // Assert.
        _fixture.WriteComparisonReport(test);
        _fixture.WriteFilesToCaseDirectory(test);
        _fixture.AssertTestCaseEncoding(test);
    }

    [Fact]
    public void Encode_cmp00007_at_2_25_matches_Nist_reference()
    {
        // Arrange.
        WsqParameters wsqParameters = _fixture.GetEncoding2_25Parameters(WsqFilterType.Odd7x9);
        var test = _fixture.ArrangeTestCase("cmp00007.pgm", wsqParameters);

        // Act.
        _fixture.EncodePgmData(test, test.OriginalPgm);

        // Assert.
        _fixture.WriteComparisonReport(test);
        _fixture.WriteFilesToCaseDirectory(test);
        _fixture.AssertTestCaseEncoding(test);
    }

    [Fact]
    public void Encode_cmp00008_at_2_25_matches_Nist_reference()
    {
        // Arrange.
        WsqParameters wsqParameters = _fixture.GetEncoding2_25Parameters(WsqFilterType.Odd7x9);
        var test = _fixture.ArrangeTestCase("cmp00008.pgm", wsqParameters);

        // Act.
        _fixture.EncodePgmData(test, test.OriginalPgm);

        // Assert.
        _fixture.WriteComparisonReport(test);
        _fixture.WriteFilesToCaseDirectory(test);
        _fixture.AssertTestCaseEncoding(test);
    }

    [Fact]
    public void Encode_cmp00009_at_2_25_matches_Nist_reference()
    {
        // Arrange.
        WsqParameters wsqParameters = _fixture.GetEncoding2_25Parameters(WsqFilterType.Odd7x9);
        var test = _fixture.ArrangeTestCase("cmp00009.pgm", wsqParameters);

        // Act.
        _fixture.EncodePgmData(test, test.OriginalPgm);

        // Assert.
        _fixture.WriteComparisonReport(test);
        _fixture.WriteFilesToCaseDirectory(test);
        _fixture.AssertTestCaseEncoding(test);
    }

    [Fact]
    public void Encode_cmp00010_at_2_25_matches_Nist_reference()
    {
        // Arrange.
        WsqParameters wsqParameters = _fixture.GetEncoding2_25Parameters(WsqFilterType.Odd7x9);
        var test = _fixture.ArrangeTestCase("cmp00010.pgm", wsqParameters);

        // Act.
        _fixture.EncodePgmData(test, test.OriginalPgm);

        // Assert.
        _fixture.WriteComparisonReport(test);
        _fixture.WriteFilesToCaseDirectory(test);
        _fixture.AssertTestCaseEncoding(test);
    }

    [Fact]
    public void Encode_cmp00011_at_2_25_matches_Nist_reference()
    {
        // Arrange.
        WsqParameters wsqParameters = _fixture.GetEncoding2_25Parameters(WsqFilterType.Odd7x9);
        var test = _fixture.ArrangeTestCase("cmp00011.pgm", wsqParameters);

        // Act.
        _fixture.EncodePgmData(test, test.OriginalPgm);

        // Assert.
        _fixture.WriteComparisonReport(test);
        _fixture.WriteFilesToCaseDirectory(test);
        _fixture.AssertTestCaseEncoding(test);
    }

    [Fact]
    public void Encode_cmp00012_at_2_25_matches_Nist_reference()
    {
        // Arrange.
        WsqParameters wsqParameters = _fixture.GetEncoding2_25Parameters(WsqFilterType.Odd7x9);
        var test = _fixture.ArrangeTestCase("cmp00012.pgm", wsqParameters);

        // Act.
        _fixture.EncodePgmData(test, test.OriginalPgm);

        // Assert.
        _fixture.WriteComparisonReport(test);
        _fixture.WriteFilesToCaseDirectory(test);
        _fixture.AssertTestCaseEncoding(test);
    }

    [Fact]
    public void Encode_cmp00013_at_2_25_matches_Nist_reference()
    {
        // Arrange.
        WsqParameters wsqParameters = _fixture.GetEncoding2_25Parameters(WsqFilterType.Odd7x9);
        var test = _fixture.ArrangeTestCase("cmp00013.pgm", wsqParameters);

        // Act.
        _fixture.EncodePgmData(test, test.OriginalPgm);

        // Assert.
        _fixture.WriteComparisonReport(test);
        _fixture.WriteFilesToCaseDirectory(test);
        _fixture.AssertTestCaseEncoding(test);
    }

    [Fact]
    public void Encode_cmp00014_at_2_25_matches_Nist_reference()
    {
        // Arrange.
        WsqParameters wsqParameters = _fixture.GetEncoding2_25Parameters(WsqFilterType.Odd7x9);
        var test = _fixture.ArrangeTestCase("cmp00014.pgm", wsqParameters);

        // Act.
        _fixture.EncodePgmData(test, test.OriginalPgm);

        // Assert.
        _fixture.WriteComparisonReport(test);
        _fixture.WriteFilesToCaseDirectory(test);
        _fixture.AssertTestCaseEncoding(test);
    }

    [Fact]
    public void Encode_cmp00015_at_2_25_matches_Nist_reference()
    {
        // Arrange.
        WsqParameters wsqParameters = _fixture.GetEncoding2_25Parameters(WsqFilterType.Odd7x9);
        var test = _fixture.ArrangeTestCase("cmp00015.pgm", wsqParameters);

        // Act.
        _fixture.EncodePgmData(test, test.OriginalPgm);

        // Assert.
        _fixture.WriteComparisonReport(test);
        _fixture.WriteFilesToCaseDirectory(test);
        _fixture.AssertTestCaseEncoding(test);
    }

    [Fact]
    public void Encode_cmp00016_at_2_25_matches_Nist_reference()
    {
        // Arrange.
        WsqParameters wsqParameters = _fixture.GetEncoding2_25Parameters(WsqFilterType.Odd7x9);
        var test = _fixture.ArrangeTestCase("cmp00016.pgm", wsqParameters);

        // Act.
        _fixture.EncodePgmData(test, test.OriginalPgm);

        // Assert.
        _fixture.WriteComparisonReport(test);
        _fixture.WriteFilesToCaseDirectory(test);
        _fixture.AssertTestCaseEncoding(test);
    }

    [Fact]
    public void Encode_cmp00017_at_2_25_matches_Nist_reference()
    {
        // Arrange.
        WsqParameters wsqParameters = _fixture.GetEncoding2_25Parameters(WsqFilterType.Odd7x9);
        var test = _fixture.ArrangeTestCase("cmp00017.pgm", wsqParameters);

        // Act.
        _fixture.EncodePgmData(test, test.OriginalPgm);

        // Assert.
        _fixture.WriteComparisonReport(test);
        _fixture.WriteFilesToCaseDirectory(test);
        _fixture.AssertTestCaseEncoding(test);
    }

    [Fact]
    public void Encode_cmp00018_at_2_25_matches_Nist_reference()
    {
        // Arrange.
        WsqParameters wsqParameters = _fixture.GetEncoding2_25Parameters(WsqFilterType.Odd7x9);
        var test = _fixture.ArrangeTestCase("cmp00018.pgm", wsqParameters);

        // Act.
        _fixture.EncodePgmData(test, test.OriginalPgm);

        // Assert.
        _fixture.WriteComparisonReport(test);
        _fixture.WriteFilesToCaseDirectory(test);
        _fixture.AssertTestCaseEncoding(test);
    }

    [Fact]
    public void Encode_cmp00019_at_2_25_matches_Nist_reference()
    {
        // Arrange.
        WsqParameters wsqParameters = _fixture.GetEncoding2_25Parameters(WsqFilterType.Odd7x9);
        var test = _fixture.ArrangeTestCase("cmp00019.pgm", wsqParameters);

        // Act.
        _fixture.EncodePgmData(test, test.OriginalPgm);

        // Assert.
        _fixture.WriteComparisonReport(test);
        _fixture.WriteFilesToCaseDirectory(test);
        _fixture.AssertTestCaseEncoding(test);
    }

    [Fact]
    public void Encode_sample_01_at_2_25_matches_Nist_reference()
    {
        // Arrange.
        WsqParameters wsqParameters = _fixture.GetEncoding2_25Parameters(WsqFilterType.Odd7x9);
        var test = _fixture.ArrangeTestCase("sample_01.pgm", wsqParameters);

        // Act.
        _fixture.EncodePgmData(test, test.OriginalPgm);

        // Assert.
        _fixture.WriteComparisonReport(test);
        _fixture.WriteFilesToCaseDirectory(test);
        _fixture.AssertTestCaseEncoding(test);
    }

    [Fact]
    public void Encode_sample_03_at_2_25_matches_Nist_reference()
    {
        // Arrange.
        WsqParameters wsqParameters = _fixture.GetEncoding2_25Parameters(WsqFilterType.Odd7x9);
        var test = _fixture.ArrangeTestCase("sample_03.pgm", wsqParameters);

        // Act.
        _fixture.EncodePgmData(test, test.OriginalPgm);

        // Assert.
        _fixture.WriteComparisonReport(test);
        _fixture.WriteFilesToCaseDirectory(test);
        _fixture.AssertTestCaseEncoding(test);
    }

    [Fact]
    public void Encode_sample_04_at_2_25_matches_Nist_reference()
    {
        // Arrange.
        WsqParameters wsqParameters = _fixture.GetEncoding2_25Parameters(WsqFilterType.Odd7x9);
        var test = _fixture.ArrangeTestCase("sample_04.pgm", wsqParameters);

        // Act.
        _fixture.EncodePgmData(test, test.OriginalPgm);

        // Assert.
        _fixture.WriteComparisonReport(test);
        _fixture.WriteFilesToCaseDirectory(test);
        _fixture.AssertTestCaseEncoding(test);
    }

    [Fact]
    public void Encode_sample_10_at_2_25_matches_Nist_reference()
    {
        // Arrange.
        WsqParameters wsqParameters = _fixture.GetEncoding2_25Parameters(WsqFilterType.Odd7x9);
        var test = _fixture.ArrangeTestCase("sample_10.pgm", wsqParameters);

        // Act.
        _fixture.EncodePgmData(test, test.OriginalPgm);

        // Assert.
        _fixture.WriteComparisonReport(test);
        _fixture.WriteFilesToCaseDirectory(test);
        _fixture.AssertTestCaseEncoding(test);
    }

    [Fact]
    public void Encode_sample_11_at_2_25_matches_Nist_reference()
    {
        // Arrange.
        WsqParameters wsqParameters = _fixture.GetEncoding2_25Parameters(WsqFilterType.Odd7x9);
        var test = _fixture.ArrangeTestCase("sample_11.pgm", wsqParameters);

        // Act.
        _fixture.EncodePgmData(test, test.OriginalPgm);

        // Assert.
        _fixture.WriteComparisonReport(test);
        _fixture.WriteFilesToCaseDirectory(test);
        _fixture.AssertTestCaseEncoding(test);
    }

    [Fact]
    public void Encode_sample_19_at_2_25_matches_Nist_reference()
    {
        // Arrange.
        WsqParameters wsqParameters = _fixture.GetEncoding2_25Parameters(WsqFilterType.Odd7x9);
        var test = _fixture.ArrangeTestCase("sample_19.pgm", wsqParameters);

        // Act.
        _fixture.EncodePgmData(test, test.OriginalPgm);

        // Assert.
        _fixture.WriteComparisonReport(test);
        _fixture.WriteFilesToCaseDirectory(test);
        _fixture.AssertTestCaseEncoding(test);
    }
}
