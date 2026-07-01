using Statevia.Service.ActionHost.Grpc;
using Statevia.Service.ActionHost.Modules;
using Statevia.Service.ActionHost.Execution;

namespace Statevia.Service.ActionHost;

/// <summary>起動時に modules ルートを scan / load する。</summary>
internal sealed class ActionHostStartupService : IHostedService
{
    private readonly ActionHostModuleLoader _moduleLoader;

    /// <summary>新しいインスタンスを初期化する。</summary>
    /// <param name="moduleLoader">Module loader。</param>
    public ActionHostStartupService(ActionHostModuleLoader moduleLoader) =>
        _moduleLoader = moduleLoader;

    /// <inheritdoc />
    public Task StartAsync(CancellationToken cancellationToken)
    {
        _moduleLoader.LoadAll(cancellationToken);
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
