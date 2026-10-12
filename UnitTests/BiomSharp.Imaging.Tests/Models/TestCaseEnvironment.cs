using System.Runtime.InteropServices;

namespace BiomSharp.Imaging.Tests.Models;

public sealed class TestCaseEnvironment
{
    private TestCaseEnvironment()
    {
        Runtime = RuntimeInformation.FrameworkDescription;
        OS = RuntimeInformation.OSDescription;
        Architecture = RuntimeInformation.ProcessArchitecture.ToString();
        BiomSharpVersion = typeof(IBitmapCodec).Assembly.GetName().Version?.ToString();
    }
    public string Runtime { get; }
    public string OS { get; }
    public string Architecture { get; }
    public string? BiomSharpVersion { get; }

    private static Lazy<TestCaseEnvironment> _instance = new Lazy<TestCaseEnvironment>(() => new TestCaseEnvironment());
    public static TestCaseEnvironment Instance => _instance.Value;
}
