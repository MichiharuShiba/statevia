using Microsoft.Extensions.DependencyInjection;
using Statevia.Core.Engine.Abstractions;
using Statevia.Core.Engine.Execution;
using Statevia.Modules;

namespace Statevia.ActionHost.Tests.Fixtures.MalformedActionModule;

/// <summary>LoadModuleCore のスキップ経路を検証するテスト Module。</summary>
public sealed class MalformedActionModule : IActionModule
{
    /// <inheritdoc />
    public string ModuleId => "malformed.module";

    /// <inheritdoc />
    public string Name => "Malformed Module";

    /// <inheritdoc />
    public string Version => "1.0.0";

    /// <inheritdoc />
    public IEnumerable<ModuleActionRegistration> GetActions(IServiceProvider serviceProvider)
    {
        _ = serviceProvider;

        yield return new ModuleActionRegistration(
            "   ",
            _ => DefaultStateExecutor.Create(new PassThroughState()));

        yield return new ModuleActionRegistration(
            "wrong.prefix.action",
            _ => DefaultStateExecutor.Create(new PassThroughState()));

        yield return new ModuleActionRegistration(
            "malformed.module.nullfactory",
            null!);

        yield return new ModuleActionRegistration(
            "malformed.module.good",
            _ => DefaultStateExecutor.Create(new PassThroughState()));
    }
}

internal sealed class PassThroughState : IState<object?, object?>
{
    /// <inheritdoc />
    public Task<object?> ExecuteAsync(StateContext ctx, object? input, CancellationToken ct) =>
        Task.FromResult(input);
}
