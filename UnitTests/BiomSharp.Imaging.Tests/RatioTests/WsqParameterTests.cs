using BiomSharp.Imaging.Wsq;

namespace BiomSharp.Imaging.Tests.RatioTests;

public class WsqParameterTests
{
    [Fact]
    public void BitRate_DefaultIs0_75()
    {
        var parameters = new WsqParameters();

        Assert.Equal(0.75f, parameters.BitRate);
    }

    [Theory]
    [InlineData(float.NegativeInfinity)]
    [InlineData(float.MinValue)]
    [InlineData(-1f)]
    [InlineData(0f)]
    [InlineData(0.1f)]
    public void BitRate_BelowMinimum_IsClampedToMinimum(float bitRate)
    {
        var parameters = new WsqParameters { BitRate = bitRate };

        Assert.Equal(0.1875f, parameters.BitRate);
        Assert.Equal(WsqParameters.BitRateMinValue, parameters.BitRate);
    }

    [Theory]
    [InlineData(2.2501f)]
    [InlineData(3f)]
    [InlineData(float.MaxValue)]
    [InlineData(float.PositiveInfinity)]
    public void BitRate_AboveMaximum_IsClampedToMaximum(float bitRate)
    {
        var parameters = new WsqParameters { BitRate = bitRate };

        Assert.Equal(2.25f, parameters.BitRate);
        Assert.Equal(WsqParameters.BitRateMaxValue, parameters.BitRate);
    }

    [Theory]
    [InlineData(0.1875f)]
    [InlineData(0.225f)]
    [InlineData(0.375f)]
    [InlineData(0.75f)]
    [InlineData(1.125f)]
    [InlineData(2.25f)]
    public void BitRate_WithinInclusiveLimits_IsUnchanged(float bitRate)
    {
        var parameters = new WsqParameters { BitRate = bitRate };

        Assert.Equal(bitRate, parameters.BitRate);
    }

    [Fact]
    public void BitRate_ImmediatelyOutsideLimits_IsClamped()
    {
        var parameters = new WsqParameters
        {
            BitRate = MathF.BitDecrement(WsqParameters.BitRateMinValue)
        };

        Assert.Equal(WsqParameters.BitRateMinValue, parameters.BitRate);

        parameters.BitRate = MathF.BitIncrement(WsqParameters.BitRateMaxValue);

        Assert.Equal(WsqParameters.BitRateMaxValue, parameters.BitRate);
    }

    [Fact]
    public void BitRate_ImmediatelyInsideLimits_IsUnchanged()
    {
        float aboveMinimum = MathF.BitIncrement(WsqParameters.BitRateMinValue);
        float belowMaximum = MathF.BitDecrement(WsqParameters.BitRateMaxValue);
        var parameters = new WsqParameters { BitRate = aboveMinimum };

        Assert.Equal(aboveMinimum, parameters.BitRate);

        parameters.BitRate = belowMaximum;

        Assert.Equal(belowMaximum, parameters.BitRate);
    }
}
