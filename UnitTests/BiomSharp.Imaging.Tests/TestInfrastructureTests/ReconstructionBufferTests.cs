using System.Buffers;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using BiomSharp.Imaging.Wsq;
using BiomSharp.Imaging.Wsq.Segment;
using BiomSharp.Imaging.Wsq.Tree;
using Xunit.Abstractions;

namespace BiomSharp.Imaging.Tests.TestInfrastructureTests;

public class ReconstructionBufferTests
{
    private readonly ITestOutputHelper output;

    public ReconstructionBufferTests(ITestOutputHelper output)
    {
        this.output = output;
    }

    [Theory]
    [InlineData(64, 64, "7x9", "F8ECF6CB9363A783E63D91507DE951E9BEB2C82CA21C6C8D18677605BB6039EC")]
    [InlineData(65, 64, "7x9", "7DE4001144D9253E5B36F7926A5E1A45ABB52A2A634D693E3A4633B53C91DFDC")]
    [InlineData(64, 65, "7x9", "EA10D52D6FE62BF68F72E951035F7CEC23ED42928969C2AD1DC14E863E3415AD")]
    [InlineData(65, 67, "7x9", "CAE28593A9ACD2C4EA98FBCAFC12D071F49E62AB830F9D263D522BEEE220C416")]
    [InlineData(127, 129, "7x9", "BAC492A0D0950E24D6B61A4FBD95FE1154AA4427EF3CE18B84AAA59B91A1EA7D")]
    [InlineData(64, 64, "8x8", "552631580798A7243AB1D7E500F27B99585CC3B5BC729584CE5E746AAF790ED9")]
    [InlineData(65, 64, "8x8", "C266E35D4EF81BBB1D77B944B30D5DCFB508DCFBD762CB97647DC0D734763995")]
    [InlineData(64, 65, "8x8", "0671EF3E6AB3FC1A065DEFB1CC976425EAEE4CA9BA51EA8D11412627C0B20CE0")]
    [InlineData(65, 67, "8x8", "D0003AE0F4E639F83D32F838660E0AAF290510C4B7F310B820E09F521B2CEB8B")]
    [InlineData(127, 129, "8x8", "8C41696639F0CB5215DA7918AC8D262CDAE6028B33B8000E50D34041E6E00B80")]
    public void Construct_preserves_reconstructed_pixel_bits(
        int width, int height, string filterName, string expectedHash)
    {
        var image = CreateImage(width, height);
        var dtt = new Dtt(Filter.Create(filterName));
        float[] originalHi = dtt.DttL1.ToArray();
        float[] originalLo = dtt.DttL0.ToArray();
        Transformer.Construct(image, dtt, Quantizer.Create(width, height));
        // Captured from the original reconstruction implementation before pooling and loop changes.
        Assert.Equal(expectedHash, Hash(image));
        Assert.All(image.Pixels, value => Assert.True(float.IsFinite(value)));

        var dirtyImage = CreateImage(width, height);
        var dirtyPool = new TrackingArrayPool<float>(image.Length, float.NaN);
        Transformer.Construct(dirtyImage, dtt, Quantizer.Create(width, height), dirtyPool);
        Assert.Equal(expectedHash, Hash(dirtyImage));
        Assert.Equal(image.Length, dirtyPool.RequestedLength);
        Assert.True(dirtyPool.TailWasUnchanged);
        Assert.Equal(1, dirtyPool.RentCount);
        Assert.Equal(1, dirtyPool.ReturnCount);
        Assert.True(dirtyPool.ClearRequested);
        Assert.All(dirtyPool.Buffer, value => Assert.Equal(0f, value));
        Assert.Equal(originalHi, dtt.DttL1.ToArray());
        Assert.Equal(originalLo, dtt.DttL0.ToArray());
    }

    [Fact]
    public void Construct_returns_and_clears_scratch_when_the_transform_throws()
    {
        var image = CreateImage(65, 67);
        var quantizer = Quantizer.Create(image.Width, image.Height);
        quantizer.WTree![WTree.Length - 1].X = image.Length;
        var pool = new TrackingArrayPool<float>(image.Length, float.NaN);

        Assert.Throws<IndexOutOfRangeException>(() =>
            Transformer.Construct(image, new Dtt(Filter.Odd7x9), quantizer, pool));

        Assert.Equal(1, pool.RentCount);
        Assert.Equal(1, pool.ReturnCount);
        Assert.True(pool.ClearRequested);
        Assert.All(pool.Buffer, value => Assert.Equal(0f, value));
    }

