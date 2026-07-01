using Statevia.Core.Api.Application.Definition;

namespace Statevia.Core.Api.Tests.Application.Definition;

/// <summary><see cref="WorkflowDefinitionYamlFormat"/> の形式判定テスト。</summary>
public sealed class WorkflowDefinitionYamlFormatTests
{
    /// <summary>空文字列は states 形式として扱う。</summary>
    [Fact]
    public void Analyze_EmptyContent_ReturnsStates()
    {
        // Act
        var kind = WorkflowDefinitionYamlFormat.Analyze("   ");

        // Assert
        Assert.Equal(WorkflowDefinitionYamlFormatKind.States, kind);
    }

    /// <summary>nodes 配列のみのとき nodes 形式を返す。</summary>
    [Fact]
    public void Analyze_NodesArray_ReturnsNodes()
    {
        // Arrange
        const string content = """
            version: 1
            nodes:
              - id: a
            """;

        // Act
        var kind = WorkflowDefinitionYamlFormat.Analyze(content);

        // Assert
        Assert.Equal(WorkflowDefinitionYamlFormatKind.Nodes, kind);
    }

    /// <summary>states オブジェクトのみのとき states 形式を返す。</summary>
    [Fact]
    public void Analyze_StatesObject_ReturnsStates()
    {
        // Arrange
        const string content = """
            workflow:
              name: W
            states:
              A: {}
            """;

        // Act
        var kind = WorkflowDefinitionYamlFormat.Analyze(content);

        // Assert
        Assert.Equal(WorkflowDefinitionYamlFormatKind.States, kind);
    }

    /// <summary>nodes と states の併存は拒否する。</summary>
    [Fact]
    public void Analyze_Throws_WhenNodesAndStatesBothPresent()
    {
        // Arrange
        const string content = """
            nodes:
              - id: a
            states:
              A: {}
            """;

        // Act
        var ex = Assert.Throws<ArgumentException>(() => WorkflowDefinitionYamlFormat.Analyze(content));

        // Assert
        Assert.Contains("cannot contain both", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>nodes が配列でないとき拒否する。</summary>
    [Fact]
    public void Analyze_Throws_WhenNodesIsNotArray()
    {
        // Arrange
        const string content = """
            nodes:
              id: not-array
            """;

        // Act
        var ex = Assert.Throws<ArgumentException>(() => WorkflowDefinitionYamlFormat.Analyze(content));

        // Assert
        Assert.Contains("must be an array", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>nodes 配列が空のとき拒否する。</summary>
    [Fact]
    public void Analyze_Throws_WhenNodesArrayEmpty()
    {
        // Arrange
        const string content = """
            nodes: []
            """;

        // Act
        var ex = Assert.Throws<ArgumentException>(() => WorkflowDefinitionYamlFormat.Analyze(content));

        // Assert
        Assert.Contains("cannot be empty", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>不正な YAML は ArgumentException でラップする。</summary>
    [Fact]
    public void Analyze_Throws_WhenYamlInvalid()
    {
        // Arrange
        const string content = ":\n  - bad\n  nested: [";

        // Act
        var ex = Assert.Throws<ArgumentException>(() => WorkflowDefinitionYamlFormat.Analyze(content));

        // Assert
        Assert.Contains("Invalid YAML", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>null は ArgumentNullException。</summary>
    [Fact]
    public void Analyze_Throws_WhenContentNull()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => WorkflowDefinitionYamlFormat.Analyze(null!));
    }
}
