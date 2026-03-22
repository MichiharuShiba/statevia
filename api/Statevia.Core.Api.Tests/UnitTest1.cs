using Statevia.Core.Api.Infrastructure;
using Statevia.Core.Api.Services;
using Xunit;

namespace Statevia.Core.Api.Tests;

public sealed class UnitTest1
{
    /// <summary>UuidV7Generator が RFC 4122 の variant とバージョン 7 の UUID を生成することを検証する。</summary>
    [Fact]
    public void UuidV7Generator_ShouldSetVersion7AndRFC4122Variant()
    {
        // Arrange
        var gen = new UuidV7Generator();

        // Act & Assert（複数回サンプルして形式を確認）
        for (var i = 0; i < 200; i++)
        {
            var g = gen.NewGuid();
            var bytes = g.ToByteArray();

            // UUID version is stored in the high nibble of byte[6].
            var version = (bytes[6] >> 4) & 0x0F;
            Assert.Equal(7, version);

            // RFC 4122 variant: the two most significant bits of byte[8] are 10xx.
            Assert.Equal(0x80, bytes[8] & 0xC0);
        }
    }

    /// <summary>CommandDedupService.Create がパス末尾スラッシュを除去し、冪等キーを正規化して重複キーを組み立てることを検証する。</summary>
    [Fact]
    public void CommandDedupService_Create_ShouldTrimEndSlashAndIncludeIdempotencyKey()
    {
        // Arrange
        var svc = new CommandDedupService();

        // Act
        var keyOpt = svc.Create(
            tenantId: "tenant-1",
            idempotencyKey: "  abc123  ",
            method: "POST",
            path: "/v1/workflows/1/")!;

        // Assert
        Assert.NotNull(keyOpt);
        var key = keyOpt.Value;
        Assert.Equal("POST /v1/workflows/1", key.Endpoint);
        Assert.Equal("abc123", key.IdempotencyKey);
        Assert.Equal("tenant-1|POST /v1/workflows/1:abc123", key.DedupKey);
    }
}