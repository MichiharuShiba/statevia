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
├─ engine/                       # C# ワークフローエンジン（.NET、v2）
│  ├─ statevia-engine.sln       # .NET ソリューション（samples は含めない）
│  ├─ Directory.Build.props
│  │
│  ├─ Statevia.Core.Engine/     # エンジンコア（旧 Statevia.Core）
│  │  ├─ Statevia.Core.Engine.csproj
│  │  ├─ Engine/
│  │  ├─ FSM/
│  │  ├─ Scheduler/
│  │  ├─ Execution/
│  │  ├─ Join/
│  │  ├─ Definition/
│  │  ├─ ExecutionGraph/
│  │  └─ Abstractions/
│  │
│  ├─ Statevia.Cli/
│  │  ├─ Statevia.Cli.csproj
│  │  └─ Program.cs
│  │
│  ├─ Statevia.Core.Engine.Tests/
│  │  └─ Statevia.Core.Engine.Tests.csproj
│  │
│  ├─ Statevia.Cli.Tests/
│  │  └─ Statevia.Cli.Tests.csproj
│  │
│  └─ samples/                   # .sln には含めない
│     └─ hello-statevia/
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
