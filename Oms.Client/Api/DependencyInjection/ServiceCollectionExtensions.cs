using Oms.Client.Infrastructure.Fix;

namespace Oms.Client.Api.DependencyInjection;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddOmsClient(this IServiceCollection services)
    {
        services.AddHttpClient("server", client =>
        {
            var baseUrl = Environment.GetEnvironmentVariable("SERVER_HTTP_BASE") ?? "http://localhost:8081";
            client.BaseAddress = new Uri(baseUrl);
        });

        services.AddSingleton<FixClientBridge>();
        services.AddHostedService(sp => sp.GetRequiredService<FixClientBridge>());
        return services;
    }
}
