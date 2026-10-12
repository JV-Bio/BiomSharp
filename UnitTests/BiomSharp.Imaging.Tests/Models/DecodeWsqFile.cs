using BiomSharp.Nist;
using System.Diagnostics;
using System.Globalization;
using System.Security.Cryptography;

namespace BiomSharp.Imaging.Tests.Models;

internal class DecodeWsqFile
{
    public DecodeWsqFile() : this(DirectoryPath)
    {
        
    }

    /// <summary>
    /// Selects the executable for this process from the supplied directory.
    /// Allows validation against isolated executable copies without changing the bundled tools.
    /// </summary>
    internal DecodeWsqFile(string executableDirectory) : this(Environment.Is64BitProcess, executableDirectory)
    {
    }

    private DecodeWsqFile(bool isX64Process, string executableDirectory)
    {
        if (!OperatingSystem.IsWindows())
        {
            throw new PlatformNotSupportedException("The bundled NBIS executables require Windows.");
        }
        ArgumentNullException.ThrowIfNull(executableDirectory);
        _executableDirectory = Path.GetFullPath(executableDirectory);

        if (isX64Process)
        {
            Sha256Value = "E268EB972228F9E8386FBEDC684220DE9F34E312E1652D7C07D4B1B8F2990500";
            FileName = "dwsq_x64.exe";
        }
        else
        {
            Sha256Value = "784A2DCA0FF95AA51078D2E9B3009EE74506EDEAFAC66313BA8D69A2B48C0718";
            FileName = "dwsq_x32.exe";
        }
    }

    public readonly string Sha256Value;
    public readonly string FileName;
    public bool IsValid => IsFileValid();
    private readonly string _executableDirectory;

    /// <summary>
    /// Directory path to native tools
    /// </summary>
    /// <value>.\NativeTools\NBIS\5.0.0</value>
    public static readonly string DirectoryPath = Path.Combine(AppContext.BaseDirectory, "NativeTools", "NBIS", "5.0.0");

    private bool IsFileValid()
    {
        var fullpath = Path.Combine(_executableDirectory, FileName);
        if (!Directory.Exists(_executableDirectory))
        {            
            throw new DirectoryNotFoundException($"Directory: '{_executableDirectory}' does not exist.");
        }

        if(!File.Exists(fullpath))
        {
            throw new FileNotFoundException($"File at: '{fullpath}' does not exist.", fullpath);
        }

        using var stream = File.OpenRead(fullpath);
        using var sha256 = SHA256.Create();
        string actual = Convert.ToHexString(sha256.ComputeHash(stream));
        return string.Equals(actual, Sha256Value, StringComparison.OrdinalIgnoreCase);
    }

    public bool ThrowIfNotValid()
    {
        if (!IsValid)
        {
            throw new InvalidDataException($"SHA-256 verification failed for '{FileName}'. Expected {Sha256Value}.");
        }
        return true;
    }    

    private void ThrowIfWsqFileNotFound(string fullfilepath)
    {
        if (!File.Exists(fullfilepath))
        {
            throw new FileNotFoundException("The WSQ input file does not exist.", fullfilepath);
        }
    }

    private async Task<(string output, string error)> DecodingWsqWork(string workDirectory, string inputPath, string wsqFileName)
    {
        // dwsq writes both raw pixels and NISTCOM next to its input.
        File.Copy(inputPath, Path.Combine(workDirectory, wsqFileName));
        var startInfo = new ProcessStartInfo(Path.Combine(_executableDirectory, FileName))
        {
            WorkingDirectory = workDirectory,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };
        startInfo.ArgumentList.Add("raw");
        startInfo.ArgumentList.Add(wsqFileName);
        startInfo.ArgumentList.Add("-raw_out");



        using var process = new Process { StartInfo = startInfo };
        if (!process.Start())
        {
            throw new InvalidOperationException($"Could not start '{FileName}'.");
        }

        Task<string> stdout = process.StandardOutput.ReadToEndAsync();
        Task<string> stderr = process.StandardError.ReadToEndAsync();
        using var cts = new CancellationTokenSource(60_000);
        try
        {

            await process.WaitForExitAsync(cts.Token).ConfigureAwait(false);      // Wait for the process to finish.            
            string output = await stdout.ConfigureAwait(false);
            string error = await stderr.ConfigureAwait(false);
            if (process.ExitCode != 0)
            {
                throw new InvalidDataException($"'{FileName}' failed decoding '{inputPath}' " +
                    $"with exit code {process.ExitCode}. stdout: {output} stderr: {error}");
            }

            return (output, error);
        }
        catch (OperationCanceledException) when (cts.IsCancellationRequested)
        {
            if(process.HasExited == false)
            {
                process.Kill(entireProcessTree: true);
                await process.WaitForExitAsync().ConfigureAwait(false);
            }

            string? stdOutStr = await stdout.ConfigureAwait(false);
            string? stdErrStr = await stderr.ConfigureAwait(false);

            throw new TimeoutException($"'{FileName}' exceeded 60 seconds decoding '{inputPath}'. " +
                $"stdout: {stdOutStr} stderr: {stdErrStr}");

        }
    }

