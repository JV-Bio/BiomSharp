using BiomSharp.Imaging.Wsq;
using BiomSharp.Imaging.Wsq.IO;
using System.Buffers;

namespace BiomSharp.Imaging.Tests.TestInfrastructureTests;

public class EncoderBufferTests
{
    public static IEnumerable<object[]> EncodingCases()
    {
        var dimensions = new[] { (64, 64), (65, 64), (64, 65), (65, 67) };
        foreach (var (width, height) in dimensions)
        {
            foreach (string filter in new[] { "7x9", "8x8" })
            {
                foreach (float bitrate in new[]
                {
                    WsqParameters.BitRateMinValue, new WsqParameters().BitRate, WsqParameters.BitRateMaxValue
                })
                {
                    yield return new object[] { width, height, filter, bitrate };
                }
            }
        }
    }

    [Theory]
    [MemberData(nameof(EncodingCases))]
    public void Encode_with_dirty_oversized_float_storage_matches_zero_initialized_storage(
        int width, int height, string filter, float bitrate)
    {
        // Arrange
        SimpleBitmap image = CreateImage(width, height);
        var pool = new TrackingArrayPool<float>(width * height, float.NaN);
        byte[] expected = Encode(image, new TrackingArrayPool<float>(width * height, 0f), filter, bitrate);

        // Act
        byte[] actual = Encode(image, pool, filter, bitrate);

        // Assert
        Assert.NotEmpty(actual);
        Assert.Equal(expected, actual);
        Assert.Equal(width * height, pool.RequestedLength);
        Assert.True(pool.Buffer.Length > width * height);
        Assert.True(pool.TailWasUnchanged);
        Assert.Equal(1, pool.RentCount);
        Assert.Equal(1, pool.ReturnCount);
        Assert.True(pool.ClearRequested);
        Assert.All(pool.Buffer, value => Assert.Equal(0f, value));
    }

    [Theory]
    [InlineData("7x9")]
    [InlineData("8x8")]
    public void Encode_with_short_input_preserves_the_zero_filled_image_remainder(string filter)
    {
        // Arrange
        SimpleBitmap complete = CreateImage(65, 67);
        var image = SimpleBitmap.Gray8(complete.Pixels[..^17], complete.Width, complete.Height);
        int length = image.Width * image.Height;
        var pool = new TrackingArrayPool<float>(length, float.NaN);
        byte[] expected = Encode(image, new TrackingArrayPool<float>(length, 0f), filter);

        // Act
        byte[] actual = Encode(image, pool, filter);

        // Assert
        Assert.NotEmpty(actual);
        Assert.Equal(expected, actual);
        Assert.True(pool.TailWasUnchanged);
        Assert.Equal(1, pool.ReturnCount);
        Assert.True(pool.ClearRequested);
    }

    [Fact]
    public void Encode_returns_and_clears_float_storage_when_normalization_throws()
    {
        // Arrange
        var image = SimpleBitmap.Gray8(new byte[65 * 67 + 1], 65, 67);
        var pool = new TrackingArrayPool<float>(image.Width * image.Height, float.NaN);
        var shorts = new TrackingArrayPool<short>(image.Width * image.Height, short.MinValue);

        // Act
        Exception? error = Record.Exception(() => Encode(image, pool, shortPool: shorts));

        // Assert
        Assert.IsType<IndexOutOfRangeException>(error);
        Assert.Equal(1, pool.RentCount);
        Assert.Equal(1, pool.ReturnCount);
        Assert.True(pool.TailWasUnchanged);
        Assert.True(pool.ClearRequested);
        Assert.All(pool.Buffer, value => Assert.Equal(0f, value));
        Assert.Equal(0, shorts.RentCount);
        Assert.Equal(0, shorts.ReturnCount);
    }

    [Fact]
    public void Encode_does_not_use_returned_float_storage_during_Huffman_encoding()
    {
        // Arrange
        SimpleBitmap image = CreateImage(65, 67);
        int length = image.Width * image.Height;
        byte[] expected = Encode(image, new TrackingArrayPool<float>(length, 0f));
        var pool = new TrackingArrayPool<float>(length, float.NaN);
        using var stream = new MemoryStream();
        using var writer = new EndianBinaryWriter(stream);
        var encoder = new Encoder(new Segmenter(writer), pool);
        long? positionAtReturn = null;
        pool.OnReturn = () =>
        {
            positionAtReturn = stream.Position;
            Array.Fill(pool.Buffer, float.NaN);
        };

        // Act
        encoder.Encode(image, false, 0.75f, 0, 255, "7x9", null, null, false);
        byte[] actual = stream.ToArray();

        // Assert
        Assert.Equal(expected, actual);
        Assert.Equal(0L, positionAtReturn);
        Assert.Equal(1, pool.ReturnCount);
        Assert.True(pool.ClearRequested);
    }

