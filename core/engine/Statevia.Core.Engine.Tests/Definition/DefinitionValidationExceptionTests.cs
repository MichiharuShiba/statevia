using Statevia.Core.Engine.Definition.Validation;
using Xunit;

namespace Statevia.Core.Engine.Tests.Definition;

/// <summary><see cref="DefinitionValidationException"/> のメッセージ接頭辞と Issues。</summary>
public class DefinitionValidationExceptionTests
{
    /// <summary>引数なしコンストラクタは空 Issues と既定メッセージ。</summary>
    [Fact]
    public void Ctor_Default_SetsEmptyIssues()
    {
        // Arrange & Act
        var ex = new DefinitionValidationException();

        // Assert
        Assert.Equal("Definition validation failed.", ex.Message);
        Assert.Empty(ex.Issues);
    }

    /// <summary>メッセージと内部例外を保持し Issues は空。</summary>
    [Fact]
    public void Ctor_MessageAndInner_PreservesInner()
    {
        // Arrange
        var inner = new InvalidOperationException("inner");

        // Act
        var ex = new DefinitionValidationException("wrapped", inner);

        // Assert
        Assert.Equal("wrapped", ex.Message);
        Assert.Same(inner, ex.InnerException);
        Assert.Empty(ex.Issues);
    }

    /// <summary>Structural の RuleId は Level 1 接頭辞になる。</summary>
    [Fact]
    public void Ctor_WhenStructuralIssue_PrefixesLevel1()
    {
        // Arrange
        var result = new ValidationResult(
        [
            new ValidationIssue("Structural.AtLeastOneState", "need a state")
        ]);

        // Act
        var ex = new DefinitionValidationException(result);

        // Assert
        Assert.StartsWith("Level 1 validation failed: ", ex.Message, StringComparison.Ordinal);
        Assert.Contains("need a state", ex.Message, StringComparison.Ordinal);
        Assert.Equal("Structural.AtLeastOneState", ex.Issues[0].RuleId);
    }

    /// <summary>Reachability の RuleId は Level 2 接頭辞になる。</summary>
    [Fact]
    public void Ctor_WhenReachabilityIssue_PrefixesLevel2()
    {
        // Arrange
        var result = new ValidationResult(
        [
            new ValidationIssue("Reachability.UnreachableStates", "orphan")
        ]);

        // Act
        var ex = new DefinitionValidationException(result);

        // Assert
        Assert.StartsWith("Level 2 validation failed: ", ex.Message, StringComparison.Ordinal);
        Assert.Equal("Reachability.UnreachableStates", ex.Issues[0].RuleId);
    }

    /// <summary>ForkRegion の RuleId は汎用接頭辞になる。</summary>
    [Fact]
    public void Ctor_WhenForkRegionIssue_UsesGenericPrefix()
    {
        // Arrange
        var result = new ValidationResult(
        [
            new ValidationIssue("ForkRegion.EgressWithoutJoin", "egress", "Left")
        ]);

        // Act
        var ex = new DefinitionValidationException(result);

        // Assert
        Assert.StartsWith("Definition validation failed: ", ex.Message, StringComparison.Ordinal);
        Assert.Equal("Left", ex.Issues[0].StateName);
    }

    /// <summary>Issues が空の結果は汎用接頭辞のみ。</summary>
    [Fact]
    public void Ctor_WhenIssuesEmpty_UsesGenericPrefix()
    {
        // Arrange
        var result = new ValidationResult(Array.Empty<ValidationIssue>());

        // Act
        var ex = new DefinitionValidationException(result);

        // Assert
        Assert.Equal("Definition validation failed: ", ex.Message);
        Assert.Empty(ex.Issues);
    }

    /// <summary>文字列だけの ValidationResult は RuleId 空の Issue になる。</summary>
    [Fact]
    public void ValidationResult_FromMessages_SetsEmptyRuleId()
    {
        // Arrange
        var result = new ValidationResult(["legacy error"]);

        // Act
        var issue = Assert.Single(result.Issues);

        // Assert
        Assert.False(result.IsValid);
        Assert.Equal(string.Empty, issue.RuleId);
        Assert.Equal("legacy error", issue.Message);
    }

    /// <summary>null の検証結果は ArgumentNullException。</summary>
    [Fact]
    public void Ctor_WhenResultIsNull_Throws()
    {
        // Arrange
        ValidationResult result = null!;

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => new DefinitionValidationException(result));
    }
}
