using Xunit;

namespace Dsw2025Tpi.IntegrationTests.Infrastructure;

[CollectionDefinition("IntegrationTests", DisableParallelization = true)]
public sealed class IntegrationTestCollection : ICollectionFixture<CustomWebApplicationFactory>
{
}
