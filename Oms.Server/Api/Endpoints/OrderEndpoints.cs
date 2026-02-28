using Oms.Shared.Application.Contracts;

namespace Oms.Server.Api.Endpoints;

public static class OrderEndpoints
{
    public static IEndpointRouteBuilder MapOrderEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/health", () => Results.Ok(new { status = "ok" }));
        app.MapGet("/snapshot", (IOrderBook store) => Results.Ok(store.GetSnapshot()));
        return app;
    }
}
