using Dsw2025Tpi.Api.Configurations;
using Dsw2025Tpi.Api.Middlewares;

namespace Dsw2025Tpi.Api;

public class Program
{
    public static void Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        // Add services to the container.
        builder.Services.AddControllers();
        builder.Services.AddEndpointsApiExplorer();
        builder.Services.AddHealthChecks();

        builder.Services.AddSwaggerConfiguration();
        builder.Services.AddDomainServices(builder.Configuration);
        builder.Services.AddSecurityConfiguration(builder.Configuration);
      
        builder.Services.AddCorsConfiguration();

        var app = builder.Build();

        // Configure the HTTP request pipeline.
        app.UseSwagger();
        app.UseSwaggerUI(c =>
        {
            c.SwaggerEndpoint("/swagger/v1/swagger.json", "Desarrollo de Software v1");
        });

        // ========= Middleware Pipeline =========
        app.UseMiddleware<ExceptionMiddleware>();
        app.UseHttpsRedirection();
        app.UseCors("PermitirFrontend");
        app.UseAuthentication();
        app.UseAuthorization();

        app.MapControllers();
        app.MapHealthChecks("/healthcheck");
        app.MapGet("/", () => Results.Redirect("/swagger"));

        app.Run();
    }
}
