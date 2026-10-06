using Dsw2025Tpi.Api.Configurations;
using Dsw2025Tpi.Api.Middlewares;
using Dsw2025Tpi.Application.Interfaces;
using Dsw2025Tpi.Application.Services;
using Dsw2025Tpi.Application.Validators;
using Dsw2025Tpi.Data;
using Dsw2025Tpi.Data.Identity;
using FluentValidation;
using FluentValidation.AspNetCore;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.Filters;
using System.Text;


namespace Dsw2025Tpi.Api;

public class Program
{
    public static void Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        // Add services to the container.
        builder.Services.AddControllers();
      
        builder.Services.AddEndpointsApiExplorer();
        builder.Services.AddSwaggerGen(o =>
        {
            o.SwaggerDoc("v1", new OpenApiInfo
            {
                Title = "Desarrollo de Software",
                Version = "v1",
            });
            o.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
            {
                In = ParameterLocation.Header,
                Name = "Authorization",
                Description = "Ingresar el token",
                Type = SecuritySchemeType.ApiKey
            });
            o.AddSecurityRequirement(new OpenApiSecurityRequirement
            {
                {
                    new OpenApiSecurityScheme
                    {
                        Reference = new OpenApiReference
                        {
                            Type = ReferenceType.SecurityScheme,
                            Id = "Bearer"
                        }
                    },
                    Array.Empty<string>()
                }
            });
            
            o.ExampleFilters();
        });

        builder.Services.AddHealthChecks();

        // ========= Identity Configuration =========
        builder.Services.AddIdentity<AppUser, IdentityRole>(options =>
        {
            options.Password = new PasswordOptions
            {
                RequiredLength = 8,
                RequireDigit = true,
                RequireLowercase = true,
                RequireUppercase = true,
                RequireNonAlphanumeric = true,
                RequiredUniqueChars = 1
            };
        })
        .AddEntityFrameworkStores<AuthenticateContext>()
        .AddDefaultTokenProviders();

        // ========= JWT Configuration (Fail-Fast Early Validation AT-04) =========
        const int minimumJwtKeyLength = 32;
        var jwtConfig = builder.Configuration.GetSection("Jwt");
        var keyText = jwtConfig["Key"];

        if (string.IsNullOrWhiteSpace(keyText))
        {
            throw new InvalidOperationException(
                "CRITICAL SECURITY CONFIGURATION ERROR (AT-04): La clave de firmado JWT (Jwt:Key) no está configurada. " +
                "Por motivos de seguridad, defina esta clave mediante la variable de entorno 'Jwt__Key' o mediante .NET User Secrets.");
        }

        if (keyText.Length < minimumJwtKeyLength)
        {
            throw new InvalidOperationException(
                $"CRITICAL SECURITY CONFIGURATION ERROR (AT-04): La clave de firmado JWT (Jwt:Key) no cumple con la longitud mínima requerida de {minimumJwtKeyLength} caracteres (256 bits) para HMAC-SHA256. Longitud detectada: {keyText.Length}.");
        }

        var key = Encoding.UTF8.GetBytes(keyText);

        builder.Services.AddAuthentication(options =>
        {
            options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
            options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
        })
        .AddJwtBearer(options =>
        {
            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidateAudience = true,
                ValidateLifetime = true,
                ValidateIssuerSigningKey = true,
                ValidIssuer = jwtConfig["Issuer"],
                ValidAudience = jwtConfig["Audience"],
                IssuerSigningKey = new SymmetricSecurityKey(key)
            };
        });

        // ========= Database Context =========
        builder.Services.AddDbContext<AuthenticateContext>(options =>
        {
            options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection"));
        });

        // ========= Application Services (includes Repository, UnitOfWork, etc.) =========
        builder.Services.AddDomainServices(builder.Configuration);

        // ========= JWT Token Service =========
        builder.Services.AddScoped<IJwtTokenService, JwtTokenService>();

        // ========= FluentValidation =========
        builder.Services.AddValidatorsFromAssemblyContaining<ProductRequestValidator>();
        builder.Services.AddValidatorsFromAssemblyContaining<ProductUpdateRequestValidator>();
        builder.Services.AddValidatorsFromAssemblyContaining<OrderRequestValidator>();
        builder.Services.AddValidatorsFromAssemblyContaining<OrderItemRequestValidator>();
        builder.Services.AddValidatorsFromAssemblyContaining<UpdateOrderStatusRequestValidator>();
        builder.Services.AddValidatorsFromAssemblyContaining<FilterOrderValidator>();

        builder.Services.AddFluentValidationAutoValidation();
        builder.Services.AddFluentValidationClientsideAdapters();

        // ========= AutoMapper =========
        builder.Services.AddAutoMapper(typeof(Dsw2025Tpi.Application.Mappings.MappingProfiles));

        // ========= Swagger Examples =========
        builder.Services.AddSwaggerExamplesFromAssemblyOf<Program>();

        // ========= Authorization =========
        builder.Services.AddAuthorization();
        
        // ========= CORS Configuration =========
        builder.Services.AddCors(options =>
        {
            options.AddPolicy("PermitirFrontend", policy =>
                policy.WithOrigins("http://localhost:5173")
                      .AllowAnyHeader()
                      .AllowAnyMethod()
                      .AllowCredentials());
        });

        var app = builder.Build();

        // Configure the HTTP request pipeline.
        if (app.Environment.IsDevelopment())
        {
            app.UseSwagger();
            app.UseSwaggerUI();
        }

        // ========= Middleware Pipeline =========
        app.UseMiddleware<ExceptionMiddleware>();
        app.UseHttpsRedirection();
        app.UseCors("PermitirFrontend");
        app.UseAuthentication();
        app.UseAuthorization();

        app.MapControllers();
        app.MapHealthChecks("/healthcheck");

        app.Run();
    }
}