    [Fact]
    public void Encode_returns_float_storage_before_an_output_failure()
    {
        // Arrange
        SimpleBitmap image = CreateImage(65, 67);
        var pool = new TrackingArrayPool<float>(image.Width * image.Height, float.NaN);
        using var stream = new MemoryStream();
        using var writer = new EndianBinaryWriter(stream);
        var segmenter = new Segmenter(writer);
        var expectedError = new IOException("Output failed.");
        segmenter.SegmentWrite += (_, _) => throw expectedError;
        var encoder = new Encoder(segmenter, pool);

        // Act
        Exception? error = Record.Exception(() =>
            encoder.Encode(image, false, 0.75f, 0, 255, "7x9", null, null, false));

        // Assert
        Assert.Same(expectedError, error);
        Assert.Equal(1, pool.ReturnCount);
        Assert.True(pool.ClearRequested);
        Assert.All(pool.Buffer, value => Assert.Equal(0f, value));
    }

    [Fact]
    public void Encode_reuses_float_storage_without_changing_earlier_output()
    {
        // Arrange
        SimpleBitmap first = CreateImage(65, 67);
        SimpleBitmap second = CreateImage(65, 67);
        Array.Reverse(second.Pixels);
        var pool = new TrackingArrayPool<float>(first.Width * first.Height, float.NaN);
        byte[] firstOutput = Encode(first, pool);
        byte[] savedOutput = firstOutput.ToArray();
        byte[] expectedSecond = Encode(second, new TrackingArrayPool<float>(second.Width * second.Height, 0f));

        // Act
        byte[] secondOutput = Encode(second, pool);

        // Assert
        Assert.Equal(savedOutput, firstOutput);
        Assert.Equal(expectedSecond, secondOutput);
        Assert.Equal(2, pool.RentCount);
        Assert.Equal(2, pool.ReturnCount);
        Assert.True(pool.TailWasUnchanged);
        Assert.True(pool.ClearRequested);
    }

    [Fact]
    public async Task Encode_shared_pool_supports_independent_concurrent_images()
    {
        // Arrange
        SimpleBitmap[] images = { CreateImage(64, 65), CreateImage(65, 67), CreateImage(128, 129) };
        byte[][] expected = images.Select(image =>
            Encode(image, new TrackingArrayPool<float>(image.Width * image.Height, 0f))).ToArray();

        // Act
        byte[][] actual = await Task.WhenAll(images.Select(image => Task.Run(() => Encode(image))));

        // Assert
        for (int i = 0; i < expected.Length; i++)
        {
            Assert.Equal(expected[i], actual[i]);
        }
    }

