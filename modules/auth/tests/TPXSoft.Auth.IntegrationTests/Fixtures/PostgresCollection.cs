namespace TPXSoft.Auth.IntegrationTests.Fixtures;

/// <summary>
/// Groups every integration test class onto one shared Postgres container. The Category trait is
/// also declared here (in addition to on each concrete test class) so it's visible even from
/// tooling that only inspects the collection definition.
/// </summary>
[CollectionDefinition(Name)]
[Trait("Category", "Integration")]
public sealed class PostgresCollection : ICollectionFixture<PostgresFixture>
{
    public const string Name = "Postgres";
}
