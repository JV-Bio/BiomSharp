namespace BiomSharp.Imaging.Tests.Models;

/// <summary>
/// Container for Wsq compressed data.
/// </summary>
public sealed class DataContainerWsq
{
    internal DataContainerWsq(string fileName, byte[] data)
    {
        // Check if data exist.
        ArgumentNullException.ThrowIfNull(data);

        FileName = fileName;
        Data = data;
        WsqInspection = WsqInspection.Read(Data);
    }

    internal DataContainerWsq(string fileName, byte[] data, WsqInspection wsqInspection) : this(fileName, data)
    {
        WsqInspection = wsqInspection;
    }


    public readonly byte[] Data;
    public readonly string FileName;
    internal readonly WsqInspection WsqInspection;

    internal WsqMetadata GetMetadata() => WsqMetadata.Create(WsqInspection);
}
