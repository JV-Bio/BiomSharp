using BiomSharp.Imaging.Wsq;
using BiomSharp.Imaging.Wsq.IO;
using BiomSharp.Imaging.Wsq.Segment;
using HuffmanDecoder = BiomSharp.Imaging.Wsq.Huffman.Decoder;

namespace BiomSharp.Imaging.Tests.TestInfrastructureTests;

public class HuffmanZeroRunTests
{
    [Theory]
    [InlineData(1, 1)]
    [InlineData(100, 100)]
    [InlineData(105, 255)]
    [InlineData(106, 65535)]
    public void Zero_run_can_exactly_fill_remaining_logical_pixels(int symbol, int runLength)
    {
        int length = runLength + 1;
        int[] storage = Enumerable.Repeat(-7, length + 29).ToArray();
        var image = new WsqPixelBuffer<int>(length, 1, storage);
        var decoder = HuffmanDecoder.Create();
        DecodeRun(decoder, image, 1, 1);

        DecodeRun(decoder, image, symbol, runLength);

        Assert.All(storage.Take(length), value => Assert.Equal(0, value));
        Assert.All(storage.Skip(length), value => Assert.Equal(-7, value));
    }

    [Theory]
    [InlineData(100, 100)]
    [InlineData(105, 255)]
    [InlineData(106, 65535)]
    public void Oversized_zero_run_throws_codec_error_without_partial_writes_or_advancing_position(
        int symbol, int runLength)
    {
        // The run would fit in the backing array, but not in the remaining logical image.
        int[] storage = Enumerable.Repeat(-7, runLength + 29).ToArray();
        var image = new WsqPixelBuffer<int>(runLength, 1, storage);
        var decoder = HuffmanDecoder.Create();
        DecodeRun(decoder, image, 1, 1);

        var error = Assert.Throws<WsqCodecException>(() => DecodeRun(decoder, image, symbol, runLength));

        Assert.Equal("Huffman zero run exceeds the decoded image size", error.Message);
        Assert.Equal(0, storage[0]);
        Assert.All(storage.Skip(1), value => Assert.Equal(-7, value));

        DecodeRun(decoder, image, 1, 1);
        Assert.Equal(0, storage[1]);
        Assert.All(storage.Skip(2), value => Assert.Equal(-7, value));
    }

    private static void DecodeRun(HuffmanDecoder decoder, WsqPixelBuffer<int> image, int symbol, int runLength)
    {
        var counts = new int[DhtTable.MaxHuffBits];
        counts[0] = 1;
        var table = new DhtTable(0, counts, new[] { symbol });
        int payloadBits = symbol == 105 ? 8 : symbol == 106 ? 16 : 0;
        string bits = "0";
        if (payloadBits != 0)
        {
            bits += Convert.ToString(runLength, 2).PadLeft(payloadBits, '0');
        }
        bits = bits.PadRight((bits.Length + 7) / 8 * 8, '1');
        using var stream = new MemoryStream();
        using var writer = new EndianBinaryWriter(stream);
        for (int i = 0; i < bits.Length; i += 8)
        {
            byte value = Convert.ToByte(bits.Substring(i, 8), 2);
            writer.Write(value);
            if (value == 0xFF)
            {
                writer.Write((byte)0);
            }
        }
        writer.Write((ushort)Marker.EOI);
        stream.Position = 0;
        using var reader = new EndianBinaryReader(stream);
        decoder.Decode(reader, image, table, table.GetDecodingTable());
    }
}