    private static int ReadMetadataInteger(string[] lines, string name)
    {
        string[][] matches = lines
            .Select(line => line.Split((char[]?)null, 2, StringSplitOptions.RemoveEmptyEntries))
            .Where(parts => parts.Length > 0 && parts[0] == name)
            .ToArray();
        if (matches.Length != 1 || 
            matches[0].Length != 2 ||
            !int.TryParse(matches[0][1], NumberStyles.Integer, CultureInfo.InvariantCulture, out int value))
        {
            throw new InvalidDataException($"NBIS NISTCOM must contain exactly one integer '{name}' value.");
        }
        return value;
    }

    private SimpleBitmap ConvertRawImgToGray(string workDirectory, string rawImgName, string metaDataFileName)
    {
        const int grayDepth = 8;
        string rawPath = Path.Combine(workDirectory, rawImgName);
        string metadataPath = Path.Combine(workDirectory, metaDataFileName);
        if (!File.Exists(rawPath) || !File.Exists(metadataPath))
        {
            throw new InvalidDataException($"'{FileName}' did not produce the expected raw image and NISTCOM files.");
        }

        // Use NBIS's output dimensions, not BiomSharp's decoding, for an independent result.
        string[] metadata = File.ReadAllLines(metadataPath);
        int width = ReadMetadataInteger(metadata, NistConstants.NcmPixWidth);
        int height = ReadMetadataInteger(metadata, NistConstants.NcmPixHeight);
        int depth = ReadMetadataInteger(metadata, NistConstants.NcmPixDepth);
        int resolution = ReadMetadataInteger(metadata, NistConstants.NcmPpi);
        if (width <= 0 || height <= 0 || depth != grayDepth ||
            (resolution <= 0 && resolution != NistConstants.NcmPpiUnknown))
        {
            throw new InvalidDataException($"'{FileName}' produced invalid grayscale image metadata.");
        }

        long pixelCount = (long)width * height;
        if (pixelCount > int.MaxValue || new FileInfo(rawPath).Length != pixelCount)
        {
            throw new InvalidDataException($"'{FileName}' raw image length does not match {width} x {height}.");
        }

        return SimpleBitmap.Gray8(File.ReadAllBytes(rawPath), width, height, resolution);
    }


    /// <summary>
    /// Runs NBIS on an isolated input copy and returns its raw grayscale pixels.
    /// No PGM is written, and the input directory is not modified.
    /// </summary>
    public SimpleBitmap DecodeWsqToBitmap(string directoryPath, string fileName)
    {
        const string wsqFileName = "input.wsq";
        const string rawFileName = "input.raw";
        const string metadataFileName = "input.ncm";
        
        ThrowIfNotValid();
        string inputPath = Path.GetFullPath(Path.Combine(directoryPath, fileName));
        ThrowIfWsqFileNotFound(inputPath);

        string output = string.Empty;
        string error = string.Empty;

        string workDirectory = Path.Combine(Path.GetTempPath(), $"biomsharp-nbis-{Guid.NewGuid():N}");
        Directory.CreateDirectory(workDirectory);

        try
        {
            var decodingResult = DecodingWsqWork(workDirectory, inputPath, wsqFileName)
                .ConfigureAwait(false)
                .GetAwaiter()
                .GetResult();
            
            output = decodingResult.output;
            if (!string.IsNullOrWhiteSpace(output))
            {
                Trace.WriteLine($"{FileName}: {output}");
            }

            error = decodingResult.error;
            if (!string.IsNullOrWhiteSpace(error))
            {
                Trace.TraceWarning($"{FileName} decoding '{inputPath}': {error}");
            }

            return ConvertRawImgToGray(workDirectory, rawFileName, metadataFileName);
        }
        finally
        {
            Directory.Delete(workDirectory, recursive: true);
        }
    }

    /// <summary>
    /// Runs NBIS on an isolated input copy and returns its raw grayscale pixels.
    /// No PGM is written, and the input directory is not modified.
    /// </summary>
    public async Task<SimpleBitmap> DecodeWsqToBitmapAsync(string directoryPath, string fileName)
    {
        const string wsqFileName = "input.wsq";
        const string rawFileName = "input.raw";
        const string metadataFileName = "input.ncm";

        ThrowIfNotValid();
        string inputPath = Path.GetFullPath(Path.Combine(directoryPath, fileName));
        ThrowIfWsqFileNotFound(inputPath);

        string output = string.Empty;
        string error = string.Empty;

        string workDirectory = Path.Combine(Path.GetTempPath(), $"biomsharp-nbis-{Guid.NewGuid():N}");
        Directory.CreateDirectory(workDirectory);

        try
        {
            var decodingResult = await DecodingWsqWork(workDirectory, inputPath, wsqFileName);

            output = decodingResult.output;
            if (!string.IsNullOrWhiteSpace(output))
            {
                Trace.WriteLine($"{FileName}: {output}");
            }

            error = decodingResult.error;
            if (!string.IsNullOrWhiteSpace(error))
            {
                Trace.TraceWarning($"{FileName} decoding '{inputPath}': {error}");
            }

            return ConvertRawImgToGray(workDirectory, rawFileName, metadataFileName);
        }
        finally
        {
            Directory.Delete(workDirectory, recursive: true);
        }
    }

}
