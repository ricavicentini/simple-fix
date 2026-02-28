namespace Oms.Client.Api.Endpoints;

public static class ClientEndpoints
{
    public static IEndpointRouteBuilder MapClientEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/health", () => Results.Ok(new { status = "ok" }));

        app.MapGet("/snapshot", async (IHttpClientFactory httpFactory, CancellationToken ct) =>
        {
            var client = httpFactory.CreateClient("server");
            using var response = await client.GetAsync("/snapshot", ct);
            if (!response.IsSuccessStatusCode)
            {
                return Results.Problem("Server snapshot failed", statusCode: (int)response.StatusCode);
            }

            var body = await response.Content.ReadAsStringAsync(ct);
            return Results.Content(body, "application/json");
        });

        return app;
    }
}
