using BiomSharp.Imaging.Pgm;
using BiomSharp.Imaging.Tests.Models;
using BiomSharp.Imaging.Wsq;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using Xunit.Abstractions;

namespace BiomSharp.Imaging.Tests.Fixtures;

public abstract class FixtureTestBase<TFixture> : IClassFixture<TFixture> where TFixture : class
{
    protected FixtureTestBase()
    {
        // Init. the Nfiq incase we need it.
        TestResources.InitializeNfiq2();    
    }

    public ITestOutputHelper TestOutput { get; set; } = null!;

    protected static readonly string RunDirectory = Path.Combine(TestResources.UnitTestsDirectory, "TestResults", $"{DateTime.UtcNow:yyyyMMddTHHmmssfffZ}_{Guid.NewGuid():N}");

    public float GetBitRate(float? ratio)
    {
        if (ratio.HasValue == false || ratio < 1)
        {
            return -1f; // Error
        }

        /* 
         * Assumptions 
         * Bitrate 2.25 - equals the cc. 5:1 compression (high quality, large file size).
         * Bitrate 0.75 - equals the cc. 15:1 compression (FBI standard - recommendation for archiving).
         *
         * Algorithm used: 
         *  var k1 = 2.25 * 5;
         *  var k2 = 0.75 * 15;
         *  
         * Simplifing the algorithm since both k1 and k2 are the same values
         * k = 11.25
         * bitrate = k / ratio        
         */

        // Simplifing the algorithm since both k1 and k2 are the same values
        return 11.25f / ratio.Value;
    }

    public float GetRatio(float? bitRate)
    {
        if (bitRate.HasValue == false || bitRate < 0)
        {
            return -1f; // Error
        }

        /* 
         * Assumptions 
         * Bitrate 2.25 - equals the cc. 5:1 compression (high quality, large file size).
         * Bitrate 0.75 - equals the cc. 15:1 compression (FBI standard - recommendation for archiving).
         * 
         *  Algorithm used: 
         *  var k1 = 2.25 * 5;
         *  var k2 = 0.75 * 15;
         *  
         * Simplifing the algorithm since both k1 and k2 are the same values
         * k = 11.25
         * ratio = k / bitrate
         */


        return 11.25f / bitRate.Value;
    }

    public string GetRatioAsString(float? bitRate)
    {
        var r = GetRatio(bitRate);
        return r == -1 ? string.Empty : r.ToString();
    }


    internal WsqParameters GetEncoding2_25Parameters(WsqFilterType filterType)
    {
        return new WsqParameters()
        {
            BitRate = 2.25f,
            Filter = filterType,
            NistHeader = true
        };
    }

    internal WsqParameters GetEncoding0_75Parameters(WsqFilterType filterType)
    {
        return new WsqParameters()
        {
            BitRate = 0.75f,
            Filter = filterType,
            NistHeader = true
        };
    }

    internal WsqParameters GetEncodingParameters(float Ratio, WsqFilterType filterType)
    {
        var bitRate = GetBitRate(Ratio);
        return new WsqParameters()
        {
            BitRate = bitRate,
            Filter = filterType,
            NistHeader = true
        };
    }


    protected byte[] ReadVerifiedInput(string relativeName)
    {
        string source = Path.Combine(TestResources.DatasetDirectory, relativeName);
        Assert.True(File.Exists(source), $"Required NIST input is missing: {source}");

        string manifest = Path.Combine(TestResources.DatasetDirectory, "wsq_v2.0_pgm.md5");
        Assert.True(File.Exists(manifest), $"Required NIST checksum manifest is missing: {manifest}");

        string manifestName = relativeName.Replace(Path.DirectorySeparatorChar, '/');
        string entry = Assert.Single(File.ReadLines(manifest)
            .Where(line => line.Length > 34 && line[34..].Trim() == manifestName));

        byte[] bytes = File.ReadAllBytes(source);
        // MD5 is used only to check the supplied NIST dataset manifest, not for authentication.
        Assert.Equal(entry[..32].ToUpperInvariant(), Convert.ToHexString(MD5.HashData(bytes)));
        return bytes;
    }

