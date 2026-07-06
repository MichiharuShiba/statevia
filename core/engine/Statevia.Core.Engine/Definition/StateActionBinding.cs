namespace Statevia.Core.Engine.Definition;

/// <summary>状態ごとの compile 時 Action バインディング（版ピンを含む）。</summary>
/// <param name="LogicalActionId">論理 actionId（<c>{moduleId}.{actionName}</c> または Builtin canonical）。</param>
/// <param name="ResolvedModuleVersion">Module action の確定版。Builtin / 曖昧解決不要時は <see langword="null"/>。</param>
/// <param name="ModuleId">Module action の Module ID。Builtin 等では <see langword="null"/>。</param>
/// <param name="ActionName">Module action 名。Builtin 等では <see langword="null"/>。</param>
public sealed record StateActionBinding(
    string LogicalActionId,
    string? ResolvedModuleVersion,
    string? ModuleId = null,
    string? ActionName = null);
