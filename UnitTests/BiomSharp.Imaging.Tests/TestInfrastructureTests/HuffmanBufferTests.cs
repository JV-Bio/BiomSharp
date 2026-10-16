using BiomSharp.Imaging.Wsq;
using BiomSharp.Imaging.Wsq.IO;
using System.Security.Cryptography;
using HuffmanEncoder = BiomSharp.Imaging.Wsq.Huffman.Encoder;

namespace BiomSharp.Imaging.Tests.TestInfrastructureTests;

public class HuffmanBufferTests
{
    // Captured before pooling quantized pixels and replacing Huffman work arrays.
    [Theory]
    [InlineData("Empty", "35956830DC0E1C6938923D39E417EBA774C6B059BB38A8A8DE026DA72F74DA51")]
    [InlineData("Escapes", "110E32FCF5AFDBEB7D2B91D866AE0AE4B2FB3A3FF29AB5A34DB78DB877897D7D")]
    [InlineData("LongRuns", "C8CADC20A20B7BC80D1017D8E00B1AE5679A84A62422863D390A12B70F51E68E")]
    [InlineData("DeepTree", "890AD9F695BDCC1090143F5AABA47DA6F8386366E89750A45B35647A01F4876C")]
    public void Huffman_tables_and_compressed_blocks_preserve_encoded_bytes(string scenario, string expectedHash)
    {
        // Arrange
        short[] pixels = CreatePixels(scenario);
        var image = new WsqPixelBuffer<short>(pixels.Length, 1, pixels);
        int firstSize = scenario == "DeepTree" ? pixels.Length : System.Math.Max(0, pixels.Length - 1024);
        int secondSize = (pixels.Length - firstSize) / 2;
        int[] sizes = { firstSize, secondSize, pixels.Length - firstSize - secondSize };
        using var stream = new MemoryStream();
        using var writer = new EndianBinaryWriter(stream);

        // Act
        var tables = HuffmanEncoder.Encode(image, sizes);
        foreach (var table in tables)
        {
            foreach (int bits in table.Bits.Span)
            {
                writer.Write(bits);
            }
            foreach (int value in table.Values.Span)
            {
                writer.Write(value);
            }
        }
        int offset = 0;
        for (int i = 0; i < sizes.Length; i++)
        {
            HuffmanEncoder.CompressBlock(writer, image, offset, sizes[i], tables[i == 0 ? 0 : 1].Codes);
            offset += sizes[i];
        }
        string hash = Convert.ToHexString(SHA256.HashData(stream.ToArray()));

        // Assert
        Assert.Equal(2, tables.Count);
        Assert.Equal(expectedHash, hash);
    }

    private static short[] CreatePixels(string scenario)
    {
        switch (scenario)
        {
            case "Empty":
                return Array.Empty<short>();
            case "Escapes":
                return new short[] { 0, -32768, -256, -255, -74, -73, -1, 0, 1, 74, 75, 255, 256, 32767, 0 };
            case "LongRuns":
                var runs = new short[70001];
                runs[^1] = 256;
                return runs;
            case "DeepTree":
                var pixels = new List<short>();
                int previous = 1, count = 1;
                for (short value = 1; value <= 20; value++)
                {
                    pixels.AddRange(Enumerable.Repeat(value, count));
                    (previous, count) = (count, previous + count);
                }
                return pixels.ToArray();
            default:
                throw new ArgumentOutOfRangeException(nameof(scenario));
        }
    }
}
