using Statevia.Core.Engine.Abstractions;

namespace Statevia.Core.Engine.Infrastructure;

/// <summary>
/// ホスト側の ID 生成（例: DI で登録済みの <c>IIdGenerator</c>）と橋渡しするための実装。
/// </summary>
public sealed class DelegateWorkflowInstanceIdGenerator : IWorkflowInstanceIdGenerator
{
    private readonly Func<string> _createId;

    public DelegateWorkflowInstanceIdGenerator(Func<string> createId) =>
        _createId = createId ?? throw new ArgumentNullException(nameof(createId));

    public string NewWorkflowInstanceId() => _createId();
}
