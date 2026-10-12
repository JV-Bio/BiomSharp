using BiomSharp.Imaging.Tests.Fixtures;
using BiomSharp.Imaging.Tests.TestCollections;
using BiomSharp.Imaging.Wsq;
using Xunit.Abstractions;

namespace BiomSharp.Imaging.Tests.DecodingTests;

/// <summary>
/// Reconstructs the 40 NIST reference WSQs from decode/75, compressed at bit allocation
/// target 0.75 (nominal 15:1), for the NIST WSQ certification procedure.
/// </summary>
/// <remarks>
/// Each case loads checksum-verified inputs and decodes the same NIST WSQ with native dwsq
/// and BiomSharp, rather than decoding a newly generated BiomSharp encoding.
/// Binary P5 images and comparison reports are written before asserting decoder agreement.
/// Original-PGM comparisons are structural and diagnostic because WSQ compression is lossy.
/// BiomSharp PGMs are submission outputs; native reconstructions are local comparison artifacts.
/// The two bitrate classes provide 80 cases; WsqFilter_CertificationProcedureTests adds the
/// six non-7/9-filter cases required for the complete 86-case decoder set.
/// BiomSharp encoding currently writes Sf = 0 (NOT-SPECIFIED in Implementer.cs), ignoring
/// WsqParameters.ImplementerId. That encoder limitation does not alter these supplied NIST
/// WSQs or PGM outputs, which have no Sf field. Passing these tests does not confer certification.
/// See <see href="https://www.nist.gov/programs-projects/wsq-certification-procedure"/>.
/// </remarks>
[Trait("Category", "Decoding")]
[Collection(EncodingDecodingCollection.Name)]
public class WsqDecoding0_75_CertificationProcedureTests
{
    public WsqDecoding0_75_CertificationProcedureTests(FixtureFullProcess fixture, ITestOutputHelper output)
    {
        _fixture = fixture;
        _fixture.TestOutput = output;
    }

    private readonly FixtureFullProcess _fixture;

    [Fact]
    public void Decode_a001_at_0_75_produces_binary_Pgm()
    {
        // Arrange.
        var wsqParam = _fixture.GetEncoding0_75Parameters(WsqFilterType.Odd7x9);
        var testData = _fixture.ArrangeTestCase("a001.pgm", wsqParam);
        _fixture.DecodeWsqDataUsingNistDwsq(testData, testData.OriginalWsqData);

        // Act.
        _fixture.DecodeWsqData(testData, testData.OriginalWsqData);

        // Assert.
        _fixture.WriteComparisonReport(testData);
        _fixture.WriteFilesToCaseDirectory(testData);
        _fixture.AssertTestCaseDecoding(testData);
    }

    [Fact]
    public void Decode_a002_at_0_75_produces_binary_Pgm()
    {
        // Arrange.
        var wsqParam = _fixture.GetEncoding0_75Parameters(WsqFilterType.Odd7x9);
        var testData = _fixture.ArrangeTestCase("a002.pgm", wsqParam);
        _fixture.DecodeWsqDataUsingNistDwsq(testData, testData.OriginalWsqData);

        // Act.
        _fixture.DecodeWsqData(testData, testData.OriginalWsqData);

        // Assert.
        _fixture.WriteComparisonReport(testData);
        _fixture.WriteFilesToCaseDirectory(testData);
        _fixture.AssertTestCaseDecoding(testData);
    }

    [Fact]
    public void Decode_a018_at_0_75_produces_binary_Pgm()
    {
        // Arrange.
        var wsqParam = _fixture.GetEncoding0_75Parameters(WsqFilterType.Odd7x9);
        var testData = _fixture.ArrangeTestCase("a018.pgm", wsqParam);
        _fixture.DecodeWsqDataUsingNistDwsq(testData, testData.OriginalWsqData);

        // Act.
        _fixture.DecodeWsqData(testData, testData.OriginalWsqData);

        // Assert.
        _fixture.WriteComparisonReport(testData);
        _fixture.WriteFilesToCaseDirectory(testData);
        _fixture.AssertTestCaseDecoding(testData);
    }

