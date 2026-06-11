using Statevia.Actions.Abstractions.Catalog;
using Statevia.Core.Engine.Abstractions;

namespace Statevia.Core.Api.Application.Actions.Execution;

/// <summary>InProcess 実行 Backend。</summary>
internal sealed class InProcessBackend
{
    private readonly IServiceProvider _serviceProvider;

    /// <summary>
    /// InProcess Backend を構築する。
    /// </summary>
    /// <param name="serviceProvider">実行器ファクトリ解決用。</param>
    public InProcessBackend(IServiceProvider serviceProvider) =>
        _serviceProvider = serviceProvider;

    /// <summary>
    /// Catalog 登録に基づき InProcess で Action を実行する。
    /// </summary>
    /// <param name="registration">Catalog 登録情報。</param>
    /// <param name="stateContext">Engine 状態コンテキスト。</param>
    /// <param name="runtimeInput">Engine が解決した入力。</param>
    /// <param name="cancellationToken">キャンセルトークン。</param>
    public Task<object?> ExecuteAsync(
        ActionRegistration registration,
        StateContext stateContext,
        object? runtimeInput,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(registration);
        ArgumentNullException.ThrowIfNull(stateContext);

        if (registration.Entry.InProcessFactory is not { } factory)
        {
            throw new InvalidOperationException(
                $"Action '{registration.Descriptor.ActionId}' does not provide an InProcess factory.");
        }

        var executor = factory(_serviceProvider);
        return executor.ExecuteAsync(stateContext, runtimeInput, cancellationToken);
    }
}
