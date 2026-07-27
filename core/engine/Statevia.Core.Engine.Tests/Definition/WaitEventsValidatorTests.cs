using Statevia.Core.Engine.Definition;
using Statevia.Core.Engine.Definition.Validation;
using Statevia.Core.Engine.FSM;
using Xunit;

namespace Statevia.Core.Engine.Tests.Definition;

/// <summary>Wait.events の Level1 検証シナリオ。</summary>
public sealed class WaitEventsValidatorTests
{
    /// <summary>正当な events はエラーにならない。</summary>
    [Fact]
    public void Validate_ValidEvents_NoErrors()
    {
        // Arrange
        var stateNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "Wait", "Approved", "Rejected" };
        var state = new StateDefinition
        {
            Wait = new WaitDefinition
            {
                Events = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    ["approve"] = "Approved",
                    ["reject"] = "Rejected"
                }
            }
        };
        var errors = new List<string>();

        // Act
        WaitEventsValidator.Validate("Wait", state, stateNames, errors);

        // Assert
        Assert.Empty(errors);
    }

    /// <summary>events が空なら失敗する。</summary>
    [Fact]
    public void Validate_EmptyEvents_Fails()
    {
        // Arrange
        var stateNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "Wait", "Done" };
        var state = new StateDefinition
        {
            Wait = new WaitDefinition
            {
                Events = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            }
        };
        var errors = new List<string>();

        // Act
        WaitEventsValidator.Validate("Wait", state, stateNames, errors);

        // Assert
        Assert.Contains(errors, e => e.Contains("must not be empty", StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>予約語イベント名は拒否される。</summary>
    [Theory]
    [InlineData(Fact.Completed)]
    [InlineData(Fact.Failed)]
    [InlineData(Fact.Cancelled)]
    [InlineData(Fact.Joined)]
    public void Validate_ReservedEventName_Fails(string reserved)
    {
        // Arrange
        var stateNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "Wait", "Done" };
        var state = new StateDefinition
        {
            Wait = new WaitDefinition
            {
                Events = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    [reserved] = "Done"
                }
            }
        };
        var errors = new List<string>();

        // Act
        WaitEventsValidator.Validate("Wait", state, stateNames, errors);

        // Assert
        Assert.Contains(errors, e => e.Contains("reserved event name", StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>未知の遷移先は拒否される。</summary>
    [Fact]
    public void Validate_UnknownNextState_Fails()
    {
        // Arrange
        var stateNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "Wait", "Done" };
        var state = new StateDefinition
        {
            Wait = new WaitDefinition
            {
                Events = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    ["approve"] = "Missing"
                }
            }
        };
        var errors = new List<string>();

        // Act
        WaitEventsValidator.Validate("Wait", state, stateNames, errors);

        // Assert
        Assert.Contains(errors, e => e.Contains("unknown state", StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>自己遷移は拒否される。</summary>
    [Fact]
    public void Validate_SelfTransition_Fails()
    {
        // Arrange
        var stateNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "Wait" };
        var state = new StateDefinition
        {
            Wait = new WaitDefinition
            {
                Events = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    ["retry"] = "Wait"
                }
            }
        };
        var errors = new List<string>();

        // Act
        WaitEventsValidator.Validate("Wait", state, stateNames, errors);

        // Assert
        Assert.Contains(errors, e => e.Contains("self-transition", StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>同一 Wait 内のイベント名重複（大文字小文字無視）は拒否される。</summary>
    [Fact]
    public void Validate_DuplicateEventNames_Fails()
    {
        // Arrange: Ordinal 辞書なら Approve / approve を別キーとして保持できる
        var stateNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "Wait", "Done" };
        var state = new StateDefinition
        {
            Wait = new WaitDefinition
            {
                Events = new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["approve"] = "Done",
                    ["Approve"] = "Done"
                }
            }
        };
        var errors = new List<string>();

        // Act
        WaitEventsValidator.Validate("Wait", state, stateNames, errors);

        // Assert
        Assert.Contains(errors, e => e.Contains("duplicate event name", StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>Level1 経由でも wait.events 空が検出される。</summary>
    [Fact]
    public void Level1_RejectsEmptyWaitEvents()
    {
        // Arrange
        var def = new WorkflowDefinition
        {
            Name = "W",
            States = new Dictionary<string, StateDefinition>(StringComparer.OrdinalIgnoreCase)
            {
                ["Wait"] = new StateDefinition
                {
                    Wait = new WaitDefinition
                    {
                        Events = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                    },
                    On = new Dictionary<string, TransitionDefinition>(StringComparer.OrdinalIgnoreCase)
                    {
                        [Fact.Completed] = new TransitionDefinition { End = true }
                    }
                }
            }
        };

        // Act
        var result = Level1Validator.Validate(def);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("must not be empty", StringComparison.OrdinalIgnoreCase));
    }
}
