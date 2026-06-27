using System.Diagnostics.CodeAnalysis;
using Statevia.Actions.Abstractions.Execution;

namespace Statevia.Core.Api.Application.Actions.Execution;

/// <summary>実行モードと設定から <see cref="IActionExecutionBackend"/> を選択する。</summary>
internal interface IActionExecutionBackendSelector
{
    /// <summary>Mode に対応する Backend を解決する。</summary>
    /// <param name="mode">Policy が決定した実行モード（隔離契約）。</param>
    /// <param name="context">実行コンテキスト（将来の Capability / scope 別選択用。現状は未使用）。</param>
    /// <param name="backend">解決された Backend。</param>
    /// <returns>解決できた場合は <c>true</c>。Mode 未登録・複数登録で未指定の場合は <c>false</c>。</returns>
    bool TryResolve(
        ActionExecutionMode mode,
        ActionExecutionContext context,
        [NotNullWhen(true)] out IActionExecutionBackend? backend);
}
