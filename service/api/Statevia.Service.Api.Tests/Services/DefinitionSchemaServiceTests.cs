using Statevia.Core.Application.Services;
using Statevia.Service.Api.Application.Definition;

namespace Statevia.Service.Api.Tests.Services;

/// <summary><see cref="DefinitionSchemaService"/> のテスト。</summary>
public sealed class DefinitionSchemaServiceTests
{
    /// <summary>nodes スキーマのバージョン定数を返す。</summary>
    [Fact]
    public void GetNodesSchemaVersion_ReturnsDefinitionConstant()
    {
        // Arrange
        var sut = new DefinitionSchemaService(new NodesSchemaProvider());

        // Act
        var version = sut.GetNodesSchemaVersion();

        // Assert
        Assert.Equal(NodesSchemaDefinition.SchemaVersion, version);
    }

    /// <summary>nodes バージョン整数を返す。</summary>
    [Fact]
    public void GetNodesVersion_ReturnsDefinitionConstant()
    {
        // Arrange
        var sut = new DefinitionSchemaService(new NodesSchemaProvider());

        // Act
        var version = sut.GetNodesVersion();

        // Assert
        Assert.Equal(NodesSchemaDefinition.NodesVersion, version);
    }

    /// <summary>スキーマ JSON ドキュメントを返す。</summary>
    [Fact]
    public void GetNodesSchemaDocument_ReturnsNonNullDocument()
    {
        // Arrange
        var sut = new DefinitionSchemaService(new NodesSchemaProvider());

        // Act
        var document = sut.GetNodesSchemaDocument();

        // Assert
        Assert.NotNull(document);
    }
}
