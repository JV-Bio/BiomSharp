using BiomSharp.Imaging.Tests.Models;

namespace BiomSharp.Imaging.Tests.TestInfrastructureTests;

public class DecodingComparisonTests
{
    [Theory]
    [InlineData(true, false, 1)]
    [InlineData(false, true, 1)]
    [InlineData(true, true, 2)]
    public void Missing_images_report_failures_without_calculating_measurements(
        bool missingBaseline, bool missingDecoded, int failureCount)
    {
        // Arrange
        var baseline = missingBaseline ? null : CreatePgm(3, 2);
        var decoded = missingDecoded ? null : CreatePgm(3, 2);

        // Act
        var result = DecodingTestComparison.Compare(baseline, decoded, DecodingComparisonMode.MeasurementsOnly);

        // Assert
        Assert.Equal(failureCount, result.Failures.Count);
        Assert.All(result.Failures, failure => Assert.Contains("must contain an image", failure));
        AssertMeasurementsUnavailable(result);
    }

    [Fact]
    public void Same_valid_container_has_identical_pixels()
    {
        // Arrange
        var pgm = CreatePgm(3, 2);

        // Act
        var result = DecodingTestComparison.Compare(pgm, pgm, DecodingComparisonMode.DecoderAcceptance);

        // Assert
        Assert.Empty(result.Failures);
        Assert.Equal(100m, result.IdenticalPixelPercent);
        Assert.Equal(0, result.HighestPixelDifference);
    }

    [Theory]
    [InlineData(4, 2)]
    [InlineData(3, 3)]
    [InlineData(2, 3)]
    public void Different_dimensions_report_failure_even_when_pixel_counts_match(int width, int height)
    {
        // Arrange
        var baseline = CreatePgm(3, 2);
        var decoded = CreatePgm(width, height);

        // Act
        var result = DecodingTestComparison.Compare(baseline, decoded,
            DecodingComparisonMode.DecoderAcceptance);

        // Assert
        Assert.Contains("Decoded dimensions", Assert.Single(result.Failures));
        AssertMeasurementsUnavailable(result);
    }

    [Fact]
    public void Same_container_is_validated_before_reporting_identical_pixels()
    {
        // Arrange
        var pgm = new DataContainerPgm("incomplete.pgm", System.Text.Encoding.ASCII.GetBytes("P5\n"));

        // Act
        var result = DecodingTestComparison.Compare(pgm, pgm, DecodingComparisonMode.DecoderAcceptance);

        // Assert
        Assert.Equal(2, result.Failures.Count);
        Assert.All(result.Failures, failure => Assert.Contains("dimensions must be positive", failure));
        AssertMeasurementsUnavailable(result);
    }

    [Fact]
    public void Measurements_only_does_not_apply_decoder_acceptance_limits()
    {
        // Arrange
        var baseline = CreatePgm(3, 2);
        var decoded = CreatePgm(3, 2, 255);

        // Act
        var result = DecodingTestComparison.Compare(baseline, decoded,
            DecodingComparisonMode.MeasurementsOnly);

        // Assert
        Assert.Empty(result.Failures);
        Assert.Equal(0m, result.IdenticalPixelPercent);
        Assert.Equal(255, result.HighestPixelDifference);
    }

    [Fact]
    public void Measurements_count_exact_matches_and_use_absolute_pixel_differences()
    {
        // Arrange
        var baseline = new DataContainerPgm("Test", "baseline.pgm",
            SimpleBitmap.Gray8(new byte[] { 0, 32, 128, 255 }, 2, 2));
        var decoded = new DataContainerPgm("Test", "decoded.pgm",
            SimpleBitmap.Gray8(new byte[] { 1, 32, 126, 255 }, 2, 2));

        // Act
        var result = DecodingTestComparison.Compare(baseline, decoded, DecodingComparisonMode.MeasurementsOnly);
        var reversed = DecodingTestComparison.Compare(decoded, baseline, DecodingComparisonMode.MeasurementsOnly);

        // Assert
        Assert.Empty(result.Failures);
        Assert.Equal(50m, result.IdenticalPixelPercent);
        Assert.Equal(2, result.HighestPixelDifference);
        Assert.Empty(reversed.Failures);
        Assert.Equal(result.IdenticalPixelPercent, reversed.IdenticalPixelPercent);
        Assert.Equal(result.HighestPixelDifference, reversed.HighestPixelDifference);
    }

    [Theory]
    [InlineData((int)DecodingComparisonMode.MeasurementsOnly)]
    [InlineData((int)DecodingComparisonMode.DecoderAcceptance)]
    public void Distinct_containers_with_identical_pixels_compare_equal(int mode)
    {
        // Arrange
        var baseline = CreatePgm(3, 2);
        var decoded = CreatePgm(3, 2);

        // Act
        var result = DecodingTestComparison.Compare(baseline, decoded, (DecodingComparisonMode)mode);

        // Assert
        Assert.Empty(result.Failures);
        Assert.Equal(100m, result.IdenticalPixelPercent);
        Assert.Equal(0, result.HighestPixelDifference);
    }

