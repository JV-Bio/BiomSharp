using BiomSharp.Imaging.Wsq;

namespace BiomSharp.Imaging.Tests.Models;

/// <summary>
/// Holds the source files and separate native and BiomSharp reconstructions for a decoding test.
/// Arrange prepares the native reconstruction; Act produces the BiomSharp reconstruction.
/// </summary>
/// <param name="TestCaseData">Shared case identity, configuration, environment, and original file bytes.</param>
/// <param name="OriginalWsqInspection">Metadata and coefficients inspected from the original NIST WSQ file.</param>
internal sealed record DecodingTestCaseCertification(TestCaseData TestCaseData)
{
    /// <summary>The image identifier supplied to ArrangeDecoding.</summary>
    public string ImageName => TestCaseData.CaseId.ImageName;
    /// <summary>The output directory for this test case's artifacts.</summary>
    public string CaseDirectory => TestCaseData.CaseId.CaseDirectory;

    /// <summary>Complete NIST WSQ file bytes supplied to both decoders.</summary>
    public DataContainerWsq OriginalWsq => TestCaseData.OriginalWsqData;
    /// <summary>
    /// Original uncompressed PGM file bytes, not a reference reconstruction of the lossy WSQ.
    /// </summary>
    public DataContainerPgm OriginalImage => TestCaseData.OriginalPgm;

    /// <summary>Frame information inspected from the original NIST WSQ file.</summary>
    public WsqFrame OriginalWspHeader => OriginalWsq.WspInspection.Header;

    /// <summary>
    /// Raw grayscale bitmap reconstructed from OriginalWsq by NIST dwsq during Arrange.
    /// Null until native decoding succeeds; contains neither compressed WSQ nor PGM file bytes.
    /// </summary>
    public SimpleBitmap? DecodedRawDataByNistDwsq { get; set; }

    /// <summary>
    /// Raw grayscale bitmap reconstructed by BiomSharp during Act using
    /// <see cref="WsqCodec.Decode(byte[])"/> with <see cref="OriginalWsq"/>.
    /// Null until BiomSharp decoding succeeds.
    /// </summary>
    /// <remarks>
    /// Kept separate from the native reconstruction; contains neither compressed WSQ nor PGM file bytes.
    /// </remarks>
    public SimpleBitmap? DecodedRawData { get; set; }

}
