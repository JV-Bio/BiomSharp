using BiomSharp.Imaging.Tests.Fixtures;
using BiomSharp.Imaging.Tests.Models;
using BiomSharp.Imaging.Tests.TestCollections;
using BiomSharp.Imaging.Wsq;
using Microsoft.VisualStudio.TestPlatform.ObjectModel;
using Xunit.Abstractions;

namespace BiomSharp.Imaging.Tests.EncodingTests;

[Trait("Category", "Encoding And Decoding")]
[Collection(EncodingDecodingCollection.Name)]
public class WsqEncodingDecodingUsingRatio
{
    public WsqEncodingDecodingUsingRatio(FixtureFullProcess fixture, ITestOutputHelper output)
    {
        _fixture = fixture;
        _fixture.TestOutput = output;
    }

    private readonly FixtureFullProcess _fixture;


    [Theory]
    [InlineData("cmp00001.pgm", 60f)]
    [InlineData("cmp00001.pgm", 30f)]
    [InlineData("cmp00001.pgm", 15f)]
    [InlineData("cmp00001.pgm", 5f)]
    [InlineData("cmp00002.pgm", 60f)]
    [InlineData("cmp00002.pgm", 30f)]
    [InlineData("cmp00002.pgm", 15f)]
    [InlineData("cmp00002.pgm", 5f)]
    [InlineData("cmp00003.pgm", 60f)]
    [InlineData("cmp00003.pgm", 30f)]
    [InlineData("cmp00003.pgm", 15f)]
    [InlineData("cmp00003.pgm", 5f)]
    [InlineData("cmp00004.pgm", 60f)]
    [InlineData("cmp00004.pgm", 30f)]
    [InlineData("cmp00004.pgm", 15f)]
    [InlineData("cmp00004.pgm", 5f)]
    [InlineData("cmp00005.pgm", 60f)]
    [InlineData("cmp00005.pgm", 30f)]
    [InlineData("cmp00005.pgm", 15f)]
    [InlineData("cmp00005.pgm", 5f)]
    [InlineData("cmp00006.pgm", 60f)]
    [InlineData("cmp00006.pgm", 30f)]
    [InlineData("cmp00006.pgm", 15f)]
    [InlineData("cmp00006.pgm", 5f)]
    [InlineData("cmp00007.pgm", 60f)]
    [InlineData("cmp00007.pgm", 30f)]
    [InlineData("cmp00007.pgm", 15f)]
    [InlineData("cmp00007.pgm", 5f)]
    [InlineData("cmp00008.pgm", 60f)]
    [InlineData("cmp00008.pgm", 30f)]
    [InlineData("cmp00008.pgm", 15f)]
    [InlineData("cmp00008.pgm", 5f)]
    [InlineData("cmp00009.pgm", 60f)]
    [InlineData("cmp00009.pgm", 30f)]
    [InlineData("cmp00009.pgm", 15f)]
    [InlineData("cmp00009.pgm", 5f)]
    [InlineData("cmp00010.pgm", 60f)]
    [InlineData("cmp00010.pgm", 30f)]
    [InlineData("cmp00010.pgm", 15f)]
    [InlineData("cmp00010.pgm", 5f)]
    public void Encode_PgmFile_at_Ratio_matches(string filename, float ratio)
    {
        // Arrange
        var bitRate = _fixture.GetBitRate(ratio);

        WsqParameters wsqParameters = _fixture.GetEncodingParameters(ratio, WsqFilterType.Odd7x9);
        var testCase = _fixture.ArrangeEncoding(filename, wsqParameters);        
        _fixture.CalculateNfiq(testCase.OriginalPgm);       // Calcualte the nfiq2 values for the original image.

        // Act
        _fixture.EncodePgmData(testCase, testCase.OriginalPgm);
        if (testCase.EncodedResults.TryGetValue(ExecutableType.BiomSharp, out DataContainerWsq? encoded) && encoded != null)
        {
            // Act - Decoding using BiomSharp
            _fixture.DecodeWsqData(testCase, encoded);
            // Act - Decoding using NIST NBIS dwsq tool.
            _fixture.DecodeWsqDataUsingNistDwsq(testCase, encoded);
        }

        // Calculate the nfiq2 values for each of the decoded images
        foreach (var item in testCase.DecodedResults)
        {
            _fixture.CalculateNfiq(item.Value);
        }

        // Assert
        Assert.Equal(bitRate, wsqParameters.BitRate);
        Assert.Equal(bitRate, testCase.Parameters.BitRate);

        _fixture.WriteComparisonReport(testCase);
        _fixture.WriteFilesToCaseDirectory(testCase);
        _fixture.WriteBmpFilesToCaseDirectory(testCase);
        _fixture.AssertTestCaseEncoding(testCase);
        _fixture.AssertTestCaseDecoding(testCase);
    }
}
