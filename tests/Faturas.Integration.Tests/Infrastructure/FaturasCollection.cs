namespace Faturas.Integration.Tests.Infrastructure;

[CollectionDefinition(FaturasCollection.Name)]
public class FaturasCollection : ICollectionFixture<IntegrationWebApplicationFactory>
{
    public const string Name = "Faturas Integration Tests";
}
