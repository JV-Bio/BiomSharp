using BiomSharp.Imaging.Tests.Fixtures;

namespace BiomSharp.Imaging.Tests.TestCollections;

[CollectionDefinition(Name, DisableParallelization = false)]
public class EncodingDecodingCollection : ICollectionFixture<FixtureFullProcess>
{
    public const string Name = "Encoding And Decoding";
}
