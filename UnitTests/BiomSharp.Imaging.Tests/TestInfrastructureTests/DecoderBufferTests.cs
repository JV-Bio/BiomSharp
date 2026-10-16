using BiomSharp.Imaging.Wsq;
using BiomSharp.Imaging.Wsq.IO;
using BiomSharp.Imaging.Wsq.Segment;
using WsqDecoder = BiomSharp.Imaging.Wsq.Decoder;

namespace BiomSharp.Imaging.Tests.TestInfrastructureTests;

public class DecoderBufferTests
{
    [Theory]
    [InlineData(64, 64, false, false)]
    [InlineData(65, 67, false, true)]
    [InlineData(64, 65, true, false)]
    [InlineData(65, 64, true, true)]
    public void Dirty_oversized_buffers_preserve_pixels_and_return_before_disposal(
        int width, int height, bool evenFilter, bool packed)
    {
        byte[] encoded = Encode(width, height, evenFilter, packed);
        int length = width * height;
        var integers = new TrackingArrayPool<int>(length, int.MinValue);
        var floats = new TrackingArrayPool<float>(length, float.NaN);
        SimpleBitmap expected = Decode(encoded,
            new TrackingArrayPool<int>(length, 0), new TrackingArrayPool<float>(length, 0));
        // Returning coefficients must happen before reconstruction; poison them to detect later reads.
        integers.OnReturn = () => Array.Fill(integers.Buffer, int.MaxValue);
        using var stream = new MemoryStream(encoded);
        using var reader = new EndianBinaryReader(stream);
        var segmenter = new Segmenter(reader);
        using var decoder = new WsqDecoder(segmenter, integers, floats);
        floats.OnReturn = () =>
        {
            Assert.Equal(1, integers.ReturnCount);
            Assert.NotNull(decoder.Decoded);
        };

        segmenter.EnumerateSegments();

        SimpleBitmap actual = Assert.IsType<SimpleBitmap>(decoder.Decoded);
        Assert.Equal(expected.Pixels, actual.Pixels);
        Assert.Equal(length, actual.Pixels.Length);
        Assert.Equal(width, actual.Width);
        Assert.Equal(height, actual.Height);
        Assert.Equal(1, integers.ReturnCount);
        Assert.Equal(1, floats.ReturnCount);
        Assert.True(integers.ClearRequested);
        Assert.True(floats.ClearRequested);
        Assert.True(integers.TailWasUnchanged);
        Assert.True(floats.TailWasUnchanged);
        Assert.All(floats.Buffer, value => Assert.Equal(0f, value));
        decoder.Dispose();
        Assert.Equal(1, integers.ReturnCount);
        Assert.Equal(1, floats.ReturnCount);
    }

    [Fact]
    public void Parsing_failure_after_Sof_returns_coefficients_and_unsubscribes()
    {
        byte[] encoded = Encode(65, 67);
        var integers = new TrackingArrayPool<int>(65 * 67, int.MinValue);
        var floats = new TrackingArrayPool<float>(65 * 67, float.NaN);
        using var stream = new MemoryStream(encoded);
        using var reader = new EndianBinaryReader(stream);
        var segmenter = new Segmenter(reader);
        var decoder = new WsqDecoder(segmenter, integers, floats);
        var failure = new IOException("Parsing interrupted after SOF");
        segmenter.SegmentRead += (_, segment) =>
        {
            if (segment.Marker == Marker.SOF)
            {
                Assert.All(integers.Buffer.Take(65 * 67), value => Assert.Equal(0, value));
                throw failure;
            }
        };

        Assert.Same(failure, Record.Exception(() =>
        {
            using (decoder)
            {
                segmenter.EnumerateSegments();
            }
        }));

        Assert.Equal(1, integers.ReturnCount);
        Assert.True(integers.ClearRequested);
        Assert.All(integers.Buffer, value => Assert.Equal(0, value));
        Assert.Equal(0, floats.RentCount);
        decoder.Dispose();
        Assert.Equal(1, integers.ReturnCount);
        // Re-enumeration must not invoke the disposed decoder or rent storage again.
        Assert.Same(failure, Record.Exception(segmenter.EnumerateSegments));
        Assert.Equal(1, integers.RentCount);
    }

    [Fact]
    public void Float_rental_failure_returns_coefficients()
    {
        var integers = new TrackingArrayPool<int>(65 * 67, int.MinValue);
        var floats = new TrackingArrayPool<float>(65 * 67, float.NaN);
        var failure = new IOException("Float rental failed");
        floats.OnRent = () => throw failure;

        Assert.Same(failure, Record.Exception(() => Decode(Encode(65, 67), integers, floats)));

        Assert.Equal(1, integers.ReturnCount);
        Assert.True(integers.ClearRequested);
        Assert.Equal(0, floats.ReturnCount);
    }

