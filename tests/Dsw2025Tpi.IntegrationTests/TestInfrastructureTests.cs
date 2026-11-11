using System.Net;
using Dsw2025Tpi.Data;
using Dsw2025Tpi.IntegrationTests.Infrastructure;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Xunit;

namespace Dsw2025Tpi.IntegrationTests;

[Collection("IntegrationTests")]
public class TestInfrastructureTests : IntegrationTestBase
{
    public TestInfrastructureTests(CustomWebApplicationFactory factory) : base(factory) { }

    [Fact]
    public async Task TestingHost_StartsWithIsolatedSqliteContexts()
    {
        // Arrange
        using var scope = Factory.Services.CreateScope();
        var business = scope.ServiceProvider.GetRequiredService<Dsw2025TpiContext>();
        var identity = scope.ServiceProvider.GetRequiredService<AuthenticateContext>();
        var environment = scope.ServiceProvider.GetRequiredService<IHostEnvironment>();

        // Act
        var response = await Client.GetAsync("/healthcheck");
        var products = await business.Products.CountAsync();
        var users = await identity.Users.CountAsync();

        // Assert: valida arranque HTTP y creacion real de ambos esquemas.
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        environment.EnvironmentName.Should().Be("Testing");
        business.Database.ProviderName.Should().Be("Microsoft.EntityFrameworkCore.Sqlite");
        identity.Database.ProviderName.Should().Be("Microsoft.EntityFrameworkCore.Sqlite");
        products.Should().Be(0);
        users.Should().Be(0);
    }
}
