# ディレクトリ構成

Version: 1.0
Project: 実行型ステートマシン

---

現在の実装（Core-Engine / Core-API / UI）に基づく構成です。

```txt
statevia/
├─ README.md
├─ LICENSE
├─ .gitignore
├─ .editorconfig
├─ docker-compose.yml            # postgres + core-api (C#) + ui
├─ .env.example                  # POSTGRES_*（.env はコミットしない）
│
├─ docs/
│  ├─ statevia-architecture.md   # システム構成 + Core-Engine レイヤー
│  ├─ statevia-design-philosophy.md
│  ├─ statevia-directory.md     # 本ファイル
│  ├─ core-api-interface.md    # Core-API HTTP 契約（v1）
│  ├─ core-engine-definition-spec.md
│  ├─ core-engine-fsm-spec.md
│  ├─ core-engine-fork-join-spec.md
│  ├─ core-engine-wait-cancel-spec.md
│  ├─ core-engine-execution-graph-spec.md
│  ├─ core-engine-events-spec.md
│  ├─ core-engine-commands-spec.md
│  ├─ core-engine-reducer-spec.md
│  ├─ core-engine-state-machine-spec.md
│  └─ （ui-*, statevia-* その他仕様）
│
├─ engine/                       # Core-Engine（C# ライブラリ + CLI）
│  ├─ statevia-engine.sln
│  ├─ Directory.Build.props
│  ├─ Statevia.Core.Engine/      # エンジンコア
│  │  ├─ Engine/                 # WorkflowEngine, WorkflowInstance
│  │  ├─ FSM/                    # 遷移評価
│  │  ├─ Scheduler/              # 並列制御
│  │  ├─ Execution/             # DefaultStateExecutor
│  │  ├─ Join/                   # JoinTracker
│  │  ├─ Definition/             # 定義ロード・コンパイル・検証
│  │  ├─ ExecutionGraph/        # 実行グラフ
│  │  └─ Abstractions/
│  ├─ Statevia.Cli/
│  ├─ Statevia.Core.Engine.Tests/
│  ├─ Statevia.Cli.Tests/
│  └─ samples/
│     └─ hello-statevia/
│
├─ api/                          # Core-API（C# ASP.NET Core）
│  ├─ statevia-api.sln
│  ├─ Dockerfile
│  └─ Statevia.Core.Api/
│     ├─ Program.cs
│     ├─ Controllers/
│     │  ├─ DefinitionsController.cs   # v1/definitions
│     │  └─ WorkflowsController.cs      # v1/workflows
│     ├─ Persistence/            # EF Core（CoreDbContext）
│     ├─ Services/               # DisplayIdService
│     └─ Hosting/                # DefinitionCompilerService
│
└─ services/
   └─ ui/                        # UI（Next.js / React / ReactFlow）
      ├─ package.json
      ├─ app/
      │  ├─ api/core/[...path]/  # Core-API プロキシ（/v1 へ転送）
      │  ├─ page.tsx
      │  ├─ graphs/              # 静的グラフ定義・registry
      │  ├─ components/
      │  ├─ features/
      │  └─ lib/
      └─ Dockerfile
```

- **Core-Engine**: `engine/`。定義駆動 FSM / Fork-Join / ExecutionGraph。API から同一プロセスで参照（IWorkflowEngine）。
- **Core-API**: `api/`。v1/definitions・v1/workflows・v1/health。PostgreSQL は EF Core マイグレーションで管理。
- **UI**: `services/ui/`。Next.js。`/api/core/*` で Core-API（C#）にプロキシ。
