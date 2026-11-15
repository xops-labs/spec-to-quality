using IncidentApp.Api.Domain;

namespace IncidentApp.Api.Application;

/// <summary>The authenticated user of the current request (loaded by the API layer after token validation).</summary>
public sealed class CurrentUser
{
    private User? _user;

    public User User => _user ?? throw ApiException.Unauthenticated();

    public void Set(User user) => _user = user;
}
