using Microsoft.Extensions.DependencyInjection;
using Statevia.Core.Engine.Abstractions;
using Statevia.Core.Engine.Execution;
using Statevia.Modules;

namespace Statevia.Core.Api.Tests.Fixtures.TestActionModule;

/// <summary>Phase1 テスト用 Action Module。</summary>
public sealed class EchoActionModule : IActionModule
{
    /// <inheritdoc />
    public string ModuleId => "test.module";

    /// <inheritdoc />
    public string Name => "Test Module";

    /// <inheritdoc />
    public string Version => "1.0.0";

    /// <inheritdoc />
    public IEnumerable<ModuleActionRegistration> GetActions(IServiceProvider serviceProvider)
    {
        _ = serviceProvider;
        const string actionId = "test.module.echo";
        yield return new ModuleActionRegistration(
            actionId,
            _ => DefaultStateExecutor.Create(new EchoState()),
            Publication: EchoActionPublication.Create(actionId));
    }
}

/// <summary>入力をそのまま返すテスト state。</summary>
internal sealed class EchoState : IState<object?, object?>
{
    /// <inheritdoc />
    public Task<object?> ExecuteAsync(StateContext ctx, object? input, CancellationToken ct) =>
        Task.FromResult(input);
}
