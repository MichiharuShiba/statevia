using Statevia.Core.Api.Contracts.Admin;

namespace Statevia.Core.Api.Abstractions.Services;

/// <summary>Module load 状態の参照と明示 reload。</summary>
public interface IModuleManagementService
{
    /// <summary>ModuleHost へ discover / load を再実行する。</summary>
    /// <param name="cancellationToken">キャンセル。</param>
    Task ReloadAsync(CancellationToken cancellationToken);

    /// <summary>load catalog のスナップショットを返す。</summary>
    IReadOnlyList<AdminModuleListItemDto> ListModules();
}
