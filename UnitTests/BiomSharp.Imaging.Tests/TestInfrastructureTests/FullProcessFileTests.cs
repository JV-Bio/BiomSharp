using BiomSharp.Imaging.Tests.Fixtures;
using BiomSharp.Imaging.Tests.Helpers;
using BiomSharp.Imaging.Tests.Models;
using BiomSharp.Imaging.Wsq;
using BiomSharp.Imaging.Pgm;

namespace BiomSharp.Imaging.Tests.TestInfrastructureTests;

public class FullProcessFileTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), $"biomsharp-file-tests-{Guid.NewGuid():N}");

    [Theory]
    [InlineData(8, 1)]
    [InlineData(8, 2)]
    [InlineData(8, 3)]
    [InlineData(8, 4)]
    [InlineData(24, 1)]
    [InlineData(24, 2)]
    [InlineData(24, 3)]
    [InlineData(24, 4)]
    [InlineData(32, 1)]
    [InlineData(32, 2)]
    [InlineData(32, 3)]
    [InlineData(32, 4)]
    public void Write_bmp_preserves_simple_bitmap_bit_depth_and_pixels(int bitDepth, int width)
    {
        int bytesPerPixel = bitDepth / 8;
        byte[] pixels = Enumerable.Range(0, width * 2 * bytesPerPixel)
            .Select(i => (byte)(i * 17 % 256)).ToArray();
        var bitmap = bitDepth switch
        {
            8 => SimpleBitmap.Gray8(pixels, width, 2),
            24 => SimpleBitmap.Rgb24(pixels, width, 2),
            32 => SimpleBitmap.Argb32(pixels, width, 2),
            _ => throw new ArgumentOutOfRangeException(nameof(bitDepth))
        };
        string path = Path.GetTempFileName();
        try
        {
            ImageConverter.WriteBmp(bitmap, path);
            using var reader = new BinaryReader(File.OpenRead(path));
            reader.BaseStream.Position = 10;
            int pixelOffset = reader.ReadInt32();
            Assert.Equal(bitDepth == 32 ? 108 : 40, reader.ReadInt32());
            Assert.Equal(width, reader.ReadInt32());
            Assert.Equal(2, reader.ReadInt32());
            Assert.Equal((ushort)1, reader.ReadUInt16());
            Assert.Equal((ushort)bitmap.BitDepth, reader.ReadUInt16());
            Assert.Equal(bitDepth == 32 ? 3 : 0, reader.ReadInt32());
            int rowSize = width * bytesPerPixel;
            int padding = (4 - rowSize % 4) % 4;
            Assert.Equal((rowSize + padding) * 2, reader.ReadInt32());
            reader.BaseStream.Position = 46;
            Assert.Equal(bitDepth == 8 ? 256 : 0, reader.ReadInt32());
            if (bitDepth == 32)
            {
                reader.BaseStream.Position = 54;
                Assert.Equal(0x00FF0000u, reader.ReadUInt32());
                Assert.Equal(0x0000FF00u, reader.ReadUInt32());
                Assert.Equal(0x000000FFu, reader.ReadUInt32());
                Assert.Equal(0xFF000000u, reader.ReadUInt32());
                Assert.Equal(0x73524742u, reader.ReadUInt32());
                Assert.Equal(new byte[48], reader.ReadBytes(48));
            }
            Assert.Equal(bitDepth == 8 ? 1078 : bitDepth == 32 ? 122 : 54, pixelOffset);
            reader.BaseStream.Position = pixelOffset;
            for (int y = 1; y >= 0; y--)
            {
                for (int x = 0; x < width; x++)
                {
                    int start = (y * width + x) * bytesPerPixel;
                    if (bitDepth == 8)
                    {
                        Assert.Equal(pixels[start], reader.ReadByte());
                    }
                    else
                    {
                        int rgb = bitDepth == 32 ? start + 1 : start;
                        Assert.Equal(pixels[rgb + 2], reader.ReadByte());
                        Assert.Equal(pixels[rgb + 1], reader.ReadByte());
                        Assert.Equal(pixels[rgb], reader.ReadByte());
                        if (bitDepth == 32)
                        {
                            Assert.Equal(pixels[start], reader.ReadByte());
                        }
                    }
                }
                Assert.Equal(new byte[padding], reader.ReadBytes(padding));
            }
            Assert.Equal(reader.BaseStream.Length, reader.BaseStream.Position);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void Write_bmp_rejects_unsupported_depth_and_incomplete_pixels_before_writing()
    {
        string path = Path.GetTempFileName();
        byte[] existing = { 1, 2, 3, 4 };
        try
        {
            File.WriteAllBytes(path, existing);
            Assert.Throws<NotSupportedException>(() => ImageConverter.WriteBmp(new SimpleBitmap(), path));
            Assert.Equal(existing, File.ReadAllBytes(path));
            Assert.Throws<InvalidDataException>(() =>
                ImageConverter.WriteBmp(SimpleBitmap.Rgb24(new byte[2], 1, 1), path));
            Assert.Equal(existing, File.ReadAllBytes(path));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Write_files_writes_pgm_artifacts_without_bmps_or_reports(bool includeDecoded)
    {
        var test = CreateTestCase(includeDecoded, 3);
        Directory.CreateDirectory(_directory);

        new FixtureFullProcess().WriteFilesToCaseDirectory(test);

        var expectedFiles = new List<string> { "example.original.pgm" };
        Assert.Equal(test.OriginalPgm.Data, File.ReadAllBytes(Path.Combine(_directory, "example.original.pgm")));
        foreach (var item in test.DecodedResults)
        {
            string fileName = $"example.{item.Key}.decoded-15.pgm";
            expectedFiles.Add(fileName);
            AssertPgm(Path.Combine(_directory, fileName), item.Value);
        }
        AssertFileNames(expectedFiles);
    }

    [Theory]
    [InlineData(false, 3)]
    [InlineData(true, 1)]
    [InlineData(true, 2)]
    [InlineData(true, 3)]
    [InlineData(true, 4)]
    [InlineData(true, 128)]
    public void Write_bmp_files_creates_directory_and_preserves_all_pgm_pixels(bool includeDecoded, int width)
    {
        var test = CreateTestCase(includeDecoded, width);
        Assert.False(Directory.Exists(_directory));

        new FixtureFullProcess().WriteBmpFilesToCaseDirectory(test);

        AssertBmp(Path.Combine(_directory, "example.original.bmp"), test.OriginalPgm);
        var expectedFiles = new List<string> { "example.original.bmp" };
        foreach (var item in test.DecodedResults)
        {
            string fileName = $"example.{item.Key}.decoded-15.bmp";
            expectedFiles.Add(fileName);
            AssertBmp(Path.Combine(_directory, fileName), item.Value);
        }
        AssertFileNames(expectedFiles);
    }

    [Fact]
    public void Write_bmp_files_keeps_outputs_for_other_ratios()
    {
        var test = CreateTestCase(true, 3);
        var fixture = new FixtureFullProcess();
        fixture.WriteBmpFilesToCaseDirectory(test);

        fixture.WriteBmpFilesToCaseDirectory(test with { Parameters = new WsqParameters { BitRate = 2.25f } });

        AssertBmp(Path.Combine(_directory, "example.original.bmp"), test.OriginalPgm);
        var expectedFiles = new List<string> { "example.original.bmp" };
        foreach (var item in test.DecodedResults)
        {
            foreach (string ratio in new[] { "15", "5" })
            {
                string fileName = $"example.{item.Key}.decoded-{ratio}.bmp";
                expectedFiles.Add(fileName);
                AssertBmp(Path.Combine(_directory, fileName), item.Value);
            }
        }
        AssertFileNames(expectedFiles);
    }

    [Fact]
    public void Write_bmp_files_replaces_existing_images_without_leaving_trailing_bytes()
    {
        var fixture = new FixtureFullProcess();
        fixture.WriteBmpFilesToCaseDirectory(CreateTestCase(true, 128));
        var replacement = CreateTestCase(true, 1);

        fixture.WriteBmpFilesToCaseDirectory(replacement);

        AssertBmp(Path.Combine(_directory, "example.original.bmp"), replacement.OriginalPgm);
        foreach (var item in replacement.DecodedResults)
            AssertBmp(Path.Combine(_directory, $"example.{item.Key}.decoded-15.bmp"), item.Value);
        AssertFileNames(new[]
        {
            "example.original.bmp",
            "example.BiomSharp.decoded-15.bmp",
            "example.NativDswq.decoded-15.bmp"
        });
    }

    private TestCaseData CreateTestCase(bool includeDecoded, int width)
    {
        byte[] shades = { 0, 1, 127, 128, 254, 255 };
        byte[] pixels = Enumerable.Range(0, width * 2)
            .Select(i => width == 128 ? (byte)i : shades[i % shades.Length]).ToArray();
        var original = new DataContainerPgm("Original file", "example.pgm",
            SimpleBitmap.Gray8(pixels, width, 2));
        var test = TestCaseData.Create(new TestCaseIdentity("example.pgm", nameof(FullProcessFileTests), _directory, "Decoding"),
            new WsqParameters { BitRate = 0.75f }, original, null);
        if (includeDecoded)
        {
            test.AddPgmContainer(ExecutableType.BiomSharp, new DataContainerPgm("BiomSharp", "example.pgm",
                SimpleBitmap.Gray8(pixels.Reverse().ToArray(), width, 2)));
            test.AddPgmContainer(ExecutableType.NativDswq, new DataContainerPgm("dwsq", "example.pgm",
                SimpleBitmap.Gray8(pixels.Select(value => (byte)(value / 2)).ToArray(), width, 2)));
        }

        return test;
    }

    private void AssertFileNames(IEnumerable<string> expected)
    {
        var actual = Directory.GetFileSystemEntries(_directory).Select(Path.GetFileName);
        Assert.Equal(expected.OrderBy(name => name), actual.OrderBy(name => name));
    }

    private static void AssertPgm(string path, DataContainerPgm pgm)
    {
        var codec = new PgmCodec();
        using var stream = File.OpenRead(path);
        codec.Read(stream, out PgmParameters? parameters);

        Assert.NotNull(parameters);
        Assert.Equal(PgmFormatType.BIN, parameters!.Format);
        var actual = Assert.IsType<SimpleBitmap>(codec.ToRaw());
        var expected = Assert.IsType<SimpleBitmap>(pgm.GetSimpleBitmap());
        Assert.Equal(expected.Width, actual.Width);
        Assert.Equal(expected.Height, actual.Height);
        Assert.Equal(expected.BitDepth, actual.BitDepth);
        Assert.Equal(expected.Pixels, actual.Pixels);
    }

    private static void AssertBmp(string path, DataContainerPgm pgm)
    {
        byte[] bytes = File.ReadAllBytes(path);
        var expected = pgm.GetSimpleBitmap()!;
        using var reader = new BinaryReader(new MemoryStream(bytes));
        Assert.Equal((byte)'B', reader.ReadByte());
        Assert.Equal((byte)'M', reader.ReadByte());
        Assert.Equal(bytes.Length, reader.ReadInt32());
        Assert.Equal(0, reader.ReadInt32());
        Assert.Equal(1078, reader.ReadInt32());
        Assert.Equal(40, reader.ReadInt32());
        Assert.Equal(expected.Width, reader.ReadInt32());
        Assert.Equal(expected.Height, reader.ReadInt32());
        Assert.Equal((ushort)1, reader.ReadUInt16());
        Assert.Equal((ushort)expected.BitDepth, reader.ReadUInt16());
        Assert.Equal(0, reader.ReadInt32());
        int padding = (4 - expected.Width % 4) % 4;
        int pixelDataSize = (expected.Width + padding) * expected.Height;
        Assert.Equal(pixelDataSize, reader.ReadInt32());
        Assert.Equal(1078 + pixelDataSize, bytes.Length);
        Assert.Equal(0, reader.ReadInt32());
        Assert.Equal(0, reader.ReadInt32());
        Assert.Equal(256, reader.ReadInt32());
        Assert.Equal(0, reader.ReadInt32());
        for (int value = 0; value < 256; value++)
        {
            Assert.Equal((byte)value, reader.ReadByte());
            Assert.Equal((byte)value, reader.ReadByte());
            Assert.Equal((byte)value, reader.ReadByte());
            Assert.Equal((byte)0, reader.ReadByte());
        }
        for (int y = expected.Height - 1; y >= 0; y--)
        {
            for (int x = 0; x < expected.Width; x++)
            {
                byte value = expected.Pixels[y * expected.Width + x];
                Assert.Equal(value, reader.ReadByte());
            }
            for (int i = 0; i < padding; i++)
            {
                Assert.Equal((byte)0, reader.ReadByte());
            }
        }
        Assert.Equal(reader.BaseStream.Length, reader.BaseStream.Position);
    }

    public void Dispose()
    {
        if (Directory.Exists(_directory))
            Directory.Delete(_directory, recursive: true);
    }
}
