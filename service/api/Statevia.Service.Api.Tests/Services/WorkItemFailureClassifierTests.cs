using Statevia.Core.Application.Contracts;
using Statevia.Core.Application.Services;

namespace Statevia.Service.Api.Tests.Services;

/// <summary>work item 失敗分類（恒久 / 一時）の単体テスト。</summary>
public sealed class WorkItemFailureClassifierTests
{
    /// <summary>Restore が短名拒否を包んだ例外は恒久。</summary>
    [Fact]
    public void IsPermanent_WhenRestoreWrapsShortName_ReturnsTrue()
    {
        // Arrange
        var exception = WrapRestoreFailure(
            new ArgumentException("Unknown action 'noop': short names are not supported."));

        // Act
        var permanent = WorkItemFailureClassifier.IsPermanent(exception);

        // Assert
        Assert.True(permanent);
        Assert.Equal(
            WorkItemFailureClassifier.ReasonRestoreInvalid,
            WorkItemFailureClassifier.DescribeReason(exception));
    }

    /// <summary>Restore が name 欠落を包んだ例外は恒久。</summary>
    [Fact]
    public void IsPermanent_WhenRestoreWrapsMissingName_ReturnsTrue()
    {
        // Arrange
        var exception = WrapRestoreFailure(new ArgumentException("Every node must have 'name'."));

        // Act
        var permanent = WorkItemFailureClassifier.IsPermanent(exception);

        // Assert
        Assert.True(permanent);
    }

    /// <summary>コンパイラ文言だけの ArgumentException は、包み無しでは恒久にしない。</summary>
    [Fact]
    public void IsPermanent_WhenUnwrappedCompilerArgument_ReturnsFalse()
    {
        // Arrange
        var exception = new ArgumentException(
            "Unknown action 'noop': short names are not supported; use FQCN or moduleAlias.actionName.");

        // Act
        var permanent = WorkItemFailureClassifier.IsPermanent(exception);

        // Assert
        Assert.False(permanent);
        Assert.Null(WorkItemFailureClassifier.DescribeReason(exception));
    }

    /// <summary>所有獲得の null は一時（上限対象外）。</summary>
    [Fact]
    public void IsOwnershipAcquisitionMiss_WhenGenerationNull_ReturnsTrue()
    {
        // Arrange / Act
        var miss = WorkItemFailureClassifier.IsOwnershipAcquisitionMiss(null);

        // Assert
        Assert.True(miss);
        Assert.False(WorkItemFailureClassifier.IsOwnershipAcquisitionMiss(1));
    }

    /// <summary>実行時 Module 欠落は初期セットに入れない。</summary>
    [Fact]
    public void IsPermanent_WhenModuleMissing_ReturnsFalse()
    {
        // Arrange
        var exception = new InvalidOperationException("Action module is not installed.");

        // Act
        var permanent = WorkItemFailureClassifier.IsPermanent(exception);

        // Assert
        Assert.False(permanent);
        Assert.Null(WorkItemFailureClassifier.DescribeReason(exception));
    }

    /// <summary>未分類例外の理由は例外からは決まらない（上限は attempts 側）。</summary>
    [Fact]
    public void DescribeReason_WhenUnclassified_ReturnsNull()
    {
        // Arrange
        var exception = new InvalidOperationException("transient host error");

        // Act
        var reason = WorkItemFailureClassifier.DescribeReason(exception);

        // Assert
        Assert.Null(reason);
        Assert.False(WorkItemFailureClassifier.IsPermanent(exception));
    }

    /// <summary>定義版行欠落は恒久。</summary>
    [Fact]
    public void IsPermanent_WhenDefinitionVersionMissing_ReturnsTrue()
    {
        // Arrange
        var exception = new NotFoundException(ExecutionValidationMessages.DefinitionNotFound);

        // Act
        var permanent = WorkItemFailureClassifier.IsPermanent(exception);

        // Assert
        Assert.True(permanent);
        Assert.Equal(
            WorkItemFailureClassifier.ReasonDefinitionVersionMissing,
            WorkItemFailureClassifier.DescribeReason(exception));
    }

    private static InvalidOperationException WrapRestoreFailure(Exception inner) =>
        new(WorkItemFailureClassifier.StoredDefinitionVersionInvalidMessage, inner);
}
