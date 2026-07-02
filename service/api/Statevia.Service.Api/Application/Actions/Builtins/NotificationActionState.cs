using Statevia.Infrastructure.Notification;
using Statevia.Core.Engine.Abstractions;

namespace Statevia.Service.Api.Application.Actions.Builtins;

/// <summary>email 通知を送信する Notification capability。</summary>
internal sealed class NotificationActionState : IState<object?, object?>
{
    private readonly IServiceScopeFactory _scopeFactory;

    /// <summary>スコープ付き sender 解決用に構築する。</summary>
    public NotificationActionState(IServiceScopeFactory scopeFactory) =>
        _scopeFactory = scopeFactory;

    /// <inheritdoc />
    public async Task<object?> ExecuteAsync(StateContext ctx, object? input, CancellationToken ct)
    {
        if (!ActionInputReader.TryReadObject(input, out var fields))
        {
            throw new ArgumentException("notification action requires input fields.");
        }

        var channel = ActionInputReader.RequireString(fields, "channel");
        if (!string.Equals(channel, "email", StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException("notification action supports channel=email only in MVP.");
        }

        var request = new NotificationEmailRequest(
            ActionInputReader.RequireString(fields, "to"),
            ActionInputReader.RequireString(fields, "subject"),
            ActionInputReader.RequireString(fields, "body"),
            ActionInputReader.OptionalString(fields, "from"));

        using var scope = _scopeFactory.CreateScope();
        var sender = scope.ServiceProvider.GetRequiredService<NotificationSenderResolver>().Resolve();
        var result = await sender.SendEmailAsync(request, ct).ConfigureAwait(false);

        return new Dictionary<string, object?>
        {
            ["channel"] = result.Channel,
            ["messageId"] = result.MessageId,
        };
    }
}
