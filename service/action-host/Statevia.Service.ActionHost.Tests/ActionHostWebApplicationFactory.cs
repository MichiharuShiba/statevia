using Grpc.Net.Client;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Statevia.Service.ActionHost;
using Statevia.Infrastructure.Modules;

namespace Statevia.Service.ActionHost.Tests;

/// <summary>テスト用 Action Host ファクトリ。</summary>
public class ActionHostWebApplicationFactory : WebApplicationFactory<Program>
{
    private readonly string? _modulesPath;
    private readonly string? _contentRoot;
    private readonly bool _configureModulesPath;

    /// <summary>既定の test.module を load するファクトリを作成する。</summary>
    public ActionHostWebApplicationFactory()
    {
        _modulesPath = TestModuleLayout.CopyBuiltAssembly("test.module");
        _configureModulesPath = true;
    }

    private ActionHostWebApplicationFactory(
        string? modulesPath,
        string? contentRoot,
        bool configureModulesPath,
        bool customFactory)
    {
        _ = customFactory;
        _modulesPath = modulesPath;
        _contentRoot = contentRoot;
        _configureModulesPath = configureModulesPath;
    }

    /// <summary>指定 modules ルート用のファクトリを作成する。</summary>
    /// <param name="modulesRoot">modules ルート絶対パス。</param>
    /// <returns>ファクトリ。</returns>
    public static ActionHostWebApplicationFactory ForModulesRoot(string modulesRoot) =>
        new(modulesRoot, contentRoot: null, configureModulesPath: true, customFactory: true);

    /// <summary>環境変数 <see cref="ModulePathResolver.EnvironmentVariable"/> で modules ルートを解決するファクトリを作成する。</summary>
    /// <param name="modulesRoot">modules ルート絶対パス。</param>
    /// <returns>ファクトリ。</returns>
    public static ActionHostWebApplicationFactory ForEnvironmentModulesRoot(string modulesRoot)
    {
        Environment.SetEnvironmentVariable(ModulePathResolver.EnvironmentVariable, modulesRoot);
        return new(modulesPath: null, contentRoot: null, configureModulesPath: false, customFactory: true);
    }

    /// <summary>content root 相対の ModulesPath を使うファクトリを作成する。</summary>
    /// <param name="contentRoot">content root 絶対パス。</param>
    /// <param name="relativeModulesDirectory">content root からの相対 modules パス。</param>
    /// <returns>ファクトリ。</returns>
    public static ActionHostWebApplicationFactory ForRelativeModulesRoot(
        string contentRoot,
        string relativeModulesDirectory)
    {
        var modulesRoot = Path.Combine(contentRoot, relativeModulesDirectory);
        Directory.CreateDirectory(modulesRoot);
        TestModuleLayout.CopyBuiltAssembly("test.module", modulesRoot);
        return new(relativeModulesDirectory, contentRoot, configureModulesPath: true, customFactory: true);
    }

    /// <summary>ListenUrl 設定付きのファクトリを作成する。</summary>
    /// <param name="listenUrl">待ち受け URL。</param>
    /// <returns>ファクトリ。</returns>
    public static ActionHostWebApplicationFactory WithListenUrl(string listenUrl) =>
        new WithListenUrlFactory(listenUrl);

    /// <summary>gRPC クライアント用チャネルを作成する。</summary>
    public GrpcChannel CreateGrpcChannel()
    {
        var httpClient = CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
        });

        return GrpcChannel.ForAddress(httpClient.BaseAddress!, new GrpcChannelOptions
        {
            HttpClient = httpClient,
        });
    }

    /// <inheritdoc />
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        if (_contentRoot is not null)
        {
            builder.UseContentRoot(_contentRoot);
        }

        if (_configureModulesPath && _modulesPath is not null)
        {
            builder.UseSetting($"{ActionHostOptions.SectionName}:ModulesPath", _modulesPath);
        }
        else if (!_configureModulesPath)
        {
            builder.UseSetting($"{ActionHostOptions.SectionName}:ModulesPath", string.Empty);
        }
    }

    private sealed class WithListenUrlFactory : ActionHostWebApplicationFactory
    {
        private readonly string _listenUrl;

        public WithListenUrlFactory(string listenUrl) =>
            _listenUrl = listenUrl;

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            base.ConfigureWebHost(builder);
            builder.UseSetting($"{ActionHostOptions.SectionName}:ListenUrl", _listenUrl);
        }
    }
}
