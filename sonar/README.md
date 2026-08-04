# SonarQube（ローカル分析）

このディレクトリには、ローカル SonarQube 用の **Docker Compose**、**コンポーネント別スキャナ（PowerShell）**、**行数集計スクリプト**（`measure-loc.ps1`）がまとまっています。

C# 分析時のカバレッジ XML（`service-*-coverage.xml` / `core-engine-coverage.xml` 等）やローカルダンプ（`*.json`）は **生成物**のため `.gitignore` で除外し、スクリプトと本 README のみリポジトリで共有します。

## 前提条件

- **Docker**（`docker compose` が使えること）
- **.NET 8 SDK** と次のツール
  - `dotnet sonarscanner`（例: `dotnet tool install --global dotnet-sonarscanner`）
  - `dotnet-coverage`（PATH で実行できること）
- **UI 分析**: Node.js / npm、`ui/studio` で `npm install` 済みであること
- 分析実行前に **`SONAR_TOKEN`** を環境変数で設定すること（SonarQube のユーザートークンまたはプロジェクトトークン）

## SonarQube サーバの起動（ローカル）

`docker-compose.yaml` は **Community Edition** と PostgreSQL をポート **9000** で起動します。

```bash
cd sonar
docker compose up -d
```

ブラウザで `http://localhost:9000` にアクセスし、初回ログイン後にプロジェクトを作成するか、下記の **projectKey** で初回分析からプロジェクトが作成されます。

## 分析の実行（推奨: PowerShell スクリプト）

`SONAR_TOKEN` を設定したうえで、**リポジトリのどのカレントディレクトリからでも**次のように実行できます（各スクリプトが `sonar/` の位置から `core/engine` / `service/api` / `ui/studio` 等を解決します）。

```powershell
$env:SONAR_TOKEN = "（SonarQube のトークン）"
dotnet build-server shutdown
& .\sonar\sonar-scanner-engine.ps1
& .\sonar\sonar-scanner-api.ps1
& .\sonar\sonar-scanner-runtime.ps1
& .\sonar\sonar-scanner-cli.ps1
& .\sonar\sonar-scanner-action-host.ps1
& .\sonar\sonar-scanner-ui.ps1
```

- **engine / api / runtime / cli / action-host**: `dotnet sonarscanner begin` → `build` → `dotnet-coverage` → `end` の順で、`sonar/*-coverage.xml` にカバレッジを出力します（XML は git 管理外）。
- **ui**: `npm run test:coverage` で `ui/studio/coverage/lcov.info` を生成したあと、`npx sonar-scanner` で `ui/studio/sonar-project.properties` を読み込んで送信します。

C# スキャナは `sonar.projectBaseDir` をリポジトリルートに固定し、Phase 0 以降のパス（`core/engine` 等）でも除外設定が効くようにしています。**テストプロジェクト**（`*.Tests`）は `sonar.dotnet.excludeTestProjects=true` で各コンポーネントの projectKey から除外します（品質ゲートの対象はプロダクションコード）。`Statevia.Runtime` は **`StateviaServiceRuntime`** で解析し、API スキャナからは `service/runtime` を除外して二重計上を避けます。

### SonarQube 側の既定 URL

スクリプトおよび `ui/studio/sonar-project.properties` の **`sonar.host.url`** は **`http://localhost:9000`** です。別ホストに送る場合は各 `.ps1` の `sonar.host.url` と `sonar-project.properties` を揃えて変更してください。

### プロジェクトキー一覧

| コンポーネント | projectKey             |
| -------------- | ---------------------- |
| Core Engine    | `StateviaCoreEngine`   |
| Service API    | `StateviaServiceApi`   |
| Runtime        | `StateviaServiceRuntime` |
| CLI            | `StateviaServiceCLI`   |
| Action Host    | `StateviaServiceActionHost` |
| Service UI     | `StateviaServiceUI`    |

## 手動実行（リポジトリルートをカレントに）

スクリプトを使わず同じ処理を手で行う場合の例です。

### Core Engine

```powershell
$env:SONAR_TOKEN = "（トークン）"
$repoRoot = (Get-Location).Path
Set-Location core\engine
dotnet sonarscanner begin /k:"StateviaCoreEngine" /d:sonar.host.url="http://localhost:9000" /d:sonar.token="$($env:SONAR_TOKEN)" /d:sonar.projectBaseDir="$repoRoot" /d:sonar.cs.vscoveragexml.reportsPaths="$repoRoot\sonar\core-engine-coverage.xml"
dotnet build "statevia-engine.sln"
dotnet-coverage collect "dotnet test" -f xml -o "$repoRoot\sonar\core-engine-coverage.xml"
dotnet sonarscanner end /d:sonar.token="$($env:SONAR_TOKEN)"
Set-Location $repoRoot
```

### Service API

