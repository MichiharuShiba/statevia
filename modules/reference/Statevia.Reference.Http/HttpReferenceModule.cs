using Statevia.Core.Actions.Abstractions.Modules;
using Statevia.Core.Engine.Execution;

namespace Statevia.Reference.Http;

/// <summary>HTTPS リクエストを提供する first-party リファレンス Module。</summary>
public sealed class HttpReferenceModule : IActionModule
{
    /// <inheritdoc />
    public string ModuleId => HttpReferenceActionIds.ModuleId;

    /// <inheritdoc />
    public string Name => "Statevia Reference HTTP";

    /// <inheritdoc />
    public string Version => "1.0.0";

    /// <inheritdoc />
    public IEnumerable<ModuleActionRegistration> GetActions(IServiceProvider serviceProvider)
    {
        _ = serviceProvider;
        var actionId = HttpReferenceActionIds.Request;
        yield return new ModuleActionRegistration(
            actionId,
            _ => DefaultStateExecutor.Create(new HttpRequestActionState()),
            Publication: HttpRequestPublication.Create(actionId));
    }
}
