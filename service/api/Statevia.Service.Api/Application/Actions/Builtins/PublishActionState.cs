using Microsoft.Extensions.DependencyInjection;
using Statevia.Core.Actions.Abstractions.Execution;
using Statevia.Core.Application.Contracts.Services;
using Statevia.Core.Engine.Abstractions;

namespace Statevia.Service.Api.Application.Actions.Builtins;

/// <summary>
/// <c>wait.subscribe</c> と同じ照合でトピックを発行する Event capability。
/// </summary>
/// <remarks>
/// <para>
/// 配送は <see cref="IEventIngressService"/>（HTTP <c>POST /v1/events</c> と同じ正本）。
/// Engine の <c>IEventProvider.PublishTopic</c> には渡さない。
/// </para>
/// <para>
/// input は <c>topic</c> 必須、<c>key</c> 任意（省略は空文字）、<c>payload</c> は受理するが再開値には使わない。
/// 一致 0 件でも <c>dispatched: true</c>。
/// </para>
/// </remarks>
internal sealed class PublishActionState : IState<object?, object?>
{
    private readonly IServiceScopeFactory _scopeFactory;

    /// <summary>スコープ付き ingress 解決用に構築する。</summary>
    /// <param name="scopeFactory">Worker 上で Application サービスを解決する factory。</param>
    public PublishActionState(IServiceScopeFactory scopeFactory) =>
        _scopeFactory = scopeFactory;

    /// <inheritdoc />
    public async Task<object?> ExecuteAsync(StateContext ctx, object? input, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(ctx);

        if (!ActionInputReader.TryReadObject(input, out var fields))
        {
            throw new ArgumentException("publish action requires input.topic.");
        }

        var topic = ActionInputReader.RequireString(fields, "topic");
        var key = ActionInputReader.OptionalString(fields, "key") ?? string.Empty;

        using var scope = _scopeFactory.CreateScope();
        var ingress = scope.ServiceProvider.GetRequiredService<IEventIngressService>();
        await ingress.PublishAsync(topic, key, ct).ConfigureAwait(false);

        return new Dictionary<string, object?>
        {
            ["topic"] = topic,
            ["dispatched"] = true,
        };
    }
}
