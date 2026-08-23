using Statevia.Service.Api.Application.Actions;

namespace Statevia.Service.Api.Tests.Application.Actions;

/// <summary><see cref="WellKnownActionIds"/> の新 FQCN 定数検証。</summary>
public sealed class WellKnownActionIdsTests
{
    /// <summary>残す builtin の canonical ID はリソース.操作形式である。</summary>
    [Fact]
    public void CanonicalIds_UseResourceOperationForm()
    {
        // Assert
        Assert.Equal("statevia.action.builtin.", WellKnownActionIds.BuiltinPrefix);
        Assert.Equal("statevia.action.builtin.execution.noop", WellKnownActionIds.NoOpCanonical);
        Assert.Equal("statevia.action.builtin.execution.sleep", WellKnownActionIds.Sleep);
        Assert.Equal("statevia.action.builtin.execution.signal", WellKnownActionIds.Signal);
        Assert.Equal("statevia.action.builtin.event.publish", WellKnownActionIds.Publish);
        Assert.Equal("statevia.action.builtin.workflow.invoke", WellKnownActionIds.Workflow);
    }
}
