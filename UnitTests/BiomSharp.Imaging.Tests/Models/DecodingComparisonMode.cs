namespace BiomSharp.Imaging.Tests.Models;

/// <summary>
/// Controls whether pixel measurements are also evaluated against decoder acceptance limits.
/// Both modes require structural validation before pixel evaluation.
/// </summary>
internal enum DecodingComparisonMode
{
    /// <summary>
    /// Measures pixel identity and maximum absolute difference without applying acceptance limits.
    /// Use when comparing a decoded image with the original uncompressed PGM, since WSQ is lossy.
    /// </summary>
    MeasurementsOnly,

    /// <summary>
    /// Measures pixel differences and checks both limits in <see cref="WsqRequirementsDecoding"/>.
    /// Use only when the baseline and decoded images reconstruct the same WSQ bytes.
    /// </summary>
    DecoderAcceptance
}
