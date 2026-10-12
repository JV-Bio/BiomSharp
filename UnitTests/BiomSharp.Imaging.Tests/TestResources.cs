using BiomSharp.Imaging.Tests.Models;
using System.Runtime.CompilerServices;
using BiomSharp.Nist.Nfiq;
using BiomSharp.Properties;

namespace BiomSharp.Imaging.Tests;

internal static class TestResources
{
    private static readonly Lazy<bool> Nfiq2Initialization = new(() => Nfiq2.Initialize(Resources.ModelInfo, Resources.ModelData));

    public static void InitializeNfiq2()
    {
        Assert.True(Nfiq2Initialization.Value, "NFIQ2 not initialized!");
    }

    static TestResources()
    {
        UnitTestsDirectory = FindUnitTestsDirectory();
        DatasetDirectory = Path.Combine(UnitTestsDirectory, _refImageFolderName);
    }

    private const string _refImageFolderName = "reference_images_v2.0_pgm";
    private static string FindUnitTestsDirectory([CallerFilePath] string sourcePath = "") =>
        Path.GetFullPath(Path.Combine(Path.GetDirectoryName(sourcePath)!, ".."));

    public static readonly string DatasetDirectory;
    public static readonly string UnitTestsDirectory;

    public static string GetReferenceWsqRelativePath(string imageName, float bitRate)
    {
        string rateDirectory = bitRate switch
        {
            0.75f => "75",
            2.25f => "225",
            // Default            
            _ => throw new ArgumentOutOfRangeException(nameof(bitRate), bitRate,
                "NIST encoder references are available only for bitrates 0.75 and 2.25.")
        };
        return Path.Combine("decode", rateDirectory, Path.ChangeExtension(imageName, ".wsq"));
    }

    public static string? GetRefWsqRelativePath(string imageName, float bitRate, string? defaultPath = null)
    {
        string? rateDirectory = bitRate switch
        {
            0.75f => "75",
            2.25f => "225",
            // Default            
            _ => null
        };

        if (string.IsNullOrEmpty(rateDirectory))
            return defaultPath;    // No relative path, return default

        return Path.Combine("decode", rateDirectory, Path.ChangeExtension(imageName, ".wsq"));
    }


    public static readonly DecodeWsqFile NativeWsqDecoder = new DecodeWsqFile();
}
