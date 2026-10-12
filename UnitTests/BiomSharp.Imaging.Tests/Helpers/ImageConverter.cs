using BiomSharp.Imaging.Tests.Models;

namespace BiomSharp.Imaging.Tests.Helpers;

internal static class ImageConverter
{
    internal static void WriteBmp(DataContainerPgm pgm, string path)
    {
        var bitmap = pgm.GetSimpleBitmap()
            ?? throw new InvalidDataException($"PGM contains no image: {pgm.FileName}.");
        WriteBmp(bitmap, path);
    }

    internal static void WriteBmp(SimpleBitmap bitmap, string path)
    {
        ArgumentNullException.ThrowIfNull(bitmap);
        int bitDepth = bitmap.BitDepth;
        if (bitDepth is not (8 or 24 or 32))
        {
            throw new NotSupportedException($"BMP export does not support bit depth {bitDepth}.");
        }

        int bytesPerPixel = bitDepth / 8;
        int rowSize = checked(bitmap.Width * bytesPerPixel);
        if (bitmap.Width <= 0 || bitmap.Height <= 0 ||
            bitmap.Pixels.Length != checked(rowSize * bitmap.Height))
        {
            throw new InvalidDataException("Bitmap dimensions and pixel data do not match.");
        }

        int paletteSize = bitDepth == 8 ? 256 : 0;
        int headerSize = bitDepth == 32 ? 108 : 40;
        int pixelOffset = 14 + headerSize + paletteSize * 4;
        int stride = checked((rowSize + 3) & ~3);
        int pixelDataSize = checked(stride * bitmap.Height);
        int fileSize = checked(pixelOffset + pixelDataSize);
        using var stream = new FileStream(path, FileMode.Create, FileAccess.Write);
        using var writer = new BinaryWriter(stream);

        // Use BITMAPV4HEADER for 32-bit images so readers honor the alpha channel.
        writer.Write((byte)'B');
        writer.Write((byte)'M');
        writer.Write(fileSize);
        writer.Write(0);
        writer.Write(pixelOffset);
        writer.Write(headerSize);
        writer.Write(bitmap.Width);
        writer.Write(bitmap.Height);
        writer.Write((ushort)1);
        writer.Write((ushort)bitDepth);
        writer.Write(bitDepth == 32 ? 3 : 0);
        writer.Write(pixelDataSize);
        writer.Write(0);
        writer.Write(0);
        writer.Write(paletteSize);
        writer.Write(0);

        if (bitDepth == 32)
        {
            writer.Write(0x00FF0000u);
            writer.Write(0x0000FF00u);
            writer.Write(0x000000FFu);
            writer.Write(0xFF000000u);
            writer.Write(0x73524742u); // LCS_sRGB
            writer.Write(new byte[48]);
        }

        // Each pixel indexes its exact gray value in the BGRA palette (reserved byte is zero).
        for (int value = 0; value < paletteSize; value++)
        {
            writer.Write((byte)value);
            writer.Write((byte)value);
            writer.Write((byte)value);
            writer.Write((byte)0);
        }

        // Positive BMP heights store rows bottom-up, padded to four-byte boundaries.
        byte[] row = new byte[stride];
        for (int y = bitmap.Height - 1; y >= 0; y--)
        {
            var source = bitmap.Pixels.AsSpan(y * rowSize, rowSize);
            if (bitDepth == 8)
            {
                source.CopyTo(row);
            }
            else
            {
                // SimpleBitmap stores RGB or ARGB; BMP stores BGR or BGRA.
                for (int x = 0; x < rowSize; x += bytesPerPixel)
                {
                    int rgb = bitDepth == 32 ? x + 1 : x;
                    row[x] = source[rgb + 2];
                    row[x + 1] = source[rgb + 1];
                    row[x + 2] = source[rgb];
                    if (bitDepth == 32)
                    {
                        row[x + 3] = source[x];
                    }
                }
            }
            writer.Write(row);
        }
    }
}
