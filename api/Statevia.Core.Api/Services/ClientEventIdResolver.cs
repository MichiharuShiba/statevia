using System.Security.Cryptography;
using System.Text;
using Statevia.Core.Api.Abstractions.Services;

namespace Statevia.Core.Api.Services;

/// <summary>
/// HTTP 冪等キーから <c>event_delivery_dedup.client_event_id</c> 用の GUID を決定する。
/// </summary>
public static class ClientEventIdResolver
{
    /// <summary>
    /// <paramref name="idempotencyKey"/> が RFC 形式の GUID ならその値を返す。
    /// 非 GUID のときは SHA-256 先頭 16 バイトから名前空間付き UUID 風の値を生成する。
    /// キーが無いときは <paramref name="idGenerator"/> で新規 GUID を返す（再送単位の冪等は持たない）。
    /// </summary>
    public static Guid FromIdempotencyKey(string? idempotencyKey, IIdGenerator idGenerator)
    {
        if (string.IsNullOrWhiteSpace(idempotencyKey))
            return idGenerator.NewGuid();

        var trimmed = idempotencyKey.Trim();
        if (Guid.TryParse(trimmed, out var parsed))
            return parsed;

        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(trimmed));
        Span<byte> bytes = stackalloc byte[16];
        hash.AsSpan(0, 16).CopyTo(bytes);
        bytes[6] = (byte)((bytes[6] & 0x0F) | 0x50);
        bytes[8] = (byte)((bytes[8] & 0x3F) | 0x80);
        return new Guid(bytes);
    }
}
