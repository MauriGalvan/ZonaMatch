namespace ZonaMatch.Api.IntegrationTests.Support;

[CollectionDefinition(Name)]
public sealed class ApiIntegrationCollection : ICollectionFixture<PostgreSqlDatabaseFixture>
{
    public const string Name = "API integration tests";
}
