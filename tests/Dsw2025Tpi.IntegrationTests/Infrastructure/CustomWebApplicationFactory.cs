using Dsw2025Tpi.Api;
using Dsw2025Tpi.Data;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Dsw2025Tpi.IntegrationTests.Infrastructure;

public sealed class CustomWebApplicationFactory : WebApplicationFactory<Program>
{
    // Valores exclusivamente de pruebas, sin relacion con secretos de produccion.
    public const string JwtKey = "TP4-testing-only-signing-key-at-least-32-characters";
    public const string JwtIssuer = "Dsw2025Tpi.Testing";
    public const string JwtAudience = "Dsw2025Tpi.Testing.Client";

    // Bases separadas: EnsureCreated de un contexto no omite las tablas del otro.
    private readonly SqliteConnection _businessConnection = OpenConnection("business");
    private readonly SqliteConnection _identityConnection = OpenConnection("identity");

    private static SqliteConnection OpenConnection(string name)
    {
        var connection = new SqliteConnection(
            $"Data Source=tp4-{name}-{Guid.NewGuid():N};Mode=Memory;Cache=Shared");
        connection.Open();
        return connection;
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting("Jwt:Key", JwtKey);
        builder.UseSetting("Jwt:Issuer", JwtIssuer);
        builder.UseSetting("Jwt:Audience", JwtAudience);
        builder.UseSetting("ConnectionStrings:DefaultConnection", "Testing-only-unused");

        builder.ConfigureServices(services =>
        {
            ReplaceContext<Dsw2025TpiContext>(services, _businessConnection);
            ReplaceContext<AuthenticateContext>(services, _identityConnection);
        });
    }

    private static void ReplaceContext<TContext>(IServiceCollection services, SqliteConnection connection)
        where TContext : DbContext
    {
        // Quitar tambien la configuracion del proveedor SQL Server original.
        services.RemoveAll<TContext>();
        services.RemoveAll<DbContextOptions<TContext>>();
        services.AddDbContext<TContext>(options => options.UseSqlite(connection));
    }

    public async Task ResetDatabasesAsync()
    {
        using var scope = Services.CreateScope();
        var business = scope.ServiceProvider.GetRequiredService<Dsw2025TpiContext>();
        var identity = scope.ServiceProvider.GetRequiredService<AuthenticateContext>();

        await business.Database.EnsureDeletedAsync();
        await identity.Database.EnsureDeletedAsync();
        await business.Database.EnsureCreatedAsync();
        await identity.Database.EnsureCreatedAsync();
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing)
        {
            _businessConnection.Dispose();
            _identityConnection.Dispose();
        }
    }
}
