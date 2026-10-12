namespace BiomSharp.Imaging.Tests.Models;

internal sealed record EncodingTestCaseCertification(EncodingTestCase Encoding, string CaseDirectory, string EncodedPath)
{
    /// <summary>
    /// Complete NIST reference WSQ file bytes for this image and bitrate, loaded from
    /// decode\75 or decode\225 beneath TestResources.DatasetDirectory.
    /// Contains compressed WSQ data, not decoded pixels; null until loaded.
    /// </summary>
    public byte[]? ReferenceWsq { get; set; }

    /// <summary>
    /// Complete WSQ file bytes produced by our encoder from the original PGM image
    /// at the selected bitrate (0.75 or 2.25). Null until encoding succeeds.
    /// </summary>
    public byte[]? EncodedWsq { get; set; }
}
