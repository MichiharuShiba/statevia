using System.Diagnostics.CodeAnalysis;
using Statevia.Actions.Abstractions.Publication;

namespace Statevia.Actions.Abstractions.Catalog;

/// <summary>actionId → Descriptor 解決。認可・Mode 決定は行わない。</summary>
public interface IActionCatalog
{
    /// <summary>actionId（canonical またはエイリアス）が登録済みか。</summary>
    /// <param name="actionId">参照 actionId。</param>
    bool Exists(string actionId);

    /// <summary>Descriptor を取得する。</summary>
    /// <param name="actionId">参照 actionId。</param>
    /// <param name="descriptor">取得した Descriptor。</param>
    bool TryGetDescriptor(string actionId, [NotNullWhen(true)] out ActionDescriptor? descriptor);

    /// <summary>Descriptor と実行エントリを取得する。</summary>
    /// <param name="actionId">参照 actionId。</param>
    /// <param name="registration">取得した登録情報。</param>
    bool TryGetRegistration(string actionId, [NotNullWhen(true)] out ActionRegistration? registration);

    /// <summary>Action を Catalog へ登録する。</summary>
    /// <param name="descriptor">Descriptor（不変条件は実装側で検証）。</param>
    /// <param name="entry">実行エントリ。</param>
    void Register(ActionDescriptor descriptor, ActionCatalogEntry entry);

    /// <summary>Schema Publication 付きで Action を Catalog へ登録する。</summary>
    /// <param name="descriptor">Descriptor（不変条件は実装側で検証）。</param>
    /// <param name="entry">実行エントリ。</param>
    /// <param name="publication">input/output schema と UI メタデータ。</param>
    void Register(ActionDescriptor descriptor, ActionCatalogEntry entry, ActionPublication publication);

    /// <summary>登録済み action の ActionPublication を取得する。</summary>
    /// <param name="actionId">参照 actionId（canonical またはエイリアス）。</param>
    /// <param name="publication">取得した Publication。</param>
    bool TryGetPublication(string actionId, [NotNullWhen(true)] out ActionPublication? publication);
}
