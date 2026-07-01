using Microsoft.Extensions.DependencyInjection;

namespace Statevia.Core.Actions.Abstractions.Modules;

/// <summary>Action Module（DLL）側の最小公開契約。</summary>
public interface IActionModule
{
    /// <summary>Module の一意識別子。</summary>
    string ModuleId { get; }

    /// <summary>表示名。</summary>
    string Name { get; }

    /// <summary>Module バージョン。</summary>
    string Version { get; }

    /// <summary>Module が提供する Action 登録情報を返す。</summary>
    /// <param name="serviceProvider">ホストの DI ルート（将来の DI 依存用）。</param>
    IEnumerable<ModuleActionRegistration> GetActions(IServiceProvider serviceProvider);
}
