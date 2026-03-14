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
├─ api/                          # Core-API（C# ASP.NET Core、v2）
│  ├─ Statevia.Core.Api/
│  ├─ statevia-api.sln
│  └─ Dockerfile
│
└─ services/                     # UI 等（Next.js/TS）
   ├─ .gitignore                 # services 共通（Node 用）
   └─ ui/                        # Next.js ダッシュボード
```

注: docker-compose はリポジトリルートの `docker-compose.yml`。Core-API は `api/`（C#）。
