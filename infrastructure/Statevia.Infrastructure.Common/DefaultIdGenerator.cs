using Statevia.Core.Application.Contracts.Services;
using System.Security.Cryptography;

namespace Statevia.Infrastructure.Common;

/// <summary>
/// <see cref="IIdGenerator"/> の既定実装。<see cref="NewSequentialGuid"/> は UUIDv7（時刻順）、
/// <see cref="NewRandomGuid"/> は UUIDv4 相当（散らばり）。
/// </summary>
/// <remarks>
/// BannedApi パイロット後も、本クラス内部で乱数バイトから version/variant を立てる。
/// RFC 4122 バイト列は <c>new Guid(span, bigEndian: true)</c> で構築する。
/// </remarks>
public sealed class DefaultIdGenerator : IIdGenerator
{
    /// <inheritdoc />
    public Guid NewSequentialGuid()
    {
        Span<byte> b = stackalloc byte[16];
        var ms = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        b[0] = (byte)(ms >> 40);
        b[1] = (byte)(ms >> 32);
        b[2] = (byte)(ms >> 24);
        b[3] = (byte)(ms >> 16);
        b[4] = (byte)(ms >> 8);
        b[5] = (byte)ms;
        RandomNumberGenerator.Fill(b[6..]);
        b[6] = (byte)((b[6] & 0x0F) | 0x70);
        b[8] = (byte)((b[8] & 0x3F) | 0x80);
        return new Guid(b, bigEndian: true);
    }

    /// <inheritdoc />
    public Guid NewRandomGuid()
    {
        Span<byte> b = stackalloc byte[16];
        RandomNumberGenerator.Fill(b);
        b[6] = (byte)((b[6] & 0x0F) | 0x40);
        b[8] = (byte)((b[8] & 0x3F) | 0x80);
        return new Guid(b, bigEndian: true);
    }
}
