using BiomSharp.Imaging.Wsq;

namespace BiomSharp.Imaging.Tests.Models;

public sealed record TestCaseData(TestCaseIdentity CaseId,
    WsqParameters Parameters,
    TestCaseEnvironment Environment,
    DataContainerPgm OriginalPgm,
    DataContainerWsq? OriginalWsqData)
{
    public static TestCaseData Create(TestCaseIdentity caseId,
        WsqParameters parameters,
        DataContainerPgm originalPgmData,
        DataContainerWsq? originalWsqData)
    {
        var testcase = new TestCaseData(
            caseId,
            parameters,
            TestCaseEnvironment.Instance,
            originalPgmData,
            originalWsqData);

        return testcase;
    }

    /// <summary>
    /// Contains all the wsq data that has been decoded into pgm images.
    /// </summary>
    public Dictionary<ExecutableType, DataContainerPgm> DecodedResults { get; } = new Dictionary<ExecutableType, DataContainerPgm>();

    /// <summary>
    /// Contains all the pgm images taht has been encoded to wsq data.
    /// </summary>
    public Dictionary<ExecutableType, DataContainerWsq> EncodedResults { get; } = new Dictionary<ExecutableType, DataContainerWsq>();


    public bool RemoveWsqContainer(ExecutableType executableType)
    {
        // check if the parameter value is not valid
        return EncodedResults.Remove(executableType);
    }

    public bool RemovePgmContainer(ExecutableType executableType)
    {
        return DecodedResults.Remove(executableType);
    }

    public void AddWsqContainer(ExecutableType executableType, DataContainerWsq? container)
    {
        // if the container parameter is null, then remove stored container.
        if (container == null)
        {
            RemoveWsqContainer(executableType);
            return;
        }

        EncodedResults[executableType] = container;
    }

    public void AddPgmContainer(ExecutableType executableType, DataContainerPgm? container)
    {        
        // if the container parameter is null, then remove stored container.
        if (container == null)
        {
            RemovePgmContainer(executableType);
            return;
        }

        DecodedResults[executableType] = container;
    }
}
