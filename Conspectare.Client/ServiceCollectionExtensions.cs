using Microsoft.Extensions.DependencyInjection;

namespace Conspectare.Client;

public static class ServiceCollectionExtensions
{
    public static IHttpClientBuilder AddConspectareClient(
        this IServiceCollection services,
        Action<ConspectareClientOptions> configureOptions)
    {
        var options = new ConspectareClientOptions();
        configureOptions(options);

        services.Configure(configureOptions);

        return services
            .AddHttpClient<IConspectareClient, ConspectareClient>(http =>
            {
                http.BaseAddress = new Uri(options.BaseUrl.TrimEnd('/'));
                http.DefaultRequestHeaders.Authorization =
                    new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", options.ApiKey);
                http.Timeout = options.Timeout;
            });
    }
}
