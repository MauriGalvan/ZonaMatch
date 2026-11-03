# API integration tests

These tests start the API with `WebApplicationFactory` and run it against a disposable PostgreSQL/PostGIS container managed by Testcontainers. They apply the real EF Core migrations and exercise HTTP endpoints through the API middleware, use cases, and repositories. Coverage includes authentication success and errors, group membership/permissions/invitations/ranking and saved group analyses, saved-analysis ownership/versions/actions, profile persistence, saved zones, community posts/contribution moderation and voting, and notification ownership/read state.

The SMTP integration tests start a disposable `rnwood/smtp4dev:v3` container. They deliver email through MailKit over STARTTLS, then inspect the received MIME message, including HTML, the embedded brand logo, and attachments. They never contact an external mail provider or use real credentials. The test trusts only the thumbprint of the self-signed certificate generated for its local SMTP container; production certificate validation is unchanged.

The test host replaces zone lookup, zone scoring data, route durations, and saved-analysis result calculation. Those depend on the imported OSM/geographic, routing, and scoring datasets, which are separate from the application's migrated database and are not loaded into the temporary test container. Group, profile, analysis, community, and notification persistence use the real PostgreSQL database and production repositories.

Email-invitation acceptance tests insert invitation records directly into the temporary database. This lets the tests exercise the real HTTP acceptance flow without sending external email.

Requirements:

- .NET 10 SDK
- Docker running locally
- Internet access for Docker to pull the PostgreSQL/PostGIS and smtp4dev images when they are not cached

Run the integration tests with:

```powershell
dotnet test tests\ZonaMatch.Api.IntegrationTests\ZonaMatch.Api.IntegrationTests.csproj
```

The test database is created inside a temporary container and is removed when the test fixture is disposed. It does not use the application's local or development database.

The CI workflow runs the unit and integration tests, adds line-coverage results to the GitHub Actions job summary, and publishes the detailed report as a downloadable artifact.
