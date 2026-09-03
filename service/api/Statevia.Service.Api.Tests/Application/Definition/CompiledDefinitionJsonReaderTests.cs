using Statevia.Core.Engine.Abstractions;
using Statevia.Service.Api.Application.Definition;

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
          "waitEventRouteTable": {
            "W1": {
              "signal": { "next": "B" }
            }
          },
          "stateInputs": {},
          "stateOutputs": { "A": "$.vars.user" }
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
        Assert.Equal("B", compiled.WaitEventRouteTable["W1"]["signal"].Next);
        Assert.Equal("$.vars.user", compiled.StateOutputs["A"]);
        Assert.Same(factory, compiled.StateExecutorFactory);
    }

    /// <summary>compiled_json の版バインディングを復元する。</summary>
    [Fact]
    public void Read_WhenBindingsPresent_RestoresVersionMetadata()
    {
        // Arrange
        const string json = """
            {
              "name": "W",
              "initialState": "A",
              "transitions": {},
              "resolvedModules": {
                "mail": { "moduleId": "demo.module", "resolvedVersion": "1.0.0" }
              },
              "stateActionBindings": {
                "A": {
                  "logicalActionId": "demo.module.echo",
                  "resolvedModuleVersion": "1.0.0",
                  "moduleId": "demo.module",
                  "actionName": "echo"
                }
              }
            }
            """;
        var factory = new StubExecutorFactory();

        // Act
        var compiled = CompiledDefinitionJsonReader.Read(json, factory);

        // Assert
        Assert.Equal("1.0.0", compiled.ResolvedModules["mail"].ResolvedVersion);
        Assert.Equal("1.0.0", compiled.StateActionBindings["A"].ResolvedModuleVersion);
    }

    /// <summary>compiled_json の waitSubscriptions を復元する。</summary>
    [Fact]
    public void Read_WhenWaitSubscriptionsPresent_RestoresSubscribeTable()
    {
        // Arrange
        const string json = """
            {
              "name": "W",
              "initialState": "A",
              "transitions": {},
              "waitSubscriptions": {
                "WaitPaid": [
                  {
                    "topic": "orders.paid",
                    "key": "k1",
                    "resumeEventName": "statevia.event.subscribe.0",
                    "next": "End"
                  }
                ]
              }
            }
            """;
        var factory = new StubExecutorFactory();

        // Act
        var compiled = CompiledDefinitionJsonReader.Read(json, factory);

        // Assert
        var entry = Assert.Single(compiled.WaitSubscriptions["WaitPaid"]);
        Assert.Equal("orders.paid", entry.Topic);
        Assert.Equal("k1", entry.Key);
        Assert.Equal("statevia.event.subscribe.0", entry.ResumeEventName);
        Assert.Equal("End", entry.Next);
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

    /// <summary>bindings 有無を判定できる。</summary>
    [Fact]
    public void HasStoredBindings_DetectsPresence()
    {
        // Arrange
        const string withBindings = """
            {
              "name": "W",
              "initialState": "A",
              "transitions": {},
              "stateActionBindings": {
                "A": {
                  "logicalActionId": "demo.echo",
                  "resolvedModuleVersion": "1.0.0",
                  "moduleId": "demo",
                  "actionName": "echo"
                }
              }
            }
            """;

        // Act + Assert
        Assert.True(CompiledDefinitionJsonReader.HasStoredBindings(withBindings));
        Assert.False(CompiledDefinitionJsonReader.HasStoredBindings(MinimalCompiledJson));
    }

    /// <summary>ReadStoredBindings は modules / bindings を返す。</summary>
    [Fact]
    public void ReadStoredBindings_ReturnsMaps()
    {
        // Arrange
        const string json = """
            {
              "name": "W",
              "initialState": "A",
              "transitions": {},
              "resolvedModules": {
                "mail": { "moduleId": "demo.module", "resolvedVersion": "1.0.0" }
              },
              "stateActionBindings": {
                "A": {
                  "logicalActionId": "demo.module.echo",
                  "resolvedModuleVersion": "1.0.0",
                  "moduleId": "demo.module",
                  "actionName": "echo"
                }
              }
            }
            """;

        // Act
        var (modules, bindings) = CompiledDefinitionJsonReader.ReadStoredBindings(json);

        // Assert
        Assert.Equal("demo.module", modules["mail"].ModuleId);
        Assert.Equal("echo", bindings["A"].ActionName);
    }

    /// <summary>end 付き遷移と Wait ルートを復元する。</summary>
    [Fact]
    public void Read_WithEndTransitionAndWaitRoute_RestoresTables()
    {
        // Arrange
        const string json = """
            {
              "name": "W",
              "initialState": "A",
              "transitions": {
                "A": { "Ok": { "end": true } }
              },
              "waitEventRouteTable": {
                "W1": {
                  "done": { "next": "End" }
                }
              }
            }
            """;

        // Act
        var compiled = CompiledDefinitionJsonReader.Read(json, new StubExecutorFactory());

        // Assert
        Assert.True(compiled.Transitions["A"]["Ok"].End);
        Assert.Equal("End", compiled.WaitEventRouteTable["W1"]["done"].Next);
    }

    private sealed class StubExecutorFactory : IStateExecutorFactory
    {
        public IStateExecutor? GetExecutor(string stateName) => null;
    }
}
