namespace BiomSharp.Imaging.Tests.Models;

public enum ExecutableType
{
    /// <summary>
    /// Original image, had not been Decoded or Encoded
    /// </summary>
    None,

    /// <summary>
    /// Decoded or Encoded by BiomSharp
    /// </summary>
    BiomSharp,

    /// <summary>
    /// Decoded or Encoded by Navite dwsq_x32 or dwsq_x64
    /// from NITS.
    /// </summary>
    NativDswq
}
