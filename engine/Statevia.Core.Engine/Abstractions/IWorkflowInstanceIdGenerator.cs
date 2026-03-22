namespace Statevia.Core.Engine.Abstractions;

/// <summary>
/// <see cref="IWorkflowEngine.Start"/> で <c>workflowId</c> が未指定のときに使うインスタンス ID 文字列を生成します。
/// Core-API では <c>IIdGenerator.NewGuid().ToString()</c> と一致させるため、DI で差し替え可能にします。
/// </summary>
public interface IWorkflowInstanceIdGenerator
{
    /// <summary>エンジン内キー・イベントプロバイダ名に使う一意文字列（通常は GUID の既定書式）。</summary>
    string NewWorkflowInstanceId();
}
