using Statevia.Core.Engine.Abstractions;
using Xunit;

namespace Statevia.Core.Engine.Tests.Abstractions;

/// <summary><see cref="ExecutionSignal"/> と <see cref="DomainEvent"/> の契約検証。</summary>
public sealed class ExecutionSemanticsTests
{
    /// <summary>ExecutionSignal は名前を保持する。</summary>
    [Fact]
    public void ExecutionSignal_StoresName()
    {
        // Arrange & Act
        var signal = new ExecutionSignal("approval");

        // Assert
        Assert.Equal("approval", signal.Name);
    }

    /// <summary>DomainEvent は topic と payload 要約を保持する。</summary>
    [Fact]
    public void DomainEvent_StoresTopicAndPayloadSummary()
    {
        // Arrange & Act
        var domainEvent = new DomainEvent("payment.completed", new { status = "ok" });

        // Assert
        Assert.Equal("payment.completed", domainEvent.Topic);
        Assert.NotNull(domainEvent.PayloadSummary);
    }
}