    [Theory]
    [InlineData(10000, 9, true)]
    [InlineData(10000, 10, true)]
    [InlineData(10000, 11, false)]
    [InlineData(1000, 1, true)]
    [InlineData(2000, 2, true)]
    [InlineData(999, 1, false)]
    [InlineData(1999, 2, false)]
    [InlineData(9999, 10, false)]
    public void Identical_pixel_limit_is_inclusive_and_not_rounded(
        int pixelCount, int mismatches, bool passes)
    {
        // Arrange
        var (baseline, decoded) = CreateChangedPixels(pixelCount, mismatches, 1);

        // Act
        var result = DecodingTestComparison.Compare(baseline, decoded, DecodingComparisonMode.DecoderAcceptance);

        // Assert
        Assert.Equal(100m * (pixelCount - mismatches) / pixelCount, result.IdenticalPixelPercent);
        Assert.Equal(1, result.HighestPixelDifference);
        if (passes)
            Assert.Empty(result.Failures);
        else
            Assert.Contains("must be identical", Assert.Single(result.Failures));
    }

    [Theory]
    [InlineData(1, true)]
    [InlineData(-1, true)]
    [InlineData(2, false)]
    [InlineData(-2, false)]
    public void Absolute_pixel_difference_limit_is_inclusive(int difference, bool passes)
    {
        // Arrange
        var (baseline, decoded) = CreateChangedPixels(1000, 1, difference);

        // Act
        var result = DecodingTestComparison.Compare(baseline, decoded, DecodingComparisonMode.DecoderAcceptance);

        // Assert
        Assert.Equal(99.9m, result.IdenticalPixelPercent);
        Assert.Equal(Math.Abs(difference), result.HighestPixelDifference);
        if (passes)
            Assert.Empty(result.Failures);
        else
            Assert.Contains("Maximum absolute pixel difference", Assert.Single(result.Failures));
    }

    [Fact]
    public void Both_acceptance_failures_are_reported_when_both_limits_are_exceeded()
    {
        // Arrange
        var (baseline, decoded) = CreateChangedPixels(1000, 2, 2);

        // Act
        var result = DecodingTestComparison.Compare(baseline, decoded, DecodingComparisonMode.DecoderAcceptance);

        // Assert
        Assert.Equal(99.8m, result.IdenticalPixelPercent);
        Assert.Equal(2, result.HighestPixelDifference);
        Assert.Equal(2, result.Failures.Count);
        Assert.Contains(result.Failures, failure => failure.Contains("must be identical"));
        Assert.Contains(result.Failures, failure => failure.Contains("Maximum absolute pixel difference"));
    }

    [Fact]
    public void Default_mode_applies_decoder_acceptance_limits()
    {
        // Arrange
        var (baseline, decoded) = CreateChangedPixels(1000, 2, 2);

        // Act
        var result = DecodingTestComparison.Compare(baseline, decoded);
        var explicitResult = DecodingTestComparison.Compare(
            baseline, decoded, DecodingComparisonMode.DecoderAcceptance);

        // Assert
        Assert.Equal(2, result.Failures.Count);
        Assert.Equal(explicitResult.Failures, result.Failures);
        Assert.Equal(explicitResult.IdenticalPixelPercent, result.IdenticalPixelPercent);
        Assert.Equal(explicitResult.HighestPixelDifference, result.HighestPixelDifference);
    }

    [Fact]
    public void Invalid_comparison_mode_is_rejected()
    {
        // Arrange
        var pgm = CreatePgm(3, 2);

        // Act
        var exception = Record.Exception(() =>
            DecodingTestComparison.Compare(pgm, pgm, (DecodingComparisonMode)(-1)));

        // Assert
        Assert.IsType<ArgumentOutOfRangeException>(exception);
    }

    private static void AssertMeasurementsUnavailable(DecodingTestComparison result)
    {
        Assert.Equal(-1m, result.IdenticalPixelPercent);
        Assert.Equal(-1, result.HighestPixelDifference);
    }

    private static (DataContainerPgm Baseline, DataContainerPgm Decoded) CreateChangedPixels(
        int pixelCount, int mismatches, int difference)
    {
        var baseline = CreatePgm(pixelCount, 1, 128);
        byte[] pixels = Enumerable.Repeat((byte)128, pixelCount).ToArray();
        for (int i = 0; i < mismatches; i++)
            pixels[i] = checked((byte)(128 + difference));

        var decoded = new DataContainerPgm("Test", "decoded.pgm", SimpleBitmap.Gray8(pixels, pixelCount, 1));
        return (baseline, decoded);
    }

    private static DataContainerPgm CreatePgm(int width, int height, byte value = 0) =>
        new DataContainerPgm("Test", "image.pgm",
            SimpleBitmap.Gray8(Enumerable.Repeat(value, width * height).ToArray(), width, height));
}