    /// <summary>
    /// Writes the supplied bytes to a new file, then reads it back and asserts
    /// that its contents exactly match the in-memory data.
    /// </summary>
    /// <param name="path">Destination file path. The parent directory must already exist.</param>
    /// <param name="bytes">The data to copy without conversion or encoding.</param>
    /// <remarks>
    /// Does not overwrite an existing file or validate the data's format or NIST compliance.
    /// </remarks>
    /// <exception cref="IOException">The destination file already exists, or a file I/O operation fails.</exception>
    /// <exception cref="Xunit.Sdk.TrueException">The saved contents differ from the supplied bytes.</exception>
    protected void WriteVerifiedCopy(string path, byte[] bytes)
    {
        if (File.Exists(path))
            return;

        using (var stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write))
        {
            stream.Write(bytes);
        }

        Assert.True(bytes.AsSpan().SequenceEqual(File.ReadAllBytes(path)), $"Input copy differs: {path}");
    }

    /// <summary>
    /// Writes a binary PGM and reads it back to verify format, dimensions, bit depth, and pixels.
    /// An existing destination is left unchanged and is not verified.
    /// </summary>
    /// <param name="path">Destination file path. The parent directory must already exist.</param>
    /// <param name="bitmap">The grayscale bitmap to save without changing its pixels.</param>
    protected void WriteVerifiedCopy(string path, SimpleBitmap bitmap)
    {
        if (File.Exists(path))
            return;

        // Write the decoded raw data using the pgmCodec to format the image file.
        var pgm = new PgmCodec();
        pgm.FromRaw(bitmap);
        using (var stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write))
        {
            pgm.Write(stream, new PgmParameters { Format = PgmFormatType.BIN });
        }       

        var savedPgm = new PgmCodec();
        using var savedStream = File.OpenRead(path);
        savedPgm.Read(savedStream, out PgmParameters? parameters);

        Assert.NotNull(parameters);
        Assert.Equal(PgmFormatType.BIN, parameters!.Format);
        SimpleBitmap savedBitmap = savedPgm.ToRaw()
            ?? throw new InvalidDataException($"PGM reader returned no image for {path}.");

        Assert.Equal(bitmap.Width, savedBitmap.Width);
        Assert.Equal(bitmap.Height, savedBitmap.Height);
        Assert.Equal(bitmap.BitDepth, savedBitmap.BitDepth);
        Assert.True(bitmap.Pixels.AsSpan().SequenceEqual(savedBitmap.Pixels), $"Saved PGM pixels differ: {path}");
    }


    /// <summary>
    /// Loads checksum-verified NIST PGM and reference WSQ inputs into a new artifact case.
    /// </summary>
    /// <param name="imageName">Original PGM filename in the dataset.</param>
    /// <param name="parameters">Test parameters, including the reference bitrate.</param>
    /// <param name="testName">Calling test name used to identify the artifact case.</param>
    /// <param name="referenceWsqRelativePath">
    /// Optional dataset-relative WSQ path, for example a decode/not_7_9 input.
    /// When omitted, selects the standard reference by image name and bitrate.
    /// </param>
    public TestCaseData ArrangeTestCase(string imageName, WsqParameters parameters, [CallerMemberName] string testName = "",
        string? referenceWsqRelativePath = null)
    {
        string caseDirectory = Path.Combine(RunDirectory, testName);
        Directory.CreateDirectory(caseDirectory);
        TestOutput.WriteLine($"Artifacts: {caseDirectory}");

        string imageStem = Path.GetFileNameWithoutExtension(imageName);
        string orignalWsqFileName = $"{imageStem}.wsq";
        string? referenceWsqPath = referenceWsqRelativePath
            ?? TestResources.GetRefWsqRelativePath(orignalWsqFileName, parameters.BitRate, defaultPath: null);
        
        // Original Image file data.
        byte[] originalImageData = ReadVerifiedInput($"{imageStem}.pgm");
        var containerPgm = new DataContainerPgm(decoderExecutable: "Original file", imageName, originalImageData);

        DataContainerWsq? containerWsq = null;
        if (!string.IsNullOrEmpty(referenceWsqPath))
        {
            // Original WSQ file data.
            byte[] originalWsqFileData = ReadVerifiedInput(referenceWsqPath);
            containerWsq = new DataContainerWsq(orignalWsqFileName, originalWsqFileData);
        }

        // JV TODO: set the correect Codec type instead of the "Todo".
        return TestCaseData.Create(
            caseId: new TestCaseIdentity(imageName, testName, caseDirectory, "Todo"),
            parameters: parameters, 
            originalPgmData: containerPgm,
            originalWsqData: containerWsq);
    }  
}