    [Fact]
    public void Decode_a039_at_0_75_produces_binary_Pgm()
    {
        // Arrange.
        var wsqParam = _fixture.GetEncoding0_75Parameters(WsqFilterType.Odd7x9);
        var testData = _fixture.ArrangeTestCase("a039.pgm", wsqParam);
        _fixture.DecodeWsqDataUsingNistDwsq(testData, testData.OriginalWsqData);

        // Act.
        _fixture.DecodeWsqData(testData, testData.OriginalWsqData);

        // Assert.
        _fixture.WriteComparisonReport(testData);
        _fixture.WriteFilesToCaseDirectory(testData);
        _fixture.AssertTestCaseDecoding(testData);
    }

    [Fact]
    public void Decode_a070_at_0_75_produces_binary_Pgm()
    {
        // Arrange.
        var wsqParam = _fixture.GetEncoding0_75Parameters(WsqFilterType.Odd7x9);
        var testData = _fixture.ArrangeTestCase("a070.pgm", wsqParam);
        _fixture.DecodeWsqDataUsingNistDwsq(testData, testData.OriginalWsqData);

        // Act.
        _fixture.DecodeWsqData(testData, testData.OriginalWsqData);

        // Assert.
        _fixture.WriteComparisonReport(testData);
        _fixture.WriteFilesToCaseDirectory(testData);
        _fixture.AssertTestCaseDecoding(testData);
    }

    [Fact]
    public void Decode_a076_at_0_75_produces_binary_Pgm()
    {
        // Arrange.
        var wsqParam = _fixture.GetEncoding0_75Parameters(WsqFilterType.Odd7x9);
        var testData = _fixture.ArrangeTestCase("a076.pgm", wsqParam);
        _fixture.DecodeWsqDataUsingNistDwsq(testData, testData.OriginalWsqData);

        // Act.
        _fixture.DecodeWsqData(testData, testData.OriginalWsqData);

        // Assert.
        _fixture.WriteComparisonReport(testData);
        _fixture.WriteFilesToCaseDirectory(testData);
        _fixture.AssertTestCaseDecoding(testData);
    }

    [Fact]
    public void Decode_a089_at_0_75_produces_binary_Pgm()
    {
        // Arrange.
        var wsqParam = _fixture.GetEncoding0_75Parameters(WsqFilterType.Odd7x9);
        var testData = _fixture.ArrangeTestCase("a089.pgm", wsqParam);
        _fixture.DecodeWsqDataUsingNistDwsq(testData, testData.OriginalWsqData);

        // Act.
        _fixture.DecodeWsqData(testData, testData.OriginalWsqData);

        // Assert.
        _fixture.WriteComparisonReport(testData);
        _fixture.WriteFilesToCaseDirectory(testData);
        _fixture.AssertTestCaseDecoding(testData);
    }

    [Fact]
    public void Decode_a107_at_0_75_produces_binary_Pgm()
    {
        // Arrange.
        var wsqParam = _fixture.GetEncoding0_75Parameters(WsqFilterType.Odd7x9);
        var testData = _fixture.ArrangeTestCase("a107.pgm", wsqParam);
        _fixture.DecodeWsqDataUsingNistDwsq(testData, testData.OriginalWsqData);

        // Act.
        _fixture.DecodeWsqData(testData, testData.OriginalWsqData);

        // Assert.
        _fixture.WriteComparisonReport(testData);
        _fixture.WriteFilesToCaseDirectory(testData);
        _fixture.AssertTestCaseDecoding(testData);
    }

    [Fact]
    public void Decode_a129_at_0_75_produces_binary_Pgm()
    {
        // Arrange.
        var wsqParam = _fixture.GetEncoding0_75Parameters(WsqFilterType.Odd7x9);
        var testData = _fixture.ArrangeTestCase("a129.pgm", wsqParam);
        _fixture.DecodeWsqDataUsingNistDwsq(testData, testData.OriginalWsqData);

        // Act.
        _fixture.DecodeWsqData(testData, testData.OriginalWsqData);

        // Assert.
        _fixture.WriteComparisonReport(testData);
        _fixture.WriteFilesToCaseDirectory(testData);
        _fixture.AssertTestCaseDecoding(testData);
    }

    [Fact]
    public void Decode_a165_at_0_75_produces_binary_Pgm()
    {
        // Arrange.
        var wsqParam = _fixture.GetEncoding0_75Parameters(WsqFilterType.Odd7x9);
        var testData = _fixture.ArrangeTestCase("a165.pgm", wsqParam);
        _fixture.DecodeWsqDataUsingNistDwsq(testData, testData.OriginalWsqData);

        // Act.
        _fixture.DecodeWsqData(testData, testData.OriginalWsqData);

        // Assert.
        _fixture.WriteComparisonReport(testData);
        _fixture.WriteFilesToCaseDirectory(testData);
        _fixture.AssertTestCaseDecoding(testData);
    }

