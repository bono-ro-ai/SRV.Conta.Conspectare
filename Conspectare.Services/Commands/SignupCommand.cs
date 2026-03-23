using Conspectare.Domain.Entities;
using Conspectare.Services.Core.Database;

namespace Conspectare.Services.Commands;

public record SignupCommandResult(ApiClient ApiClient, User User, RefreshToken RefreshToken);

public class SignupCommand(ApiClient apiClient, User user, RefreshToken refreshToken)
    : NHibernateConspectareCommand<SignupCommandResult>
{
    protected override SignupCommandResult OnExecute()
    {
        Session.Save(apiClient);
        user.TenantId = apiClient.Id;
        Session.Save(user);
        Session.Flush();
        refreshToken.UserId = user.Id;
        Session.Save(refreshToken);
        return new SignupCommandResult(apiClient, user, refreshToken);
    }
}