```powershell
$env:SONAR_TOKEN = "（トークン）"
$repoRoot = (Get-Location).Path
Set-Location service\api
dotnet sonarscanner begin /k:"StateviaServiceApi" /d:sonar.host.url="http://localhost:9000" /d:sonar.token="$($env:SONAR_TOKEN)" /d:sonar.projectBaseDir="$repoRoot" /d:sonar.cs.vscoveragexml.reportsPaths="$repoRoot\sonar\service-api-coverage.xml"
dotnet build "statevia-api.sln"
dotnet-coverage collect "dotnet test" -f xml -o "$repoRoot\sonar\service-api-coverage.xml"
dotnet sonarscanner end /d:sonar.token="$($env:SONAR_TOKEN)"
Set-Location $repoRoot
```

### Runtime

```powershell
$env:SONAR_TOKEN = "（トークン）"
& .\sonar\sonar-scanner-runtime.ps1
```

### CLI

```powershell
$env:SONAR_TOKEN = "（トークン）"
$repoRoot = (Get-Location).Path
Set-Location service\cli
dotnet sonarscanner begin /k:"StateviaServiceCLI" /d:sonar.host.url="http://localhost:9000" /d:sonar.token="$($env:SONAR_TOKEN)" /d:sonar.projectBaseDir="$repoRoot" /d:sonar.cs.vscoveragexml.reportsPaths="$repoRoot\sonar\service-cli-coverage.xml"
dotnet build "statevia-cli.sln"
dotnet-coverage collect "dotnet test" -f xml -o "$repoRoot\sonar\service-cli-coverage.xml"
dotnet sonarscanner end /d:sonar.token="$($env:SONAR_TOKEN)"
Set-Location $repoRoot
```

### Service UI（手動）

```powershell
$env:SONAR_TOKEN = "（トークン）"
Set-Location ui\studio
npm run test:coverage
npx --yes sonar-scanner "-Dsonar.token=$($env:SONAR_TOKEN)"
Set-Location ..\..
```

## 行数集計（`measure-loc.ps1`）

現行リポジトリ構成（`core/` / `infrastructure/` / `service/` / `ui/studio` / `tests/`）のソース行数を、**プロダクト**と**テスト**に分けて集計します。SonarQube の分析とは独立しており、**`SONAR_TOKEN` は不要**です。

```powershell
.\sonar\measure-loc.ps1
.\sonar\measure-loc.ps1 -Detailed
.\sonar\measure-loc.ps1 -Json | Set-Content loc-report.json
```

### 集計対象

| コンポーネント | プロダクト | テスト |
| -------------- | ---------- | ------ |
| engine | `Statevia.Core.Engine` | `Statevia.Core.Engine.Tests` |
| application | `Statevia.Core.Application`, `Statevia.Core.Application.Contracts`, `Statevia.Core.Actions.Abstractions` | （なし） |
| infrastructure | `Statevia.Infrastructure.*`（Common / Persistence / Security / Modules / Notification / Actions.Grpc） | `*.Modules.Tests`, `*.Actions.Grpc.Tests` |
| api | `Statevia.Service.Api`, `Statevia.Service.Api.Bootstrap` | `Statevia.Service.Api.Tests` |
| runtime | `Statevia.Runtime`, `Statevia.Service.Runtime.Scheduler`, `Statevia.Service.Runtime.Worker` | （なし。HostedService テストは Api.Tests） |
| cli | `Statevia.Service.Cli` | `Statevia.Service.Cli.Tests` |
| action-host | `Statevia.Service.ActionHost` | `Statevia.Service.ActionHost.Tests` |
| ui | `ui/studio`（`tests/`・`e2e/`・`*.test.*` を除く `*.ts` / `*.tsx`） | `tests/`、`*.test.*`、`*.spec.*`、`e2e/` |
| architecture | （なし） | `Statevia.Architecture.Tests` |

除外: `bin/`, `obj/`, `node_modules/`, `.next/`, `coverage/`。`core/engine/samples/` と `service/action-host/Fixtures/` は対象プロジェクト定義に含めません。Migrations は `infrastructure/Statevia.Infrastructure.Persistence/Migrations` を内訳表示します。

## 主なファイル

| ファイル | 説明 |
| -------- | ---- |
| `docker-compose.yaml` | ローカル SonarQube + PostgreSQL |
| `sonar-scanner-engine.ps1` | Engine 向け一括分析 |
| `sonar-scanner-api.ps1` | API 向け一括分析 |
| `sonar-scanner-runtime.ps1` | Runtime（HostedService / Scheduler / Worker）向け一括分析 |
| `sonar-scanner-cli.ps1` | CLI 向け一括分析 |
| `sonar-scanner-action-host.ps1` | Action Host 向け一括分析 |
| `sonar-scanner-ui.ps1` | UI 向け一括分析 |
| `measure-loc.ps1` | 行数集計（プロダクト・テスト別） |
| `core-*-coverage.xml` | C# カバレッジ（**生成物・git 管理外**） |

## 関連ドキュメント

- 開発ガイド Sonar 節: `docs/development-guidelines.md` §5
- SonarQube MCP: `.cursor/skills/sonarqube-mcp-ops/SKILL.md`
