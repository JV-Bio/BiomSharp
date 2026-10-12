namespace BiomSharp.Imaging.Tests.Models;

/// <summary>
/// Holds decoding comparison measurements and failures from the first unsuccessful stage.
/// </summary>
/// <param name="IdenticalPixelPercent">The precent of pixels that are identical. Value of -1 means not calculated.</param>
/// <param name="HighestPixelDifference">The highest pixel difference found. Value of -1 means not calculated.</param>
/// <param name="Failures">The list of failure when comparing.</param>
internal sealed record DecodingTestComparison(decimal IdenticalPixelPercent, int HighestPixelDifference, List<string> Failures)
{
    /// <summary>
    /// Checks image availability, compatibility, and buffers before measuring pixel differences
    /// and optionally evaluating decoder acceptance. Stops after the first stage with failures.
    /// </summary>
    /// <param name="baselineReference">The baseline supplying expected dimensions, bit depth and pixel values.</param>
    /// <param name="decoded">The decoded WSQ pgm image being evaluated.</param>
    /// <param name="mode">
    /// MeasurementsOnly permits comparisons with the original uncompressed PGM.
    /// DecoderAcceptance requires reference and decoded reconstructions of the same WSQ bytes.
    /// </param>
    /// <remarks>
    /// The same container compares equal only after all structural stages pass.
    /// A pixel is identical when all its stored bytes match. For multi-byte pixels,
    /// the highest difference is the maximum absolute byte-component difference.
    /// Decoder limits describe grayscale reconstruction agreement, not color-image certification.
    /// </remarks>
    public static DecodingTestComparison Compare(DataContainerPgm? baselineReference, DataContainerPgm? decoded,
        DecodingComparisonMode mode = DecodingComparisonMode.DecoderAcceptance)
    {
        if (mode != DecodingComparisonMode.MeasurementsOnly && mode != DecodingComparisonMode.DecoderAcceptance)
        {
            throw new ArgumentOutOfRangeException(nameof(mode), mode, "Unknown decoding comparison mode.");
        }

        var failures = new List<string>();
        SimpleBitmap? baselineBitmap = baselineReference?.GetSimpleBitmap();
        SimpleBitmap? decodedBitmap = decoded?.GetSimpleBitmap();
        Check(failures, baselineBitmap != null, "Baseline reference PGM must contain an image.");
        Check(failures, decodedBitmap != null, "Decoded PGM must contain an image.");
        if (baselineBitmap == null || decodedBitmap == null)
        {
            return new DecodingTestComparison(-1, -1, failures);
        }

        CheckCompatibility(failures, baselineBitmap, decodedBitmap);
        if (failures.Count > 0)
        {
            return new DecodingTestComparison(-1, -1, failures);
        }

        CheckBuffers(failures, baselineBitmap, decodedBitmap);
        if (failures.Count > 0)
        {
            return new DecodingTestComparison(-1, -1, failures);
        }

        // Check if the parameters are the same. 
        if (ReferenceEquals(baselineReference, decoded))
        {
            // No need for checking the pixel values.
            return new DecodingTestComparison(IdenticalPixelPercent: 100, 0, failures);
        }

        var measurements = EvaluatePixels(baselineBitmap, decodedBitmap);
        if (mode == DecodingComparisonMode.DecoderAcceptance)
        {
            CheckDecoderAcceptance(failures, measurements.PixelCount, measurements.IdenticalPixels,
                measurements.HighestDifference);
        }

        return new DecodingTestComparison(100m * measurements.IdenticalPixels / measurements.PixelCount,
            measurements.HighestDifference, failures);
    }

    private static (long PixelCount, long IdenticalPixels, int HighestDifference) EvaluatePixels(
        SimpleBitmap baselineBitmap, SimpleBitmap decodedBitmap)
    {
        long pixelCount = (long)baselineBitmap.Width * baselineBitmap.Height;
        int bytesPerPixel = baselineBitmap.BitDepth / 8;
        long identicalPixels = 0;
        int highestDifference = 0;
        for (int offset = 0; offset < baselineBitmap.Pixels.Length; offset += bytesPerPixel)
        {
            bool identical = true;
            for (int component = 0; component < bytesPerPixel; component++)
            {
                int difference = Math.Abs(baselineBitmap.Pixels[offset + component] - decodedBitmap.Pixels[offset + component]);
                identical &= difference == 0;
                highestDifference = Math.Max(highestDifference, difference);
            }

            if (identical)
            {
                identicalPixels++;
            }
        }
        return (pixelCount, identicalPixels, highestDifference);
    }

    private static void CheckDecoderAcceptance(List<string> failures, long pixelCount, long identicalPixels,
        int highestDifference)
    {
        decimal identicalPixelsPercent = 100m * identicalPixels / pixelCount;
        Check(failures, 100m * identicalPixels >= WsqRequirementsDecoding.MinimumIdenticalPixelPercent * pixelCount,
            $"{identicalPixelsPercent}% of pixels are identical; " +
            $"at least {WsqRequirementsDecoding.MinimumIdenticalPixelPercent}% must be identical.");
        Check(failures, highestDifference <= WsqRequirementsDecoding.MaximumPixelDifference,
            $"Maximum absolute pixel difference is {highestDifference}; " +
            $"the limit is {WsqRequirementsDecoding.MaximumPixelDifference}.");
    }

    private static void CheckCompatibility(List<string> failures, SimpleBitmap baselineBitmap, SimpleBitmap decodedBitmap)
    {
        Check(failures, baselineBitmap.BitDepth == decodedBitmap.BitDepth,
            $"Decoded bit depth is {decodedBitmap.BitDepth}; expected {baselineBitmap.BitDepth} from the baseline reference.");
        Check(failures, baselineBitmap.Width > 0 && baselineBitmap.Height > 0,
            "Baseline reference dimensions must be positive.");
        Check(failures, decodedBitmap.Width > 0 && decodedBitmap.Height > 0,
            "Decoded dimensions must be positive.");
        Check(failures, baselineBitmap.Width == decodedBitmap.Width &&
            baselineBitmap.Height == decodedBitmap.Height 
            , $"Decoded dimensions are [{decodedBitmap.Width} X {decodedBitmap.Height}]; " +
            $"expected [{baselineBitmap.Width} X {baselineBitmap.Height}].");
    }

    private static void CheckBuffers(List<string> failures, SimpleBitmap baselineBitmap, SimpleBitmap decodedBitmap)
    {
        CheckBuffer(failures, baselineBitmap, "Baseline reference");
        CheckBuffer(failures, decodedBitmap, "Decoded");
    }

    private static void CheckBuffer(List<string> failures, SimpleBitmap bitmap, string image)
    {
        if (bitmap.BitDepth <= 0 || bitmap.BitDepth % 8 != 0)
        {
            Check(failures, false, $"{image} buffer layout requires a positive, whole-byte bit depth.");
            return;
        }

        if (bitmap.Pixels == null)
        {
            Check(failures, false, $"{image} pixel buffer must not be null.");
            return;
        }

        decimal expectedBytes = (decimal)bitmap.Width * bitmap.Height * bitmap.BitDepth / 8;
        Check(failures, bitmap.Pixels.LongLength == expectedBytes,
            $"{image} pixel buffer contains {bitmap.Pixels.LongLength} bytes; " +
            $"expected {expectedBytes} for dimensions {bitmap.Width}x{bitmap.Height} at {bitmap.BitDepth} bits per pixel.");
    }

    private static void Check(List<string> failures, bool passed, string message)
    {
        if (!passed)
        {
            failures.Add(message);
        }
    }

}