    [Fact]
    public void Decode_b082_at_0_75_produces_binary_Pgm()
    {
        // Arrange.
        var wsqParam = _fixture.GetEncoding0_75Parameters(WsqFilterType.Odd7x9);
        var testData = _fixture.ArrangeTestCase("b082.pgm", wsqParam);
        _fixture.DecodeWsqDataUsingNistDwsq(testData, testData.OriginalWsqData);

        // Act.
        _fixture.DecodeWsqData(testData, testData.OriginalWsqData);

        // Assert.
        _fixture.WriteComparisonReport(testData);
        _fixture.WriteFilesToCaseDirectory(testData);
        _fixture.AssertTestCaseDecoding(testData);
    }

    [Fact]
    public void Decode_b124_at_0_75_produces_binary_Pgm()
    {
        // Arrange.
        var wsqParam = _fixture.GetEncoding0_75Parameters(WsqFilterType.Odd7x9);
        var testData = _fixture.ArrangeTestCase("b124.pgm", wsqParam);
        _fixture.DecodeWsqDataUsingNistDwsq(testData, testData.OriginalWsqData);

        // Act.
        _fixture.DecodeWsqData(testData, testData.OriginalWsqData);

        // Assert.
        _fixture.WriteComparisonReport(testData);
        _fixture.WriteFilesToCaseDirectory(testData);
        _fixture.AssertTestCaseDecoding(testData);
    }

    [Fact]
    public void Decode_b157_at_0_75_produces_binary_Pgm()
    {
        // Arrange.
        var wsqParam = _fixture.GetEncoding0_75Parameters(WsqFilterType.Odd7x9);
        var testData = _fixture.ArrangeTestCase("b157.pgm", wsqParam);
        _fixture.DecodeWsqDataUsingNistDwsq(testData, testData.OriginalWsqData);

        // Act.
        _fixture.DecodeWsqData(testData, testData.OriginalWsqData);

        // Assert.
        _fixture.WriteComparisonReport(testData);
        _fixture.WriteFilesToCaseDirectory(testData);
        _fixture.AssertTestCaseDecoding(testData);
    }

    [Fact]
    public void Decode_b158_at_0_75_produces_binary_Pgm()
    {
        // Arrange.
        var wsqParam = _fixture.GetEncoding0_75Parameters(WsqFilterType.Odd7x9);
        var testData = _fixture.ArrangeTestCase("b158.pgm", wsqParam);
        _fixture.DecodeWsqDataUsingNistDwsq(testData, testData.OriginalWsqData);

        // Act.
        _fixture.DecodeWsqData(testData, testData.OriginalWsqData);

        // Assert.
        _fixture.WriteComparisonReport(testData);
        _fixture.WriteFilesToCaseDirectory(testData);
        _fixture.AssertTestCaseDecoding(testData);
    }

    [Fact]
    public void Decode_b186_at_0_75_produces_binary_Pgm()
    {
        // Arrange.
        var wsqParam = _fixture.GetEncoding0_75Parameters(WsqFilterType.Odd7x9);
        var testData = _fixture.ArrangeTestCase("b186.pgm", wsqParam);
        _fixture.DecodeWsqDataUsingNistDwsq(testData, testData.OriginalWsqData);

        // Act.
        _fixture.DecodeWsqData(testData, testData.OriginalWsqData);

        // Assert.
        _fixture.WriteComparisonReport(testData);
        _fixture.WriteFilesToCaseDirectory(testData);
        _fixture.AssertTestCaseDecoding(testData);
    }

    [Fact]
    public void Decode_cmp00001_at_0_75_produces_binary_Pgm()
    {
        // Arrange.
        var wsqParam = _fixture.GetEncoding0_75Parameters(WsqFilterType.Odd7x9);
        var testData = _fixture.ArrangeTestCase("cmp00001.pgm", wsqParam);
        _fixture.DecodeWsqDataUsingNistDwsq(testData, testData.OriginalWsqData);

        // Act.
        _fixture.DecodeWsqData(testData, testData.OriginalWsqData);

        // Assert.
        _fixture.WriteComparisonReport(testData);
        _fixture.WriteFilesToCaseDirectory(testData);
        _fixture.AssertTestCaseDecoding(testData);
    }

