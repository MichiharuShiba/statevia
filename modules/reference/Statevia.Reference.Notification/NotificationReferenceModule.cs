using Statevia.Core.Actions.Abstractions.Modules;
using Statevia.Core.Engine.Execution;

namespace Statevia.Reference.Notification;

/// <summary>email 通知を提供する first-party リファレンス Module。</summary>
public sealed class NotificationReferenceModule : IActionModule
{
    /// <inheritdoc />
    public string ModuleId => NotificationReferenceActionIds.ModuleId;

    /// <inheritdoc />
    public string Name => "Statevia Reference Notification";

    /// <inheritdoc />
    public string Version => "1.0.0";

    /// <inheritdoc />
    public IEnumerable<ModuleActionRegistration> GetActions(IServiceProvider serviceProvider)
    {
        _ = serviceProvider;
        var actionId = NotificationReferenceActionIds.Send;
        yield return new ModuleActionRegistration(
            actionId,
            _ => DefaultStateExecutor.Create(new NotificationSendActionState()),
            Publication: NotificationSendPublication.Create(actionId));
    }
}
