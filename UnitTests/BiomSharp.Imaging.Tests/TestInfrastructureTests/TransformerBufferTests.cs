using BiomSharp.Imaging.Wsq;
using BiomSharp.Imaging.Wsq.Tree;

namespace BiomSharp.Imaging.Tests.TestInfrastructureTests;

public class TransformerBufferTests
{
    [Theory]
    [InlineData(64, 64, "7x9")]
    [InlineData(65, 64, "7x9")]
    [InlineData(64, 65, "7x9")]
    [InlineData(65, 67, "7x9")]
    [InlineData(127, 129, "7x9")]
    [InlineData(64, 64, "8x8")]
    [InlineData(65, 64, "8x8")]
    [InlineData(64, 65, "8x8")]
    [InlineData(65, 67, "8x8")]
    [InlineData(127, 129, "8x8")]
    public void Decompose_with_dirty_oversized_scratch_matches_zero_initialized_scratch(
        int width, int height, string filterName)
    {
        // Arrange
        var image = CreateImage(width, height);
        var expected = CreateImage(width, height);
        Filter filter = Filter.Create(filterName);
        var cleanPool = new TrackingArrayPool<float>(image.Length, 0f);
        var dirtyPool = new TrackingArrayPool<float>(image.Length, float.NaN);
        Transformer.Decompose(expected, Quantizer.Create(width, height), filter, cleanPool);

        // Act
        Transformer.Decompose(image, Quantizer.Create(width, height), filter, dirtyPool);

        // Assert
        Assert.Equal(Bits(expected), Bits(image));
        Assert.All(image.Pixels, value => Assert.True(float.IsFinite(value)));
        Assert.Equal(image.Length, dirtyPool.RequestedLength);
        Assert.True(dirtyPool.Buffer.Length > image.Length);
        Assert.True(dirtyPool.TailWasUnchanged);
        Assert.Equal(1, dirtyPool.RentCount);
        Assert.Equal(1, dirtyPool.ReturnCount);
        Assert.True(dirtyPool.ClearRequested);
        Assert.All(dirtyPool.Buffer, value => Assert.Equal(0f, value));
    }

    [Fact]
    public void Decompose_returns_and_clears_scratch_when_the_transform_throws()
    {
        // Arrange
        var image = CreateImage(65, 67);
        var quantizer = Quantizer.Create(image.Width, image.Height);
        quantizer.WTree![0].X = image.Length;
        var pool = new TrackingArrayPool<float>(image.Length, float.NaN);

        // Act
        Exception? error = Record.Exception(() => Transformer.Decompose(image, quantizer, Filter.Odd7x9, pool));

        // Assert
        Assert.IsType<IndexOutOfRangeException>(error);
        Assert.Equal(1, pool.RentCount);
        Assert.Equal(1, pool.ReturnCount);
        Assert.True(pool.ClearRequested);
        Assert.All(pool.Buffer, value => Assert.Equal(0f, value));
    }

    [Fact]
    public void Decompose_reuses_scratch_without_changing_an_earlier_image()
    {
        // Arrange
        var first = CreateImage(65, 67);
        var second = CreateImage(65, 67);
        var pool = new TrackingArrayPool<float>(first.Length, float.NaN);
        Transformer.Decompose(first, Quantizer.Create(first.Width, first.Height), Filter.Odd7x9, pool);
        int[] firstBits = Bits(first);

        // Act
        Transformer.Decompose(second, Quantizer.Create(second.Width, second.Height), Filter.Odd7x9, pool);

        // Assert
        Assert.Equal(firstBits, Bits(first));
        Assert.Equal(firstBits, Bits(second));
        Assert.Equal(2, pool.RentCount);
        Assert.Equal(2, pool.ReturnCount);
        Assert.True(pool.ClearRequested);
    }

    [Fact]
    public async Task Decompose_shared_pool_supports_independent_concurrent_images()
    {
        // Arrange
        var cases = new[] { (64, 65, "7x9"), (65, 67, "8x8"), (128, 129, "7x9"), (129, 128, "8x8") };
        var expected = cases.Select(item =>
        {
            var image = CreateImage(item.Item1, item.Item2);
            Transformer.Decompose(image, Quantizer.Create(image.Width, image.Height), Filter.Create(item.Item3),
                new TrackingArrayPool<float>(image.Length, 0f));
            return Bits(image);
        }).ToArray();

        // Act
        int[][] actual = await Task.WhenAll(cases.Select(item => Task.Run(() =>
        {
            var image = CreateImage(item.Item1, item.Item2);
            Transformer.Decompose(image, Quantizer.Create(image.Width, image.Height), Filter.Create(item.Item3));
            return Bits(image);
        })));

        // Assert
        for (int i = 0; i < expected.Length; i++)
        {
            Assert.Equal(expected[i], actual[i]);
        }
    }

    private static WsqPixelBuffer<float> CreateImage(int width, int height)
    {
        var image = new WsqPixelBuffer<float>(width, height);
        for (int i = 0; i < image.Length; i++)
        {
            image[i] = ((i * 13 + i / width * 7) % 256 - 127.5f) / 32f;
        }
        return image;
    }

    private static int[] Bits(WsqPixelBuffer<float> image) =>
        image.Pixels.Select(BitConverter.SingleToInt32Bits).ToArray();

}
