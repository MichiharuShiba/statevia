using Statevia.Core.Api.Abstractions.Services;
using Statevia.Core.Engine.Definition;

namespace Statevia.Core.Api.Application.Definition;

/// <summary>
/// ルート形式に応じて <see cref="DefinitionLoader"/>（states）または <see cref="NodeDefinitionLoader"/>（nodes）へ委譲する既定ストラテジ。
/// </summary>
public sealed class DefinitionLoadStrategy : IDefinitionLoadStrategy
{
    private readonly DefinitionLoader _statesLoader;
    private readonly NodeDefinitionLoader _nodesLoader;

    public DefinitionLoadStrategy(DefinitionLoader statesLoader, NodeDefinitionLoader nodesLoader)
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
