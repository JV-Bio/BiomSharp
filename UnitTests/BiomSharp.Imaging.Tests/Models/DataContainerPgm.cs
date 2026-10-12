using BiomSharp.Imaging.Pgm;

namespace BiomSharp.Imaging.Tests.Models;

/// <summary>
/// Container for uncompressed image data.
/// </summary>
public sealed class DataContainerPgm
{
    public DataContainerPgm(string fileName, byte[] data)
        : this(decoderExecutable: "BiomSharp", fileName, data)
    {

    }

    public DataContainerPgm(string decoderExecutable, string fileName, byte[] data)
    {
        // Check if data exist.
        ArgumentNullException.ThrowIfNull(data);

        DecoderExecutable = decoderExecutable;
        FileName = fileName;
        Data = data;
        Nfiq2Data = Nfiq2Data.CreateEmpty();
    }

    public DataContainerPgm(string decoderExecutable, string fileName, SimpleBitmap data)
    {
        // Check if data exist.
        ArgumentNullException.ThrowIfNull(data);

        DecoderExecutable = decoderExecutable;
        FileName = fileName;
        Data = GetBytes(data);
        Nfiq2Data = Nfiq2Data.CreateEmpty();
    }

    public readonly byte[] Data;
    public readonly string FileName;

    public string? DecoderExecutable { get; init; }
    public string? DecoderSha256 { get; init; }
    public Nfiq2Data Nfiq2Data { get; init; }

    public SimpleBitmap? GetSimpleBitmap()
    {
        var pgm = new PgmCodec();
        using (var input = new MemoryStream(Data, writable: false))
        {
            pgm.Read(input);
        }

        return pgm.ToRaw();
    }

    public byte[] GetBytes(SimpleBitmap data)
    {
        var pgmCodec = new PgmCodec();
        pgmCodec.FromRaw(data);
        using MemoryStream stream = new MemoryStream();
        pgmCodec.Write(stream);

        return stream.ToArray();
    }
}
