using BiomSharp.Imaging.Tests.Models;

namespace BiomSharp.Imaging.Tests.NativeTools;

public class DecodeWsqFilesTests : IDisposable
{
    private readonly string _directory = CreateTestDirectory();

    [Fact]
    public void Default_constructor_selects_executable_and_checksum_for_current_process()
    {
        // Arrange
        string expectedFile = Environment.Is64BitProcess ? "dwsq_x64.exe" : "dwsq_x32.exe";
        string expectedChecksum = Environment.Is64BitProcess
            ? "E268EB972228F9E8386FBEDC684220DE9F34E312E1652D7C07D4B1B8F2990500"
            : "784A2DCA0FF95AA51078D2E9B3009EE74506EDEAFAC66313BA8D69A2B48C0718";

        // Act
        var decoder = new DecodeWsqFile();

        // Assert
        Assert.Equal(expectedFile, decoder.FileName);
        Assert.Equal(expectedChecksum, decoder.Sha256Value);
    }

    [Fact]
    public void Bundled_executable_passes_validation()
    {
        // Arrange
        var decoder = new DecodeWsqFile();

        // Act
        bool isValid = decoder.IsValid;
        bool validationResult = decoder.ThrowIfNotValid();

        // Assert
        Assert.True(isValid);
        Assert.True(validationResult);
    }

    [Fact]
    public void Missing_executable_directory_is_reported()
    {
        // Arrange
        string missingDirectory = Path.Combine(_directory, "missing tools");
        var decoder = new DecodeWsqFile(missingDirectory);

        // Act
        var isValidError = Record.Exception(() => decoder.IsValid);
        var validationError = Record.Exception(() => decoder.ThrowIfNotValid());

        // Assert
        Assert.IsType<DirectoryNotFoundException>(isValidError);
        var error = Assert.IsType<DirectoryNotFoundException>(validationError);
        Assert.Contains(missingDirectory, error.Message);
    }

    [Fact]
    public void Missing_executable_is_reported()
    {
        // Arrange
        var decoder = new DecodeWsqFile(_directory);
        string expectedPath = Path.Combine(_directory, decoder.FileName);

        // Act
        var isValidError = Record.Exception(() => decoder.IsValid);
        var validationError = Record.Exception(() => decoder.ThrowIfNotValid());

        // Assert
        Assert.IsType<FileNotFoundException>(isValidError);
        var error = Assert.IsType<FileNotFoundException>(validationError);
        Assert.Equal(expectedPath, error.FileName);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Wrong_checksum_is_rejected_before_decoding(bool useAsync)
    {
        // Arrange
        var decoder = new DecodeWsqFile(_directory);
        File.WriteAllBytes(Path.Combine(_directory, decoder.FileName), new byte[] { 1, 2, 3 });

        // Act
        bool isValid = decoder.IsValid;
        var validationException = Record.Exception(() => decoder.ThrowIfNotValid());
        var decodeException = await Record.ExceptionAsync(() =>
            Decode(decoder, _directory, "missing.wsq", useAsync));

        // Assert
        Assert.False(isValid);
        var validationError = Assert.IsType<InvalidDataException>(validationException);
        Assert.Contains(decoder.FileName, validationError.Message);
        Assert.Contains(decoder.Sha256Value, validationError.Message);

        var error = Assert.IsType<InvalidDataException>(decodeException);
        Assert.Equal(validationError.Message, error.Message);
    }

    [Fact]
    public void Validation_detects_file_changes_after_construction()
    {
        // Arrange
        var decoder = CopyBundledDecoder();
        string path = Path.Combine(_directory, decoder.FileName);
        byte[] original = File.ReadAllBytes(path);
        byte[] changed = original.ToArray();
        changed[^1] ^= 1;

        // Act
        bool initiallyValid = decoder.IsValid;
        bool initialValidation = decoder.ThrowIfNotValid();
        File.WriteAllBytes(path, changed);
        bool changedFileValid = decoder.IsValid;
        var changedFileException = Record.Exception(() => decoder.ThrowIfNotValid());
        File.WriteAllBytes(path, original);
        bool restoredFileValid = decoder.IsValid;
        bool restoredValidation = decoder.ThrowIfNotValid();

        // Assert
        Assert.True(initiallyValid);
        Assert.True(initialValidation);
        Assert.False(changedFileValid);
        Assert.IsType<InvalidDataException>(changedFileException);
        Assert.True(restoredFileValid);
        Assert.True(restoredValidation);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Native_decode_failure_includes_diagnostics_and_preserves_input(bool useAsync)
    {
        // Arrange
        var decoder = new DecodeWsqFile();
        string path = Path.Combine(_directory, "invalid.wsq");
        byte[] input = { 0, 0, 0, 0 };
        File.WriteAllBytes(path, input);

        // Act
        var exception = await Record.ExceptionAsync(() =>
            Decode(decoder, _directory, "invalid.wsq", useAsync));

        // Assert
        var error = Assert.IsType<InvalidDataException>(exception);
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
        // Arrange
        var decoder = new DecodeWsqFile();
        string path = Path.Combine(_directory, "missing.wsq");

        // Act
        var exception = await Record.ExceptionAsync(() =>
            Decode(decoder, _directory, "missing.wsq", useAsync));

        // Assert
        var error = Assert.IsType<FileNotFoundException>(exception);
        Assert.Equal(path, error.FileName);
        Assert.Empty(Directory.GetFileSystemEntries(_directory));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Reference_decode_returns_grayscale_pixels_and_preserves_input(bool useAsync)
    {
        // Arrange
        var decoder = CopyBundledDecoder();
        string inputDirectory = Path.Combine(_directory, "input files");
        Directory.CreateDirectory(inputDirectory);
        const string fileName = "reference image.wsq";
        string path = Path.Combine(inputDirectory, fileName);
        byte[] input = File.ReadAllBytes(Path.Combine(
            TestResources.DatasetDirectory, "decode", "75", "cmp00001.wsq"));
        File.WriteAllBytes(path, input);

        // Act
        var bitmap = await Decode(decoder, inputDirectory, fileName, useAsync);

        // Assert
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
