using System.Diagnostics.CodeAnalysis;
using Statevia.Core.Engine.Abstractions;

namespace Statevia.Core.Api.Application.Actions.Abstractions;

/// <summary>登録済み actionId と <see cref="IStateExecutor"/> の対応。Core-API が定義登録時に検証し、実行時ファクトリが解決する。</summary>
public interface IActionRegistry
{
    void Register(string actionId, IStateExecutor executor);

    bool Exists(string actionId);

    bool TryResolve(string actionId, [NotNullWhen(true)] out IStateExecutor? executor);
}
