using Statevia.Core.Engine.Abstractions;
using Statevia.Core.Engine.Definition;

namespace Statevia.Core.Api.Application.Definition;

/// <summary>
/// nodes（UI）形式の YAML を <see cref="WorkflowDefinition"/> に変換する。
/// 変換仕様: <c>.workspace-docs/specs/in-progress/v2-nodes-to-states-conversion-spec.md</c>（未実装時は <see cref="NotSupportedException"/>）。
/// </summary>
public sealed class NodeDefinitionLoader : IDefinitionLoader
{
    /// <inheritdoc />
    public WorkflowDefinition Load(string content)
    {
        ArgumentNullException.ThrowIfNull(content);
        throw new NotSupportedException(
            "Workflow definition in nodes (UI) format is not yet supported. Use the states format until conversion is implemented (see v2-nodes-to-states-conversion-spec).");
    }
}
