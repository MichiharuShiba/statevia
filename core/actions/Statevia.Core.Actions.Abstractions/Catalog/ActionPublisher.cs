namespace Statevia.Core.Actions.Abstractions.Catalog;

/// <summary>Action / Module の公開者情報。</summary>
/// <param name="PublisherId">公開者 ID。</param>
/// <param name="DisplayName">表示名。</param>
public sealed record ActionPublisher(string PublisherId, string DisplayName);