    [Fact]
    public void Decode_cmp00002_at_0_75_produces_binary_Pgm()
    {
        // Arrange.
        var wsqParam = _fixture.GetEncoding0_75Parameters(WsqFilterType.Odd7x9);
        var testData = _fixture.ArrangeTestCase("cmp00002.pgm", wsqParam);
        _fixture.DecodeWsqDataUsingNistDwsq(testData, testData.OriginalWsqData);

        // Act.
        _fixture.DecodeWsqData(testData, testData.OriginalWsqData);

        // Assert.
        _fixture.WriteComparisonReport(testData);
        _fixture.WriteFilesToCaseDirectory(testData);
        _fixture.AssertTestCaseDecoding(testData);
    }

    [Fact]
    public void Decode_cmp00003_at_0_75_produces_binary_Pgm()
    {
        // Arrange.
        var wsqParam = _fixture.GetEncoding0_75Parameters(WsqFilterType.Odd7x9);
        var testData = _fixture.ArrangeTestCase("cmp00003.pgm", wsqParam);
        _fixture.DecodeWsqDataUsingNistDwsq(testData, testData.OriginalWsqData);

        // Act.
        _fixture.DecodeWsqData(testData, testData.OriginalWsqData);

        // Assert.
        _fixture.WriteComparisonReport(testData);
        _fixture.WriteFilesToCaseDirectory(testData);
        _fixture.AssertTestCaseDecoding(testData);
    }

    [Fact]
    public void Decode_cmp00004_at_0_75_produces_binary_Pgm()
    {
        // Arrange.
        var wsqParam = _fixture.GetEncoding0_75Parameters(WsqFilterType.Odd7x9);
        var testData = _fixture.ArrangeTestCase("cmp00004.pgm", wsqParam);
        _fixture.DecodeWsqDataUsingNistDwsq(testData, testData.OriginalWsqData);

        // Act.
        _fixture.DecodeWsqData(testData, testData.OriginalWsqData);

        // Assert.
        _fixture.WriteComparisonReport(testData);
        _fixture.WriteFilesToCaseDirectory(testData);
        _fixture.AssertTestCaseDecoding(testData);
    }

    [Fact]
    public void Decode_cmp00005_at_0_75_produces_binary_Pgm()
    {
        // Arrange.
        var wsqParam = _fixture.GetEncoding0_75Parameters(WsqFilterType.Odd7x9);
        var testData = _fixture.ArrangeTestCase("cmp00005.pgm", wsqParam);
        _fixture.DecodeWsqDataUsingNistDwsq(testData, testData.OriginalWsqData);

        // Act.
        _fixture.DecodeWsqData(testData, testData.OriginalWsqData);

        // Assert.
        _fixture.WriteComparisonReport(testData);
        _fixture.WriteFilesToCaseDirectory(testData);
        _fixture.AssertTestCaseDecoding(testData);
    }

    [Fact]
    public void Decode_cmp00006_at_0_75_produces_binary_Pgm()
    {
        // Arrange.
        var wsqParam = _fixture.GetEncoding0_75Parameters(WsqFilterType.Odd7x9);
        var testData = _fixture.ArrangeTestCase("cmp00006.pgm", wsqParam);
        _fixture.DecodeWsqDataUsingNistDwsq(testData, testData.OriginalWsqData);

        // Act.
        _fixture.DecodeWsqData(testData, testData.OriginalWsqData);

        // Assert.
        _fixture.WriteComparisonReport(testData);
        _fixture.WriteFilesToCaseDirectory(testData);
        _fixture.AssertTestCaseDecoding(testData);
    }

    [Fact]
    public void Decode_cmp00007_at_0_75_produces_binary_Pgm()
    {
        // Arrange.
        var wsqParam = _fixture.GetEncoding0_75Parameters(WsqFilterType.Odd7x9);
        var testData = _fixture.ArrangeTestCase("cmp00007.pgm", wsqParam);
        _fixture.DecodeWsqDataUsingNistDwsq(testData, testData.OriginalWsqData);

        // Act.
        _fixture.DecodeWsqData(testData, testData.OriginalWsqData);

        // Assert.
        _fixture.WriteComparisonReport(testData);
        _fixture.WriteFilesToCaseDirectory(testData);
        _fixture.AssertTestCaseDecoding(testData);
    }

