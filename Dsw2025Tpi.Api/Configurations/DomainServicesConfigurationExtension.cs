using System.Text;
using Dsw2025Tpi.Application.Interfaces;
using Dsw2025Tpi.Application.Mappings;
using Dsw2025Tpi.Application.Services;
using Dsw2025Tpi.Application.Validators;
using Dsw2025Tpi.Data;
using Dsw2025Tpi.Data.Identity;
using Dsw2025Tpi.Data.Persistence;
using Dsw2025Tpi.Data.Repositories;
using Dsw2025Tpi.Domain.Interfaces;
using FluentValidation;
using FluentValidation.AspNetCore;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.Filters;

namespace Dsw2025Tpi.Api.Configurations;

public static class DomainServicesConfigurationExtension
{
    public static IServiceCollection AddDomainServices(this IServiceCollection services, IConfiguration configuration)
    {
        // ========= DbContext Configuration =========
        var connectionString = configuration.GetConnectionString("DefaultConnection");

        services.AddDbContext<Dsw2025TpiContext>(options =>
            options.UseSqlServer(connectionString));

        // ========= Application Services =========
        services.AddScoped(typeof(IRepository<>), typeof(EfRepository<>));
        services.AddScoped<IUserService, UserService>();
        services.AddScoped<IProductService, ProductService>();
        services.AddScoped<IOrderService, OrderService>();
        services.AddScoped<IJwtTokenService, JwtTokenService>();

        // ========= Repository & UnitOfWork =========
        services.AddScoped<IUnitOfWork, UnitOfWork>();

        // ========= AutoMapper =========
        services.AddAutoMapper(typeof(MappingProfiles));

        // ========= FluentValidation =========
        services.AddValidatorsFromAssemblyContaining<ProductRequestValidator>();
        services.AddValidatorsFromAssemblyContaining<ProductUpdateRequestValidator>();
        services.AddValidatorsFromAssemblyContaining<OrderRequestValidator>();
        services.AddValidatorsFromAssemblyContaining<OrderItemRequestValidator>();
        services.AddValidatorsFromAssemblyContaining<UpdateOrderStatusRequestValidator>();
        services.AddValidatorsFromAssemblyContaining<FilterOrderValidator>();

        services.AddFluentValidationAutoValidation();
        services.AddFluentValidationClientsideAdapters();

        return services;
    }

    public static IServiceCollection AddSecurityConfiguration(this IServiceCollection services, IConfiguration configuration)
    {
        // ========= Database Context (Identity) =========
        services.AddDbContext<AuthenticateContext>(options =>
        {
            options.UseSqlServer(configuration.GetConnectionString("DefaultConnection"));
        });

        // ========= Identity Configuration =========
        services.AddIdentity<AppUser, IdentityRole>(options =>
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
        var jwtConfig = configuration.GetSection("Jwt");
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

        services.AddAuthentication(options =>
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

        // ========= Authorization =========
        services.AddAuthorization();

        return services;
    }

    public static IServiceCollection AddSwaggerConfiguration(this IServiceCollection services)
    {
        services.AddSwaggerGen(o =>
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

        services.AddSwaggerExamplesFromAssemblyOf<Program>();

        return services;
    }

    public static IServiceCollection AddCorsConfiguration(this IServiceCollection services)
    {
        services.AddCors(options =>
        {
            options.AddPolicy("PermitirFrontend", policy =>
                policy.WithOrigins("http://localhost:5173")
                      .AllowAnyHeader()
                      .AllowAnyMethod()
                      .AllowCredentials());
        });

        return services;
    }
}

