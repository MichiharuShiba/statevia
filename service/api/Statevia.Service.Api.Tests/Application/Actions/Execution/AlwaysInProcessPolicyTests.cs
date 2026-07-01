using Statevia.Actions.Abstractions.Catalog;
using Statevia.Actions.Abstractions.Execution;
using Statevia.Service.Api.Application.Actions.Execution;

namespace Statevia.Service.Api.Tests.Application.Actions.Execution;

/// <summary><see cref="AlwaysInProcessPolicy"/> の単体テスト。</summary>
public sealed class AlwaysInProcessPolicyTests
{
    /// <summary>任意 Descriptor に InProcess が返る。</summary>
    [Fact]
    public void Resolve_ReturnsInProcess_ForAnyDescriptor()
    {
        // Arrange
        var sut = new AlwaysInProcessPolicy();
        var descriptor = new ActionDescriptor
        {
            ActionId = "statevia.action.builtin.noop",
            ModuleId = "statevia.builtin",
            Version = "1.0.0",
            TrustLevel = ActionTrustLevel.Untrusted,
            Visibility = ActionVisibility.Builtin,
        };
        var context = new ActionExecutionContext("tenant", "Production", null);

        // Act
        var mode = sut.Resolve(context, descriptor);

        // Assert
        Assert.Equal(ActionExecutionMode.InProcess, mode);
    }
}
