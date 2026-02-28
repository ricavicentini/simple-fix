using Oms.Shared.Application.Contracts;
using Oms.Shared.Application.Services;
using Oms.Server.Infrastructure.Fix;

namespace Oms.Server.Api.DependencyInjection;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddOmsServer(this IServiceCollection services)
    {
        services.AddSingleton<IOrderValidator, DefaultOrderValidator>();
        services.AddSingleton<IOrderBook, OrderBook>();
        services.AddHostedService<FixServerGateway>();
        return services;
    }
}
