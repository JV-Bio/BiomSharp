namespace BiomSharp.Imaging.Tests.Models;

internal abstract class TestReportBase
{
    public TestReportBase(string Image,
        float BitRate,
        string Filter,
        string Runtime,
        string OS,
        string Architecture,
        string? BiomSharpVersion,
        WsqMetadata EncodedMetadata,
        WsqMetadata ReferenceMetadata,
        EncodingTestComparison Comparison,
        string CertificationLimitation)
    {

    }
}
