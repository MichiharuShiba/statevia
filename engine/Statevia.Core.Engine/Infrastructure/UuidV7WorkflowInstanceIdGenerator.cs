using System.Security.Cryptography;
using Statevia.Core.Engine.Abstractions;

namespace Statevia.Core.Engine.Infrastructure;

/// <summary>
/// UUID v7（時刻プレフィックス + ランダム）。Core-API の <c>UuidV7Generator</c> と同じバイト構成。
/// </summary>
public sealed class UuidV7WorkflowInstanceIdGenerator : IWorkflowInstanceIdGenerator
{
    public string NewWorkflowInstanceId() => CreateVersion7().ToString();

    private static Guid CreateVersion7()
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
