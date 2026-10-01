using NetromEcoMeal.Entities;

namespace NetromEcoMeal.Services.Interfaces;

// Issues the bearer token handed back on login/register. See JwtTokenService for the claim shape
// and the security_stamp revocation check.
public interface IJwtTokenService
{
    public Task<string> GenerateTokenAsync(ApplicationUser user);
}
