using Statevia.Service.Api.Application.Definition;
using Statevia.Core.Engine.Abstractions;

namespace Statevia.Service.Api.Tests.Application.Definition;

/// <summary><see cref="CompiledDefinitionJsonReader"/> の検証。</summary>
public sealed class CompiledDefinitionJsonReaderTests
{
    private const string MinimalCompiledJson = """
        {
          "name": "W",
          "initialState": "A",
          "transitions": {
            "A": {
              "Ok": { "next": "B", "end": false }
            }
          },
          "conditionalTransitions": {},
          "forkTable": { "F": ["B", "C"] },
          "joinTable": { "J": ["B", "C"] },
          "waitTable": { "W1": "signal" },
          "stateInputs": {}
        }
        """;

    /// <summary>compiled_json から Engine 定義を復元できる。</summary>
    [Fact]
    public void Read_ValidJson_ReturnsCompiledDefinition()
    {
        // Arrange
        var factory = new StubExecutorFactory();

        // Act
        var compiled = CompiledDefinitionJsonReader.Read(MinimalCompiledJson, factory);

        // Assert
        Assert.Equal("W", compiled.Name);
        Assert.Equal("A", compiled.InitialState);
        Assert.True(compiled.Transitions.ContainsKey("A"));
        Assert.Equal("B", compiled.Transitions["A"]["Ok"].Next);
        Assert.Equal(["B", "C"], compiled.ForkTable["F"]);
        Assert.Equal("signal", compiled.WaitTable["W1"]);
        Assert.Same(factory, compiled.StateExecutorFactory);
    }

    /// <summary>無効な compiled_json は ArgumentException になる。</summary>
    [Fact]
    public void Read_InvalidJson_ThrowsArgumentException()
    {
        // Arrange
        var factory = new StubExecutorFactory();

        // Act & Assert
        Assert.Throws<ArgumentException>(() => CompiledDefinitionJsonReader.Read("null", factory));
    }

    /// <summary>空白のみの compiled_json は ArgumentException になる。</summary>
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Read_WhitespaceJson_ThrowsArgumentException(string compiledJson)
    {
        // Arrange
        var factory = new StubExecutorFactory();

        // Act & Assert
        Assert.Throws<ArgumentException>(() => CompiledDefinitionJsonReader.Read(compiledJson, factory));
    }

    /// <summary>null の compiled_json は ArgumentNullException になる。</summary>
    [Fact]
    public void Read_NullJson_ThrowsArgumentNullException()
    {
        // Arrange
        var factory = new StubExecutorFactory();

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => CompiledDefinitionJsonReader.Read(null!, factory));
    }

    /// <summary>factory 未指定は ArgumentNullException になる。</summary>
    [Fact]
    public void Read_NullFactory_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => CompiledDefinitionJsonReader.Read(MinimalCompiledJson, null!));
    }

    private sealed class StubExecutorFactory : IStateExecutorFactory
    {
        public IStateExecutor? GetExecutor(string stateName) => null;
    }
}
