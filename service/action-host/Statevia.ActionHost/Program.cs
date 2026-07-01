using Microsoft.AspNetCore.Server.Kestrel.Core;
using Statevia.ActionHost;
using Statevia.ActionHost.Execution;
using Statevia.ActionHost.Grpc;
using Statevia.ActionHost.Modules;

var builder = WebApplication.CreateBuilder(args);

builder.WebHost.ConfigureKestrel(options =>
{
    options.ConfigureEndpointDefaults(listenOptions =>
        listenOptions.Protocols = HttpProtocols.Http1AndHttp2);
});

// Docker では ASPNETCORE_URLS（例: http://+:5001）を優先。未設定時のみ appsettings の ListenUrl を使う。
if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("ASPNETCORE_URLS")))
{
    var listenUrl = builder.Configuration[$"{ActionHostOptions.SectionName}:ListenUrl"];
    if (!string.IsNullOrWhiteSpace(listenUrl))
    {
        builder.WebHost.UseUrls(listenUrl.Trim());
    }
}

builder.Services.Configure<ActionHostOptions>(
    builder.Configuration.GetSection(ActionHostOptions.SectionName));

builder.Services.AddGrpc();
builder.Services.AddSingleton<ActionHostActionRegistry>();
builder.Services.AddSingleton<FilesystemModuleDiscoverer>();
builder.Services.AddSingleton<ActionHostModuleLoader>();
builder.Services.AddSingleton<ActionHostExecutor>();
builder.Services.AddHostedService<ActionHostStartupService>();

var app = builder.Build();

app.MapGrpcService<ActionExecutionGrpcService>();
app.MapGet("/", () => "Statevia Action Host (gRPC). Use a gRPC client for ActionExecutionService.");

await app.RunAsync();

/// <summary>WebApplicationFactory 用のエントリポイント参照。</summary>
public partial class Program
{
    /// <summary>インスタンス化を防ぐ。</summary>
    protected Program()
    {
    }
}
