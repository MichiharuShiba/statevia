using Statevia.Core.Api.Contracts;

namespace Statevia.Core.Api.Tests.Contracts;

/// <summary>API 契約例外のコンストラクタ網羅テスト。</summary>
public sealed class ContractExceptionsTests
{
    /// <summary><see cref="NotFoundException"/> の各コンストラクタ。</summary>
    [Fact]
    public void NotFoundException_Constructors_SetMessage()
    {
        // Act
        var defaultEx = new NotFoundException();
        var messageEx = new NotFoundException("missing");
        var innerEx = new NotFoundException("missing", new InvalidOperationException("inner"));

        // Assert
        Assert.Contains("not found", defaultEx.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal("missing", messageEx.Message);
        Assert.NotNull(innerEx.InnerException);
    }

    /// <summary><see cref="IdempotencyConflictException"/> の各コンストラクタ。</summary>
    [Fact]
    public void IdempotencyConflictException_Constructors_SetMessage()
    {
        // Act
        var defaultEx = new IdempotencyConflictException();
        var messageEx = new IdempotencyConflictException("conflict");
        var innerEx = new IdempotencyConflictException("conflict", new InvalidOperationException("inner"));

        // Assert
        Assert.Contains("idempotency", defaultEx.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal("conflict", messageEx.Message);
        Assert.NotNull(innerEx.InnerException);
    }

    /// <summary><see cref="ApiValidationException"/> の各コンストラクタ。</summary>
    [Fact]
    public void ApiValidationException_Constructors_SetDetails()
    {
        // Arrange
        var details = new { field = "yaml" };

        // Act
        var defaultEx = new ApiValidationException();
        var messageEx = new ApiValidationException("bad");
        var innerEx = new ApiValidationException("bad", new InvalidOperationException("inner"));
        var detailsEx = new ApiValidationException("bad", details);
        var fullEx = new ApiValidationException("bad", details, new InvalidOperationException("inner"));

        // Assert
        Assert.Contains("invalid", defaultEx.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal("bad", messageEx.Message);
        Assert.NotNull(innerEx.InnerException);
        Assert.Same(details, detailsEx.Details);
        Assert.Same(details, fullEx.Details);
        Assert.NotNull(fullEx.InnerException);
    }
}
