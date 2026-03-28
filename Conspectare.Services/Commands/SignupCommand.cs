using Conspectare.Domain.Entities;
using Conspectare.Services.Core.Database;

namespace Conspectare.Services.Commands;

public record SignupCommandResult(ApiClient ApiClient, User User, RefreshToken RefreshToken);

public class SignupCommand(ApiClient apiClient, User user, RefreshToken refreshToken)
    : NHibernateConspectareCommand<SignupCommandResult>
{
    /// <summary>
    /// Persists a new tenant signup in a single transaction: saves the API client
    /// (which becomes the tenant id), copies its generated id onto the user record,
    /// saves the user, flushes so the user id is available, then saves the initial
    /// refresh token. Returns all three created entities.
    /// </summary>
    protected override SignupCommandResult OnExecute()
    {
        Session.Save(apiClient);

        // The API client id doubles as the tenant id for this user.
        user.TenantId = apiClient.Id;
        Session.Save(user);

        // Flush is required so the auto-generated user id is assigned before it is
        // copied onto the refresh token foreign key below.
        Session.Flush();

        refreshToken.UserId = user.Id;
        Session.Save(refreshToken);

        return new SignupCommandResult(apiClient, user, refreshToken);
    }
}