    [Theory]
    [MemberData(nameof(EncodingCases))]
    public void Encode_with_dirty_oversized_short_storage_matches_zero_initialized_storage(
        int width, int height, string filter, float bitrate)
    {
        // Arrange
        SimpleBitmap image = CreateImage(width, height);
        var pool = new TrackingArrayPool<short>(width * height, short.MinValue);
        byte[] expected = Encode(image, filter: filter, bitrate: bitrate,
            shortPool: new TrackingArrayPool<short>(width * height, 0));

        // Act
        byte[] actual = Encode(image, filter: filter, bitrate: bitrate, shortPool: pool);

        // Assert
        Assert.NotEmpty(actual);
        Assert.Equal(expected, actual);
        Assert.Equal(width * height, pool.RequestedLength);
        Assert.True(pool.TailWasUnchanged);
        Assert.Equal(1, pool.RentCount);
        Assert.Equal(1, pool.ReturnCount);
        Assert.True(pool.ClearRequested);
        Assert.All(pool.Buffer, value => Assert.Equal((short)0, value));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Encode_keeps_short_storage_until_all_blocks_are_written(bool packedDht)
    {
        // Arrange
        SimpleBitmap image = CreateImage(65, 67);
        var pool = new TrackingArrayPool<short>(image.Width * image.Height, short.MinValue);
        byte[] expected = Encode(image, packedDht: packedDht,
            shortPool: new TrackingArrayPool<short>(image.Width * image.Height, 0));
        using var stream = new MemoryStream();
        using var writer = new EndianBinaryWriter(stream);
        var segmenter = new Segmenter(writer);
        var encoder = new Encoder(segmenter, ArrayPool<float>.Shared, pool);
        int returnsDuringWriting = 0;
        int blocksWritten = 0;
        bool endWritten = false, endWrittenAtReturn = false;
        segmenter.SegmentWrite += (_, segment) =>
        {
            returnsDuringWriting += pool.ReturnCount;
            if (segment.Marker == Marker.SOB)
            {
                blocksWritten++;
            }
            endWritten = segment.Marker == Marker.EOI;
        };
        pool.OnReturn = () =>
        {
            endWrittenAtReturn = endWritten;
            Array.Fill(pool.Buffer, short.MaxValue);
        };

        // Act
        encoder.Encode(image, false, 0.75f, 0, 255, "7x9", null, null, packedDht);

        // Assert
        Assert.Equal(expected, stream.ToArray());
        Assert.Equal(3, blocksWritten);
        Assert.Equal(0, returnsDuringWriting);
        Assert.True(endWrittenAtReturn);
        Assert.Equal(1, pool.ReturnCount);
        Assert.True(pool.ClearRequested);
    }

    [Fact]
    public void Encode_returns_float_storage_when_short_rental_fails()
    {
        // Arrange
        SimpleBitmap image = CreateImage(65, 67);
        int length = image.Width * image.Height;
        var floats = new TrackingArrayPool<float>(length, float.NaN);
        var shorts = new TrackingArrayPool<short>(length, short.MinValue);
        var expectedError = new InvalidOperationException("Short rental failed.");
        shorts.OnRent = () => throw expectedError;

        // Act
        Exception? error = Record.Exception(() => Encode(image, floats, shortPool: shorts));

        // Assert
        Assert.Same(expectedError, error);
        Assert.Equal(1, floats.ReturnCount);
        Assert.True(floats.ClearRequested);
        Assert.All(floats.Buffer, value => Assert.Equal(0f, value));
        Assert.Equal(0, shorts.ReturnCount);
    }

    [Fact]
    public void Encode_returns_short_storage_when_float_return_fails()
    {
        // Arrange
        SimpleBitmap image = CreateImage(65, 67);
        int length = image.Width * image.Height;
        var floats = new TrackingArrayPool<float>(length, float.NaN);
        var shorts = new TrackingArrayPool<short>(length, short.MinValue);
        var expectedError = new InvalidOperationException("Float return failed.");
        floats.OnReturn = () => throw expectedError;

        // Act
        Exception? error = Record.Exception(() => Encode(image, floats, shortPool: shorts));

        // Assert
        Assert.Same(expectedError, error);
        Assert.Equal(1, floats.ReturnCount);
        Assert.Equal(1, shorts.ReturnCount);
        Assert.True(shorts.ClearRequested);
        Assert.All(shorts.Buffer, value => Assert.Equal((short)0, value));
    }

    [Fact]
    public void Encode_returns_and_clears_short_storage_when_block_writing_fails()
    {
        // Arrange
        SimpleBitmap image = CreateImage(65, 67);
        var pool = new TrackingArrayPool<short>(image.Width * image.Height, short.MinValue);
        var expectedError = new IOException("Block output failed.");
        using var stream = new FailingWriteStream(expectedError);
        using var writer = new EndianBinaryWriter(stream);
        var segmenter = new Segmenter(writer);
        segmenter.SegmentWrite += (_, segment) =>
        {
            if (segment.Marker == Marker.SOB)
            {
                stream.FailWrites = true;
            }
        };
        var encoder = new Encoder(segmenter, ArrayPool<float>.Shared, pool);

        // Act
        Exception? error = Record.Exception(() =>
            encoder.Encode(image, false, 0.75f, 0, 255, "7x9", null, null, false));

        // Assert
        Assert.Same(expectedError, error);
        Assert.Equal(1, pool.ReturnCount);
        Assert.True(pool.ClearRequested);
        Assert.All(pool.Buffer, value => Assert.Equal((short)0, value));
    }

    [Fact]
    public void Encode_does_not_leave_returned_short_storage_accessible_to_block_callbacks()
    {
        // Arrange
        SimpleBitmap image = CreateImage(65, 67);
        var pool = new TrackingArrayPool<short>(image.Width * image.Height, short.MinValue);
        using var stream = new MemoryStream();
        using var writer = new EndianBinaryWriter(stream);
        var segmenter = new Segmenter(writer);
        var encoder = new Encoder(segmenter, ArrayPool<float>.Shared, pool);
        encoder.Encode(image, false, 0.75f, 0, 255, "7x9", null, null, false);
        Array.Fill(pool.Buffer, short.MaxValue);

        // Act
        Exception? error = Record.Exception(() => segmenter.WriteAll());

        // Assert
        Assert.IsType<WsqCodecException>(error);
        Assert.Contains("Encode quantizer is null or invalid", error!.Message);
        Assert.Equal(1, pool.ReturnCount);
    }

    [Fact]
    public void Encode_reuses_short_storage_for_a_different_image()
    {
        // Arrange
        SimpleBitmap first = CreateImage(65, 67);
        SimpleBitmap second = CreateImage(65, 67);
        Array.Reverse(second.Pixels);
        var pool = new TrackingArrayPool<short>(first.Width * first.Height, short.MinValue);
        byte[] firstOutput = Encode(first, shortPool: pool);
        byte[] savedOutput = firstOutput.ToArray();
        byte[] expectedSecond = Encode(second,
            shortPool: new TrackingArrayPool<short>(second.Width * second.Height, 0));

        // Act
        byte[] actualSecond = Encode(second, shortPool: pool);

        // Assert
        Assert.Equal(savedOutput, firstOutput);
        Assert.Equal(expectedSecond, actualSecond);
        Assert.Equal(2, pool.RentCount);
        Assert.Equal(2, pool.ReturnCount);
        Assert.True(pool.ClearRequested);
        Assert.True(pool.TailWasUnchanged);
    }

    [Theory]
    [InlineData((byte)0)]
    [InlineData((byte)128)]
    [InlineData((byte)255)]
    public void Encode_flat_image_does_not_read_unused_quantized_storage(byte value)
    {
        // Arrange
        var image = SimpleBitmap.Gray8(Enumerable.Repeat(value, 64 * 65).ToArray(), 64, 65);
        var pool = new TrackingArrayPool<short>(64 * 65, short.MinValue);
        byte[] expected = Encode(image, shortPool: new TrackingArrayPool<short>(64 * 65, 0));

        // Act
        byte[] actual = Encode(image, shortPool: pool);

        // Assert
        Assert.NotEmpty(actual);
        Assert.Equal(expected, actual);
        Assert.True(pool.TailWasUnchanged);
        Assert.Equal(1, pool.ReturnCount);
        Assert.True(pool.ClearRequested);
    }

    [Fact]
    public void Write_to_stream_matches_Encode_output()
    {
        // Arrange
        var codec = new WsqCodec();
        codec.FromRaw(CreateImage(65, 67));
        var parameters = new WsqParameters();
        byte[]? expected = codec.Encode(parameters);
        using var stream = new MemoryStream();

        // Act
        codec.Write(stream, parameters);

        // Assert
        Assert.NotNull(expected);
        Assert.Equal(expected, stream.ToArray());
        Assert.True(stream.CanWrite);
    }

    [Fact]
    public void Quantize_rejects_mismatched_destination_dimensions()
    {
        // Arrange
        var image = new WsqPixelBuffer<float>(65, 67);
        var destination = new WsqPixelBuffer<short>(65, 66);
        var quantizer = Quantizer.Create(image.Width, image.Height);
        var dqt = new Wsq.Segment.Dqt(0.75f);

        // Act
        Exception? error = Record.Exception(() => quantizer.Quantize(out _, image, dqt, destination));

        // Assert
        var argumentError = Assert.IsType<ArgumentException>(error);
        Assert.Equal("sImage", argumentError.ParamName);
    }

    private sealed class FailingWriteStream : MemoryStream
    {
        private readonly IOException error;
        internal bool FailWrites { get; set; }

        internal FailingWriteStream(IOException error)
        {
            this.error = error;
        }

        public override void WriteByte(byte value)
        {
            if (FailWrites)
            {
                throw error;
            }
            base.WriteByte(value);
        }
    }

    private static SimpleBitmap CreateImage(int width, int height)
    {
        var pixels = new byte[width * height];
        for (int i = 0; i < pixels.Length; i++)
        {
            pixels[i] = (byte)((i * 13 + i / width * 7) % 256);
        }
        return SimpleBitmap.Gray8(pixels, width, height);
    }

    private static byte[] Encode(SimpleBitmap image, ArrayPool<float>? pool = null,
        string filter = "7x9", float bitrate = 0.75f, ArrayPool<short>? shortPool = null, bool packedDht = false)
    {
        using var stream = new MemoryStream();
        using var writer = new EndianBinaryWriter(stream);
        var segmenter = new Segmenter(writer);
        var encoder = shortPool != null
            ? new Encoder(segmenter, pool ?? ArrayPool<float>.Shared, shortPool)
            : pool == null ? new Encoder(segmenter) : new Encoder(segmenter, pool);
        encoder.Encode(image, false, bitrate, 0, 255, filter, null, null, packedDht);
        return stream.ToArray();
    }
}
