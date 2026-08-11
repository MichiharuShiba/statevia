using Statevia.Core.Application.Contracts.Persistence;
using Statevia.Core.Application.Services;

namespace Statevia.Service.Api.Tests.Services;

/// <summary>runtime checkpoint 内容寿命表（PendingWaits / PendingPhysicalJoin）の単体。</summary>
public sealed class RuntimeCheckpointLifetimePolicyTests
{
    /// <summary>終端は保持理由があっても内容破棄候補。</summary>
    [Theory]
    [InlineData(ExecutionProjectionStatuses.Completed)]
    [InlineData(ExecutionProjectionStatuses.Failed)]
    [InlineData(ExecutionProjectionStatuses.Cancelled)]
    public void IsContentDiscardCandidate_WhenTerminal_ReturnsTrue(
        string status)
    {
        // Arrange
        var reasons = RuntimeCheckpointRetainReasons.PendingWaits
            | RuntimeCheckpointRetainReasons.PendingPhysicalJoin;

        // Act
        var discard = RuntimeCheckpointLifetimePolicy.IsContentDiscardCandidate(status, reasons);

        // Assert
        Assert.True(discard);
    }

    /// <summary>Running かつ保持理由なしは内容破棄候補。</summary>
    [Fact]
    public void IsContentDiscardCandidate_WhenRunningWithoutReasons_ReturnsTrue()
    {
        // Arrange / Act
        var discard = RuntimeCheckpointLifetimePolicy.IsContentDiscardCandidate(
            ExecutionProjectionStatuses.Running,
            RuntimeCheckpointRetainReasons.None);

        // Assert
        Assert.True(discard);
    }

    /// <summary>PendingWaits がある Running は保持。</summary>
    [Fact]
    public void IsContentDiscardCandidate_WhenPendingWaits_ReturnsFalse()
    {
        // Arrange / Act
        var discard = RuntimeCheckpointLifetimePolicy.IsContentDiscardCandidate(
            ExecutionProjectionStatuses.Running,
            RuntimeCheckpointRetainReasons.PendingWaits);

        // Assert
        Assert.False(discard);
    }

    /// <summary>両方の保持理由がある Running も内容破棄しない。</summary>
    [Fact]
    public void IsContentDiscardCandidate_WhenBothRetainReasons_ReturnsFalse()
    {
        // Arrange
        var reasons = RuntimeCheckpointRetainReasons.PendingWaits
            | RuntimeCheckpointRetainReasons.PendingPhysicalJoin;

        // Act
        var discard = RuntimeCheckpointLifetimePolicy.IsContentDiscardCandidate(
            ExecutionProjectionStatuses.Running,
            reasons);

        // Assert
        Assert.False(discard);
    }
}
