using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Statevia.Runtime.Configuration;
using Statevia.Runtime.Services;
using Statevia.Service.Api.Application.Actions.Execution;
using Statevia.Service.Api.Configuration;
using Statevia.Service.Api.Hosting;

namespace Statevia.Service.Api.Tests.Hosting;

/// <summary>Options 分類 A（Validate / ValidateOnStart）の境界値テスト。</summary>
public sealed class OptionsValidationTests
{
    /// <summary>既定構成では Options 解決が成功する。</summary>
    [Fact]
    public void AddStateviaServiceApi_DefaultOptions_ResolveSuccessfully()
    {
        // Arrange
        using var provider = CreateProvider();

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
        var worker = provider.GetRequiredService<IOptions<WorkerRuntimeOptions>>().Value;
        Assert.Equal(WorkerRuntimeOptions.MinMaxConcurrency, worker.MaxConcurrency);
        Assert.Equal(WorkerRuntimeOptions.MinCancelConcurrency, worker.CancelConcurrency);
        Assert.Equal(TimeSpan.FromMinutes(10), worker.NoProgressTimeout);
        Assert.Equal(20, worker.MaxAttempts);
    }

    /// <summary>Worker MaxConcurrency が 0 だと Options 解決で失敗する。</summary>
    [Fact]
    public void WorkerRuntimeOptions_WhenMaxConcurrencyZero_Throws()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddStateviaServiceApi(BuildValidConfiguration(new Dictionary<string, string?>
        {
            ["Statevia:Runtime:Worker:MaxConcurrency"] = "0"
        }));
        using var provider = services.BuildServiceProvider();

        // Act
        var act = () => _ = provider.GetRequiredService<IOptions<WorkerRuntimeOptions>>().Value;