    [Theory]
    [InlineData("7x9")]
    [InlineData("8x8")]
    public void Construct_reuses_scratch_without_changing_an_earlier_image(string filterName)
    {
        var first = CreateImage(65, 67);
        var second = CreateImage(65, 67);
        var dtt = new Dtt(Filter.Create(filterName));
        var quantizer = Quantizer.Create(first.Width, first.Height);
        var pool = new TrackingArrayPool<float>(first.Length, float.NaN);
        Transformer.Construct(first, dtt, quantizer, pool);
        string firstHash = Hash(first);

        Transformer.Construct(second, dtt, quantizer, pool);

        Assert.Equal(firstHash, Hash(first));
        Assert.Equal(firstHash, Hash(second));
        Assert.Equal(2, pool.RentCount);
        Assert.Equal(2, pool.ReturnCount);
        Assert.True(pool.ClearRequested);
    }

    [Theory]
    [InlineData("7x9")]
    [InlineData("8x8")]
    public async Task Construct_supports_concurrent_images_with_shared_coefficients(string filterName)
    {
        var dtt = new Dtt(Filter.Create(filterName));
        var expected = CreateImage(65, 67);
        var quantizer = Quantizer.Create(expected.Width, expected.Height);
        Transformer.Construct(expected, dtt, quantizer);

        string[] hashes = await Task.WhenAll(Enumerable.Range(0, 8).Select(_ => Task.Run(() =>
        {
            var image = CreateImage(expected.Width, expected.Height);
            Transformer.Construct(image, dtt, quantizer);
            return Hash(image);
        })));

        Assert.All(hashes, hash => Assert.Equal(Hash(expected), hash));
    }

    [Fact]
    public void Construct_does_not_touch_spare_image_capacity()
    {
        var source = CreateImage(65, 67);
        var storage = new float[source.Length + 29];
        Array.Fill(storage, float.NaN);
        source.Pixels.CopyTo(storage, 0);
        var image = new WsqPixelBuffer<float>(source.Width, source.Height, storage);

        Transformer.Construct(image, new Dtt(Filter.Even8x8),
            Quantizer.Create(image.Width, image.Height));

        Assert.Equal("D0003AE0F4E639F83D32F838660E0AAF290510C4B7F310B820E09F521B2CEB8B", Hash(image));
        Assert.All(storage.Skip(image.Length), value => Assert.True(float.IsNaN(value)));
    }

    [Theory]
    [InlineData(64, 64)]
    [InlineData(65, 67)]
    public void Construct_with_two_tap_filters_does_not_depend_on_scratch_contents(int width, int height)
    {
        var filter = Filter.Create(new[] { 0.5f, 0.5f }, new[] { 0.5f, -0.5f });
        var dtt = new Dtt(filter);
        var expected = CreateImage(width, height);
        var actual = CreateImage(width, height);
        var quantizer = Quantizer.Create(width, height);
        var dirtyPool = new TrackingArrayPool<float>(actual.Length, float.NaN);
        Transformer.Construct(expected, dtt, quantizer, new TrackingArrayPool<float>(expected.Length, 0f));

        Transformer.Construct(actual, dtt, quantizer, dirtyPool);

        Assert.Equal(Hash(expected), Hash(actual));
        Assert.All(actual.Pixels, value => Assert.True(float.IsFinite(value)));
        Assert.Equal(new[] { 0.5f, -0.5f }, dtt.DttL1.ToArray());
        Assert.True(dirtyPool.TailWasUnchanged);
        Assert.True(dirtyPool.ClearRequested);
    }

    [Theory]
    [InlineData("7x9")]
    [InlineData("8x8")]
    public void Construct_reuses_warmed_scratch_without_image_sized_allocations(string filterName)
    {
        var source = CreateImage(512, 513);
        var image = CreateImage(source.Width, source.Height);
        var dtt = new Dtt(Filter.Create(filterName));
        var quantizer = Quantizer.Create(image.Width, image.Height);
        var pool = ArrayPool<float>.Create(image.Length * 2, 1);
        for (int i = 0; i < 10; i++)
        {
            source.Pixels.CopyTo(image.Pixels, 0);
            Transformer.Construct(image, dtt, quantizer, pool);
        }
        var elapsed = new double[20];
        long allocated = 0;
        for (int i = 0; i < elapsed.Length; i++)
        {
            source.Pixels.CopyTo(image.Pixels, 0);
            long before = GC.GetAllocatedBytesForCurrentThread();
            long start = Stopwatch.GetTimestamp();
            Transformer.Construct(image, dtt, quantizer, pool);
            elapsed[i] = (Stopwatch.GetTimestamp() - start) * 1000.0 / Stopwatch.Frequency;
            allocated += GC.GetAllocatedBytesForCurrentThread() - before;
        }
        Array.Sort(elapsed);
        output.WriteLine($"{filterName}: median={elapsed[10]:F3}ms allocated={allocated / elapsed.Length} bytes");
        Assert.InRange(allocated / elapsed.Length, 0L, 1024L);
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

    private static string Hash(WsqPixelBuffer<float> image)
    {
        return Convert.ToHexString(SHA256.HashData(MemoryMarshal.AsBytes(image.Pixels.AsSpan(0, image.Length))));
    }
}
