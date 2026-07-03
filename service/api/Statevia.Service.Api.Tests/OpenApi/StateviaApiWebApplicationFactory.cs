using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Statevia.Infrastructure.Modules;
using Statevia.Service.Api.Hosting;
using Statevia.Service.Api.Tests.Infrastructure;

namespace Statevia.Service.Api.Tests.OpenApi;

/// <summary>
/// OpenAPI スモークテスト用の <see cref="WebApplicationFactory{TEntryPoint}"/>。
/// </summary>
public sealed class StateviaApiWebApplicationFactory : WebApplicationFactory<Statevia.Service.Api.Program>
{
    /// <inheritdoc />
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.UseEnvironment(Environments.Development);
        builder.ConfigureServices(services =>
        {
            var bootstrap = services.SingleOrDefault(d =>
                d.ServiceType == typeof(IHostedService) &&
                d.ImplementationType == typeof(TenantBootstrapHostedService));
            if (bootstrap is not null)
                services.Remove(bootstrap);

            // PostgreSQL なしでも起動できるよう、既定テナント ID を直接指定する（DB lookup を回避）。
            services.Configure<ModuleHostOptions>(options =>
                options.OwnerTenantId = TestTenantIds.DefaultTenantId.ToString("D"));
        });
    }
}
