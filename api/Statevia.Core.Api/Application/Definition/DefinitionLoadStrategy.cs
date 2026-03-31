using Statevia.Core.Api.Abstractions.Services;
using Statevia.Core.Engine.Definition;

namespace Statevia.Core.Api.Application.Definition;

/// <summary>
/// ルート形式に応じて <see cref="StateWorkflowDefinitionLoader"/>（states）または <see cref="NodesWorkflowDefinitionLoader"/>（nodes）へ委譲する既定ストラテジ。
/// </summary>
public sealed class DefinitionLoadStrategy : IDefinitionLoadStrategy
{
    private readonly StateWorkflowDefinitionLoader _statesLoader;
    private readonly NodesWorkflowDefinitionLoader _nodesLoader;

    public DefinitionLoadStrategy(StateWorkflowDefinitionLoader statesLoader, NodesWorkflowDefinitionLoader nodesLoader)
    {
        _statesLoader = statesLoader ?? throw new ArgumentNullException(nameof(statesLoader));
        _nodesLoader = nodesLoader ?? throw new ArgumentNullException(nameof(nodesLoader));
    }

    /// <inheritdoc />
    public WorkflowDefinition Load(string content)
    {
        var kind = WorkflowDefinitionYamlFormat.Analyze(content);
        if (kind == WorkflowDefinitionYamlFormatKind.Nodes)
        {
            return _nodesLoader.Load(content);
        }

        return _statesLoader.Load(content);
    }
}

