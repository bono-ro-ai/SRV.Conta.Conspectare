using Conspectare.Domain.Entities;
using Conspectare.Services.Core.Database;

namespace Conspectare.Services.Commands;

public class SaveApiClientCommand(ApiClient apiClient)
    : NHibernateConspectareCommand
{
    protected override void OnExecute()
    {
        Session.Save(apiClient);
    }
}
