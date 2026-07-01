using Statevia.Actions.Abstractions.Catalog;
using Statevia.Actions.Abstractions.Execution;

namespace Statevia.Service.Api.Application.Actions.Execution;

/// <summary>Phase 1 スタブ。常に InProcess を返す。</summary>
internal sealed class AlwaysInProcessPolicy : IActionExecutionPolicy
{
    /// <inheritdoc />
    public ActionExecutionMode Resolve(ActionExecutionContext context, ActionDescriptor descriptor) =>
        ActionExecutionMode.InProcess;
}
