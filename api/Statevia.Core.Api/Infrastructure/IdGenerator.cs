using System.Security.Cryptography;
using Statevia.Core.Api.Abstractions.Services;

namespace Statevia.Core.Api.Infrastructure;

/// <summary>.NET 8 向け UUID v7（時刻プレフィックス + ランダム）。.NET 9 の Guid.CreateVersion7 と同趣旨。</summary>
public sealed class UuidV7Generator : IIdGenerator
{
    public Guid NewGuid()
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
        return new Guid(b);
    }
}
