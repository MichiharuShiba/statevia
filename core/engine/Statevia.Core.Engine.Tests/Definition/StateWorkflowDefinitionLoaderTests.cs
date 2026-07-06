using Statevia.Core.Engine.Definition;
using Statevia.Core.Engine.Definition.Validation;
using Xunit;

namespace Statevia.Core.Engine.Tests.Definition;

public class StateWorkflowDefinitionLoaderTests
{
    /// <summary>StateWorkflowDefinitionLoader が YAML をパースし、workflow/states/fork/join を正しく読み込むことを検証する。</summary>
    [Fact]
    public void Load_ParsesHelloWorkflow()
    {
        // Arrange: Hello ワークフロー相当の YAML
        var yaml = """
            workflow:
              name: HelloWorkflow

            states:
              Start:
                on:
                  Completed:
                    fork: [Prepare, AskUser]
              Prepare:
                on:
                  Completed:
                    next: Join1
              Join1:
                join:
                  all: [Prepare, AskUser]
                on:
                  Joined:
                    next: Work
            """;

        // Act
        var loader = new StateWorkflowDefinitionLoader();
        var def = loader.Load(yaml);

        // Assert
        Assert.Equal("HelloWorkflow", def.Name);
        Assert.Equal(3, def.States.Count);
        Assert.True(def.States.ContainsKey("Start"));
        Assert.True(def.States.ContainsKey("Prepare"));
        Assert.True(def.States.ContainsKey("Join1"));

        var start = def.States["Start"];
        Assert.NotNull(start.On);
        var fork = start.On!["Completed"].Fork;
        Assert.NotNull(fork);
        Assert.Contains("Prepare", fork!);
        Assert.Contains("AskUser", fork!);

        var join1 = def.States["Join1"];
        Assert.NotNull(join1.Join);
        Assert.Equal(2, join1.Join!.All.Count);
    }

    /// <summary>wait のみの状態をパースすることを検証する。</summary>
    [Fact]
    public void Load_ParsesStateWithWaitOnly()
    {
        // Arrange
        var yaml = """
            workflow:
              name: W
            states:
              WaitState:
                wait:
                  event: resume
            """;
        var loader = new StateWorkflowDefinitionLoader();

        // Act
        var def = loader.Load(yaml);
        var state = def.States["WaitState"];

        // Assert
        Assert.NotNull(state.Wait);
        Assert.Equal("resume", state.Wait!.Event);
    }

    /// <summary>join のみの状態をパースすることを検証する。</summary>
    [Fact]
    public void Load_ParsesStateWithJoinOnly()
    {
        // Arrange
        var yaml = """
            workflow:
              name: W
            states:
              JoinOnly:
                join:
                  all: [A, B]
            """;
        var loader = new StateWorkflowDefinitionLoader();

        // Act
        var def = loader.Load(yaml);
        var state = def.States["JoinOnly"];

        // Assert
        Assert.NotNull(state.Join);
        Assert.Equal(2, state.Join!.All.Count);
        Assert.Contains("A", state.Join.All);
        Assert.Contains("B", state.Join.All);
    }

    /// <summary>end: true の遷移をパースすることを検証する。</summary>
    [Fact]
    public void Load_ParsesTransitionWithEndTrue()
    {
        // Arrange
        var yaml = """
            workflow:
              name: W
            states:
              EndState:
                on:
                  Completed:
                    end: true
            """;
        var loader = new StateWorkflowDefinitionLoader();

        // Act
        var def = loader.Load(yaml);
        var state = def.States["EndState"];

        // Assert
        Assert.NotNull(state.On);
        Assert.True(state.On!["Completed"].End);
    }

    /// <summary>状態直下の input.path をパースできることを検証する。</summary>
    [Fact]
    public void Load_ParsesStateInputPath()
    {
        // Arrange
        var yaml = """
            workflow:
              name: W
            states:
              A:
                on:
                  Completed:
                    next: B
              B:
                input:
                  path: $.payload.value
                on:
                  Completed:
                    end: true
            """;
        var loader = new StateWorkflowDefinitionLoader();

        // Act
        var def = loader.Load(yaml);

        // Assert
        Assert.Equal("$.payload.value", def.States["B"].Input?.Path);
    }

