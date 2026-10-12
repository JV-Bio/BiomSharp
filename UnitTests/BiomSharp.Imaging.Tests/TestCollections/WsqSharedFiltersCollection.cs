using BiomSharp.Imaging.Tests.Fixtures;

namespace BiomSharp.Imaging.Tests.TestCollections;

[CollectionDefinition(Name, DisableParallelization = true)]
public class WsqSharedFiltersCollection : ICollectionFixture<FixtureEncodingFilter>
{
    public const string Name = "WSQ shared filters";
}