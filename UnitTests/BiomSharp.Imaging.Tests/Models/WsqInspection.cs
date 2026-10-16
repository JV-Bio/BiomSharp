using BiomSharp.Imaging.Wsq;
using BiomSharp.Imaging.Wsq.Huffman;
using BiomSharp.Imaging.Wsq.IO;
using BiomSharp.Imaging.Wsq.Segment;
using HuffmanDecoder = BiomSharp.Imaging.Wsq.Huffman.Decoder;

namespace BiomSharp.Imaging.Tests.Models;

internal sealed record WsqInspection(WsqFrame Header, 
    long SizeWithoutComments, 
    decimal Center,
    decimal[] BinWidths, 
    decimal[] ZeroWidths, 
    int[] Coefficients, 
    int[] BlockCounts)
{
    public readonly static WsqInspection EmptyInspection = new WsqInspection(
                Header: new WsqFrame(0, 0, 0, 0, 0, 0, 0, 0),
                SizeWithoutComments: 0,
                Center: 0,
                BinWidths: Array.Empty<decimal>(),
                ZeroWidths: Array.Empty<decimal>(),
                Coefficients: Array.Empty<int>(),
                BlockCounts: Array.Empty<int>()); 



    private static T Single<T>(IEnumerable<BaseSegment> segments) where T : BaseSegment
    {
        T[] matches = segments.OfType<T>().ToArray();
        Require(matches.Length == 1, $"Expected exactly one {typeof(T).Name} segment.");
        return matches[0];
    }

    private static decimal ReadDecimal(EndianBinaryReader reader)
    {
        byte exponent = reader.ReadByte();
        decimal value = reader.ReadUInt16();
        Require(exponent <= 28, "Decimal exponent outside the comparator's supported precision.");
        for (int i = 0; i < exponent; i++)
        {
            value /= 10m;
        }
        return value;
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidDataException(message);
        }
    }

    public static WsqInspection Read(byte[] bytes)
    {        
        // Check the data length. 
        // If the data is 0 long the inner WsqReader will keep looping.
        if (bytes.Length == 0)
        {
            return EmptyInspection;
        }

        using var stream = new MemoryStream(bytes, writable: false);
        using var reader = new EndianBinaryReader(stream);
        var segmenter = new Segmenter(reader);
        var decoder = HuffmanDecoder.Create();
        WsqPixelBuffer<int>? coefficients = null;
        var blockCounts = new List<int>();
        int decodedCount = 0;
        long nextPosition = 0;
        segmenter.SegmentRead += (_, segment) =>
        {
            Require(segment.Position == nextPosition, "Unexpected bytes between WSQ segments.");
            if (segment is Sof frame)
            {
                Require(coefficients == null, "Multiple frame headers are not supported.");
                Require(frame.X > 0 && frame.Y > 0, "Invalid frame dimensions.");
                coefficients = new WsqPixelBuffer<int>(frame.X, frame.Y);
                // Detect missing coefficients rather than interpreting an unwritten buffer tail as zeros.
                Array.Fill(coefficients.Pixels, int.MinValue);
            }
            if (segment is Sob block)
            {
                Require(coefficients != null, "Entropy block precedes the frame header.");
                var buffer = coefficients ?? throw new InvalidDataException("Missing coefficient buffer.");
                DhtTable table = segmenter.DhtTableWithId(block.Td);
                decoder.Decode(reader, buffer, table, table.GetDecodingTable());
                int start = decodedCount;
                while (decodedCount < buffer.Pixels.Length && buffer.Pixels[decodedCount] != int.MinValue)
                {
                    decodedCount++;
                }
                blockCounts.Add(decodedCount - start);
            }
            nextPosition = stream.Position;
        };

        segmenter.EnumerateSegments();
        BaseSegment[] segments = segmenter.AllSegments().ToArray();
        Require(segments[0].Marker == Marker.SOI && segments[^1].Marker == Marker.EOI,
            "WSQ must start with SOI and end with EOI.");
        Require(stream.Position == stream.Length, "Trailing bytes after EOI.");
        Require(segments.All(s => s.Marker is Marker.SOI or Marker.SOF or Marker.DTT or Marker.DQT
            or Marker.DHT or Marker.SOB or Marker.COM or Marker.EOI),
            "Unsupported segment in the prescribed encoder output.");
        Sof sof = Single<Sof>(segments);
        Dqt dqt = Single<Dqt>(segments);
        Dtt dtt = Single<Dtt>(segments);
        Require(blockCounts.Count == 3, "Expected three entropy blocks.");
        Require(sof.ContentSize == 15 && dqt.ContentSize == 387, "Invalid SOF or DQT segment length.");

        // Read serialized decimal values exactly; float conversions can distort tolerance boundaries.
        stream.Position = dqt.Position + 4;
        decimal center = ReadDecimal(reader);
        var widths = new decimal[64];
        var zeros = new decimal[64];
        for (int i = 0; i < widths.Length; i++)
        {
            widths[i] = ReadDecimal(reader);
            zeros[i] = ReadDecimal(reader);
        }
        var tree = Quantizer.Create(sof).QTree ?? throw new InvalidDataException("Missing quantization tree.");
        var expectedBlocks = new int[3];
        for (int i = 0; i < WsqRequirementsEncoding.ComparedSubbandCount; i++)
        {
            if (widths[i] != 0)
            {
                expectedBlocks[i < 19 ? 0 : i < 52 ? 1 : 2] += tree[i].LenX * tree[i].LenY;
            }
        }
        Require(blockCounts.SequenceEqual(expectedBlocks),
            $"Coefficient block counts [{string.Join(", ", blockCounts)}] differ from expected [{string.Join(", ", expectedBlocks)}].");
        Require(widths.Skip(WsqRequirementsEncoding.ComparedSubbandCount).All(v => v == 0) &&
            zeros.Skip(WsqRequirementsEncoding.ComparedSubbandCount).All(v => v == 0),
            $"Subbands {WsqRequirementsEncoding.ComparedSubbandCount} through 63 must be discarded.");
        int[] indices = (coefficients ?? throw new InvalidDataException("Missing coefficients."))
            .Pixels.Take(decodedCount).ToArray();
        Require(indices.Length > 0, "No quantized coefficients found.");
        long commentBytes = segments.OfType<Com>().Sum(c => (long)c.ContentSize + 4);
        return new WsqInspection(
            new WsqFrame(sof.X, sof.Y, sof.A, sof.B, sof.Ev, sof.Sf, dtt.L0, dtt.L1),
            bytes.LongLength - commentBytes, center, widths.Take(WsqRequirementsEncoding.ComparedSubbandCount).ToArray(),
            zeros.Take(WsqRequirementsEncoding.ComparedSubbandCount).ToArray(), indices, blockCounts.ToArray());
    }
}
