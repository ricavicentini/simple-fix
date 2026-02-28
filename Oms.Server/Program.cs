using Oms.Server.Api.DependencyInjection;
using Oms.Server.Api.Endpoints;

var builder = WebApplication.CreateBuilder(args);

builder.AddLogger();

builder.WebHost.UseUrls("http://0.0.0.0:8081");
builder.Services.AddOmsServer();

var app = builder.Build();

app.MapOrderEndpoints();

await app.RunAsync();
