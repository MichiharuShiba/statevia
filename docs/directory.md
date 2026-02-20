# ディレクトリ構成

```txt
statevia/
├─ README.md
├─ LICENSE
├─ .gitignore                    # リポジトリルート（.NET / 共通）
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
├─ core/                         # C# ワークフローエンジン（.NET）
│  ├─ statevia.sln              # .NET ソリューション
│  ├─ .gitignore                # core 配下用 .NET
│  ├─ .editorconfig
│  ├─ Directory.Build.props
│  │
│  ├─ Statevia.Core/            # エンジンコア
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
│  ├─ Statevia.Cli/             # CLI
│  │  ├─ Statevia.Cli.csproj
│  │  └─ Program.cs
│  │
│  ├─ Statevia.Core.Tests/       # コア単体テスト
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
│  ├─ Statevia.Cli.Tests/        # CLI 単体テスト
│  │  ├─ Statevia.Cli.Tests.csproj
│  │  └─ ProgramTests.cs
│  │
│  └─ samples/                   # サンプル
│     └─ hello-statevia/
│        ├─ hello-statevia.csproj
│        ├─ hello.yaml
│        ├─ Program.cs
│        └─ README.md
│
└─ services/                     # Node/TypeScript スタック（独立）
   ├─ .gitignore                 # services 共通（Node 用）
   ├─ docker-compose.yml         # postgres + core-api
   └─ core-api/                  # Express API（Execution/Node の HTTP API、DDD 構成）
      ├─ .gitignore              # core-api 配下用 Node
      ├─ .env.example
      ├─ Dockerfile
      ├─ package.json
      ├─ tsconfig.json
      ├─ src/
      │  ├─ server.ts
      │  │
      │  ├─ domain/                    # Domain Layer (DDD)
      │  │  ├─ entities/               # エンティティ
      │  │  │  ├─ execution.ts
      │  │  │  └─ node.ts
      │  │  ├─ value-objects/          # 値オブジェクト
      │  │  │  ├─ actor.ts
      │  │  │  ├─ event-envelope.ts
      │  │  │  └─ execution-state.ts
      │  │  ├─ domain-services/        # ドメインサービス
      │  │  │  ├─ reducer.ts
      │  │  │  └─ guards.ts
      │  │  ├─ events/                 # ドメインイベント
      │  │  │  └─ event-types.ts
      │  │  └─ errors.ts              # ドメインエラー
      │  │
      │  ├─ application/              # Application Layer
      │  │  ├─ commands/               # コマンドハンドラー
      │  │  │  └─ command-handlers.ts
      │  │  ├─ use-cases/              # ユースケース
      │  │  │  ├─ create-execution-use-case.ts
      │  │  │  └─ execute-command-use-case.ts
      │  │  └─ services/               # アプリケーションサービス
      │  │     └─ orchestrator.ts
      │  │
      │  ├─ infrastructure/            # Infrastructure Layer
      │  │  ├─ persistence/            # 永続化
      │  │  │  ├─ db.ts
      │  │  │  └─ repositories/
      │  │  │     ├─ event-store.ts
      │  │  │     └─ execution-repository.ts
      │  │  └─ idempotency/            # 冪等性管理
      │  │     └─ idempotency-service.ts
      │  │
      │  └─ presentation/               # Presentation Layer
      │     ├─ http/                    # HTTP層
      │     │  ├─ routes.ts
      │     │  ├─ routes/
      │     │  │  ├─ executions.ts
      │     │  │  └─ nodes.ts
      │     │  ├─ errors.ts
      │     │  ├─ error-handler.ts
      │     │  ├─ middleware.ts
      │     │  └─ idempotent-handler.ts
      │     └─ dto/                     # DTO
      │        └─ validators.ts
      │
      └─ sql/
         └─ 001_init.sql
```
