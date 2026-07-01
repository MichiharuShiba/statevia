using Statevia.Modules;

namespace Statevia.Service.ActionHost.Tests.Fixtures.PrivateCtorActionModule;

/// <summary>公開コンストラクタが無く load 時にスキップされるテスト Module。</summary>
#pragma warning disable S3453 // テスト用: 公開コンストラクタ無しで Module インスタンス化失敗を再現する
public sealed class PrivateCtorActionModule : IActionModule
#pragma warning restore S3453
{
    private PrivateCtorActionModule()
    {
    }

    /// <inheritdoc />
    public string ModuleId => "private.ctor";

    /// <inheritdoc />
    public string Name => "Private Ctor Module";

    /// <inheritdoc />
    public string Version => "1.0.0";

    /// <inheritdoc />
    public IEnumerable<ModuleActionRegistration> GetActions(IServiceProvider serviceProvider)
    {
        _ = serviceProvider;
        yield break;
    }
}
