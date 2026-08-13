using Statevia.Core.Application.Contracts.Persistence;
using Statevia.Core.Application.Services;

namespace Statevia.Service.Api.Tests.Services;

/// <summary><see cref="RuntimeCheckpointLifetimePolicy"/> の寿命表破棄候補判定。</summary>
public sealed class RuntimeCheckpointLifetimePolicyTests
{
    /// <summary>終端 status は保持理由に関わらず破棄候補。</summary>
    [Theory]
    [InlineData(ExecutionProjectionStatuses.Completed)]
    [InlineData(ExecutionProjectionStatuses.Failed)]
    [InlineData(ExecutionProjectionStatuses.Cancelled)]
    public void IsContentDiscardCandidate_WhenTerminal_ReturnsTrue(string status)
    {
        // Arrange / Act
        var discard = RuntimeCheckpointLifetimePolicy.IsContentDiscardCandidate(
            status,
            RuntimeCheckpointRetainReasons.PendingWaits | RuntimeCheckpointRetainReasons.PendingPhysicalJoin);

        // Assert
        Assert.True(discard);
    }

    /// <summary>Running で保持理由なしは破棄候補。</summary>
    [Fact]
    public void IsContentDiscardCandidate_WhenRunningWithoutRetainReasons_ReturnsTrue()
    {
        // Arrange / Act
        var discard = RuntimeCheckpointLifetimePolicy.IsContentDiscardCandidate(
            ExecutionProjectionStatuses.Running,
            RuntimeCheckpointRetainReasons.None);

        // Assert
        Assert.True(discard);
    }

    /// <summary>Running で Wait 保持は破棄しない。</summary>
    [Fact]
    public void IsContentDiscardCandidate_WhenRunningWithPendingWaits_ReturnsFalse()
    {
        // Arrange / Act
        var discard = RuntimeCheckpointLifetimePolicy.IsContentDiscardCandidate(
            ExecutionProjectionStatuses.Running,
            RuntimeCheckpointRetainReasons.PendingWaits);

        // Assert
        Assert.False(discard);
    }

    /// <summary>Running で物理 Join 保持は破棄しない。</summary>
    [Fact]
    public void IsContentDiscardCandidate_WhenRunningWithPendingPhysicalJoin_ReturnsFalse()
    {
        // Arrange / Act
        var discard = RuntimeCheckpointLifetimePolicy.IsContentDiscardCandidate(
            ExecutionProjectionStatuses.Running,
            RuntimeCheckpointRetainReasons.PendingPhysicalJoin);

        // Assert
        Assert.False(discard);
    }
}
