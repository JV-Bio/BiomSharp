namespace BiomSharp.Imaging.Tests.Models;

internal sealed record EncodingTestComparison(decimal SizeDifferencePercent, BinComparison[] Bins,
    int CoefficientCount, int Mismatches, int AllowedMismatches, long MaximumIndexDifference,
    List<string> Failures)
{   
    /// <summary>
    /// Evaluates an encoded WSQ against the baseline reference's dimensions and values.
    /// </summary>
    /// <param name="baselineReference">The baseline supplying expected dimensions, values, and tolerance denominators.</param>
    /// <param name="encoded">The BiomSharp or NBIS encoding being evaluated.</param>
    public static EncodingTestComparison Compare(WsqInspection baselineReference, WsqInspection encoded)
    {
        var failures = new List<string>();
        void Check(bool passed, string message)
        {
            if (!passed)
            {
                failures.Add(message);
            }
        }

        Check(baselineReference.Header.Width > 0 && baselineReference.Header.Height > 0, "Reference dimensions must be positive.");
        Check(encoded.Header.Width == baselineReference.Header.Width && encoded.Header.Height == baselineReference.Header.Height, "Encoded dimensions differ from the baseline reference.");
        Check(encoded.Header.Encoder == WsqRequirementsEncoding.RequiredEncoderNumber, $"Frame header Ev must be {WsqRequirementsEncoding.RequiredEncoderNumber}.");
        Check(baselineReference.Header.Black == encoded.Header.Black && baselineReference.Header.White == encoded.Header.White, "Black/white calibration differs from the reference.");
        Check(encoded.Header.LowFilterLength == WsqRequirementsEncoding.RequiredLowFilterLength && encoded.Header.HighFilterLength == WsqRequirementsEncoding.RequiredHighFilterLength, $"Expected the prescribed {WsqRequirementsEncoding.RequiredLowFilterLength}/{WsqRequirementsEncoding.RequiredHighFilterLength} filter.");
        Check(baselineReference.Center == encoded.Center, "Quantizer bin center differs from the reference.");

        if (encoded.SizeWithoutComments <= 0 || baselineReference.SizeWithoutComments <= 0)
        {
            throw new InvalidDataException("Cannot compare empty WSQ sizes.");
        }

        decimal sizeDifference = Math.Abs(baselineReference.SizeWithoutComments - encoded.SizeWithoutComments);
        decimal sizePercent = sizeDifference * 100m / baselineReference.SizeWithoutComments;
        Check(sizeDifference * 100m <= baselineReference.SizeWithoutComments * WsqRequirementsEncoding.MaximumCompressedSizeDifferencePercent, $"Compressed size differs by {sizePercent}% (limit {WsqRequirementsEncoding.MaximumCompressedSizeDifferencePercent}%, excluding comments).");

        if (baselineReference.BinWidths.Length != WsqRequirementsEncoding.ComparedSubbandCount ||
            baselineReference.ZeroWidths.Length != WsqRequirementsEncoding.ComparedSubbandCount ||
            encoded.BinWidths.Length != WsqRequirementsEncoding.ComparedSubbandCount ||
            encoded.ZeroWidths.Length != WsqRequirementsEncoding.ComparedSubbandCount)
        {
            throw new InvalidDataException($"Expected quantization widths for all {WsqRequirementsEncoding.ComparedSubbandCount} subbands.");
        }

        void CompareBin(List<BinComparison> bins, int subband, string kind, decimal value, decimal expected)
        {
            decimal difference = Math.Abs(value - expected);
            bool passed = difference * 100m <= expected * WsqRequirementsEncoding.MaximumQuantizationBinWidthDifferencePercent;
            decimal? percent = expected == 0 ? null : difference * 100m / expected;
            bins.Add(new BinComparison(subband, kind, value, expected, percent, passed));
            Check(passed, $"Subband {subband} {kind}: actual {value}, reference {expected}; exceeds {WsqRequirementsEncoding.MaximumQuantizationBinWidthDifferencePercent}% tolerance.");
        }

        var bins = new List<BinComparison>();
        for (int i = 0; i < WsqRequirementsEncoding.ComparedSubbandCount; i++)
        {
            CompareBin(bins, i, "Q", encoded.BinWidths[i], baselineReference.BinWidths[i]);
            CompareBin(bins, i, "Z", encoded.ZeroWidths[i], baselineReference.ZeroWidths[i]);
        }

        bool matchingLayout = baselineReference.BinWidths.Zip(encoded.BinWidths).All(pair => (pair.First == 0) == (pair.Second == 0));
        bool matchingCount = baselineReference.Coefficients.Length == encoded.Coefficients.Length;
        
        Check(matchingLayout, "Active subbands differ; packed coefficient positions cannot be compared.");
        Check(matchingCount, "Quantized coefficient counts differ.");
        Check(baselineReference.Coefficients.Length > 0, "Reference has no quantized coefficients.");

        int mismatches = 0;
        long maxDifference = 0;
        int allowedMismatches = (int)(baselineReference.Coefficients.Length * (100m - WsqRequirementsEncoding.MinimumIdenticalCoefficientPercent) / 100m);
        if (matchingLayout && matchingCount)
        {
            for (int i = 0; i < baselineReference.Coefficients.Length; i++)
            {
                long difference = Math.Abs((long)baselineReference.Coefficients[i] - encoded.Coefficients[i]);
                if (difference != 0)
                {
                    mismatches++;
                    maxDifference = Math.Max(maxDifference, difference);
                }
            }

            Check(mismatches <= allowedMismatches, $"{mismatches} of {baselineReference.Coefficients.Length} coefficient indices differ; fewer than {WsqRequirementsEncoding.MinimumIdenticalCoefficientPercent}% are identical.");
            Check(maxDifference <= WsqRequirementsEncoding.MaximumCoefficientIndexDifference, $"Maximum coefficient index difference is {maxDifference} (limit {WsqRequirementsEncoding.MaximumCoefficientIndexDifference}).");
        }

        return new EncodingTestComparison(sizePercent, bins.ToArray(), baselineReference.Coefficients.Length, mismatches, allowedMismatches, maxDifference, failures);
    }
}
