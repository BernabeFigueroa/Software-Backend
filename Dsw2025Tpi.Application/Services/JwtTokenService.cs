using Dsw2025Tpi.Application.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace Dsw2025Tpi.Application.Services;

public class JwtTokenService : IJwtTokenService
{
    private const int MinimumKeyLength = 32; // Mínimo 256 bits para HMAC-SHA256
    private readonly string _keyText;
    private readonly string _issuer;
    private readonly string _audience;
    private readonly double _expireMinutes;

    public JwtTokenService(IConfiguration config)
    {
        var jwtConfig = config.GetSection("Jwt");

        _keyText = jwtConfig["Key"]
            ?? throw new InvalidOperationException("CRITICAL: La clave de firmado JWT (Jwt:Key) no está configurada.");

        if (_keyText.Length < MinimumKeyLength)
        {
            throw new InvalidOperationException(
                $"CRITICAL: La clave de firmado JWT es insegura. Debe tener al menos {MinimumKeyLength} caracteres (256 bits). Longitud actual: {_keyText.Length}.");
        }

        _issuer = jwtConfig["Issuer"]
            ?? throw new InvalidOperationException("CRITICAL: El emisor de JWT (Jwt:Issuer) no está configurado.");

        _audience = jwtConfig["Audience"]
            ?? throw new InvalidOperationException("CRITICAL: La audiencia de JWT (Jwt:Audience) no está configurada.");

        if (!double.TryParse(jwtConfig["ExpireInMinutes"], out _expireMinutes) || _expireMinutes <= 0)
        {
            _expireMinutes = 60;
        }
    }

    public string GenerateToken(string username, string role, Guid? customerId = null)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_keyText));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claimsList = new List<Claim>
        {
            new Claim(JwtRegisteredClaimNames.Sub, username),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new Claim(ClaimTypes.Role, role)
        };

        // Agregar customerId si está presente (solo para clientes)
        if (customerId.HasValue && customerId.Value != Guid.Empty)
        {
            claimsList.Add(new Claim("customerId", customerId.Value.ToString()));
        }

        var token = new JwtSecurityToken(
            issuer: _issuer,
            audience: _audience,
            claims: claimsList,
            expires: DateTime.UtcNow.AddMinutes(_expireMinutes),
            signingCredentials: creds
        );

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