    [Fact]
    public void Coefficient_return_failure_still_returns_float_storage()
    {
        var integers = new TrackingArrayPool<int>(65 * 67, int.MinValue);
        var floats = new TrackingArrayPool<float>(65 * 67, float.NaN);
        var failure = new IOException("Coefficient return failed");
        integers.OnReturn = () => throw failure;

        Assert.Same(failure, Record.Exception(() => Decode(Encode(65, 67), integers, floats)));

        Assert.Equal(1, integers.ReturnCount);
        Assert.Equal(1, floats.ReturnCount);
        Assert.True(floats.ClearRequested);
        Assert.All(floats.Buffer, value => Assert.Equal(0f, value));
    }

    [Fact]
    public void Reused_buffers_do_not_change_previous_output()
    {
        var integers = new TrackingArrayPool<int>(65 * 67, int.MinValue);
        var floats = new TrackingArrayPool<float>(65 * 67, float.NaN);
        SimpleBitmap first = Decode(Encode(65, 67), integers, floats);
        byte[] saved = (byte[])first.Pixels.Clone();

        SimpleBitmap second = Decode(Encode(65, 67, true), integers, floats);

        Assert.Equal(saved, first.Pixels);
        Assert.NotSame(first.Pixels, second.Pixels);
        Assert.Equal(2, integers.ReturnCount);
        Assert.Equal(2, floats.ReturnCount);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Missing_quantization_or_transform_table_returns_both_rentals(bool includeQuantization)
    {
        using var stream = new MemoryStream();
        using var writer = new EndianBinaryWriter(stream);
        writer.Write((ushort)Marker.SOI);
        new Sof(67, 65, 0, 1, 0, 255, 2, 0).Write(writer);
        if (includeQuantization)
        {
            new Dqt(0.75f).Write(writer);
        }
        writer.Write((ushort)Marker.EOI);
        var integers = new TrackingArrayPool<int>(65 * 67, int.MinValue);
        var floats = new TrackingArrayPool<float>(65 * 67, float.NaN);

        Assert.Throws<WsqCodecException>(() => Decode(stream.ToArray(), integers, floats));

        Assert.Equal(1, integers.ReturnCount);
        Assert.Equal(1, floats.ReturnCount);
        Assert.True(integers.ClearRequested);
        Assert.True(floats.ClearRequested);
        Assert.All(integers.Buffer, value => Assert.Equal(0, value));
        Assert.All(floats.Buffer, value => Assert.Equal(0f, value));
    }

    [Fact]
    public void Unquantize_clears_inactive_subbands_without_touching_spare_capacity()
    {
        var quantizer = Quantizer.Create(new Sof(67, 65, 0, 1, 0, 255, 2, 0));
        var coefficients = new WsqPixelBuffer<int>(65, 67);
        var storage = Enumerable.Repeat(float.NaN, coefficients.Length + 29).ToArray();
        var image = new WsqPixelBuffer<float>(65, 67, storage);
        var dqt = new Dqt(0.75f);
        Array.Clear(dqt.DqtQ);

        quantizer.Unquantize(coefficients, dqt, image);

        Assert.All(storage.Take(image.Length), value => Assert.Equal(0f, value));
        Assert.All(storage.Skip(image.Length), value => Assert.True(float.IsNaN(value)));
        Assert.Throws<ArgumentException>(() =>
            quantizer.Unquantize(coefficients, dqt, new WsqPixelBuffer<float>(64, 67)));
        Assert.Throws<ArgumentException>(() =>
            quantizer.Unquantize(new WsqPixelBuffer<int>(64, 67), dqt, image));
    }

    private static SimpleBitmap Decode(byte[] encoded, TrackingArrayPool<int> integers,
        TrackingArrayPool<float> floats)
    {
        using var stream = new MemoryStream(encoded);
        using var reader = new EndianBinaryReader(stream);
        var segmenter = new Segmenter(reader);
        using var decoder = new WsqDecoder(segmenter, integers, floats);
        segmenter.EnumerateSegments();
        return Assert.IsType<SimpleBitmap>(decoder.Decoded);
    }

    private static byte[] Encode(int width, int height, bool evenFilter = false, bool packed = false)
    {
        byte[] pixels = Enumerable.Range(0, width * height)
            .Select(i => (byte)((i * 13 + i / width * 7) % 256)).ToArray();
        var codec = new WsqCodec();
        codec.FromRaw(SimpleBitmap.Gray8(pixels, width, height));
        return codec.Encode(new WsqParameters
        {
            Filter = evenFilter ? WsqFilterType.Even8x8 : WsqFilterType.Odd7x9,
            PackedDHT = packed
        })!;
    }
}
