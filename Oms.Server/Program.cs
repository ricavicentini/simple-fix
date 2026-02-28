using Oms.Server.Api.DependencyInjection;
using Oms.Server.Api.Endpoints;

var builder = WebApplication.CreateBuilder(args);

builder.Logging.ClearProviders();
builder.Logging.AddSimpleConsole(options =>
{
    options.SingleLine = true;
    options.TimestampFormat = "yyyy-MM-dd HH:mm:ss ";
});
builder.Logging.SetMinimumLevel(LogLevel.Information);

builder.WebHost.UseUrls("http://0.0.0.0:8081");
builder.Services.AddOmsServer();

var app = builder.Build();

app.MapOrderEndpoints();

await app.RunAsync();