    [Fact]
    public void Decode_cmp00008_at_0_75_produces_binary_Pgm()
    {
        // Arrange.
        var wsqParam = _fixture.GetEncoding0_75Parameters(WsqFilterType.Odd7x9);
        var testData = _fixture.ArrangeTestCase("cmp00008.pgm", wsqParam);
        _fixture.DecodeWsqDataUsingNistDwsq(testData, testData.OriginalWsqData);

        // Act.
        _fixture.DecodeWsqData(testData, testData.OriginalWsqData);

        // Assert.
        _fixture.WriteComparisonReport(testData);
        _fixture.WriteFilesToCaseDirectory(testData);
        _fixture.AssertTestCaseDecoding(testData);
    }

    [Fact]
    public void Decode_cmp00009_at_0_75_produces_binary_Pgm()
    {
        // Arrange.
        var wsqParam = _fixture.GetEncoding0_75Parameters(WsqFilterType.Odd7x9);
        var testData = _fixture.ArrangeTestCase("cmp00009.pgm", wsqParam);
        _fixture.DecodeWsqDataUsingNistDwsq(testData, testData.OriginalWsqData);

        // Act.
        _fixture.DecodeWsqData(testData, testData.OriginalWsqData);

        // Assert.
        _fixture.WriteComparisonReport(testData);
        _fixture.WriteFilesToCaseDirectory(testData);
        _fixture.AssertTestCaseDecoding(testData);
    }

    [Fact]
    public void Decode_cmp00010_at_0_75_produces_binary_Pgm()
    {
        // Arrange.
        var wsqParam = _fixture.GetEncoding0_75Parameters(WsqFilterType.Odd7x9);
        var testData = _fixture.ArrangeTestCase("cmp00010.pgm", wsqParam);
        _fixture.DecodeWsqDataUsingNistDwsq(testData, testData.OriginalWsqData);

        // Act.
        _fixture.DecodeWsqData(testData, testData.OriginalWsqData);

        // Assert.
        _fixture.WriteComparisonReport(testData);
        _fixture.WriteFilesToCaseDirectory(testData);
        _fixture.AssertTestCaseDecoding(testData);
    }

    [Fact]
    public void Decode_cmp00011_at_0_75_produces_binary_Pgm()
    {
        // Arrange.
        var wsqParam = _fixture.GetEncoding0_75Parameters(WsqFilterType.Odd7x9);
        var testData = _fixture.ArrangeTestCase("cmp00011.pgm", wsqParam);
        _fixture.DecodeWsqDataUsingNistDwsq(testData, testData.OriginalWsqData);

        // Act.
        _fixture.DecodeWsqData(testData, testData.OriginalWsqData);

        // Assert.
        _fixture.WriteComparisonReport(testData);
        _fixture.WriteFilesToCaseDirectory(testData);
        _fixture.AssertTestCaseDecoding(testData);
    }

    [Fact]
    public void Decode_cmp00012_at_0_75_produces_binary_Pgm()
    {
        // Arrange.
        var wsqParam = _fixture.GetEncoding0_75Parameters(WsqFilterType.Odd7x9);
        var testData = _fixture.ArrangeTestCase("cmp00012.pgm", wsqParam);
        _fixture.DecodeWsqDataUsingNistDwsq(testData, testData.OriginalWsqData);

        // Act.
        _fixture.DecodeWsqData(testData, testData.OriginalWsqData);

        // Assert.
        _fixture.WriteComparisonReport(testData);
        _fixture.WriteFilesToCaseDirectory(testData);
        _fixture.AssertTestCaseDecoding(testData);
    }

    [Fact]
    public void Decode_cmp00013_at_0_75_produces_binary_Pgm()
    {
        // Arrange.
        var wsqParam = _fixture.GetEncoding0_75Parameters(WsqFilterType.Odd7x9);
        var testData = _fixture.ArrangeTestCase("cmp00013.pgm", wsqParam);
        _fixture.DecodeWsqDataUsingNistDwsq(testData, testData.OriginalWsqData);

        // Act.
        _fixture.DecodeWsqData(testData, testData.OriginalWsqData);

        // Assert.
        _fixture.WriteComparisonReport(testData);
        _fixture.WriteFilesToCaseDirectory(testData);
        _fixture.AssertTestCaseDecoding(testData);
    }

