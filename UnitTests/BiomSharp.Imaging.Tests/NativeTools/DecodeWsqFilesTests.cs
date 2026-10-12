using BiomSharp.Imaging.Tests.Models;

namespace BiomSharp.Imaging.Tests.NativeTools;

public class DecodeWsqFilesTests : IDisposable
{
    private readonly string _directory = CreateTestDirectory();

    [Fact]
    public void Default_constructor_selects_executable_and_checksum_for_current_process()
    {
        var decoder = new DecodeWsqFile();

        string expectedFile = Environment.Is64BitProcess ? "dwsq_x64.exe" : "dwsq_x32.exe";
        string expectedChecksum = Environment.Is64BitProcess
            ? "E268EB972228F9E8386FBEDC684220DE9F34E312E1652D7C07D4B1B8F2990500"
            : "784A2DCA0FF95AA51078D2E9B3009EE74506EDEAFAC66313BA8D69A2B48C0718";

        Assert.Equal(expectedFile, decoder.FileName);
        Assert.Equal(expectedChecksum, decoder.Sha256Value);
    }

    [Fact]
    public void Bundled_executable_passes_validation()
    {
        var decoder = new DecodeWsqFile();

        Assert.True(decoder.IsValid);
        Assert.True(decoder.ThrowIfNotValid());
    }

    [Fact]
    public void Missing_executable_directory_is_reported()
    {
        string missingDirectory = Path.Combine(_directory, "missing tools");
        var decoder = new DecodeWsqFile(missingDirectory);

        Assert.Throws<DirectoryNotFoundException>(() => decoder.IsValid);
        var error = Assert.Throws<DirectoryNotFoundException>(() => decoder.ThrowIfNotValid());
        Assert.Contains(missingDirectory, error.Message);
    }

    [Fact]
    public void Missing_executable_is_reported()
    {
        var decoder = new DecodeWsqFile(_directory);
        string expectedPath = Path.Combine(_directory, decoder.FileName);

        Assert.Throws<FileNotFoundException>(() => decoder.IsValid);
        var error = Assert.Throws<FileNotFoundException>(() => decoder.ThrowIfNotValid());
        Assert.Equal(expectedPath, error.FileName);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Wrong_checksum_is_rejected_before_decoding(bool useAsync)
    {
        var decoder = new DecodeWsqFile(_directory);
        File.WriteAllBytes(Path.Combine(_directory, decoder.FileName), new byte[] { 1, 2, 3 });

        Assert.False(decoder.IsValid);
        var validationError = Assert.Throws<InvalidDataException>(() => decoder.ThrowIfNotValid());
        Assert.Contains(decoder.FileName, validationError.Message);
        Assert.Contains(decoder.Sha256Value, validationError.Message);

        var error = await Assert.ThrowsAsync<InvalidDataException>(() =>
            Decode(decoder, _directory, "missing.wsq", useAsync));
        Assert.Equal(validationError.Message, error.Message);
    }

    [Fact]
    public void Validation_detects_file_changes_after_construction()
    {
        var decoder = CopyBundledDecoder();
        string path = Path.Combine(_directory, decoder.FileName);
        byte[] original = File.ReadAllBytes(path);
        Assert.True(decoder.IsValid);
        Assert.True(decoder.ThrowIfNotValid());

        byte[] changed = original.ToArray();
        changed[^1] ^= 1;
        File.WriteAllBytes(path, changed);

        Assert.False(decoder.IsValid);
        Assert.Throws<InvalidDataException>(() => decoder.ThrowIfNotValid());

        File.WriteAllBytes(path, original);
        Assert.True(decoder.IsValid);
        Assert.True(decoder.ThrowIfNotValid());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Native_decode_failure_includes_diagnostics_and_preserves_input(bool useAsync)
    {
        var decoder = new DecodeWsqFile();
        string path = Path.Combine(_directory, "invalid.wsq");
        byte[] input = { 0, 0, 0, 0 };
        File.WriteAllBytes(path, input);

        var error = await Assert.ThrowsAsync<InvalidDataException>(() =>
            Decode(decoder, _directory, "invalid.wsq", useAsync));

        Assert.Contains(decoder.FileName, error.Message);
        Assert.Contains("exit code", error.Message);
        Assert.Contains("ERROR", error.Message);
        Assert.Equal(input, File.ReadAllBytes(path));
        Assert.Equal(path, Assert.Single(Directory.GetFileSystemEntries(_directory)));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Missing_input_is_reported(bool useAsync)
    {
        var decoder = new DecodeWsqFile();
        string path = Path.Combine(_directory, "missing.wsq");

        var error = await Assert.ThrowsAsync<FileNotFoundException>(() =>
            Decode(decoder, _directory, "missing.wsq", useAsync));

        Assert.Equal(path, error.FileName);
        Assert.Empty(Directory.GetFileSystemEntries(_directory));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Reference_decode_returns_grayscale_pixels_and_preserves_input(bool useAsync)
    {
        var decoder = CopyBundledDecoder();
        string inputDirectory = Path.Combine(_directory, "input files");
        Directory.CreateDirectory(inputDirectory);
        const string fileName = "reference image.wsq";
        string path = Path.Combine(inputDirectory, fileName);
        byte[] input = File.ReadAllBytes(Path.Combine(
            TestResources.DatasetDirectory, "decode", "75", "cmp00001.wsq"));
        File.WriteAllBytes(path, input);

        var bitmap = await Decode(decoder, inputDirectory, fileName, useAsync);

        Assert.Equal(589, bitmap.Width);
        Assert.Equal(605, bitmap.Height);
        Assert.Equal(8, bitmap.BitDepth);
        Assert.Equal(589 * 605, bitmap.Pixels.Length);
        Assert.Equal(input, File.ReadAllBytes(path));
        Assert.Equal(path, Assert.Single(Directory.GetFileSystemEntries(inputDirectory)));
    }

    private DecodeWsqFile CopyBundledDecoder()
    {
        var decoder = new DecodeWsqFile(_directory);
        File.Copy(Path.Combine(DecodeWsqFile.DirectoryPath, decoder.FileName),
            Path.Combine(_directory, decoder.FileName));
        return decoder;
    }

    private static async Task<SimpleBitmap> Decode(
        DecodeWsqFile decoder, string directory, string fileName, bool useAsync)
    {
        if (useAsync)
            return await decoder.DecodeWsqToBitmapAsync(directory, fileName);

        return decoder.DecodeWsqToBitmap(directory, fileName);
    }

    private static string CreateTestDirectory()
    {
        string path = Path.Combine(Path.GetTempPath(), $"nbis decoder tests {Guid.NewGuid():N}");
        Directory.CreateDirectory(path);
        return path;
    }

    public void Dispose() => Directory.Delete(_directory, recursive: true);
}
