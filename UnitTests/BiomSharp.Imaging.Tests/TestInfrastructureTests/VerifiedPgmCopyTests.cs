using BiomSharp.Imaging.Pgm;
using BiomSharp.Imaging.Tests.Fixtures;

namespace BiomSharp.Imaging.Tests.TestInfrastructureTests;

public class VerifiedPgmCopyTests
{
    [Theory]
    [InlineData(1, 1)]
    [InlineData(3, 2)]
    [InlineData(256, 1)]
    public void Copy_verifies_decoded_pixels_instead_of_header_and_pixel_bytes(int width, int height)
    {
        var pixels = Enumerable.Range(0, width * height).Select(i => (byte)(i % 256)).ToArray();
        var bitmap = SimpleBitmap.Gray8(pixels, width, height);
        string path = Path.Combine(Path.GetTempPath(), $"verified-pgm-{Guid.NewGuid():N}.pgm");
        try
        {
            new CopyFixture().WritePgm(path, bitmap);

            Assert.True(new FileInfo(path).Length > pixels.Length);
            var pgm = new PgmCodec();
            using var stream = File.OpenRead(path);
            pgm.Read(stream, out PgmParameters? parameters);
            Assert.Equal(PgmFormatType.BIN, parameters!.Format);
            var saved = Assert.IsType<SimpleBitmap>(pgm.ToRaw());
            Assert.Equal(width, saved.Width);
            Assert.Equal(height, saved.Height);
            Assert.Equal(bitmap.BitDepth, saved.BitDepth);
            Assert.Equal(pixels, saved.Pixels);
        }
        finally
        {
            File.Delete(path);
        }
    }

    private sealed class CopyFixture : FixtureTestBase<CopyFixture>
    {
        public void WritePgm(string path, SimpleBitmap bitmap) => WriteVerifiedCopy(path, bitmap);
    }
}
