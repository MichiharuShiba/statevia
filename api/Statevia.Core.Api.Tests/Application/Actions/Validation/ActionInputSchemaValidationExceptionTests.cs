using Statevia.Core.Api.Application.Actions.Validation;

namespace Statevia.Core.Api.Tests.Application.Actions.Validation;

/// <summary><see cref="ActionInputSchemaValidationException"/> のメッセージ構築と標準コンストラクター。</summary>
public sealed class ActionInputSchemaValidationExceptionTests
{
    /// <summary>構造化エラーからメッセージと Errors を保持する。</summary>
    [Fact]
    public void Constructor_WithErrors_BuildsMessageAndPreservesErrors()
    {
        // Arrange
        var errors = new[]
        {
            new ActionInputValidationError("A", "test.action", "$.input.url", "Required input property 'url' is missing."),
        };

        // Act
        var ex = new ActionInputSchemaValidationException(errors);

        // Assert
        Assert.Single(ex.Errors);
        Assert.Contains("state 'A'", ex.Message, StringComparison.Ordinal);
        Assert.Contains("$.input.url", ex.Message, StringComparison.Ordinal);
    }

    /// <summary>空のエラー一覧は ArgumentException。</summary>
    [Fact]
    public void Constructor_WithEmptyErrors_ThrowsArgumentException()
    {
        // Act & Assert
        Assert.Throws<ArgumentException>(() => new ActionInputSchemaValidationException([]));
    }

    /// <summary>標準コンストラクターは空 Errors を返す。</summary>
    [Fact]
    public void StandardConstructors_InitializeEmptyErrors()
    {
        // Act
        var defaultEx = new ActionInputSchemaValidationException();
        var messageEx = new ActionInputSchemaValidationException("custom");
        var innerEx = new ActionInputSchemaValidationException("custom", new InvalidOperationException("inner"));

        // Assert
        Assert.Empty(defaultEx.Errors);
        Assert.Empty(messageEx.Errors);
        Assert.Empty(innerEx.Errors);
        Assert.Equal("custom", messageEx.Message);
    }
}
