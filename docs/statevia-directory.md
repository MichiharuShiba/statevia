# ディレクトリ構成

Version: 2.0
Project: Statevia — 実行型ステートマシン
更新日: 2026-07-04

---

4 カテゴリ構成（`core/` / `infrastructure/` / `service/` / `ui/`）に基づく現行レイアウトです。

```txt
statevia/
├─ README.md
├─ LICENSE
├─ .gitignore
├─ .editorconfig
├─ docker-compose.yml            # postgres + service-api (C#) + ui
├─ .env.example                  # POSTGRES_*（.env はコミットしない）
│
├─ docs/
│  ├─ statevia-architecture.md   # システム構成 + レイヤー
│  ├─ statevia-design-philosophy.md
│  ├─ statevia-directory.md      # 本ファイル
│  ├─ development-guidelines.md  # 開発ガイドライン
│  ├─ core-api-interface.md      # Core-API HTTP 契約（v1）
│  ├─ operations-docker.md       # Docker Compose 運用
│  ├─ core-engine-*.md           # Engine 各仕様
│  └─ statevia-data-integration-contract.md
│
├─ core/                         # ドメイン・契約（非デプロイ）
│  ├─ engine/
│  │  ├─ statevia-engine.sln
│  │  ├─ Statevia.Core.Engine/          # ワークフローエンジン
│  │  │  ├─ Engine/                     # ExecutionEngine, ExecutionInstance
│  │  │  ├─ FSM/                        # 遷移評価
│  │  │  ├─ Scheduler/                  # 並列制御
│  │  │  ├─ Execution/                  # DefaultStateExecutor
│  │  │  ├─ Join/                       # JoinTracker
│  │  │  ├─ Definition/                 # 定義ロード・コンパイル・検証
│  │  │  ├─ ExecutionGraph/             # 実行グラフ
│  │  │  └─ Abstractions/
│  │  ├─ Statevia.Core.Engine.Tests/
│  │  └─ samples/hello-statevia/
│  ├─ application/
│  │  ├─ Statevia.Core.Application/           # ユースケース実装
│  │  └─ Statevia.Core.Application.Contracts/ # DDD ポート・DTO
│  └─ actions/
│     └─ Statevia.Core.Actions.Abstractions/  # Action / Module SPI
│
├─ infrastructure/               # 技術実装（差し替え可能・最外殻）
│  ├─ statevia-infrastructure.sln
│  ├─ Statevia.Infrastructure.Persistence/    # EF Core + Migrations
│  ├─ Statevia.Infrastructure.Security/       # JWT, Tenant, 認可
│  ├─ Statevia.Infrastructure.Notification/   # SMTP 通知
│  ├─ Statevia.Infrastructure.Modules/        # Module ホスト + Source
│  ├─ Statevia.Infrastructure.Actions.Grpc/   # gRPC Action Backend
│  ├─ Statevia.Infrastructure.Common/         # IdGenerator 等
│  ├─ Statevia.Infrastructure.Modules.Tests/
│  └─ Statevia.Infrastructure.Actions.Grpc.Tests/
│
├─ service/                      # デプロイ可能なインターフェイス
│  ├─ api/
│  │  ├─ statevia-api.sln
│  │  ├─ Dockerfile
│  │  ├─ Statevia.Service.Api/              # HTTP アダプタ（Controllers + DI）
│  │  ├─ Statevia.Service.Api.Bootstrap/    # エントリポイント（Program.cs）
│  │  └─ Statevia.Service.Api.Tests/
│  ├─ action-host/
│  │  ├─ statevia-action-host.sln
│  │  ├─ Statevia.Service.ActionHost/       # gRPC sandbox
│  │  └─ Statevia.Service.ActionHost.Tests/
│  └─ cli/
│     ├─ statevia-cli.sln
│     ├─ Statevia.Service.Cli/              # statevia コマンド
│     └─ Statevia.Service.Cli.Tests/
│
├─ ui/
│  └─ studio/                    # UI（Next.js / React / ReactFlow — @statevia/studio）
│     ├─ package.json
│     ├─ app/
│     │  ├─ api/core/[...path]/  # Core-API プロキシ（/v1 へ転送）
│     │  ├─ components/
│     │  ├─ features/
│     │  └─ lib/
│     └─ Dockerfile
│
├─ tests/                        # 横断テスト
│  └─ Statevia.Architecture.Tests/  # 依存方向テスト
│
└─ scripts/                      # ビルド・運用スクリプト
```

- **core/**: ドメインと契約。Engine は HTTP/DB に非依存。Application がユースケースを実装し、Contracts が DDD ポートを定義。
- **infrastructure/**: core の契約の技術実装。DB、認証、通知、Module ホスト等。
- **service/**: HTTP / gRPC / CLI のアダプタ。Composition Root として DI を組み立てる。
- **ui/studio/**: Next.js（`@statevia/studio`）。`/api/core/*` で Core-API にプロキシ。
- **tests/**: ソリューション横断のアーキテクチャテスト。
