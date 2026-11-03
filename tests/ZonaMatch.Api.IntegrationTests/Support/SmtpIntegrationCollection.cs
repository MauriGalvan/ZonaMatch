namespace ZonaMatch.Api.IntegrationTests.Support;

[CollectionDefinition(Name)]
public sealed class SmtpIntegrationCollection : ICollectionFixture<Smtp4DevFixture>
{
    public const string Name = "SMTP integration tests";
}
