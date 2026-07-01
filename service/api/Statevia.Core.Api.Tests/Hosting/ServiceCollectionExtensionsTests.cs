using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Statevia.Core.Api.Abstractions.Services;
using Statevia.Core.Api.Configuration;
using Statevia.Core.Api.Contracts;
using Statevia.Core.Api.Hosting;

namespace Statevia.Core.Api.Tests.Hosting;

/// <summary><see cref="ServiceCollectionExtensions"/> の DI 登録テスト。</summary>
public sealed class ServiceCollectionExtensionsTests
{
    /// <summary>Core-API の主要サービスが登録される。</summary>
    [Fact]
    public void AddStateviaCoreApi_RegistersCoreServices()
    {
        // Arrange
        var services = new ServiceCollection();
        var config = BuildConfiguration();

        // Act
        services.AddStateviaCoreApi(config);

        // Assert
        Assert.Contains(services, d => d.ServiceType == typeof(IExecutionService));
        Assert.Contains(services, d => d.ServiceType == typeof(IDefinitionSchemaService));
        Assert.Contains(services, d => d.ServiceType == typeof(ICommandDedupService));
    }

    /// <summary>本番環境では HTTP 本文ログが既定オフになる。</summary>
    [Fact]
    public void AddStateviaCoreApi_Production_DisablesHttpBodyLoggingByDefault()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddSingleton<IHostEnvironment>(new TestHostEnvironment(Environments.Production));
        var config = BuildConfiguration();

        // Act
        services.AddStateviaCoreApi(config);
        using var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<IOptions<RequestLogOptions>>().Value;

        // Assert
        Assert.False(options.LogRequestBody);
        Assert.False(options.LogResponseBody);
    }

    /// <summary>STATEVIA_LOG_HTTP_BODIES=true で本番でも本文ログを有効化する。</summary>
    [Fact]
    public void AddStateviaCoreApi_StateviaLogHttpBodies_EnablesBodyLoggingInProduction()
    {
        // Arrange
        Environment.SetEnvironmentVariable("STATEVIA_LOG_HTTP_BODIES", "true");
        try
        {
            var services = new ServiceCollection();
            services.AddSingleton<IHostEnvironment>(new TestHostEnvironment(Environments.Production));
            var config = BuildConfiguration();

            // Act
            services.AddStateviaCoreApi(config);
            using var provider = services.BuildServiceProvider();
            var options = provider.GetRequiredService<IOptions<RequestLogOptions>>().Value;

            // Assert
            Assert.True(options.LogRequestBody);
            Assert.True(options.LogResponseBody);
        }
        finally
        {
            Environment.SetEnvironmentVariable("STATEVIA_LOG_HTTP_BODIES", null);
        }
    }

    /// <summary>モデル検証失敗時に 422 と details を返すファクトリが登録される。</summary>
    [Fact]
    public void AddStateviaCoreApi_InvalidModelState_ReturnsValidationErrorResponse()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddSingleton<IHostEnvironment>(new TestHostEnvironment(Environments.Development));
        services.AddStateviaCoreApi(BuildConfiguration());
        using var provider = services.BuildServiceProvider();
        var apiBehavior = provider.GetRequiredService<IOptions<ApiBehaviorOptions>>().Value;
        var http = new DefaultHttpContext();
        var modelState = new ModelStateDictionary();
        modelState.AddModelError("DefinitionId", "required");
        var context = new ActionContext(
            http,
            new Microsoft.AspNetCore.Routing.RouteData(),
            new Microsoft.AspNetCore.Mvc.Controllers.ControllerActionDescriptor(),
            modelState);

        // Act
        var result = apiBehavior.InvalidModelStateResponseFactory!(context);

        // Assert
        var objectResult = Assert.IsType<UnprocessableEntityObjectResult>(result);
        var payload = Assert.IsType<ErrorResponse>(objectResult.Value);
        Assert.Equal("VALIDATION_ERROR", payload.Error.Code);
        Assert.NotNull(payload.Error.Details);
    }

    private static IConfiguration BuildConfiguration() =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
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
            })
            .Build();

    private sealed class TestHostEnvironment : IHostEnvironment
    {
        public TestHostEnvironment(string environmentName) => EnvironmentName = environmentName;

        public string EnvironmentName { get; set; }

        public string ApplicationName { get; set; } = "Statevia.Core.Api.Tests";

        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;

        public Microsoft.Extensions.FileProviders.IFileProvider ContentRootFileProvider { get; set; } =
            new Microsoft.Extensions.FileProviders.NullFileProvider();
    }
}
