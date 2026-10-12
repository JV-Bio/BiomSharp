using BiomSharp.Nist.Nfiq;

namespace BiomSharp.Imaging.Tests.Models;

public sealed class Nfiq2Data
{
    public Nfiq2Data(string version, int errorCode, Nfiq2Analysis? nfiq2Analysis)
    {
        UpdateValues(version, errorCode, nfiq2Analysis);
    }

    public string Version { get; private set; }
    public int ErrorCode { get; private set; }
    public Nfiq2Analysis? Nfiq2Analysis { get; private set; }

    public string Status
    {
        get
        {
            return ErrorCode != 0 ?
                "Failed" :
                Nfiq2Analysis != null ?
                    "Calculated" :
                    "NotCalculated";
        }
    }

    public void UpdateValues(string version, int errorCode, Nfiq2Analysis? nfiq2Analysis)
    {
        Version = version;
        ErrorCode = errorCode;
        Nfiq2Analysis = errorCode == 0 ? nfiq2Analysis : null;
    }

    public static Nfiq2Data CreateEmpty() => new Nfiq2Data(string.Empty, 0, null);
}
