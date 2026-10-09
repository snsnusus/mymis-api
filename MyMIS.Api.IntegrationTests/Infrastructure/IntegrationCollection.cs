namespace MyMIS.Api.IntegrationTests.Infrastructure;

// Puts every integration test class into one collection, so they share a single
// ApiFactory (one Postgres container for the whole run) and run one after another.
[CollectionDefinition(Name)]
public class IntegrationCollection : ICollectionFixture<ApiFactory>
{
  public const string Name = "integration";
}