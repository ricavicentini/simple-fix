using Oms.Client.Infrastructure.Fix;

var builder = WebApplication.CreateBuilder(args);

builder.WebHost.UseUrls("http://0.0.0.0:8080");
builder.Services.AddHttpClient("server", client =>
{
    var baseUrl = Environment.GetEnvironmentVariable("SERVER_HTTP_BASE") ?? "http://localhost:8081";
    client.BaseAddress = new Uri(baseUrl);
});
builder.Services.AddSingleton<FixClientBridge>();
builder.Services.AddHostedService(sp => sp.GetRequiredService<FixClientBridge>());

var app = builder.Build();

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

await app.RunAsync();
