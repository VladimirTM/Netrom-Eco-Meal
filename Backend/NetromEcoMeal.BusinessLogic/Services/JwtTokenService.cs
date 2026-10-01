using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using NetromEcoMeal.Entities;
using NetromEcoMeal.Services.Interfaces;

namespace NetromEcoMeal.Services;

public class JwtTokenService(UserManager<ApplicationUser> userManager, IConfiguration configuration) : IJwtTokenService
{
    public async Task<string> GenerateTokenAsync(ApplicationUser user)
    {
        var key = configuration["Jwt:Key"]
            ?? throw new InvalidOperationException("Jwt:Key is not configured.");
        if (key.Length < 32)
            throw new InvalidOperationException("Jwt:Key must be at least 32 characters (256 bits) for HMAC-SHA256.");

        var roles = await userManager.GetRolesAsync(user);

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.Id),
            new(ClaimTypes.Name, user.Name),
            new(ClaimTypes.Email, user.Email ?? string.Empty),
            // IdentityUser always has one; a new user whose security stamp hasn't been generated
            // yet would otherwise sign a token OnTokenValidated can never match against the DB.
            new("security_stamp", user.SecurityStamp ?? await RotateAndGetStampAsync(user)),
        };
        claims.AddRange(roles.Select(role => new Claim(ClaimTypes.Role, role)));

        var credentials = new SigningCredentials(
            new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key)), SecurityAlgorithms.HmacSha256);

        var expiresDays = int.TryParse(configuration["Jwt:ExpiresInDays"], out var d) ? d : 7;

        var token = new JwtSecurityToken(
            issuer: configuration["Jwt:Issuer"],
            audience: configuration["Jwt:Audience"],
            claims: claims,
            expires: DateTime.UtcNow.AddDays(expiresDays),
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    private async Task<string> RotateAndGetStampAsync(ApplicationUser user)
    {
        await userManager.UpdateSecurityStampAsync(user);
        return user.SecurityStamp ?? throw new InvalidOperationException($"User {user.Id} has no security stamp after rotation.");
    }
}
