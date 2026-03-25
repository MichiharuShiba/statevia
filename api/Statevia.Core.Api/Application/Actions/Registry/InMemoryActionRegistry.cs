using System.Diagnostics.CodeAnalysis;
using Statevia.Core.Engine.Abstractions;
using Statevia.Core.Api.Application.Actions.Abstractions;

namespace Statevia.Core.Api.Application.Actions.Registry;

/// <summary>プロセス内の actionId → <see cref="IStateExecutor"/> マップ。</summary>
public sealed class InMemoryActionRegistry : IActionRegistry
{
    private readonly Dictionary<string, IStateExecutor> _executors = new(StringComparer.Ordinal);

    public void Register(string actionId, IStateExecutor executor)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(actionId);
        ArgumentNullException.ThrowIfNull(executor);
        _executors[actionId.Trim()] = executor;
    }

    public bool Exists(string actionId) =>
        !string.IsNullOrWhiteSpace(actionId) && _executors.ContainsKey(actionId.Trim());

    public bool TryResolve(string actionId, [NotNullWhen(true)] out IStateExecutor? executor)
    {
        if (string.IsNullOrWhiteSpace(actionId))
        {
            executor = null;
            return false;
        }

        return _executors.TryGetValue(actionId.Trim(), out executor);
    }
}
