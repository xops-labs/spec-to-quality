using System.Security.Claims;
using System.Text;
using IncidentApp.Api.Domain;
using IncidentApp.Api.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace IncidentApp.Api.Application;

public sealed class JwtSettings(string signingKey)
{
    public SymmetricSecurityKey Key { get; } = new(Encoding.UTF8.GetBytes(signingKey));
    public TimeSpan Lifetime { get; } = TimeSpan.FromHours(12); // real (system) time, independent of the injectable clock
}

public sealed class AuthService(AppDbContext db, JwtSettings jwt)
{
    // Used to spend the same effort when the user is unknown, so both failures look alike.
    private static readonly string DummyHash = PasswordHasher.Hash("dummy-password-for-timing");

    public async Task<LoginResponse> LoginAsync(RequestBody body)
    {
        var f = body.Parse();
        var username = f.String("username", 1, 200);
        var password = f.Secret("password");
        f.ThrowIfInvalid();

        var normalized = username!.ToLowerInvariant();
        var user = await db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Username == normalized);
        var ok = PasswordHasher.Verify(password!, user?.PasswordHash ?? DummyHash);
        if (user is null || !ok) throw ApiException.InvalidCredentials();

        var descriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity([new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString())]),
            Expires = DateTime.UtcNow.Add(jwt.Lifetime),
            SigningCredentials = new SigningCredentials(jwt.Key, SecurityAlgorithms.HmacSha256),
        };
        return new LoginResponse(new JsonWebTokenHandler().CreateToken(descriptor), UserDto.From(user));
    }
}
