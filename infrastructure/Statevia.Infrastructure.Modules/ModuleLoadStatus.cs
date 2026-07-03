namespace Statevia.Infrastructure.Modules;

/// <summary>Module load の結果状態。</summary>
internal enum ModuleLoadStatus
{
    /// <summary>正常に load・登録された。</summary>
    Loaded,

    /// <summary>load または登録に失敗した。</summary>
    Failed,

    /// <summary>namespace 規約違反等で skip された。</summary>
    Skipped,

    /// <summary>既存 actionId と衝突して skip された。</summary>
    Duplicate,
}
