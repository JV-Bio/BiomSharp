using BiomSharp.Imaging.Tests.Fixtures;
using BiomSharp.Imaging.Tests.TestCollections;
using Xunit.Abstractions;

namespace BiomSharp.Imaging.Tests.RatioTests;

[Collection(EncodingDecodingCollection.Name)]
public class RatioEncodingTests
{
    public RatioEncodingTests(FixtureFullProcess fixture, ITestOutputHelper testOutput)
    {
        _fixture = fixture;
        _fixture.TestOutput = testOutput;   
    }

    private readonly FixtureFullProcess _fixture;

    [Fact]
    public void Ratio5Equals2_25BitRate()
    {
        // Arrange
        float ratio = 5f;      // 5:1
        // Act
        var bitrate = _fixture.GetBitRate(ratio);

        // Assert
        Assert.Equal(2.25d, bitrate);
    }

    [Fact]
    public void Ratio10Equals1_125BitRate()
    {
        // Arrange
        float ratio = 10f;     // 10:1
        // Act
        var bitrate = _fixture.GetBitRate(ratio);

        // Assert
        Assert.Equal(1.125d, bitrate);
    }

    [Fact]
    public void Ratio15Equals0_75BitRate()
    {
        // Arrange
        float ratio = 15f;     // 15:1
        // Act
        var bitrate = _fixture.GetBitRate(ratio);

        // Assert
        Assert.Equal(0.75d, bitrate);
    }

    [Fact]
    public void Ratio30Equals0_375BitRate()
    {
        // Arrange
        float ratio = 30f;     // 30:1
        // Act
        var bitrate = _fixture.GetBitRate(ratio);

        // Assert
        Assert.Equal(0.375d, bitrate);
    }

    [Fact]
    public void BitRate_0_375_Equals_Ratio30()
    {
        // Arrange
        float bitRate = 0.375f;

        // Act
        float ratio = _fixture.GetRatio(bitRate);

        // Assert - 30:1
        Assert.Equal(30f, ratio);
    }

    [Fact]
    public void BitRate_0_75_Equals_Ratio15()
    {
        // Arrange
        float bitRate = 0.75f;

        // Act
        float ratio = _fixture.GetRatio(bitRate);

        // Assert - 15:1
        Assert.Equal(15f, ratio);
    }

    [Fact]
    public void BitRate_2_25_Equals_Ratio5()
    {
        // Arrange
        float bitRate = 2.25f;

        // Act
        float ratio = _fixture.GetRatio(bitRate);

        // Assert - 5:1
        Assert.Equal(5f, ratio);
    }
}
