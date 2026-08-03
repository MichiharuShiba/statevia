using Statevia.Core.Application.Contracts.Services;

namespace Statevia.Core.Application.Contracts.Persistence;

/// <summary>
/// <see cref="ExecutionWorkItemKinds.Start"/> の JSON ペイロード。
/// </summary>
/// <remarks>
/// HTTP Start が実行行を先に作成したうえで enqueue する。
/// Worker は <see cref="ExecutionId"/> を正として Engine を起動する。
/// </remarks>
/// <param name="ExecutionId">受理済み実行 ID。</param>
/// <param name="Request">開始リクエスト（定義・入力）。</param>
public sealed record ExecutionStartWorkItemPayload(
    Guid ExecutionId,
    StartExecutionRequest Request);
