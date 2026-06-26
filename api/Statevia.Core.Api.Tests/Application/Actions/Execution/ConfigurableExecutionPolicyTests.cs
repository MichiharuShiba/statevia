using Microsoft.Extensions.Options;
using Statevia.Actions.Abstractions.Catalog;
using Statevia.Actions.Abstractions.Execution;
using Statevia.Core.Api.Application.Actions.Execution;

namespace Statevia.Core.Api.Tests.Application.Actions.Execution;

/// <summary><see cref="ConfigurableExecutionPolicy"/> の単体テスト。</summary>
public sealed class ConfigurableExecutionPolicyTests
{
    private static ConfigurableExecutionPolicy CreateSut(string? deploymentProfile = null) =>
        new(Options.Create(new ExecutionPolicyOptions
        {
            DeploymentProfile = deploymentProfile,
        }));

    /// <summary>TrustLevel × Environment マトリクスを検証する。</summary>
    [Theory]
    [InlineData(ActionTrustLevel.Trusted, "Development", null, ActionExecutionMode.InProcess)]
    [InlineData(ActionTrustLevel.Trusted, "Production", null, ActionExecutionMode.InProcess)]
    [InlineData(ActionTrustLevel.Trusted, "Production", "saas-shared", ActionExecutionMode.InProcess)]
    [InlineData(ActionTrustLevel.Verified, "Development", null, ActionExecutionMode.InProcess)]
    [InlineData(ActionTrustLevel.Verified, "Production", null, ActionExecutionMode.OutOfProcess)]
    [InlineData(ActionTrustLevel.Verified, "Production", "saas-shared", ActionExecutionMode.OutOfProcess)]
    [InlineData(ActionTrustLevel.Signed, "Development", null, ActionExecutionMode.OutOfProcess)]
    [InlineData(ActionTrustLevel.Signed, "Production", null, ActionExecutionMode.OutOfProcess)]
    [InlineData(ActionTrustLevel.Signed, "Production", "saas-shared", ActionExecutionMode.OutOfProcess)]
    [InlineData(ActionTrustLevel.Community, "Development", null, ActionExecutionMode.OutOfProcess)]
    [InlineData(ActionTrustLevel.Community, "Production", null, ActionExecutionMode.OutOfProcess)]
    [InlineData(ActionTrustLevel.Untrusted, "Development", null, ActionExecutionMode.OutOfProcess)]
    [InlineData(ActionTrustLevel.Untrusted, "Production", null, ActionExecutionMode.OutOfProcess)]
    [InlineData(ActionTrustLevel.Untrusted, "Production", "saas-shared", ActionExecutionMode.Container)]
    public void Resolve_TrustEnvironmentMatrix_ReturnsExpectedMode(
        ActionTrustLevel trustLevel,
        string environment,
        string? deploymentProfile,
        ActionExecutionMode expectedMode)
    {
        // Arrange
        var sut = CreateSut();
        var descriptor = CreateDescriptor(trustLevel);
        var context = new ActionExecutionContext("tenant", environment, deploymentProfile);

        // Act
        var mode = sut.Resolve(context, descriptor);

        // Assert
        Assert.Equal(expectedMode, mode);
    }

    /// <summary>appsettings の DeploymentProfile が context 未指定時に使われる。</summary>
    [Fact]
    public void Resolve_WhenContextProfileMissing_UsesOptionsDeploymentProfile()
    {
        // Arrange
        var sut = CreateSut(deploymentProfile: "saas-shared");
        var descriptor = CreateDescriptor(ActionTrustLevel.Untrusted);
        var context = new ActionExecutionContext("tenant", "Production", DeploymentProfile: null);

        // Act
        var mode = sut.Resolve(context, descriptor);

        // Assert
        Assert.Equal(ActionExecutionMode.Container, mode);
    }

    /// <summary>RequiresIsolation は OutOfProcess 以上を強制する。</summary>
    [Fact]
    public void Resolve_WhenRequiresIsolation_ForcesOutOfProcessOrStricter()
    {
        // Arrange
        var sut = CreateSut();
        var descriptor = CreateDescriptor(ActionTrustLevel.Trusted) with
        {
            ExecutionHints = new ActionExecutionHints
            {
                RequiresIsolation = true,
            },
        };
        var context = new ActionExecutionContext("tenant", "Development", null);

        // Act
        var mode = sut.Resolve(context, descriptor);

        // Assert
        Assert.Equal(ActionExecutionMode.OutOfProcess, mode);
    }

    /// <summary>PreferredMode で TrustLevel 下限を緩和できない。</summary>
    [Fact]
    public void Resolve_WhenPreferredModeRelaxesTrust_DoesNotRelax()
    {
        // Arrange
        var sut = CreateSut();
        var descriptor = CreateDescriptor(ActionTrustLevel.Community) with
        {
            ExecutionHints = new ActionExecutionHints
            {
                PreferredMode = ActionExecutionMode.InProcess,
            },
        };
        var context = new ActionExecutionContext("tenant", "Development", null);

        // Act
        var mode = sut.Resolve(context, descriptor);

        // Assert
        Assert.Equal(ActionExecutionMode.OutOfProcess, mode);
    }

    /// <summary>より厳しい PreferredMode は反映される。</summary>
    [Fact]
    public void Resolve_WhenPreferredModeIsStricter_AppliesPreferredMode()
    {
        // Arrange
        var sut = CreateSut();
        var descriptor = CreateDescriptor(ActionTrustLevel.Community) with
        {
            ExecutionHints = new ActionExecutionHints
            {
                PreferredMode = ActionExecutionMode.Container,
            },
        };
        var context = new ActionExecutionContext("tenant", "Production", null);

        // Act
        var mode = sut.Resolve(context, descriptor);

        // Assert
        Assert.Equal(ActionExecutionMode.Container, mode);
    }

    /// <summary>AllowedModes が trust 下限より緩い場合は下限を優先する。</summary>
    [Fact]
    public void Resolve_WhenAllowedModesConflictWithTrustMinimum_PrefersTrustMinimum()
    {
        // Arrange
        var sut = CreateSut();
        var descriptor = CreateDescriptor(ActionTrustLevel.Community) with
        {
            ExecutionHints = new ActionExecutionHints
            {
                AllowedModes = new HashSet<ActionExecutionMode> { ActionExecutionMode.InProcess },
            },
        };
        var context = new ActionExecutionContext("tenant", "Production", null);

        // Act
        var mode = sut.Resolve(context, descriptor);

        // Assert
        Assert.Equal(ActionExecutionMode.OutOfProcess, mode);
    }

    private static ActionDescriptor CreateDescriptor(ActionTrustLevel trustLevel) =>
        new()
        {
            ActionId = "test.module.echo",
            ModuleId = "test.module",
            Version = "1.0.0",
            TrustLevel = trustLevel,
            Source = ActionSourceKind.Filesystem,
            Visibility = ActionVisibility.Tenant,
            OwnerTenantId = "00000000-0000-4000-8000-000000000001",
        };
}
