using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Statevia.Infrastructure.Security.Configuration;
using Statevia.Service.Api.Application.Actions.Execution;
using Statevia.Service.Api.Configuration;
using Statevia.Service.Api.Hosting;

namespace Statevia.Service.Api.Tests.Hosting;

/// <summary>Options 分類 A（Validate / ValidateOnStart）の境界値テスト。</summary>
public sealed class OptionsValidationTests
{
    /// <summary>既定構成では Options 解決が成功する。</summary>
    [Fact]
    public void AddStateviaCoreApi_DefaultOptions_ResolveSuccessfully()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddStateviaCoreApi(BuildValidConfiguration());
        using var provider = services.BuildServiceProvider();

        // Act
        var projection = provider.GetRequiredService<IOptions<ExecutionProjectionQueueOptions>>().Value;
        var policy = provider.GetRequiredService<IOptions<ExecutionPolicyOptions>>().Value;
        var actionHost = provider.GetRequiredService<IOptions<ActionHostClientOptions>>().Value;
        var jwt = provider.GetRequiredService<IOptions<JwtAuthOptions>>().Value;

        // Assert
        Assert.True(projection.MaxGlobalQueueSize >= 1);
        Assert.InRange(
            policy.Sandbox.Docker.DefaultTimeoutSeconds,
            DockerSandboxOptions.MinDefaultTimeoutSeconds,
            DockerSandboxOptions.MaxDefaultTimeoutSeconds);
        Assert.Null(actionHost.BaseUrl);
        Assert.False(string.IsNullOrWhiteSpace(jwt.SigningKey));
    }

    /// <summary>Docker DefaultTimeoutSeconds が下限未満だと Options 解決で失敗する。</summary>
    [Fact]
    public void ExecutionPolicyOptions_WhenDefaultTimeoutSecondsBelowMin_Throws()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddStateviaCoreApi(BuildValidConfiguration(new Dictionary<string, string?>
        {
            ["Statevia:ExecutionPolicy:Sandbox:Docker:DefaultTimeoutSeconds"] = "9"
        }));
        using var provider = services.BuildServiceProvider();

        // Act
        var act = () => _ = provider.GetRequiredService<IOptions<ExecutionPolicyOptions>>().Value;

        // Assert
        var ex = Assert.Throws<OptionsValidationException>(act);
        Assert.Contains("DefaultTimeoutSeconds", ex.Message, StringComparison.Ordinal);
    }

    /// <summary>Sandbox TimeoutSeconds が下限未満だと Options 解決で失敗する。</summary>
    [Fact]
    public void ExecutionPolicyOptions_WhenSandboxTimeoutSecondsBelowMin_Throws()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddStateviaCoreApi(BuildValidConfiguration(new Dictionary<string, string?>
        {
            ["Statevia:ExecutionPolicy:Sandbox:TimeoutSeconds"] = "9"
        }));
        using var provider = services.BuildServiceProvider();

        // Act
        var act = () => _ = provider.GetRequiredService<IOptions<ExecutionPolicyOptions>>().Value;

        // Assert
        var ex = Assert.Throws<OptionsValidationException>(act);
        Assert.Contains("TimeoutSeconds", ex.Message, StringComparison.Ordinal);
    }

    /// <summary>Docker DefaultTimeoutSeconds が上限超過だと Options 解決で失敗する。</summary>
    [Fact]
    public void ExecutionPolicyOptions_WhenDefaultTimeoutSecondsAboveMax_Throws()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddStateviaCoreApi(BuildValidConfiguration(new Dictionary<string, string?>
        {
            ["Statevia:ExecutionPolicy:Sandbox:Docker:DefaultTimeoutSeconds"] = "3601"
        }));
        using var provider = services.BuildServiceProvider();

        // Act
        var act = () => _ = provider.GetRequiredService<IOptions<ExecutionPolicyOptions>>().Value;

        // Assert
        var ex = Assert.Throws<OptionsValidationException>(act);
        Assert.Contains("DefaultTimeoutSeconds", ex.Message, StringComparison.Ordinal);
        Assert.Contains(
            DockerSandboxOptions.MaxDefaultTimeoutSeconds.ToString(System.Globalization.CultureInfo.InvariantCulture),
            ex.Message,
            StringComparison.Ordinal);
    }

    /// <summary>Docker GrpcPort が well-known 帯だと Options 解決で失敗する。</summary>
    [Fact]
    public void ExecutionPolicyOptions_WhenGrpcPortBelowMin_Throws()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddStateviaCoreApi(BuildValidConfiguration(new Dictionary<string, string?>
        {
            ["Statevia:ExecutionPolicy:Sandbox:Docker:GrpcPort"] = "80"
        }));
        using var provider = services.BuildServiceProvider();

        // Act
        var act = () => _ = provider.GetRequiredService<IOptions<ExecutionPolicyOptions>>().Value;

        // Assert
        var ex = Assert.Throws<OptionsValidationException>(act);
        Assert.Contains("GrpcPort", ex.Message, StringComparison.Ordinal);
    }

    /// <summary>Docker NetworkMode none は起動時検証で拒否する。</summary>
    [Fact]
    public void ExecutionPolicyOptions_WhenNetworkModeNone_Throws()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddStateviaCoreApi(BuildValidConfiguration(new Dictionary<string, string?>
        {
            ["Statevia:ExecutionPolicy:Sandbox:Docker:NetworkMode"] = "none"
        }));
        using var provider = services.BuildServiceProvider();

        // Act
        var act = () => _ = provider.GetRequiredService<IOptions<ExecutionPolicyOptions>>().Value;

        // Assert
        var ex = Assert.Throws<OptionsValidationException>(act);
        Assert.Contains("NetworkMode", ex.Message, StringComparison.Ordinal);
    }

    /// <summary>Sandbox MemoryLimitMiB が運用下限未満だと Options 解決で失敗する。</summary>
    [Fact]
    public void ExecutionPolicyOptions_WhenMemoryLimitBelowMin_Throws()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddStateviaCoreApi(BuildValidConfiguration(new Dictionary<string, string?>
        {
            ["Statevia:ExecutionPolicy:Sandbox:MemoryLimitMiB"] = "63"
        }));
        using var provider = services.BuildServiceProvider();

        // Act
        var act = () => _ = provider.GetRequiredService<IOptions<ExecutionPolicyOptions>>().Value;

        // Assert
        var ex = Assert.Throws<OptionsValidationException>(act);
        Assert.Contains("MemoryLimitMiB", ex.Message, StringComparison.Ordinal);
    }

    /// <summary>Sandbox CpuLimit が運用下限未満だと Options 解決で失敗する。</summary>
    [Fact]
    public void ExecutionPolicyOptions_WhenCpuLimitBelowMin_Throws()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddStateviaCoreApi(BuildValidConfiguration(new Dictionary<string, string?>
        {
            ["Statevia:ExecutionPolicy:Sandbox:CpuLimit"] = "0.1"
        }));
        using var provider = services.BuildServiceProvider();

        // Act
        var act = () => _ = provider.GetRequiredService<IOptions<ExecutionPolicyOptions>>().Value;

        // Assert
        var ex = Assert.Throws<OptionsValidationException>(act);
        Assert.Contains("CpuLimit", ex.Message, StringComparison.Ordinal);
    }

    /// <summary>Sandbox MemoryLimitMiB が上限超過だと Options 解決で失敗する。</summary>
    [Fact]
    public void ExecutionPolicyOptions_WhenMemoryLimitAboveMax_Throws()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddStateviaCoreApi(BuildValidConfiguration(new Dictionary<string, string?>
        {
            ["Statevia:ExecutionPolicy:Sandbox:MemoryLimitMiB"] = "8193"
        }));
        using var provider = services.BuildServiceProvider();

        // Act
        var act = () => _ = provider.GetRequiredService<IOptions<ExecutionPolicyOptions>>().Value;

        // Assert
        var ex = Assert.Throws<OptionsValidationException>(act);
        Assert.Contains("MemoryLimitMiB", ex.Message, StringComparison.Ordinal);
    }

    /// <summary>Sandbox CpuLimit が上限超過だと Options 解決で失敗する。</summary>
    [Fact]
    public void ExecutionPolicyOptions_WhenCpuLimitAboveMax_Throws()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddStateviaCoreApi(BuildValidConfiguration(new Dictionary<string, string?>
        {
            ["Statevia:ExecutionPolicy:Sandbox:CpuLimit"] = "8.1"
        }));
        using var provider = services.BuildServiceProvider();

        // Act
        var act = () => _ = provider.GetRequiredService<IOptions<ExecutionPolicyOptions>>().Value;

        // Assert
        var ex = Assert.Throws<OptionsValidationException>(act);
        Assert.Contains("CpuLimit", ex.Message, StringComparison.Ordinal);
    }

    /// <summary>ActionHost BaseUrl が相対 URI だと失敗する。</summary>
    [Fact]
    public void ActionHostClientOptions_WhenBaseUrlRelative_Throws()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddStateviaCoreApi(BuildValidConfiguration(new Dictionary<string, string?>
        {
            ["Statevia:ActionHost:BaseUrl"] = "localhost:5001"
        }));
        using var provider = services.BuildServiceProvider();

        // Act
        var act = () => _ = provider.GetRequiredService<IOptions<ActionHostClientOptions>>().Value;

        // Assert
        var ex = Assert.Throws<OptionsValidationException>(act);
        Assert.Contains("BaseUrl", ex.Message, StringComparison.Ordinal);
    }

    /// <summary>Retry MaxDelayMs が BaseDelayMs 未満だと失敗する。</summary>
    [Fact]
    public void EventDeliveryRetryOptions_WhenMaxDelayLessThanBase_Throws()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddStateviaCoreApi(BuildValidConfiguration(new Dictionary<string, string?>
        {
            ["EventDelivery:Retry:BaseDelayMs"] = "100",
            ["EventDelivery:Retry:MaxDelayMs"] = "10"
        }));
        using var provider = services.BuildServiceProvider();

        // Act
        var act = () => _ = provider
            .GetRequiredService<IOptions<Statevia.Core.Application.Configuration.EventDeliveryRetryOptions>>()
            .Value;

        // Assert
        var ex = Assert.Throws<OptionsValidationException>(act);
        Assert.Contains("MaxDelayMs", ex.Message, StringComparison.Ordinal);
    }

    /// <summary>JWT AccessTokenLifetimeMinutes が 0 以下だと失敗する。</summary>
    [Fact]
    public void JwtAuthOptions_WhenLifetimeMinutesInvalid_Throws()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddStateviaCoreApi(BuildValidConfiguration(new Dictionary<string, string?>
        {
            ["Auth:Jwt:AccessTokenLifetimeMinutes"] = "0"
        }));
        using var provider = services.BuildServiceProvider();

        // Act
        var act = () => _ = provider.GetRequiredService<IOptions<JwtAuthOptions>>().Value;

        // Assert
        var ex = Assert.Throws<OptionsValidationException>(act);
        Assert.Contains("AccessTokenLifetimeMinutes", ex.Message, StringComparison.Ordinal);
    }

    /// <summary>JWT SigningKey が空文字だと失敗する（未設定時のプロパティ既定値とは別契約）。</summary>
    [Fact]
    public void JwtAuthOptions_WhenSigningKeyEmpty_Throws()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddStateviaCoreApi(BuildValidConfiguration(new Dictionary<string, string?>
        {
            ["Auth:Jwt:SigningKey"] = ""
        }));
        using var provider = services.BuildServiceProvider();

        // Act
        var act = () => _ = provider.GetRequiredService<IOptions<JwtAuthOptions>>().Value;

        // Assert
        var ex = Assert.Throws<OptionsValidationException>(act);
        Assert.Contains("SigningKey", ex.Message, StringComparison.Ordinal);
    }

    /// <summary>ValidateOnStart 登録により主要 Options が起動検証対象になる。</summary>
    [Fact]
    public void AddStateviaCoreApi_RegistersValidateOnStartForCriticalOptions()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddStateviaCoreApi(BuildValidConfiguration());

        // Act / Assert
        Assert.Contains(
            services,
            d => d.ServiceType.IsGenericType
                && d.ServiceType.GetGenericTypeDefinition() == typeof(IValidateOptions<>)
                && d.ServiceType.GenericTypeArguments[0] == typeof(ExecutionProjectionQueueOptions));
        Assert.Contains(
            services,
            d => d.ServiceType.IsGenericType
                && d.ServiceType.GetGenericTypeDefinition() == typeof(IValidateOptions<>)
                && d.ServiceType.GenericTypeArguments[0] == typeof(ExecutionPolicyOptions));
        Assert.Contains(
            services,
            d => d.ServiceType.IsGenericType
                && d.ServiceType.GetGenericTypeDefinition() == typeof(IValidateOptions<>)
                && d.ServiceType.GenericTypeArguments[0] == typeof(JwtAuthOptions));
    }

    private static IConfiguration BuildValidConfiguration(IReadOnlyDictionary<string, string?>? overrides = null)
    {
        var values = new Dictionary<string, string?>
        {
            ["ConnectionStrings:DefaultConnection"] = "Host=localhost;Database=test;Username=u;Password=p",
            ["ExecutionProjectionQueue:MaxGlobalQueueSize"] = "100",
            ["ExecutionProjectionQueue:ProjectionFlushDebounceMs"] = "50",
            ["ExecutionProjectionQueue:MaxRetryAttempts"] = "3",
            ["ExecutionProjectionQueue:RetryBaseDelayMs"] = "10",
            ["ExecutionProjectionQueue:RetryMaxDelayMs"] = "100",
            ["EventDelivery:Retry:MaxAttempts"] = "3",
            ["EventDelivery:Retry:BaseDelayMs"] = "10",
            ["EventDelivery:Retry:MaxDelayMs"] = "100",
            ["EventDelivery:Retry:MaxTotalBackoffMs"] = "1000",
            ["EventDelivery:Retry:SerializablePersistenceMaxAttempts"] = "3"
        };

        if (overrides is not null)
        {
            foreach (var (key, value) in overrides)
            {
                values[key] = value;
            }
        }

        return new ConfigurationBuilder().AddInMemoryCollection(values).Build();
    }
}