    [Fact]
    public void Decode_cmp00014_at_0_75_produces_binary_Pgm()
    {
        // Arrange.
        var wsqParam = _fixture.GetEncoding0_75Parameters(WsqFilterType.Odd7x9);
        var testData = _fixture.ArrangeTestCase("cmp00014.pgm", wsqParam);
        _fixture.DecodeWsqDataUsingNistDwsq(testData, testData.OriginalWsqData);

        // Act.
        _fixture.DecodeWsqData(testData, testData.OriginalWsqData);

        // Assert.
        _fixture.WriteComparisonReport(testData);
        _fixture.WriteFilesToCaseDirectory(testData);
        _fixture.AssertTestCaseDecoding(testData);
    }

    [Fact]
    public void Decode_cmp00015_at_0_75_produces_binary_Pgm()
    {
        // Arrange.
        var wsqParam = _fixture.GetEncoding0_75Parameters(WsqFilterType.Odd7x9);
        var testData = _fixture.ArrangeTestCase("cmp00015.pgm", wsqParam);
        _fixture.DecodeWsqDataUsingNistDwsq(testData, testData.OriginalWsqData);

        // Act.
        _fixture.DecodeWsqData(testData, testData.OriginalWsqData);

        // Assert.
        _fixture.WriteComparisonReport(testData);
        _fixture.WriteFilesToCaseDirectory(testData);
        _fixture.AssertTestCaseDecoding(testData);
    }

    [Fact]
    public void Decode_cmp00016_at_0_75_produces_binary_Pgm()
    {
        // Arrange.
        var wsqParam = _fixture.GetEncoding0_75Parameters(WsqFilterType.Odd7x9);
        var testData = _fixture.ArrangeTestCase("cmp00016.pgm", wsqParam);
        _fixture.DecodeWsqDataUsingNistDwsq(testData, testData.OriginalWsqData);

        // Act.
        _fixture.DecodeWsqData(testData, testData.OriginalWsqData);

        // Assert.
        _fixture.WriteComparisonReport(testData);
        _fixture.WriteFilesToCaseDirectory(testData);
        _fixture.AssertTestCaseDecoding(testData);
    }

    [Fact]
    public void Decode_cmp00017_at_0_75_produces_binary_Pgm()
    {
        // Arrange.
        var wsqParam = _fixture.GetEncoding0_75Parameters(WsqFilterType.Odd7x9);
        var testData = _fixture.ArrangeTestCase("cmp00017.pgm", wsqParam);
        _fixture.DecodeWsqDataUsingNistDwsq(testData, testData.OriginalWsqData);

        // Act.
        _fixture.DecodeWsqData(testData, testData.OriginalWsqData);

        // Assert.
        _fixture.WriteComparisonReport(testData);
        _fixture.WriteFilesToCaseDirectory(testData);
        _fixture.AssertTestCaseDecoding(testData);
    }

    [Fact]
    public void Decode_cmp00018_at_0_75_produces_binary_Pgm()
    {
        // Arrange.
        var wsqParam = _fixture.GetEncoding0_75Parameters(WsqFilterType.Odd7x9);
        var testData = _fixture.ArrangeTestCase("cmp00018.pgm", wsqParam);
        _fixture.DecodeWsqDataUsingNistDwsq(testData, testData.OriginalWsqData);

        // Act.
        _fixture.DecodeWsqData(testData, testData.OriginalWsqData);

        // Assert.
        _fixture.WriteComparisonReport(testData);
        _fixture.WriteFilesToCaseDirectory(testData);
        _fixture.AssertTestCaseDecoding(testData);
    }

    [Fact]
    public void Decode_cmp00019_at_0_75_produces_binary_Pgm()
    {
        // Arrange.
        var wsqParam = _fixture.GetEncoding0_75Parameters(WsqFilterType.Odd7x9);
        var testData = _fixture.ArrangeTestCase("cmp00019.pgm", wsqParam);
        _fixture.DecodeWsqDataUsingNistDwsq(testData, testData.OriginalWsqData);

        // Act.
        _fixture.DecodeWsqData(testData, testData.OriginalWsqData);

        // Assert.
        _fixture.WriteComparisonReport(testData);
        _fixture.WriteFilesToCaseDirectory(testData);
        _fixture.AssertTestCaseDecoding(testData);
    }