    /// <summary>states.&lt;name&gt;.action をパースできることを検証する。</summary>
    [Fact]
    public void Load_ParsesStateAction()
    {
        var yaml = """
            workflow:
              name: W
            states:
              CreateOrder:
                action: order.create
                on:
                  Completed:
                    end: true
            """;
        var loader = new StateWorkflowDefinitionLoader();

        var def = loader.Load(yaml);

        Assert.Equal("order.create", def.States["CreateOrder"].Action);
    }

    /// <summary>join と action の併記は Level 1 検証で拒否されることを検証する。</summary>
    [Fact]
    public void Level1_RejectsJoinAndActionTogether()
    {
        var yaml = """
            workflow:
              name: W
            states:
              A:
                action: order.create
                join:
                  all: [B, C]
                on:
                  Joined:
                    end: true
              B:
                on:
                  Completed:
                    next: A
              C:
                on:
                  Completed:
                    next: A
            """;
        var loader = new StateWorkflowDefinitionLoader();
        var def = loader.Load(yaml);

        var r = Level1Validator.Validate(def);

        Assert.False(r.IsValid);
        Assert.Contains(r.Errors, e => e.Contains("both join and action", StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>wait と action の併記は Level 1 検証で拒否されることを検証する。</summary>
    [Fact]
    public void Level1_RejectsWaitAndActionTogether()
    {
        var yaml = """
            workflow:
              name: W
            states:
              A:
                action: order.create
                wait:
                  event: E
                on:
                  Completed:
                    end: true
            """;
        var loader = new StateWorkflowDefinitionLoader();
        var def = loader.Load(yaml);

        var r = Level1Validator.Validate(def);

        Assert.False(r.IsValid);
        Assert.Contains(r.Errors, e => e.Contains("both wait and action", StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>input のマップ値にパス式とリテラルを混在してパースできることを検証する。</summary>
    [Fact]
    public void Load_ParsesStateInputMap_WithPathAndLiterals()
    {
        // Arrange
        var yaml = """
            workflow:
              name: W
            states:
              B:
                input:
                  foo: $.payload.value
                  title: my song
                  count: 2
                  enabled: true
                on:
                  Completed:
                    end: true
            """;
        var loader = new StateWorkflowDefinitionLoader();

        // Act
        var def = loader.Load(yaml);

        // Assert
        var input = def.States["B"].Input;
        Assert.NotNull(input);
        Assert.Null(input!.Path);
        Assert.NotNull(input.Values);
        Assert.Equal("$.payload.value", input.Values!["foo"].Path);
        Assert.Equal("my song", input.Values["title"].Literal);
        Assert.Equal(2L, input.Values["count"].Literal);
        Assert.True(Assert.IsType<bool>(input.Values["enabled"].Literal));
    }

    /// <summary>states が空の YAML をパースすることを検証する。</summary>
    [Fact]
    public void Load_WithEmptyStates_ReturnsEmptyStates()
    {
        // Arrange
        var yaml = """
            workflow:
              name: Empty
            states: {}
            """;
        var loader = new StateWorkflowDefinitionLoader();

        // Act
        var def = loader.Load(yaml);

        // Assert
        Assert.NotNull(def.States);
        Assert.Empty(def.States);
    }

    /// <summary>states キーが存在しない YAML では GetChildDict が空辞書を返し、空の States になることを検証する。</summary>
    [Fact]
    public void Load_WithNoStatesKey_ReturnsEmptyStates()
    {
        // Arrange
        var yaml = """
            workflow:
              name: NoStates
            """;
        var loader = new StateWorkflowDefinitionLoader();

        // Act
        var def = loader.Load(yaml);

        // Assert
        Assert.NotNull(def.States);
        Assert.Empty(def.States);
        Assert.Equal("NoStates", def.Name);
    }

    /// <summary>end が文字列 "true" のとき GetBool が true を返すことを検証する。</summary>
    [Fact]
    public void Load_TransitionEndAsStringTrue_ParsesAsTrue()
    {
        // Arrange: YAML で end が文字列 "true" になる場合（クォートで明示）
        var yaml = """
            workflow:
              name: W
            states:
              EndState:
                on:
                  Completed:
                    end: "true"
            """;
        var loader = new StateWorkflowDefinitionLoader();

        // Act
        var def = loader.Load(yaml);
        var state = def.States["EndState"];

        // Assert
        Assert.NotNull(state.On);
        Assert.True(state.On!["Completed"].End);
    }

    /// <summary>end: false の遷移で GetBool が false を返すことを検証する。</summary>
    [Fact]
    public void Load_TransitionEndFalse_ParsesAsFalse()
    {
        // Arrange
        var yaml = """
            workflow:
              name: W
            states:
              MidState:
                on:
                  Completed:
                    next: Other
                    end: false
              Other:
                on:
                  Completed:
                    end: true
            """;
        var loader = new StateWorkflowDefinitionLoader();

        // Act
        var def = loader.Load(yaml);
        var state = def.States["MidState"];

        // Assert
        Assert.NotNull(state.On);
        Assert.False(state.On!["Completed"].End);
        Assert.Equal("Other", state.On!["Completed"].Next);
    }

    /// <summary>fork にスカラー（true）を指定した場合、スカラーは bool としてデシリアライズされ GetStrList は null を返すため Fork は null になる。</summary>
    [Fact]
    public void Load_ForkAsScalar_ResultsInNullFork()
    {
        // Arrange: fork: true は NodeTypeResolver により bool としてデシリアライズされ、GetStrList は IEnumerable でないので null を返す
        var yaml = """
            workflow:
              name: W
            states:
              Start:
                on:
                  Completed:
                    fork: true
            """;
        var loader = new StateWorkflowDefinitionLoader();

        // Act
        var def = loader.Load(yaml);
        var state = def.States["Start"];

        // Assert
        Assert.NotNull(state.On);
        Assert.Null(state.On!["Completed"].Fork);
    }

    /// <summary>YAML に数値スカラー（整数・小数）が含まれる場合も NodeTypeResolver により long/double としてデシリアライズされ Load が成功することを検証する。</summary>
    [Fact]
    public void Load_WithNumericScalars_DeserializesSuccessfully()
    {
        // Arrange: ルートに数値キーを置き、スカラーが long/double として解決される経路をカバーする（未使用キーでもデシリアライズ時に Resolver が呼ばれる）
        var yaml = """
            workflow:
              name: W
            _int: 42
            _float: 1.5
            states:
              S:
                on:
                  Done:
                    next: End
              End:
                on:
                  Done:
                    end: true
            """;
        var loader = new StateWorkflowDefinitionLoader();

        // Act
        var def = loader.Load(yaml);

        // Assert
        Assert.Equal("W", def.Name);
        Assert.Equal(2, def.States.Count);
    }

    /// <summary>end が数値（0/1）の場合、GetBool が false を返すことを検証する。</summary>
    [Fact]
    public void Load_TransitionEndAsNumber_ReturnsFalse()
    {
        // Arrange: YAML で end が数値になる場合（通常は発生しないが、カバレッジ向上のため）
        var yaml = """
            workflow:
              name: W
            states:
              MidState:
                on:
                  Completed:
                    next: End
                    end: 0
              End:
                on:
                  Completed:
                    end: true
            """;
        var loader = new StateWorkflowDefinitionLoader();

        // Act
        var def = loader.Load(yaml);
        var state = def.States["MidState"];

        // Assert: 数値は bool として解釈されないため false になる
        Assert.NotNull(state.On);
        Assert.False(state.On!["Completed"].End);
    }

    [Fact]
    public void Load_StateInputTemplate_ThrowsArgumentException()
    {
        var yaml = """
            workflow:
              name: W
            states:
              A:
                input: ${input.value}
                on:
                  Completed:
                    end: true
            """;

        var loader = new StateWorkflowDefinitionLoader();

        var ex = Assert.Throws<ArgumentException>(() => loader.Load(yaml));
        Assert.Contains("${...}", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Load_StateInputInvalidPath_ThrowsArgumentException()
    {
        var yaml = """
            workflow:
              name: W
            states:
              A:
                input:
                  path: $.a.-b
                on:
                  Completed:
                    end: true
            """;

        var loader = new StateWorkflowDefinitionLoader();

        var ex = Assert.Throws<ArgumentException>(() => loader.Load(yaml));
        Assert.Contains("invalid input.path", ex.Message, StringComparison.Ordinal);
    }

    /// <summary>`on.&lt;Fact&gt;.cases/default` と `default` ショートハンドをパースできることを検証する。</summary>
    [Fact]
    public void Load_ParsesTransitionCasesAndDefaultShorthand()
    {
        // Arrange
        var yaml = """
            workflow:
              name: W
            states:
              Route:
                on:
                  Completed:
                    cases:
                      - order: 1
                        when:
                          path: $.risk.score
                          op: gt
                          value: 30
                        next: Manual
                      - when:
                          path: $.risk.band
                          op: in
                          value: [1, 2, 3]
                        next: Auto
                    default: Auto
              Manual:
                on:
                  Completed:
                    end: true
              Auto:
                on:
                  Completed:
                    end: true
            """;
        var loader = new StateWorkflowDefinitionLoader();

        // Act
        var def = loader.Load(yaml);
        var transition = def.States["Route"].On!["Completed"];

        // Assert
        Assert.NotNull(transition.Cases);
        Assert.Equal(2, transition.Cases!.Count);
        Assert.Equal(1, transition.Cases[0].Order);
        Assert.Equal("$.risk.score", transition.Cases[0].When.Path);
        Assert.Equal("gt", transition.Cases[0].When.Op);
        Assert.Equal("Manual", transition.Cases[0].Transition.Next);
        Assert.Null(transition.Cases[1].Order);
        Assert.Equal("in", transition.Cases[1].When.Op);
        Assert.Equal("Auto", transition.Cases[1].Transition.Next);
        Assert.NotNull(transition.Default);
        Assert.Equal("Auto", transition.Default!.Next);
    }

    /// <summary>`default` オブジェクト形式で `end: true` をパースできることを検証する。</summary>
    [Fact]
    public void Load_ParsesTransitionDefaultAsObject()
    {
        // Arrange
        var yaml = """
            workflow:
              name: W
            states:
              Route:
                on:
                  Completed:
                    cases:
                      - when:
                          path: $.result.ok
                          op: eq
                          value: true
                        next: Done
                    default:
                      end: true
              Done:
                on:
                  Completed:
                    end: true
            """;
        var loader = new StateWorkflowDefinitionLoader();

        // Act
        var def = loader.Load(yaml);
        var transition = def.States["Route"].On!["Completed"];

        // Assert
        Assert.NotNull(transition.Default);
        Assert.True(transition.Default!.End);
        Assert.Null(transition.Default.Next);
    }

    /// <summary>workflow.modules を WorkflowDefinition.Modules に保持する。</summary>
    [Fact]
    public void Load_ParsesWorkflowModules()
    {
        // Arrange
        var yaml = """
            workflow:
              name: W
              modules:
                mail: com.company.mail
            states:
              A:
                action: mail.send
                on:
                  Completed:
                    end: true
            """;
        var loader = new StateWorkflowDefinitionLoader();

        // Act
        var def = loader.Load(yaml);

        // Assert
        Assert.NotNull(def.Modules);
        Assert.Single(def.Modules!);
        Assert.Equal("com.company.mail", def.Modules!["mail"].ModuleId);
        Assert.Equal("mail.send", def.States["A"].Action);
    }

    /// <summary>workflow.modules の moduleId@range を構造化して保持する。</summary>
    [Fact]
    public void Load_ParsesWorkflowModulesWithVersionRange()
    {
        // Arrange
        var yaml = """
            workflow:
              name: W
              modules:
                mail: com.company.mail@^1.2
            states:
              A:
                action: mail.send
                on:
                  Completed:
                    end: true
            """;
        var loader = new StateWorkflowDefinitionLoader();

        // Act
        var def = loader.Load(yaml);

        // Assert
        Assert.NotNull(def.Modules);
        Assert.Equal("com.company.mail", def.Modules!["mail"].ModuleId);
        Assert.Equal("^1.2", def.Modules!["mail"].VersionRange);
    }

    /// <summary>状態直下の retry を RetryDefinition として保持する。</summary>
    [Fact]
    public void Load_ParsesStateRetryBlock()
    {
        // Arrange
        var yaml = """
            workflow:
              name: W
            states:
              A:
                action: noop
                retry:
                  limit: 3
                  backoff: exponential
                  errors: [timeout, 5xx]
                on:
                  Completed:
                    end: true
            """;
        var loader = new StateWorkflowDefinitionLoader();

        // Act
        var def = loader.Load(yaml);
        var retry = def.States["A"].Retry;

        // Assert
        Assert.NotNull(retry);
        Assert.Equal(3, retry!.Limit);
        Assert.Equal("exponential", retry.Backoff);
        Assert.NotNull(retry.Errors);
        Assert.Equal(2, retry.Errors!.Count);
        Assert.Contains("timeout", retry.Errors);
        Assert.Contains("5xx", retry.Errors);
    }

    /// <summary>input 内の retry は構文エラーになる。</summary>
    [Fact]
    public void Load_RetryInsideInput_Throws()
    {
        // Arrange
        var yaml = """
            workflow:
              name: W
            states:
              A:
                action: noop
                input:
                  retry:
                    limit: 3
                on:
                  Completed:
                    end: true
            """;
        var loader = new StateWorkflowDefinitionLoader();

        // Act & Assert
        var ex = Assert.Throws<ArgumentException>(() => loader.Load(yaml));

        Assert.Contains("retry", ex.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("input", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>空の workflow.modules は Modules=null として扱う。</summary>
    [Fact]
    public void Load_EmptyWorkflowModules_ReturnsNullModules()
    {
        // Arrange
        var yaml = """
            workflow:
              name: W
              modules: {}
            states:
              A:
                on:
                  Completed:
                    end: true
            """;
        var loader = new StateWorkflowDefinitionLoader();

        // Act
        var def = loader.Load(yaml);

        // Assert
        Assert.Null(def.Modules);
    }

    /// <summary>空の retry ブロックは Retry=null として扱う。</summary>
    [Fact]
    public void Load_EmptyRetryBlock_ReturnsNullRetry()
    {
        // Arrange
        var yaml = """
            workflow:
              name: W
            states:
              A:
                action: noop
                retry: {}
                on:
                  Completed:
                    end: true
            """;
        var loader = new StateWorkflowDefinitionLoader();

        // Act
        var def = loader.Load(yaml);

        // Assert
        Assert.Null(def.States["A"].Retry);
    }

    /// <summary>modules の空 ModuleId は構文エラーになる。</summary>
    [Fact]
    public void Load_WorkflowModulesWithBlankModuleId_Throws()
    {
        // Arrange
        var yaml = """
            workflow:
              name: W
              modules:
                mail: "   "
            states:
              A:
                on:
                  Completed:
                    end: true
            """;
        var loader = new StateWorkflowDefinitionLoader();

        // Act & Assert
        var ex = Assert.Throws<ArgumentException>(() => loader.Load(yaml));

        Assert.Contains("workflow.modules['mail']", ex.Message, StringComparison.Ordinal);
    }

    /// <summary>modules の alias 重複（大文字小文字無視）は Loader で拒否される。</summary>
    [Fact]
    public void Load_DuplicateModuleAlias_Throws()
    {
        // Arrange
        var yaml = """
            workflow:
              name: W
              modules:
                mail: com.company.mail
                Mail: com.company.other
            states:
              A:
                action: noop
                on:
                  Completed:
                    end: true
            """;
        var loader = new StateWorkflowDefinitionLoader();

        // Act & Assert
        var ex = Assert.Throws<ArgumentException>(() => loader.Load(yaml));

        Assert.Contains("duplicate alias 'mail'", ex.Message, StringComparison.OrdinalIgnoreCase);
    }
}