        // Assert
        var ex = Assert.Throws<OptionsValidationException>(act);
        Assert.Contains("MaxConcurrency", ex.Message, StringComparison.Ordinal);
    }

    /// <summary>Worker MaxConcurrency が上限超過だと Options 解決で失敗する。</summary>
    [Fact]
    public void WorkerRuntimeOptions_WhenMaxConcurrencyAboveMax_Throws()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddStateviaServiceApi(BuildValidConfiguration(new Dictionary<string, string?>
        {
            ["Statevia:Runtime:Worker:MaxConcurrency"] = "65"
        }));
        using var provider = services.BuildServiceProvider();

        // Act
        var act = () => _ = provider.GetRequiredService<IOptions<WorkerRuntimeOptions>>().Value;

        // Assert
        var ex = Assert.Throws<OptionsValidationException>(act);
        Assert.Contains("MaxConcurrency", ex.Message, StringComparison.Ordinal);
    }

    /// <summary>Worker MaxConcurrency に 4 をバインドできる。</summary>
    [Fact]
    public void WorkerRuntimeOptions_WhenMaxConcurrencyFour_Binds()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddStateviaServiceApi(BuildValidConfiguration(new Dictionary<string, string?>
        {
            ["Statevia:Runtime:Worker:MaxConcurrency"] = "4"
        }));
        using var provider = services.BuildServiceProvider();

        // Act
        var worker = provider.GetRequiredService<IOptions<WorkerRuntimeOptions>>().Value;

        // Assert
        Assert.Equal(4, worker.MaxConcurrency);
    }

    /// <summary>Worker MaxAttempts が 0 だと Options 解決で失敗する。</summary>
    [Fact]
    public void WorkerRuntimeOptions_WhenMaxAttemptsZero_Throws()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddStateviaServiceApi(BuildValidConfiguration(new Dictionary<string, string?>
        {
            ["Statevia:Runtime:Worker:MaxAttempts"] = "0"
        }));
        using var provider = services.BuildServiceProvider();

        // Act
        var act = () => _ = provider.GetRequiredService<IOptions<WorkerRuntimeOptions>>().Value;

        // Assert
        var ex = Assert.Throws<OptionsValidationException>(act);
        Assert.Contains("MaxAttempts", ex.Message, StringComparison.Ordinal);
    }

    /// <summary>Docker DefaultTimeoutSeconds が下限未満だと Options 解決で失敗する。</summary>
    [Fact]
    public void ExecutionPolicyOptions_WhenDefaultTimeoutSecondsBelowMin_Throws()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddStateviaServiceApi(BuildValidConfiguration(new Dictionary<string, string?>
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
        services.AddStateviaServiceApi(BuildValidConfiguration(new Dictionary<string, string?>
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
        services.AddStateviaServiceApi(BuildValidConfiguration(new Dictionary<string, string?>
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
        services.AddStateviaServiceApi(BuildValidConfiguration(new Dictionary<string, string?>
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
        services.AddStateviaServiceApi(BuildValidConfiguration(new Dictionary<string, string?>
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
        services.AddStateviaServiceApi(BuildValidConfiguration(new Dictionary<string, string?>
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
        services.AddStateviaServiceApi(BuildValidConfiguration(new Dictionary<string, string?>
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
        services.AddStateviaServiceApi(BuildValidConfiguration(new Dictionary<string, string?>
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
        services.AddStateviaServiceApi(BuildValidConfiguration(new Dictionary<string, string?>
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
        services.AddStateviaServiceApi(BuildValidConfiguration(new Dictionary<string, string?>
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
        services.AddStateviaServiceApi(BuildValidConfiguration(new Dictionary<string, string?>
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
        using var provider = CreateProvider(new Dictionary<string, string?>
        {
            ["Auth:Jwt:AccessTokenLifetimeMinutes"] = "0"
        });

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
        using var provider = CreateProvider(new Dictionary<string, string?>
        {
            ["Auth:Jwt:SigningKey"] = ""
        });

        // Act
        var act = () => _ = provider.GetRequiredService<IOptions<JwtAuthOptions>>().Value;

        // Assert
        var ex = Assert.Throws<OptionsValidationException>(act);
        Assert.Contains("SigningKey", ex.Message, StringComparison.Ordinal);
    }

    /// <summary>Development では既定 SigningKey で起動検証が成功する。</summary>
    [Fact]
    public void JwtAuthOptions_WhenDevelopmentAndDefaultSigningKey_Resolves()
    {
        // Arrange
        using var provider = CreateProvider(environmentName: Environments.Development);

        // Act
        var jwt = provider.GetRequiredService<IOptions<JwtAuthOptions>>().Value;

        // Assert
        Assert.Equal(JwtAuthOptions.DevelopmentSigningKey, jwt.SigningKey);
    }

    /// <summary>Production / Staging では既定 SigningKey で起動検証が失敗し、メッセージに鍵値を出さない。</summary>
    [Theory]
    [InlineData("Production")]
    [InlineData("Staging")]
    public void JwtAuthOptions_WhenRestrictedEnvironmentAndDefaultSigningKey_Throws(string environmentName)
    {
        // Arrange
        using var provider = CreateProvider(environmentName: environmentName);

        // Act
        var act = () => _ = provider.GetRequiredService<IOptions<JwtAuthOptions>>().Value;

        // Assert
        var ex = Assert.Throws<OptionsValidationException>(act);
        Assert.Contains(JwtAuthOptions.RestrictedEnvironmentSigningKeyMessage, ex.Message, StringComparison.Ordinal);
        Assert.DoesNotContain(JwtAuthOptions.DevelopmentSigningKey, ex.Message, StringComparison.Ordinal);
    }

    /// <summary>Production / Staging では 32 文字未満の SigningKey で起動検証が失敗する。</summary>
    [Theory]
    [InlineData("Production")]
    [InlineData("Staging")]
    public void JwtAuthOptions_WhenRestrictedEnvironmentAndShortSigningKey_Throws(string environmentName)
    {
        // Arrange
        const string shortKey = "abcdefghijklmnopqrstuvwxyz12345";
        using var provider = CreateProvider(
            new Dictionary<string, string?>
            {
                ["Auth:Jwt:SigningKey"] = shortKey
            },
            environmentName);

        // Act
        var act = () => _ = provider.GetRequiredService<IOptions<JwtAuthOptions>>().Value;

        // Assert
        Assert.Equal(JwtAuthOptions.MinSigningKeyLength - 1, shortKey.Length);
        var ex = Assert.Throws<OptionsValidationException>(act);
        Assert.Contains(JwtAuthOptions.RestrictedEnvironmentSigningKeyMessage, ex.Message, StringComparison.Ordinal);
        Assert.DoesNotContain(shortKey, ex.Message, StringComparison.Ordinal);
    }

    /// <summary>Production / Staging では既定以外かつ 32 文字以上なら成功する。</summary>
    [Theory]
    [InlineData("Production")]
    [InlineData("Staging")]
    public void JwtAuthOptions_WhenRestrictedEnvironmentAndCustomLongSigningKey_Resolves(string environmentName)
    {
        // Arrange
        const string customKey = "test-signing-key-at-least-32-bytes-long!!";
        using var provider = CreateProvider(
            new Dictionary<string, string?>
            {
                ["Auth:Jwt:SigningKey"] = customKey
            },
            environmentName);

        // Act
        var jwt = provider.GetRequiredService<IOptions<JwtAuthOptions>>().Value;

        // Assert
        Assert.True(customKey.Length >= JwtAuthOptions.MinSigningKeyLength);
        Assert.Equal(customKey, jwt.SigningKey);
    }

    /// <summary>ValidateOnStart 登録により主要 Options が起動検証対象になる。</summary>
    [Fact]
    public void AddStateviaServiceApi_RegistersValidateOnStartForCriticalOptions()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddStateviaServiceApi(BuildValidConfiguration());

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
        Assert.Contains(
            services,
            d => d.ServiceType.IsGenericType
                && d.ServiceType.GetGenericTypeDefinition() == typeof(IValidateOptions<>)
                && d.ServiceType.GenericTypeArguments[0] == typeof(RuntimeOptions));
        Assert.Contains(
            services,
            d => d.ServiceType.IsGenericType
                && d.ServiceType.GetGenericTypeDefinition() == typeof(IValidateOptions<>)
                && d.ServiceType.GenericTypeArguments[0] == typeof(WorkerRuntimeOptions));
    }

    /// <summary>Runtime フラグ Off のときプロセス内 Worker / Scheduler を登録しない。</summary>
    [Fact]
    public void AddStateviaServiceApi_DisablesInProcessRuntime_WhenFlagsAreFalse()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddStateviaServiceApi(BuildValidConfiguration(new Dictionary<string, string?>
        {
            ["Statevia:Runtime:EnableInProcessWorker"] = "false",
            ["Statevia:Runtime:EnableInProcessDelayWaitScheduler"] = "false",
            ["Statevia:Runtime:EnableInProcessOwnershipRecovery"] = "false"
        }));

        // Act / Assert
        Assert.DoesNotContain(
            services,
            d => d.ImplementationType == typeof(ExecutionWorkItemWorkerHostedService));
        Assert.DoesNotContain(
            services,
            d => d.ImplementationType == typeof(DelayWaitSchedulerHostedService));
        Assert.DoesNotContain(
            services,
            d => d.ImplementationType == typeof(ExecutionOwnershipRecoveryHostedService));
    }

    /// <summary>Worker ホストは実行系のみで API 向けサービスを登録しない。</summary>
    [Fact]
    public void AddStateviaWorkerHost_RegistersExecutionRuntimeWithoutApiHost()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddStateviaWorkerHost(BuildValidConfiguration());

        // Act / Assert
        Assert.Contains(
            services,
            d => d.ImplementationType == typeof(ExecutionWorkItemWorkerHostedService));
        Assert.DoesNotContain(
            services,
            d => d.ImplementationType == typeof(DelayWaitSchedulerHostedService));
        Assert.DoesNotContain(
            services,
            d => d.ImplementationType == typeof(ExecutionOwnershipRecoveryHostedService));
        Assert.DoesNotContain(
            services,
            d => d.ServiceType == typeof(Statevia.Service.Api.Services.IAuthService));
        Assert.DoesNotContain(
            services,
            d => d.ImplementationType == typeof(TenantBootstrapHostedService));
        Assert.DoesNotContain(
            services,
            d => d.ServiceType == typeof(Statevia.Infrastructure.Security.JwtTokenService));
        Assert.DoesNotContain(
            services,
            d => d.ServiceType.IsGenericType
                && d.ServiceType.GetGenericTypeDefinition() == typeof(IValidateOptions<>)
                && d.ServiceType.GenericTypeArguments[0] == typeof(JwtAuthOptions));
    }

    /// <summary>
    /// 専用 Worker は Production でも開発既定 JWT 鍵で起動検証しない（ASP.NET イメージ既定が Production）。
    /// </summary>
    [Fact]
    public void AddStateviaWorkerHost_WhenProductionAndDefaultSigningKey_StartupValidationSucceeds()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddSingleton<IHostEnvironment>(new JwtTestHostEnvironment(Environments.Production));
        services.AddStateviaWorkerHost(BuildValidConfiguration());
        using var provider = services.BuildServiceProvider();

        // Act
        var act = () =>
        {
            foreach (var validator in provider.GetServices<IStartupValidator>())
                validator.Validate();
        };

        // Assert
        var exception = Record.Exception(act);
        Assert.Null(exception);
        Assert.Null(provider.GetService<Statevia.Infrastructure.Security.JwtTokenService>());
    }

    /// <summary>
    /// Options 解決用のプロバイダを構築する。JWT の環境別検証は <see cref="IHostEnvironment"/> に依存する。
    /// </summary>
    private static ServiceProvider CreateProvider(
        IReadOnlyDictionary<string, string?>? overrides = null,
        string environmentName = "Development")
    {
        var services = new ServiceCollection();
        services.AddSingleton<IHostEnvironment>(new JwtTestHostEnvironment(environmentName));
        services.AddStateviaServiceApi(BuildValidConfiguration(overrides));
        return services.BuildServiceProvider();
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

    /// <summary>JWT Options 検証テスト用のホスト環境。</summary>
    /// <param name="environmentName">ホスト環境名。</param>
    private sealed class JwtTestHostEnvironment(string environmentName) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = environmentName;

        public string ApplicationName { get; set; } = "Statevia.Service.Api.Tests";

        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;

        public Microsoft.Extensions.FileProviders.IFileProvider ContentRootFileProvider { get; set; } =
            new Microsoft.Extensions.FileProviders.NullFileProvider();
    }
}
