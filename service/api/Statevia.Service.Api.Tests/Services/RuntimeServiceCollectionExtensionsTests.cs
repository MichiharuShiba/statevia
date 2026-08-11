using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Statevia.Runtime.DependencyInjection;
using Statevia.Runtime.Services;

namespace Statevia.Service.Api.Tests.Services;

/// <summary>Runtime DI 拡張の薄い登録テスト（Runtime カバレッジ用）。</summary>
public sealed class RuntimeServiceCollectionExtensionsTests
{
    /// <summary>scheduler / worker hosted service が ServiceDescriptor として登録される。</summary>
    [Fact]
    public void AddRuntimeHostedServices_RegistersExpectedBackgroundServiceDescriptors()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services.AddStateviaRuntimeSchedulers();
        services.AddStateviaRuntimeWorkerHostedService();
        services.AddStateviaRuntimeOptions(new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Statevia:Runtime:WorkerEnabled"] = "true"
        }).Build());

        // Assert
        var hostedImplementations = services
            .Where(descriptor => descriptor.ServiceType == typeof(IHostedService))
            .Select(descriptor => descriptor.ImplementationType)
            .ToList();
        Assert.Contains(typeof(DelayWaitSchedulerHostedService), hostedImplementations);
        Assert.Contains(typeof(ExecutionOwnershipRecoveryHostedService), hostedImplementations);
        Assert.Contains(typeof(ExecutionWorkItemWorkerHostedService), hostedImplementations);
    }

    /// <summary>AddStateviaSchedulerHost は Persistence と Scheduler を登録する。</summary>
    [Fact]
    public void AddStateviaSchedulerHost_WithConnectionString_RegistersSchedulers()
    {
        // Arrange
        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:DefaultConnection"] = "Host=127.0.0.1;Database=statevia;Username=x;Password=y"
        }).Build();

        // Act
        services.AddStateviaSchedulerHost(configuration);

        // Assert
        var hostedImplementations = services
            .Where(descriptor => descriptor.ServiceType == typeof(IHostedService))
            .Select(descriptor => descriptor.ImplementationType)
            .ToList();
        Assert.Contains(typeof(DelayWaitSchedulerHostedService), hostedImplementations);
        Assert.Contains(typeof(ExecutionOwnershipRecoveryHostedService), hostedImplementations);
    }

    /// <summary>null 引数は ArgumentNullException になる。</summary>
    [Fact]
    public void AddRuntimeExtensions_NullArguments_Throw()
    {
        // Arrange
        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder().Build();

        // Act + Assert
        Assert.Throws<ArgumentNullException>(
            () => RuntimeServiceCollectionExtensions.AddStateviaSchedulerHost(null!, configuration));
        Assert.Throws<ArgumentNullException>(() => services.AddStateviaSchedulerHost(null!));
        Assert.Throws<ArgumentNullException>(
            () => RuntimeServiceCollectionExtensions.AddStateviaRuntimeSchedulers(null!));
        Assert.Throws<ArgumentNullException>(
            () => RuntimeServiceCollectionExtensions.AddStateviaRuntimeWorkerHostedService(null!));
        Assert.Throws<ArgumentNullException>(
            () => RuntimeServiceCollectionExtensions.AddStateviaRuntimeOptions(null!, configuration));
        Assert.Throws<ArgumentNullException>(() => services.AddStateviaRuntimeOptions(null!));
    }
}
