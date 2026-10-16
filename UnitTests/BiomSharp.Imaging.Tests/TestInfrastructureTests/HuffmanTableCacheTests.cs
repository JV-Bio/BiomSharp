using BiomSharp.Imaging.Wsq;
using BiomSharp.Imaging.Wsq.Huffman;
using BiomSharp.Imaging.Wsq.IO;
using BiomSharp.Imaging.Wsq.Segment;

namespace BiomSharp.Imaging.Tests.TestInfrastructureTests;

public class HuffmanTableCacheTests
{
    [Theory]
    [InlineData("Empty")]
    [InlineData("Mixed")]
    [InlineData("SixteenBits")]
    public void Byte_counts_produce_the_same_canonical_codes_as_integer_counts(string scenario)
    {
        int[] counts = CreateCounts(scenario);
        byte[] bytes = counts.Select(value => (byte)value).ToArray();

        CodeEntries expected = CodeEntries.Create(counts);
        CodeEntries actual = CodeEntries.Create(bytes.AsSpan());

        Assert.Equal(expected.Size, actual.Size);
        Assert.Equal(expected.Codes, actual.Codes);
        Assert.Equal(expected.CheckAllOnes(), actual.CheckAllOnes());
    }

    [Fact]
    public void Repeated_lookup_access_reuses_the_table_without_allocating()
    {
        var table = new DhtTable(0, CreateCounts("Mixed"), new[] { 179, 180, 181 });
        Table expected = table.GetDecodingTable();
        Table actual = expected;

        long before = GC.GetAllocatedBytesForCurrentThread();
        for (int i = 0; i < 1000; i++)
        {
            actual = table.GetDecodingTable();
        }
        long allocated = GC.GetAllocatedBytesForCurrentThread() - before;

        Assert.Same(expected, actual);
        Assert.Equal(0L, allocated);
        Assert.NotNull(actual.Entries);
        Assert.Equal(0, actual.Entries![1].Min);
        Assert.Equal(0, actual.Entries[1].Max);
        Assert.Equal(2, actual.Entries[2].Min);
        Assert.Equal(2, actual.Entries[2].Max);
        Assert.Equal(1, actual.Entries[2].ValIndex);
        Assert.Equal(6, actual.Entries[3].Min);
        Assert.Equal(6, actual.Entries[3].Max);
        Assert.Equal(2, actual.Entries[3].ValIndex);
        Assert.Equal(-1, actual.Entries[4].Max);
    }

    [Fact]
    public void Tables_with_the_same_id_do_not_share_cached_lookups()
    {
        var first = new DhtTable(1, CreateCounts("Mixed"), new[] { 179, 180, 181 });
        var second = new DhtTable(1, CreateCounts("SixteenBits"), new[] { 180 });

        Table firstLookup = first.GetDecodingTable();
        Table secondLookup = second.GetDecodingTable();

        Assert.NotSame(firstLookup, secondLookup);
        Assert.Equal(0, firstLookup.Entries![1].Max);
        Assert.Equal(-1, secondLookup.Entries![1].Max);
        Assert.Equal(0, secondLookup.Entries[16].Max);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Packed_and_separate_Dht_segments_preserve_counts_values_and_lookups(bool packed)
    {
        var first = new Dht(0, CreateCounts("Mixed"), new[] { 179, 180, 181 });
        var second = new Dht(1, CreateCounts("SixteenBits"), new[] { 180 });
        var originals = new[] { first.Tables[0], second.Tables[0] };
        if (packed)
        {
            first.AddTable(originals[1]);
        }
        using var stream = new MemoryStream();
        using var writer = new EndianBinaryWriter(stream);
        writer.Write((ushort)Marker.SOI);
        first.Write(writer);
        if (!packed)
        {
            second.Write(writer);
        }
        writer.Write((ushort)Marker.EOI);
        stream.Position = 0;
        using var reader = new EndianBinaryReader(stream);
        var segmenter = new Segmenter(reader);

        segmenter.EnumerateSegments();

        DhtTable[] parsed = segmenter.Segments<Dht>().SelectMany(dht => dht.Tables).ToArray();
        Assert.Equal(2, parsed.Length);
        Assert.Equal(stream.Length, stream.Position);
        for (int i = 0; i < parsed.Length; i++)
        {
            Assert.Equal(originals[i].Th, parsed[i].Th);
            Assert.Equal(originals[i].CodeCount, parsed[i].CodeCount);
            Assert.Equal(originals[i].SerializeLength, parsed[i].SerializeLength);
            Assert.Equal(originals[i].L.ToArray(), parsed[i].L.ToArray());
            Assert.Equal(originals[i].V.ToArray(), parsed[i].V.ToArray());

            long before = GC.GetAllocatedBytesForCurrentThread();
            Table lookup = parsed[i].GetDecodingTable();
            long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
            Assert.Equal(0L, allocated);
            Assert.Same(lookup, parsed[i].GetDecodingTable());
            Assert.Equal(originals[i].GetDecodingTable().Entries, lookup.Entries);
        }
    }

    [Fact]
    public void Parsing_still_rejects_counts_that_exceed_the_declared_segment_size()
    {
        using var stream = new MemoryStream();
        using var writer = new EndianBinaryWriter(stream);
        writer.Write((ushort)Marker.DHT);
        writer.Write((ushort)(sizeof(ushort) + DhtTable.ZeroCodeSize));
        writer.Write((byte)0);
        writer.Write((byte)1);
        writer.Write(new byte[DhtTable.MaxHuffBits - 1]);
        stream.Position = 0;
        using var reader = new EndianBinaryReader(stream);

        var error = Assert.Throws<WsqCodecException>(() => BaseSegment.CreateRead(reader));

        Assert.Equal("Huffman code count exceeds reported length in DHT header", error.Message);
    }

    private static int[] CreateCounts(string scenario)
    {
        var counts = new int[DhtTable.MaxHuffBits];
        switch (scenario)
        {
            case "Empty":
                break;
            case "Mixed":
                counts[0] = 1;
                counts[1] = 1;
                counts[2] = 1;
                break;
            case "SixteenBits":
                counts[15] = 1;
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(scenario));
        }
        return counts;
    }
}
