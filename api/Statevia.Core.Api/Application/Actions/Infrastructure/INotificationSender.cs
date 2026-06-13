namespace Statevia.Core.Api.Application.Actions.Infrastructure;

/// <summary>notification builtin の送信先（email 等）。</summary>
internal interface INotificationSender
{
    /// <summary>メール通知を送信する。</summary>
    /// <param name="request">送信内容。</param>
    /// <param name="ct">キャンセルトークン。</param>
    /// <returns>送信結果（messageId 等）。</returns>
    Task<NotificationSendResult> SendEmailAsync(NotificationEmailRequest request, CancellationToken ct);
}

/// <summary>email 通知リクエスト。</summary>
/// <param name="To">宛先。</param>
/// <param name="Subject">件名。</param>
/// <param name="Body">本文。</param>
/// <param name="From">差出人（任意）。</param>
internal sealed record NotificationEmailRequest(string To, string Subject, string Body, string? From);

/// <summary>通知送信結果。</summary>
/// <param name="Channel">チャネル名。</param>
/// <param name="MessageId">外部メッセージ ID（任意）。</param>
internal sealed record NotificationSendResult(string Channel, string? MessageId);
