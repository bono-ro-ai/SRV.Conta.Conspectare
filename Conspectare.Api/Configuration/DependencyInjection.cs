using Conspectare.Services.Core.Database;

namespace Conspectare.Api.Configuration;

internal static class DependencyInjection
{
    internal static void RegisterAppServices(IConfiguration config, IServiceCollection services)
    {
        NHibernateConspectare.Configure<NHibernateConspectare>(config.GetConnectionString("ConspectareDb")!);
    }
}
