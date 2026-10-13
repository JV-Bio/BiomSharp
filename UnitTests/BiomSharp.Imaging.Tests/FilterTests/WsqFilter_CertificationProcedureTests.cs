using BiomSharp.Imaging.Tests.Fixtures;
using BiomSharp.Imaging.Tests.TestCollections;
using BiomSharp.Imaging.Wsq;
using Xunit.Abstractions;

namespace BiomSharp.Imaging.Tests.FilterTests;

/// <summary>
/// Generates the six binary P5 reconstructions required by the NIST WSQ certification
/// procedure for reference WSQs in decode/not_7_9, compressed at bit allocation target 0.75.
/// </summary>
/// <remarks>
/// These cases supplement the 80 standard decoder cases with 6/10, 10/10, and 9/11 filter taps.
/// Both BiomSharp and native NIST dwsq decode the same checksum-verified reference bytes.
/// Filters come from the WSQ transform table, not the encoding filter in the test parameters.
/// Reports and images are written before asserting decoder agreement; original-PGM comparisons
/// are structural and diagnostic, not lossless-compression requirements.
/// BiomSharp PGMs are submission outputs; native reconstructions are local comparison artifacts.
/// Passing these tests does not confer NIST/FBI certification.
/// See <see href="https://www.nist.gov/programs-projects/wsq-certification-procedure"/>.
/// </remarks>
[Trait("Category", "Decoding")]
[Collection(EncodingDecodingCollection.Name)]
public class WsqFilter_CertificationProcedureTests
{
    public WsqFilter_CertificationProcedureTests(FixtureFullProcess fixture, ITestOutputHelper output)
    {
        _fixture = fixture;
        _fixture.TestOutput = output;
    }

    private readonly FixtureFullProcess _fixture;

    [Fact]
    public void Decode_cmp00009_with_nonstandard_filter_produces_binary_Pgm()
    {
        // Arrange
        var parameters = _fixture.GetEncoding0_75Parameters(WsqFilterType.Odd7x9);        
        var test = _fixture.ArrangeTestCase("cmp00009.pgm", 
            parameters,
            referenceWsqRelativePath: Path.Combine("decode", "not_7_9", "cmp00009.wsq"));
        _fixture.DecodeWsqDataUsingNistDwsq(test, test.OriginalWsqData);

        // Act
        _fixture.DecodeWsqData(test, test.OriginalWsqData);

        // Assert
        _fixture.WriteComparisonReport(test);
        _fixture.WriteFilesToCaseDirectory(test);
        _fixture.AssertTestCaseDecoding(test);
    }

    [Fact]
    public void Decode_cmp00010_with_nonstandard_filter_produces_binary_Pgm()
    {
        // Arrange
        var parameters = _fixture.GetEncoding0_75Parameters(WsqFilterType.Odd7x9);
        var test = _fixture.ArrangeTestCase("cmp00010.pgm", parameters,
            referenceWsqRelativePath: Path.Combine("decode", "not_7_9", "cmp00010.wsq"));
        _fixture.DecodeWsqDataUsingNistDwsq(test, test.OriginalWsqData);

        // Act
        _fixture.DecodeWsqData(test, test.OriginalWsqData);

        // Assert
        _fixture.WriteComparisonReport(test);
        _fixture.WriteFilesToCaseDirectory(test);
        _fixture.AssertTestCaseDecoding(test);
    }

    [Fact]
    public void Decode_cmp00013_with_nonstandard_filter_produces_binary_Pgm()
    {
        // Arrange
        var parameters = _fixture.GetEncoding0_75Parameters(WsqFilterType.Odd7x9);
        var test = _fixture.ArrangeTestCase("cmp00013.pgm", parameters,
            referenceWsqRelativePath: Path.Combine("decode", "not_7_9", "cmp00013.wsq"));
        _fixture.DecodeWsqDataUsingNistDwsq(test, test.OriginalWsqData);

        // Act
        _fixture.DecodeWsqData(test, test.OriginalWsqData);

        // Assert
        _fixture.WriteComparisonReport(test);
        _fixture.WriteFilesToCaseDirectory(test);
        _fixture.AssertTestCaseDecoding(test);
    }

    [Fact]
    public void Decode_cmp00014_with_nonstandard_filter_produces_binary_Pgm()
    {
        // Arrange
        var parameters = _fixture.GetEncoding0_75Parameters(WsqFilterType.Odd7x9);
        var test = _fixture.ArrangeTestCase("cmp00014.pgm", parameters,
            referenceWsqRelativePath: Path.Combine("decode", "not_7_9", "cmp00014.wsq"));
        _fixture.DecodeWsqDataUsingNistDwsq(test, test.OriginalWsqData);

        // Act
        _fixture.DecodeWsqData(test, test.OriginalWsqData);

        // Assert
        _fixture.WriteComparisonReport(test);
        _fixture.WriteFilesToCaseDirectory(test);
        _fixture.AssertTestCaseDecoding(test);
    }

    [Fact]
    public void Decode_cmp00015_with_nonstandard_filter_produces_binary_Pgm()
    {
        // Arrange
        var parameters = _fixture.GetEncoding0_75Parameters(WsqFilterType.Odd7x9);
        var test = _fixture.ArrangeTestCase("cmp00015.pgm", parameters,
            referenceWsqRelativePath: Path.Combine("decode", "not_7_9", "cmp00015.wsq"));
        _fixture.DecodeWsqDataUsingNistDwsq(test, test.OriginalWsqData);

        // Act
        _fixture.DecodeWsqData(test, test.OriginalWsqData);

        // Assert
        _fixture.WriteComparisonReport(test);
        _fixture.WriteFilesToCaseDirectory(test);
        _fixture.AssertTestCaseDecoding(test);
    }

    [Fact]
    public void Decode_cmp00016_with_nonstandard_filter_produces_binary_Pgm()
    {
        // Arrange
        var parameters = _fixture.GetEncoding0_75Parameters(WsqFilterType.Odd7x9);
        var test = _fixture.ArrangeTestCase("cmp00016.pgm", parameters,
            referenceWsqRelativePath: Path.Combine("decode", "not_7_9", "cmp00016.wsq"));
        _fixture.DecodeWsqDataUsingNistDwsq(test, test.OriginalWsqData);

        // Act
        _fixture.DecodeWsqData(test, test.OriginalWsqData);

        // Assert
        _fixture.WriteComparisonReport(test);
        _fixture.WriteFilesToCaseDirectory(test);
        _fixture.AssertTestCaseDecoding(test);
    }
}
