using BiomSharp.Imaging.Tests.Helpers;
using BiomSharp.Imaging.Tests.Models;
using BiomSharp.Imaging.Wsq;
using BiomSharp.Nist;
using BiomSharp.Nist.Nfiq;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace BiomSharp.Imaging.Tests.Fixtures;

internal sealed record PixelComparison(
        string Baseline,
        int Width,
        int Height,
        int? PixelCount,
        int? Mismatches,
        decimal? IdenticalPixelPercent,
        int? MaximumAbsoluteDifference,
        decimal? MeanAbsoluteDifference,
        decimal? MeanSquaredError)
{
    public string Status => Failures.Count == 0 ? "Available" : "Unavailable";
    public List<string> Failures { get; init; } = new();
    public string DecoderExecutable { get; init; } = "BiomSharp";

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? DecoderSha256 { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? ReferenceDecoderExecutable { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? ReferenceDecoderSha256 { get; init; }
}


internal static class PixelComparer
{
    internal static PixelComparison ComparePixels(DataContainerPgm? original, DataContainerPgm? decoded, string baseline)
    {
        var originalBitmap = original?.GetSimpleBitmap();
        var decodedBitmap = decoded?.GetSimpleBitmap();
        var failures = new List<string>();
        void Check(bool passed, string message)
        {
            if (!passed)
                failures.Add(message);
        }
        var unavailable = new PixelComparison(baseline, originalBitmap?.Width ?? 0, originalBitmap?.Height ?? 0,
            null, null, null, null, null, null)
        {
            Failures = failures,
            DecoderExecutable = decoded?.DecoderExecutable ?? "Unknown",
            DecoderSha256 = decoded?.DecoderSha256,
            ReferenceDecoderExecutable = original?.DecoderExecutable,
            ReferenceDecoderSha256 = original?.DecoderSha256
        };
        Check(originalBitmap != null, "Baseline image is unavailable.");
        Check(decodedBitmap != null, "Decoded image is unavailable.");
        if (decodedBitmap == null || originalBitmap == null)
        {
            return unavailable;
        }

        Check(originalBitmap.BitDepth == 8 && decodedBitmap.BitDepth == 8,
            "PGM pixel measurements require 8-bit grayscale images.");
        Check(originalBitmap.Width > 0 && originalBitmap.Height > 0, "Baseline dimensions must be positive.");
        Check(decodedBitmap.Width > 0 && decodedBitmap.Height > 0, "Decoded dimensions must be positive.");
        Check(originalBitmap.Width == decodedBitmap.Width && originalBitmap.Height == decodedBitmap.Height,
            $"Decoded dimensions {decodedBitmap.Width}x{decodedBitmap.Height} differ from baseline dimensions {originalBitmap.Width}x{originalBitmap.Height}.");
        if (failures.Count > 0)
            return unavailable;

        long expectedPixels = (long)originalBitmap.Width * originalBitmap.Height;
        Check(originalBitmap.Pixels != null && originalBitmap.Pixels.LongLength == expectedPixels,
            $"Baseline pixel buffer must contain {expectedPixels} bytes.");
        Check(decodedBitmap.Pixels != null && decodedBitmap.Pixels.LongLength == expectedPixels,
            $"Decoded pixel buffer must contain {expectedPixels} bytes.");
        if (failures.Count > 0)
            return unavailable;
        int pixelCount = checked((int)expectedPixels);

        int mismatches = 0;
        int maximumAbsoluteDifference = 0;
        long totalAbsoluteDifference = 0;
        long totalSquaredDifference = 0;
        for (int i = 0; i < pixelCount; i++)
        {
            int difference = System.Math.Abs(decodedBitmap.Pixels![i] - originalBitmap.Pixels![i]);
            if (difference != 0)
            {
                mismatches++;
            }
            maximumAbsoluteDifference = System.Math.Max(maximumAbsoluteDifference, difference);
            totalAbsoluteDifference += difference;
            totalSquaredDifference += difference * difference;
        }

        return new PixelComparison(baseline, originalBitmap.Width, originalBitmap.Height, pixelCount, mismatches,
            100m * (pixelCount - mismatches) / pixelCount, maximumAbsoluteDifference,
            (decimal)totalAbsoluteDifference / pixelCount, (decimal)totalSquaredDifference / pixelCount)
        {
            DecoderExecutable = decoded?.DecoderExecutable ?? "BiomSharp",
            DecoderSha256 = decoded?.DecoderSha256,
            ReferenceDecoderExecutable = original?.DecoderExecutable,
            ReferenceDecoderSha256 = original?.DecoderSha256
        };
    }
}

internal sealed record DecodedComparisonReport(
    long? SizeInBytes, PixelComparison Pixels, string Nfiq2Status, Nfiq2Data? Nfiq2,
    int? Nfiq2ScoreDifference);

public class FixtureFullProcess : FixtureTestBase<FixtureFullProcess>
{
    public TestCaseData ArrangeEncoding(string imageName, WsqParameters wsqParameters, [CallerMemberName] string testName = "")
    {
        TestCaseData baseData = base.ArrangeTestCase(imageName, wsqParameters, testName);
        return baseData;
    }


    /// <summary>
    /// Encodes the <paramref name="pgm"/> and stores result in <see cref="TestCaseData.EncodedResults" />.
    /// </summary>
    internal void EncodePgmData(TestCaseData test, DataContainerPgm pgm)
    {
        ArgumentNullException.ThrowIfNull(test);

        try
        {
            ArgumentNullException.ThrowIfNull(pgm);
            var executableType = ExecutableType.BiomSharp;

            // Reset any stored data.
            test.RemoveWsqContainer(executableType);

            // Create Simple
            var bitmap = pgm.GetSimpleBitmap();

            // Setup Codec
            var encodingCodec = new WsqCodec();
            encodingCodec.FromRaw(bitmap);

            // Use Wsq Codec to encode the 
            var encodedWsqData = encodingCodec.Encode(test.Parameters)
                ?? throw new InvalidDataException($"WSQ encoder returned no output for {pgm.FileName}.");

            // Store the encoded wsq data
            test.AddWsqContainer(executableType, new DataContainerWsq(pgm.FileName, encodedWsqData));
        }
        catch (Exception error)
        {
            File.WriteAllText(Path.Combine(test.CaseId.CaseDirectory, "failure.txt"), error.ToString());
            throw;
        }
    }

    internal void DecodeWsqDataUsingNistDwsq(TestCaseData test, DataContainerWsq? wsqData)
    {
        ArgumentNullException.ThrowIfNull(test);
        var executableType = ExecutableType.NativDswq;

        // The native tool requires a file, so stage the same bytes used by BiomSharp.
        string fileName = $"native-input-{Guid.NewGuid():N}.wsq";
        string inputPath = Path.Combine(test.CaseId.CaseDirectory, fileName);
        try
        {
            ArgumentNullException.ThrowIfNull(wsqData);

            // Write Original WSQ data from memory to inputPath.
            WriteVerifiedCopy(inputPath, wsqData.Data);

            // Decode Wsq data.
            SimpleBitmap pgmBitmap = TestResources.NativeWsqDecoder.DecodeWsqToBitmap(test.CaseId.CaseDirectory, fileName);

            // Store the decode Wsq data.
            test.AddPgmContainer(executableType,
                new DataContainerPgm(
                    decoderExecutable: TestResources.NativeWsqDecoder.FileName,
                    wsqData.FileName,
                    pgmBitmap)
                {
                    DecoderSha256 = TestResources.NativeWsqDecoder.Sha256Value
                });
        }
        catch (Exception error)
        {
            File.WriteAllText(Path.Combine(test.CaseId.CaseDirectory, "failure.txt"), error.ToString());
            throw;
        }
        finally
        {
            File.Delete(inputPath);
        }
    }

    internal void DecodeWsqData(TestCaseData test, DataContainerWsq? wsqData)
    {
        ArgumentNullException.ThrowIfNull(test);

        try
        {
            ArgumentNullException.ThrowIfNull(wsqData);
            var executableType = ExecutableType.BiomSharp;
            test.RemovePgmContainer(executableType);

            // Decode Wsq data.
            var codec = new WsqCodec();
            codec.Decode(wsqData.Data);

            var pgmBitmap = codec.ToRaw() ?? throw new InvalidDataException($"WSQ decoder returned no image for {wsqData.FileName}.");

            // Store the decode Wsq data.
            test.AddPgmContainer(executableType, new DataContainerPgm(
                decoderExecutable: executableType.ToString(),
                wsqData.FileName,
                pgmBitmap));
        }
        catch (Exception error)
        {
            File.WriteAllText(Path.Combine(test.CaseId.CaseDirectory, "failure.txt"), error.ToString());
            throw;
        }
    }

    private string GetCodecOperation(TestCaseData testCase)
    {
        return (testCase.EncodedResults.Count > 0, testCase.DecodedResults.Count > 0) switch
        {
            (true, true) => "EncodingAndDecoding",
            (true, false) => "Encoding",
            (false, true) => "Decoding",
            _ => "None"
        };
    }

    /// <summary>
    /// Records encoding and decoding measurements and decoder requirement outcomes without assertions.
    /// Missing baselines, structural comparison failures, and uncomputed NFIQ scores are explicit.
    /// PGM parsing and file I/O errors propagate to the caller.
    /// </summary>
    internal void WriteComparisonReport(TestCaseData testcase)
    {
        ArgumentNullException.ThrowIfNull(testcase);
        ArgumentNullException.ThrowIfNull(testcase.OriginalPgm);
        
        if (!float.IsFinite(testcase.Parameters.BitRate) || testcase.Parameters.BitRate <= 0)
        {
            throw new InvalidOperationException("A round-trip report requires a finite, positive bitrate.");
        }

        var original = testcase.OriginalPgm.GetSimpleBitmap()
            ?? throw new InvalidDataException("Original PGM contains no image.");

        var reference = testcase.OriginalWsqData;
        long originalFileSize = testcase.OriginalPgm.Data.LongLength;
        long rawPixelSize = original.Pixels.LongLength;

        // Create decoded WSQ Section. Shows the info about the generated images.
        var decodedComparisons = testcase.DecodedResults.ToDictionary(
            item => item.Key.ToString(),
            item => CreateDecodedComparison(testcase.OriginalPgm, item.Value, "Original uncompressed PGM"));


        // if there is a NativDswq, then evaluate all decoding results against this Native library generated result.
        var referencedComparisons = new Dictionary<string, DecoderPixelDifferences>();
        if (testcase.DecodedResults.TryGetValue(ExecutableType.NativDswq, out DataContainerPgm? refPgmData) 
            && refPgmData != null)
        {
            foreach (var item in testcase.DecodedResults)
            {
                if (item.Key == ExecutableType.NativDswq)
                    continue;

                var pixels = PixelComparer.ComparePixels(refPgmData, item.Value, "NIST native dwsq reconstruction");
                bool? meetsRequirements = pixels.Failures.Count == 0 &&
                    pixels.PixelCount is int pixelCount && pixels.Mismatches is int mismatches &&
                    pixels.MaximumAbsoluteDifference is int maximumDifference
                    ? 100m * (pixelCount - mismatches) >= WsqRequirementsDecoding.MinimumIdenticalPixelPercent * pixelCount &&
                        maximumDifference <= WsqRequirementsDecoding.MaximumPixelDifference
                    : null;
                referencedComparisons.Add(item.Key.ToString(), new DecoderPixelDifferences(
                    pixels.Status, pixels.Failures, pixels.Mismatches, pixels.IdenticalPixelPercent,
                    pixels.MaximumAbsoluteDifference, pixels.MeanAbsoluteDifference, pixels.MeanSquaredError,
                    meetsRequirements));
            }
        }

        // Comparison section using a baseline decoder result (not the original pgm) to compare other decoded results against.
        var decoderComparisons = new
        {
            Status = refPgmData == null ? "BaselineUnavailable"
                : referencedComparisons.Count == 0 ? "NoOtherDecoderResults" : "Available",
            Limitation = refPgmData == null ? "No NIST native dwsq reconstruction is available."
                : referencedComparisons.Count == 0 ? "No other decoder results are available for comparison." : null,
            Baseline = refPgmData == null ? null : ExecutableType.NativDswq.ToString(),
            WsqRequirementsDecoding = new
            {
                WsqRequirementsDecoding.MinimumIdenticalPixelPercent,
                WsqRequirementsDecoding.MaximumPixelDifference
            },
            Results = referencedComparisons
        };

        // Create the Encoded Image section. Shows the info about the generated Wsq.
        var encodedComparisons = testcase.EncodedResults.ToDictionary(
            item => item.Key.ToString(),
            item => new
            {
                SizeInBytes = item.Value.Data.LongLength,
                Metadata = WsqMetadata.Create(item.Value.WspInspection, originalFileSize),
                FileCompressionRatio = (decimal)originalFileSize / item.Value.Data.LongLength,
                RawPixelCompressionRatio = (decimal)rawPixelSize / item.Value.Data.LongLength,
                FileCompressionRatioExcludingComments = (decimal)originalFileSize / item.Value.WspInspection.SizeWithoutComments,
                RawPixelCompressionRatioExcludingComments = (decimal)rawPixelSize / item.Value.WspInspection.SizeWithoutComments,
                AchievedBitsPerPixel = item.Value.Data.LongLength * 8m / rawPixelSize,
                ReferenceComparison = reference == null ? null : new
                {
                    BytesIdentical = item.Value.Data.AsSpan().SequenceEqual(reference.Data),
                    SizeDifferenceInBytes = item.Value.Data.LongLength - reference.Data.LongLength,
                    Structure = EncodingTestComparison.Compare(reference.WspInspection, item.Value.WspInspection)
                }
            });

        var report = new
        {
            TestName = testcase.CaseId.TestName,
            CodecOperation = GetCodecOperation(testcase),
            Environment = testcase.Environment,
            WsqParameters = testcase.Parameters,
            ConfiguredRatio = GetRatioAsString(testcase.Parameters.BitRate),
            RatioDefinition = "ConfiguredRatio is derived from the test mapping 11.25 / BitRate, not measured compression. " +
                "File ratios use original PGM bytes; raw-pixel ratios use 8-bit pixel bytes. " +
                "WSQ comments are included unless ExcludingComments is specified. " +
                "Metadata.CompressionRatio uses PGM bytes and excludes WSQ comments. " +
                "DecodedComparisons.Nfiq2ScoreDifference is decoded minus original.",
            OriginalImage = new
            {
                testcase.OriginalPgm.FileName,
                SizeInBytes = originalFileSize,
                RawPixelSizeInBytes = rawPixelSize,
                original.Width,
                original.Height,
                original.BitDepth,
                Nfiq2Status = testcase.OriginalPgm.Nfiq2Data.Status,
                Nfiq2 = testcase.OriginalPgm.Nfiq2Data
            },
            ReferenceStatus = reference == null ? "Unavailable" : "Available",
            ReferenceLimitation = reference == null
                ? "No reference WSQ was supplied for this case. Dataset reference selection supports only bitrates 0.75 and 2.25; no reference comparison was performed."
                : null,
            OriginalWsq = reference == null ? null : new
            {
                reference.FileName,
                SizeInBytes = reference.Data.LongLength,
                Metadata = WsqMetadata.Create(reference.WspInspection, originalFileSize)
            },
            EncodedComparisons = encodedComparisons,
            DecodedComparisons = decodedComparisons,
            DecoderComparisons = decoderComparisons,
            CertificationEvaluated = false,
            CertificationLimitation = "Original-PGM comparisons include lossy WSQ compression differences. " +
                "DecoderComparisons records measurements and local decoder requirement outcomes without asserting them. " +
                "IsWsqRequirementsDecodingPassed is null when measurements are unavailable. " +
                "Decoder agreement requires reconstructions of the same WSQ bytes; input identity is not verified by this report. " +
                "This report does not claim NIST/FBI certification."
        };


        var caseDirectory = testcase.CaseId.CaseDirectory;
        Directory.CreateDirectory(caseDirectory);
        string imageStem = Path.GetFileNameWithoutExtension(testcase.OriginalPgm.FileName);
        string reportPath = Path.Combine(caseDirectory, $"{imageStem}.comparison-{report.ConfiguredRatio}.json");
        File.WriteAllText(reportPath, JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true }));
    }

    /// <summary>
    /// Pairwise pixel differences and requirement outcome; null means the outcome could not be evaluated.
    /// Decoder metadata and NFIQ results are in DecodedComparisons.
    /// </summary>
    private sealed record DecoderPixelDifferences(
        string Status, List<string> Failures, int? Mismatches, decimal? IdenticalPixelPercent,
        int? MaximumAbsoluteDifference, decimal? MeanAbsoluteDifference, decimal? MeanSquaredError,
        bool? IsWsqRequirementsDecodingPassed);

    private static DecodedComparisonReport CreateDecodedComparison(
        DataContainerPgm baseline, DataContainerPgm? decoded, string baselineDescription)
    {
        return new DecodedComparisonReport(
            decoded?.Data.LongLength,
            PixelComparer.ComparePixels(baseline, decoded, baselineDescription),
            decoded?.Nfiq2Data.Status ?? "Unavailable",
            decoded?.Nfiq2Data,
            baseline.Nfiq2Data.Status == "Calculated" && decoded?.Nfiq2Data.Status == "Calculated"
                ? decoded.Nfiq2Data.Nfiq2Analysis!.Score - baseline.Nfiq2Data.Nfiq2Analysis!.Score
                : null);
    }

    /// <summary>
    /// Writes original PGM and WSQ inputs, encoded WSQs, and decoded binary PGMs.
    /// BMP copies are written separately by <see cref="WriteBmpFilesToCaseDirectory"/>.
    /// </summary>
    internal void WriteFilesToCaseDirectory(TestCaseData testCase)
    {
        ArgumentNullException.ThrowIfNull(testCase);
        var caseDirectory = testCase.CaseId.CaseDirectory;
        string imageStem = Path.GetFileNameWithoutExtension(testCase.OriginalPgm.FileName);

        // Write original Pgm data to file - unnecessary to change any data.
        WriteVerifiedCopy(Path.Combine(caseDirectory, $"{imageStem}.original.pgm"), testCase.OriginalPgm.Data);

        if (testCase.OriginalWsqData != null)
        {
            // Write original Wsq data to file - unnecessary to change any data.
            WriteVerifiedCopy(Path.Combine(caseDirectory, $"{imageStem}.original-{GetRatioAsString(testCase.Parameters.BitRate)}.wsq"), testCase.OriginalWsqData.Data);
        }

        // Write created encoded Wsq data to file. 
        foreach (var item in testCase.EncodedResults)
        {
            var wsqContainer = item.Value;
            string testCasePgmImagePath = Path.Combine(caseDirectory, $"{imageStem}.{item.Key}.encoded-{GetRatioAsString(testCase.Parameters.BitRate)}.wsq");

            WriteVerifiedCopy(testCasePgmImagePath, wsqContainer.Data);
        }

        // Write created decoded Pgm data to file. 
        foreach (var item in testCase.DecodedResults)
        {
            var pgmContainer = item.Value;
            
            string testCasePgmImagePath = Path.Combine(caseDirectory, $"{imageStem}.{item.Key}.decoded-{GetRatioAsString(testCase.Parameters.BitRate)}.pgm");
            SimpleBitmap? bitmap = pgmContainer.GetSimpleBitmap();

            if (bitmap == null) 
                continue;

            // Write the decoded raw data using the pgmCodec to format the image file.
            WriteVerifiedCopy(testCasePgmImagePath, bitmap);
        }
    }

    internal void CalculateNfiq(DataContainerPgm pgmData)
    {
        if (Nfiq2.Initialized)
        {
            Nfiq2.GetVersion(out string libNfiq2Version, out string libOpenCvVersion);

            SimpleBitmap rawImage = pgmData.GetSimpleBitmap();
            var errorCode = Nfiq2.ComputeAll(out Nfiq2Analysis? nfiq2Analysis, out int nfiq2Error, (int)NistFingerCode.AnyFinger, rawImage!);
            pgmData.Nfiq2Data.UpdateValues(libNfiq2Version, errorCode, nfiq2Analysis);
        }
    }

    internal void CalculateNfiq(IEnumerable<DataContainerPgm> pgmDataCollection)
    {
        foreach (var pgmData in pgmDataCollection)
        {
            CalculateNfiq(pgmData);
        }
    }

    /// <summary>
    /// Saves the original PGM and every decoded PGM as BMP files preserving SimpleBitmap.BitDepth,
    /// using System.IO only, without a platform-specific or third-party imaging library.
    /// Decoder and ratio suffixes match the PGM artifacts; repeated calls replace the BMP copies.
    /// </summary>
    internal void WriteBmpFilesToCaseDirectory(TestCaseData testCase)
    {
        ArgumentNullException.ThrowIfNull(testCase);
        Directory.CreateDirectory(testCase.CaseId.CaseDirectory);
        string imageStem = Path.GetFileNameWithoutExtension(testCase.OriginalPgm.FileName);

        ImageConverter.WriteBmp(testCase.OriginalPgm, Path.Combine(testCase.CaseId.CaseDirectory, $"{imageStem}.original.bmp"));

        foreach (var item in testCase.DecodedResults)
        {
            string path = Path.Combine(testCase.CaseId.CaseDirectory,
                $"{imageStem}.{item.Key}.decoded-{GetRatioAsString(testCase.Parameters.BitRate)}.bmp");
            ImageConverter.WriteBmp(item.Value, path);
        }
    }

    internal void AssertTestCase(TestCaseData testCase)
    {
        AssertTestCaseDecoding(testCase);
        AssertTestCaseEncoding(testCase);
    }

    /// <summary>
    /// Asserts that an encoded WSQ satisfies <see cref="WsqRequirementsEncoding"/>
    /// against the corresponding baseLineReference WSQ.
    /// </summary>
    /// <param name="baseLineReference">The baseLineReference WSQ for the same source image and encoding configuration.</param>
    /// <param name="encoded">The generated WSQ to validate.</param>
    /// <remarks>
    /// Checks compressed size excluding complete comment segments, quantization bin and
    /// zero-bin widths, quantized coefficient agreement, the encoder number, and filter lengths.
    /// Acceptance limits are inclusive and evaluated without rounding; zero baseLineReference widths
    /// require exact zero values. Byte-for-byte identity of the WSQ files is not required.
    /// Expected image dimensions are taken from the baseLineReference WSQ.
    /// </remarks>
    internal void AssertEncoding(DataContainerWsq baseLineReference, DataContainerWsq encoded)
    {
        Assert.NotNull(baseLineReference);
        Assert.NotNull(encoded);
        Assert.NotEmpty(baseLineReference.Data);
        Assert.NotEmpty(encoded.Data);

        var referenceInspection = baseLineReference.WspInspection;
        Assert.True(referenceInspection.Header.Width > 0 && referenceInspection.Header.Height > 0,
            $"{baseLineReference.FileName}: reference WSQ dimensions must be positive.");

        var comparison = EncodingTestComparison.Compare(referenceInspection, encoded.WspInspection);
        Assert.True(comparison.Failures.Count == 0,
            $"{encoded.FileName}: encoding requirements failed against reference '{baseLineReference.FileName}'." +
            Environment.NewLine + string.Join(Environment.NewLine, comparison.Failures));
    }

    /// <summary>
    /// Asserts that the test case contains at least one encoding result and a baseLineReference WSQ,
    /// then validates each generated encoding using <see cref="AssertEncoding"/>.
    /// </summary>
    /// <param name="testCase">The test case containing the baseLineReference WSQ and encoding results.</param>
    /// <remarks>
    /// Missing expected results or a missing baseLineReference must fail validation rather than
    /// silently pass without evaluating the encoding requirements.
    /// </remarks>
    internal void AssertTestCaseEncoding(TestCaseData testCase)
    {
        Assert.NotNull(testCase);
        string imageName = testCase.CaseId.ImageName;
        Assert.True(testCase.OriginalWsqData != null, $"{imageName}: missing reference WSQ.");
        Assert.True(testCase.EncodedResults.Count > 0, $"{imageName}: missing encoding results.");

        foreach (var result in testCase.EncodedResults)
        {
            Assert.True(result.Value != null, $"{imageName}: missing {result.Key} encoding result.");
            AssertEncoding(testCase.OriginalWsqData!, result.Value!);
        }
    }

    /// <summary>
    /// Asserts that the test case contains the expected BiomSharp and NIST decoding results,
    /// validates their image structure, and checks decoder agreement.
    /// </summary>
    /// <param name="testCase">The test case containing the original image and decoding results.</param>
    /// <remarks>
    /// Both decoders must reconstruct the same WSQ bytes. The identical-pixel percentage
    /// and maximum pixel difference must each satisfy <see cref="WsqRequirementsDecoding"/>
    /// for each image, without rounding the percentage or averaging across images.
    /// </remarks>
    internal void AssertTestCaseDecoding(TestCaseData testCase)
    {
        Assert.NotNull(testCase);
        string imageName = testCase.CaseId.ImageName;
        Assert.True(testCase.DecodedResults.TryGetValue(ExecutableType.BiomSharp, out var decodedPgmUsingBiomSharp), $"{imageName}: missing 'BiomSharp' decoding result.");

        Assert.True(testCase.DecodedResults.TryGetValue(ExecutableType.NativDswq, out var decodedPgmUsingNativeDwsq), $"{imageName}: missing 'NIST native dwsq' decoding result.");
        Assert.NotNull(decodedPgmUsingBiomSharp);
        Assert.NotNull(decodedPgmUsingNativeDwsq);

        // Assert the decoded images with the original image
        var resultUsingBiomSharp = DecodingTestComparison.Compare(testCase.OriginalPgm, decodedPgmUsingBiomSharp, DecodingComparisonMode.MeasurementsOnly);
        Assert.True(resultUsingBiomSharp.Failures.Count == 0, string.Join(Environment.NewLine, resultUsingBiomSharp.Failures));

        var resultUsingNativeDwsq = DecodingTestComparison.Compare(testCase.OriginalPgm, decodedPgmUsingNativeDwsq, DecodingComparisonMode.MeasurementsOnly);
        Assert.True(resultUsingNativeDwsq.Failures.Count == 0, string.Join(Environment.NewLine, resultUsingNativeDwsq.Failures));

        // Assert decoded image using BiomSharp with tje decode image using NIST native dwsq program.
        var resultBetweenDecoded = DecodingTestComparison.Compare(decodedPgmUsingNativeDwsq!, decodedPgmUsingBiomSharp!, DecodingComparisonMode.DecoderAcceptance);
        Assert.True(resultBetweenDecoded.Failures.Count == 0, string.Join(Environment.NewLine, resultBetweenDecoded.Failures));

    }
}
