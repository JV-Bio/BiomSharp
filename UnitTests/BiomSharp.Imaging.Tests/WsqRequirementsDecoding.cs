namespace BiomSharp.Imaging.Tests;

/// <summary>
/// WSQ decoder acceptance values for comparing the output of the decoder under test
/// (such as BiomSharp) with a reference reconstruction from an independent decoder
/// (such as NIST NBIS dwsq). Both decoders must decode the exact same WSQ bytes.
/// Both requirements apply to each pair of reconstructed images, not to averages
/// across the dataset.
/// </summary>
/// <remarks>
/// These limits measure agreement between decoding sources. They do not apply when
/// comparing a decoded image with the original uncompressed PGM: that comparison
/// includes changes introduced by lossy WSQ encoding.
/// Decoding two different WSQ encodings of the same original image does not satisfy
/// the same-input requirement.
/// </remarks>
internal static class WsqRequirementsDecoding
{
    /// <summary>
    /// Minimum percentage of pixels from the decoder under test that must exactly
    /// match the corresponding pixels from the independent reference decoder,
    /// with both decoders using the same WSQ bytes. The limit is inclusive and
    /// must be evaluated without rounding the percentage; the allowed mismatch
    /// count is rounded down to a whole number.
    /// </summary>
    /// <remarks>
    /// Source: WSQ Specification v3.1 (IAFIS-IC-0110, October 4, 2010),
    /// Annex AA.3, item 1, printed page 37.
    /// <see href="https://fbibiospecs.fbi.gov/@@dvpdffiles/a/f/aff54b5436a846dabd4b2e110d3d819b/text/dump_45.txt">FBI decoder compliance measures</see>.
    /// </remarks>
    public const decimal MinimumIdenticalPixelPercent = 99.9m;

    /// <summary>
    /// Maximum absolute difference in gray levels between a pixel from the decoder
    /// under test and the corresponding pixel from the independent reference decoder,
    /// with both decoders using the same WSQ bytes. This inclusive limit
    /// applies to every pixel independently of the identical-pixel percentage.
    /// </summary>
    /// <remarks>
    /// Source: WSQ Specification v3.1 (IAFIS-IC-0110, October 4, 2010),
    /// Annex AA.3, item 1, printed page 37.
    /// <see href="https://fbibiospecs.fbi.gov/@@dvpdffiles/a/f/aff54b5436a846dabd4b2e110d3d819b/text/dump_45.txt">FBI decoder compliance measures</see>.
    /// </remarks>
    public const int MaximumPixelDifference = 1;
}
