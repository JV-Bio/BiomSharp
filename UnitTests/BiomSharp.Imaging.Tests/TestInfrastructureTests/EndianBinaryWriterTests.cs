using BiomSharp.Imaging.Wsq.IO;

namespace BiomSharp.Imaging.Tests.TestInfrastructureTests;

public class EndianBinaryWriterTests
{
    [Theory]
    [InlineData((short)0, "0000")]
    [InlineData((short)1, "0001")]
    [InlineData((short)-1, "FFFF")]
    [InlineData(short.MinValue, "8000")]
    [InlineData(short.MaxValue, "7FFF")]
    [InlineData((short)0x1234, "1234")]
    [InlineData((short)-0x1234, "EDCC")]
    public void Write_short_uses_big_endian_bytes(short value, string expectedHex)
    {
        // Arrange
        using var stream = new MemoryStream();
        using var writer = new EndianBinaryWriter(stream);

        // Act
        writer.Write(value);

        // Assert
        Assert.Equal(Convert.FromHexString(expectedHex), stream.ToArray());
    }

    [Theory]
    [InlineData((ushort)0, "0000")]
    [InlineData((ushort)1, "0001")]
    [InlineData(ushort.MaxValue, "FFFF")]
    [InlineData((ushort)0x8000, "8000")]
    [InlineData((ushort)0x1234, "1234")]
    public void Write_ushort_uses_big_endian_bytes(ushort value, string expectedHex)
    {
        // Arrange
        using var stream = new MemoryStream();
        using var writer = new EndianBinaryWriter(stream);

        // Act
        writer.Write(value);

        // Assert
        Assert.Equal(Convert.FromHexString(expectedHex), stream.ToArray());
    }

    [Theory]
    [InlineData(0, "00000000")]
    [InlineData(1, "00000001")]
    [InlineData(-1, "FFFFFFFF")]
    [InlineData(int.MinValue, "80000000")]
    [InlineData(int.MaxValue, "7FFFFFFF")]
    [InlineData(0x12345678, "12345678")]
    [InlineData(-0x12345678, "EDCBA988")]
    public void Write_int_uses_big_endian_bytes(int value, string expectedHex)
    {
        // Arrange
        using var stream = new MemoryStream();
        using var writer = new EndianBinaryWriter(stream);

        // Act
        writer.Write(value);

        // Assert
        Assert.Equal(Convert.FromHexString(expectedHex), stream.ToArray());
    }

    [Theory]
    [InlineData(0u, "00000000")]
    [InlineData(1u, "00000001")]
    [InlineData(uint.MaxValue, "FFFFFFFF")]
    [InlineData(0x80000000u, "80000000")]
    [InlineData(0x12345678u, "12345678")]
    public void Write_uint_uses_big_endian_bytes(uint value, string expectedHex)
    {
        // Arrange
        using var stream = new MemoryStream();
        using var writer = new EndianBinaryWriter(stream);

        // Act
        writer.Write(value);

        // Assert
        Assert.Equal(Convert.FromHexString(expectedHex), stream.ToArray());
    }

    [Fact]
    public void Write_overloads_preserve_byte_order_and_stream_position()
    {
        // Arrange
        using var stream = new MemoryStream();
        using var writer = new EndianBinaryWriter(stream);
        stream.WriteByte(0xAA);

        // Act
        writer.Write((byte)0xFF);
        writer.Write(new byte[] { 0x12, 0x34 });
        writer.Write("A\u00E9");
        writer.Write((short)-2);
        writer.Write((ushort)0xABCD);
        writer.Write(-2);
        writer.Write(0x89ABCDEFu);
        writer.Write(Array.Empty<byte>());
        writer.Write(string.Empty);

        // Assert
        byte[] expected = Convert.FromHexString("AAFF1234413FFFFEABCDFFFFFFFE89ABCDEF");
        Assert.Equal(expected, stream.ToArray());
        Assert.Equal(expected.Length, stream.Position);
    }
}
