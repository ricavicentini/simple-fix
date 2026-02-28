using Oms.Client.Api.DependencyInjection;
using Oms.Client.Api.Endpoints;

var builder = WebApplication.CreateBuilder(args);

builder.AddLogger();
builder.WebHost.UseUrls("http://0.0.0.0:8080");
builder.Services.AddOmsClient();

var app = builder.Build();

app.MapClientEndpoints();

await app.RunAsync();