    [Fact]
    public void Decode_sample_01_at_0_75_produces_binary_Pgm()
    {
        // Arrange.
        var wsqParam = _fixture.GetEncoding0_75Parameters(WsqFilterType.Odd7x9);
        var testData = _fixture.ArrangeTestCase("sample_01.pgm", wsqParam);
        _fixture.DecodeWsqDataUsingNistDwsq(testData, testData.OriginalWsqData);

        // Act.
        _fixture.DecodeWsqData(testData, testData.OriginalWsqData);

        // Assert.
        _fixture.WriteComparisonReport(testData);
        _fixture.WriteFilesToCaseDirectory(testData);
        _fixture.AssertTestCaseDecoding(testData);
    }

    [Fact]
    public void Decode_sample_03_at_0_75_produces_binary_Pgm()
    {
        // Arrange.
        var wsqParam = _fixture.GetEncoding0_75Parameters(WsqFilterType.Odd7x9);
        var testData = _fixture.ArrangeTestCase("sample_03.pgm", wsqParam);
        _fixture.DecodeWsqDataUsingNistDwsq(testData, testData.OriginalWsqData);

        // Act.
        _fixture.DecodeWsqData(testData, testData.OriginalWsqData);

        // Assert.
        _fixture.WriteComparisonReport(testData);
        _fixture.WriteFilesToCaseDirectory(testData);
        _fixture.AssertTestCaseDecoding(testData);
    }

    [Fact]
    public void Decode_sample_04_at_0_75_produces_binary_Pgm()
    {
        // Arrange.
        var wsqParam = _fixture.GetEncoding0_75Parameters(WsqFilterType.Odd7x9);
        var testData = _fixture.ArrangeTestCase("sample_04.pgm", wsqParam);
        _fixture.DecodeWsqDataUsingNistDwsq(testData, testData.OriginalWsqData);

        // Act.
        _fixture.DecodeWsqData(testData, testData.OriginalWsqData);

        // Assert.
        _fixture.WriteComparisonReport(testData);
        _fixture.WriteFilesToCaseDirectory(testData);
        _fixture.AssertTestCaseDecoding(testData);
    }

    [Fact]
    public void Decode_sample_10_at_0_75_produces_binary_Pgm()
    {
        // Arrange.
        var wsqParam = _fixture.GetEncoding0_75Parameters(WsqFilterType.Odd7x9);
        var testData = _fixture.ArrangeTestCase("sample_10.pgm", wsqParam);
        _fixture.DecodeWsqDataUsingNistDwsq(testData, testData.OriginalWsqData);

        // Act.
        _fixture.DecodeWsqData(testData, testData.OriginalWsqData);

        // Assert.
        _fixture.WriteComparisonReport(testData);
        _fixture.WriteFilesToCaseDirectory(testData);
        _fixture.AssertTestCaseDecoding(testData);
    }

    [Fact]
    public void Decode_sample_11_at_0_75_produces_binary_Pgm()
    {
        // Arrange.
        var wsqParam = _fixture.GetEncoding0_75Parameters(WsqFilterType.Odd7x9);
        var testData = _fixture.ArrangeTestCase("sample_11.pgm", wsqParam);
        _fixture.DecodeWsqDataUsingNistDwsq(testData, testData.OriginalWsqData);

        // Act.
        _fixture.DecodeWsqData(testData, testData.OriginalWsqData);

        // Assert.
        _fixture.WriteComparisonReport(testData);
        _fixture.WriteFilesToCaseDirectory(testData);
        _fixture.AssertTestCaseDecoding(testData);
    }

    [Fact]
    public void Decode_sample_19_at_0_75_produces_binary_Pgm()
    {
        // Arrange.
        var wsqParam = _fixture.GetEncoding0_75Parameters(WsqFilterType.Odd7x9);
        var testData = _fixture.ArrangeTestCase("sample_19.pgm", wsqParam);
        _fixture.DecodeWsqDataUsingNistDwsq(testData, testData.OriginalWsqData);

        // Act.
        _fixture.DecodeWsqData(testData, testData.OriginalWsqData);

        // Assert.
        _fixture.WriteComparisonReport(testData);
        _fixture.WriteFilesToCaseDirectory(testData);
        _fixture.AssertTestCaseDecoding(testData);
    }

}
