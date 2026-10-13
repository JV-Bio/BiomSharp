namespace BiomSharp.Imaging.Tests.Models;

internal sealed record class WsqFrame
{

    internal WsqFrame(int width,
        int height,
        byte black,
        byte white,
        byte encoder,
        short implementer,
        byte lowFilterLength,
        byte highFilterLength)
    {
        Width = width; 
        Height = height;
        Black = black;  
        White = white;
        Encoder = encoder;  
        Implementer = implementer;
        LowFilterLength = lowFilterLength;
        HighFilterLength = highFilterLength;
    }

    public int Width { get; set; }
    public int Height { get; set; }
    public byte Black { get; set; }
    public byte White { get; set; }
    public byte Encoder { get; set; }
    public short Implementer { get; set; }
    public byte LowFilterLength { get; set; }
    public byte HighFilterLength { get; set; }
}
