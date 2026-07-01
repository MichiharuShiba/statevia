using Statevia.ActionHost.Grpc;
using Statevia.ActionHost.Modules;
using Statevia.ActionHost.Execution;

namespace Statevia.ActionHost;

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
