using System.Text.Json;

namespace Statevia.Core.Application.Contracts.Persistence;

/// <summary>
/// <see cref="ExecutionWorkItemRow.Payload"/> の JSON 契約（camelCase・大文字小文字非区別）。
/// </summary>
/// <remarks>
/// enqueue 側と Worker deserialize 側で同一オプションを使う。
/// recovery の手書き JSON（<c>{"mode":"recovery"}</c>）とも整合する。
/// </remarks>
public static class ExecutionWorkItemPayloadJson
{
    /// <summary>work item Payload 用の共有シリアライザ設定。</summary>
    public static JsonSerializerOptions Options { get; } = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };
}
