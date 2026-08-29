using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Application.Dtos;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace Api.Auth;

public sealed class JwtTokenService
{
    private readonly JwtOptions _options;

    public JwtTokenService(IOptions<JwtOptions> options) => _options = options.Value;

    public string CreateToken(AccountDto account)
    {
        var now = DateTime.UtcNow;
        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, account.Id.ToString()),
            new Claim("accountId", account.Id.ToString()),
            new Claim(ClaimTypes.Name, account.Name)
        };
        var credentials = new SigningCredentials(
            new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_options.Key)),
            SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: _options.Issuer,
            audience: _options.Audience,
            claims: claims,
            notBefore: now,
            expires: now.AddMinutes(_options.ExpiryMinutes),
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
