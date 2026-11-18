using System.Net.Http.Headers;
using Dsw2025Tpi.Application.Interfaces;
using Dsw2025Tpi.Data;
using Microsoft.Extensions.DependencyInjection;

namespace Dsw2025Tpi.IntegrationTests.Infrastructure;

public abstract class ApiIntegrationTestBase(CustomWebApplicationFactory factory)
    : IntegrationTestBase(factory)
{
    protected async Task WithDatabaseAsync(Func<Dsw2025TpiContext, Task> action)
    {
        await using var scope = Factory.Services.CreateAsyncScope();
        await action(scope.ServiceProvider.GetRequiredService<Dsw2025TpiContext>());
    }

    protected void AuthenticateAs(string role, Guid? customerId = null)
    {
        using var scope = Factory.Services.CreateScope();
        var tokens = scope.ServiceProvider.GetRequiredService<IJwtTokenService>();
        var token = tokens.GenerateToken("integration-test", role, customerId);
        Client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
    }
}
