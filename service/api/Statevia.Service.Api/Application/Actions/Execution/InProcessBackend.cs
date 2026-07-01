using Statevia.Actions.Abstractions.Execution;

namespace Statevia.Service.Api.Application.Actions.Execution;

/// <summary>InProcess 実行 Backend。Core-API プロセス内で <c>IStateExecutor</c> を実行する。</summary>
internal sealed class InProcessBackend : IActionExecutionBackend
{
    private readonly IServiceProvider _serviceProvider;

    /// <summary>InProcess Backend を構築する。</summary>
    /// <param name="serviceProvider">実行器ファクトリ解決用。</param>
    public InProcessBackend(IServiceProvider serviceProvider) =>
        _serviceProvider = serviceProvider;

    /// <inheritdoc />
    public ActionExecutionMode Mode => ActionExecutionMode.InProcess;

    /// <inheritdoc />
    public string ProviderKey => "in-process";

    /// <inheritdoc />
    /// <exception cref="InvalidOperationException">
    /// 登録情報・状態コンテキストが渡されない、または InProcess ファクトリが未設定の場合。
    /// </exception>
    public async Task<ActionExecutionResult> ExecuteAsync(
        ActionBackendInvocation invocation,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(invocation);

        if (invocation.Registration is not { } registration)
        {
            throw new InvalidOperationException("InProcess backend requires a catalog registration.");
        }

        if (invocation.StateContext is not { } stateContext)
        {
            throw new InvalidOperationException("InProcess backend requires a state context.");
        }

        if (registration.Entry.InProcessFactory is not { } factory)
        {
            throw new InvalidOperationException(
                $"Action '{registration.Descriptor.ActionId}' does not provide an InProcess factory.");
        }

        var executor = factory(_serviceProvider);
        var output = await executor
            .ExecuteAsync(stateContext, invocation.RuntimeInput, cancellationToken)
            .ConfigureAwait(false);

        return new ActionExecutionResult
        {
            Success = true,
            RuntimeOutput = output,
        };
    }
}
