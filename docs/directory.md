# ディレクトリ構成

```txt
statevia-engine/
├─ README.md
├─ LICENSE
├─ statevia.sln
├─ .gitignore
├─ .editorconfig
├─ .github/ (未作成)
│  ├─ ISSUE_TEMPLATE.md
│  ├─ PULL_REQUEST_TEMPLATE.md
│  └─ workflows/
│     └─ ci.yml
│
├─ docs/
│  ├─ architecture.md
│  ├─ design-philosophy.md
│  ├─ definition-spec.md
│  ├─ fsm-spec.md
│  ├─ fork-join-spec.md
│  ├─ wait-cancel-spec.md
│  ├─ execution-graph-spec.md
│  ├─ ui-visual-spec.md
│  └─ directory.md
│
├─ src/
│  ├─ Statevia.Core/
│  │  ├─ Statevia.Core.csproj
│  │  ├─ Engine/
│  │  │  ├─ WorkflowEngine.cs
│  │  │  ├─ WorkflowEngineOptions.cs
│  │  │  ├─ WorkflowInstance.cs
│  │  │  ├─ WorkflowStateStore.cs
│  │  │  ├─ WorkflowSnapshotExtensions.cs
│  │  │  └─ EventProvider.cs
│  │  │
│  │  ├─ FSM/
│  │  │  ├─ IFsm.cs
│  │  │  ├─ TransitionTable.cs
│  │  │  ├─ TransitionResult.cs
│  │  │  └─ Fact.cs
│  │  │
│  │  ├─ Scheduler/
│  │  │  ├─ IScheduler.cs
│  │  │  ├─ DefaultScheduler.cs
│  │  │  └─ ExecutionLimiter.cs
│  │  │
│  │  ├─ Execution/
│  │  │  ├─ StateExecution.cs
│  │  │  ├─ IStateExecutor.cs
│  │  │  └─ DefaultStateExecutor.cs
│  │  │
│  │  ├─ Join/
│  │  │  ├─ IJoinTracker.cs
│  │  │  └─ JoinTracker.cs
│  │  │
│  │  ├─ Definition/
│  │  │  ├─ DefinitionLoader.cs
│  │  │  ├─ DefinitionCompiler.cs
│  │  │  ├─ WorkflowDefinition.cs
│  │  │  ├─ DictionaryStateExecutorFactory.cs
│  │  │  ├─ ScalarPreservingNodeTypeResolver.cs
│  │  │  └─ Validation/
│  │  │     ├─ DefinitionValidator.cs
│  │  │     ├─ Level1Validator.cs
│  │  │     └─ Level2Validator.cs
│  │  │
│  │  ├─ ExecutionGraph/
│  │  │  ├─ ExecutionGraph.cs
│  │  │  ├─ ExecutionNode.cs
│  │  │  └─ ExecutionEdge.cs
│  │  │
│  │  └─ Abstractions/
│  │     ├─ IWorkflowEngine.cs
│  │     ├─ IState.cs
│  │     ├─ IStateExecutor.cs
│  │     ├─ IStateExecutorFactory.cs
│  │     ├─ IEventProvider.cs
│  │     ├─ IReadOnlyStateStore.cs
│  │     ├─ StateContext.cs
│  │     ├─ CompiledWorkflowDefinition.cs
│  │     ├─ WorkflowSnapshot.cs
│  │     └─ Unit.cs
│  │
│  └─ Statevia.Cli/
│     ├─ Statevia.Cli.csproj
│     └─ Program.cs
│
├─ samples/
│  └─ hello-statevia/
│     ├─ hello-statevia.csproj
│     ├─ hello.yaml
│     ├─ Program.cs
│     └─ README.md
│
└─ tests/
│  ├─ Statevia.Core.Tests/
│  │  ├─ Statevia.Core.Tests.csproj
│  │  ├─ Abstractions/
│  │  │  └─ UnitTests.cs
│  │  ├─ Engine/
│  │  │  ├─ CancelTests.cs
│  │  │  ├─ WaitResumeTests.cs
│  │  │  └─ WorkflowEngineTests.cs
│  │  ├─ Definition/
│  │  │  ├─ DefinitionLoaderTests.cs
│  │  │  ├─ DefinitionCompilerTests.cs
│  │  │  ├─ DefinitionValidatorTests.cs
│  │  │  ├─ DictionaryStateExecutorFactoryTests.cs
│  │  │  ├─ Level1ValidationTests.cs
│  │  │  └─ Level2ValidationTests.cs
│  │  ├─ Execution/
│  │  │  └─ DefaultStateExecutorTests.cs
│  │  ├─ FSM/
│  │  │  └─ FsmTests.cs
│  │  ├─ Join/
│  │  │  └─ ForkJoinTests.cs
│  │  ├─ Scheduler/
│  │  │  └─ SchedulerTests.cs
│  │  └─ ExecutionGraph/
│  │     └─ ExecutionGraphTests.cs
│  │
│  └─ Statevia.Cli.Tests/
│     ├─ Statevia.Cli.Tests.csproj
│     └─ ProgramTests.cs
│
├─ docker-compose.yml
├─ .env.example
└─ services/
   └─ core-api/
      ├─ Dockerfile
      ├─ package.json
      ├─ tsconfig.json
      ├─ src/
      │  ├─ server.ts
      │  ├─ http/
      │  │  ├─ routes.ts
      │  │  ├─ errors.ts
      │  │  └─ idempotency.ts
      │  ├─ core/
      │  │  ├─ types.ts
      │  │  ├─ events.ts
      │  │  ├─ reducer.ts
      │  │  ├─ commands.ts
      │  │  └─ guards.ts
      │  └─ store/
      │     ├─ db.ts
      │     ├─ eventStore.ts
      │     └─ projections.ts
      │     └─ load.ts
      └─ sql/
         └─ 001_init.sql
```
