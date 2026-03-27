using Statevia.Core.Api.Services;

namespace Statevia.Core.Api.Tests.Services;

public sealed class CommandDedupServiceTests
{
    /// <summary>
    /// 冪等キーが未指定の場合は重複判定キーを生成しない。
    /// </summary>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void CommandDedupService_Create_WhenIdempotencyKeyIsMissing_ReturnsNull(string? idempotencyKey)
    {
        // Arrange
        var svc = new CommandDedupService();

        // Act
        var keyOpt = svc.Create(
            tenantId: "tenant-1",
            idempotencyKey: idempotencyKey,
            method: "POST",
            path: "/v1/workflows");

        // Assert
        Assert.Null(keyOpt);
    }

    /// <summary>
    /// requestHashが空白のみならDedupKeyにハッシュを含めない。
    /// </summary>
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void CommandDedupService_Create_WhenRequestHashIsWhitespace_DoesNotIncludeRequestHash(string requestHash)
    {
        // Arrange
        var svc = new CommandDedupService();

        // Act
        var keyOpt = svc.Create(
            tenantId: "tenant-1",
            idempotencyKey: "abc123",
            method: "POST",
            path: "/v1/workflows",
            requestHash: requestHash);

        // Assert
        Assert.NotNull(keyOpt);
        var key = keyOpt.Value;
        Assert.Equal("tenant-1|POST /v1/workflows:abc123", key.DedupKey);
    }

    /// <summary>
    /// pathがnullでも空パスとしてエンドポイント文字列を組み立てる。
    /// </summary>
    [Fact]
    public void CommandDedupService_Create_WhenPathIsNull_BuildsEndpointWithEmptyPath()
    {
        // Arrange
        var svc = new CommandDedupService();

        // Act
        var keyOpt = svc.Create(
            tenantId: "tenant-1",
            idempotencyKey: "abc123",
            method: "POST",
            path: null!,
            requestHash: null);

        // Assert
        Assert.NotNull(keyOpt);
        var key = keyOpt.Value;
        Assert.Equal("POST ", key.Endpoint);
        Assert.Equal("abc123", key.IdempotencyKey);
        Assert.Equal("tenant-1|POST :abc123", key.DedupKey);
    }
}

