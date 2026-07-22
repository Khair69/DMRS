namespace DMRS.IntegrationTests.Harness;

/// <summary>
/// Groups every integration test into one xUnit collection so they share a single
/// <see cref="DmrsApiFactory"/> — the PostgreSQL container and the hosted API are started once for
/// the whole run rather than per class. Tests within the collection run sequentially, which the
/// shared database requires.
/// </summary>
[CollectionDefinition(Name)]
public sealed class IntegrationCollection : ICollectionFixture<DmrsApiFactory>
{
    public const string Name = "DMRS integration";
}
