namespace Statevia.Actions.Abstractions.Catalog;

/// <summary>Action の導入元。</summary>
public enum ActionSourceKind
{
    /// <summary>プラットフォーム Builtin。</summary>
    Builtin,

    /// <summary>ローカル filesystem Module。</summary>
    Filesystem,

    /// <summary>Marketplace（Phase 4）。</summary>
    Marketplace,

    /// <summary>OCI レジストリ（将来）。</summary>
    Oci,

    /// <summary>Git ソース（将来）。</summary>
    Git,
}
