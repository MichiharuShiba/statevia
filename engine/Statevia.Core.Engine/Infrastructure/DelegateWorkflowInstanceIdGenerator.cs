using Statevia.Core.Engine.Abstractions;

namespace Statevia.Core.Engine.Infrastructure;

/// <summary>
/// ホスト側の ID 生成（例: DI で登録済みの <c>IIdGenerator</c>）と橋渡しするための実装。
/// </summary>
public sealed class DelegateWorkflowInstanceIdGenerator : IWorkflowInstanceIdGenerator
{
    private readonly Func<string> _createId;

    /// <summary>
    /// 指定デリゲートで ID を生成する生成器を構築する。
    /// </summary>
    /// <param name="createId">新しいワークフローインスタンス ID を返す関数。</param>
    public DelegateWorkflowInstanceIdGenerator(Func<string> createId) =>
        _createId = createId ?? throw new ArgumentNullException(nameof(createId));

    /// <inheritdoc />
    public string NewWorkflowInstanceId() => _createId();
}
