# 開発ガイドライン

本ドキュメントは、リポジトリでコードを書く際の**共通ルール**をまとめたものです。  
アーキテクチャや起動方法の詳細は **`AGENTS.md`** を正本とします。

---

## 1. 正本と参照先

| 種類 | 場所 |
|------|------|
| エージェント／レイヤー／DI／テスト実行 | リポジトリ直下 `AGENTS.md` |
| HTTP 契約・エラー形式 | `docs/core-api-interface.md`, `docs/data-integration-contract.md` |
| 作業用仕様・計画・タスク（任意） | `.workspace-docs/`（`README.md` が入口） |
| C# 細則（命名・コメント・パターンマッチ・テスト体裁） | `.cursor/rules/csharp-standards.mdc`（Cursor 利用時） |
| コメント・ドキュメント充実方針（言語横断） | `.cursor/rules/documentation-standards.mdc`（Cursor 利用時） |
| TypeScript / React 細則（命名・JSDoc・型絞り込み・テスト体裁） | `.cursor/rules/typescript-standards.mdc`（Cursor 利用時） |
| Markdown（`.spec-workflow` 等） | ルート `.markdownlint.json`（`markdownlint-cli2` で検証可能） |

---

## 2. 変更の基本方針

- **依頼された範囲に集中する。** 無関係なリファクタ・ファイルの掃除・ドキュメントの拡大は行わない。
- **周辺コードのスタイルに合わせる。** 命名、型の置き方、コメント量、import／using の並びを既存に揃える。
- **契約を壊さない。** API のステータスコード・エラー JSON 形・ヘッダ（`X-Tenant-Id`, `X-Idempotency-Key`）は `docs/` の契約に従う。
- **機微データの露出に注意する。** 一覧／GET で Start 時の `input` や state `output` を広げる変更は、IO-14（`AGENTS.md`）と整合させる。

---

## 3. コンポーネント別

### 3.1 Core-API（`api/`）

- **Controller**: ルーティング・バインディング・ヘッダ・HTTP ステータスのみ。ビジネスロジックは Service へ。
- **Service**: ユースケース境界。`ICoreTransactionExecutor` / `IExecutionMutationPersistence` 経由でコミット境界を決め、Repository・DisplayId・command dedup・`IExecutionEngine` を組み合わせる。Service から `IDbContextFactory` を直接使わない。
- **Repository**: 永続化のみ。書き込み API の第一引数は常に `ICoreUnitOfWork`。`SaveChanges` / `BeginTransaction` / `IDbContextFactory` を Repository 内に持たない。
- **UoW**: `IDbContextFactory<CoreDbContext>` は `CoreUnitOfWork` 実装内に閉じる。
- **例外**: `ApiExceptionFilter` と契約に沿ったエラー JSON（404 / 422 / 500）。
- **OpenAPI / Scalar**: Development では `http://localhost:8080/scalar/v1`（起動 URL に依存）。JSON は `/swagger/v1/swagger.json`。コミット用 export はリポジトリルートから `.\scripts\export-core-api-openapi.ps1` → `api/openapi/core-api-v1.openapi.json`。本番は既定オフ、`STATEVIA_ENABLE_API_DOCS=true` で有効化可。手書き契約は `docs/core-api-interface.md`（運用叙述は Markdown に残す）。

### 3.2 Engine（`engine/`）

- ワークフロー実行・FSM・グラフの責務に留める。HTTP や DB に直接触れない。
- 公開 API の変更は、Core-API や契約ドキュメントへの影響を確認する。

### 3.3 UI（`services/ui/`）

- Core-API へは Next.js の route handler 経由でプロキシ（CORS 回避）。環境変数は `AGENTS.md` の表を参照。
- 静的解析: `npm run lint`（ESLint 9 strict）。型チェック: `npm run typecheck`（`tsc --noEmit`）。テスト: `npm run test:run`（Vitest）。Sonar は **§5.2**。

---

## 4. コーディングと静的チェック

- **C#** の細則は **`.cursor/rules/csharp-standards.mdc`**（§4.1）。**TypeScript / React** は **`.cursor/rules/typescript-standards.mdc`**（§4.2）。**コメント・ドキュメント**は §4.3。**リンター・ビルド**は §4.4。

### 4.1 C#（コメント・XML・テストの要約）

- **モダン C# のパターンマッチ**（`is` パターン、switch 式）を優先する。粒度の詳細は **`csharp-standards.mdc`**。
- **public / internal** の型・メンバーには **XML ドキュメント**（`/// <summary>` 等）を付ける。
- **private** は、意図がコードだけでは分かりにくい箇所（ヘルパー・複雑な分岐・仕様参照）に `/// <summary>` や行コメントを付ける。
- **単体テスト**では、メソッドごとに **日本語の `/// <summary>`**（何を検証するか）と、本体の **`// Arrange` / `// Act` / `// Assert`** 区切りを付ける。
- 上記の粒度・例は **`csharp-standards.mdc`** に従う。横断方針は **`documentation-standards.mdc`**。

### 4.2 TypeScript（UI）

- 詳細は **`.cursor/rules/typescript-standards.mdc`**（変数名・JSDoc・型の絞り込み・React の props・Vitest の AAA と日本語ケース名）。
- コメント・ドキュメントの横断方針は **`.cursor/rules/documentation-standards.mdc`**。
- 品質チェックは **§4.4** の UI 行に従う。

### 4.3 コメント・ドキュメント（言語横断）

今後の標準として、**実装と同時にドキュメントを充実させる**。詳細は **`.cursor/rules/documentation-standards.mdc`**。

- **公開 API**（C# の `public` / `internal`、TS の `export`）には、目的・引数・戻り値・例外（またはエラー条件）を記載する。
- **クラス / モジュール単位**では、利用コンテキスト・セキュリティ・制限値・上書きなど、コードだけでは分かりにくい契約を `<remarks>` / JSDoc で補足する。
- **定数（上限値など）**には、値の意味と採用理由を書く（推測や誤った技術的根拠は避ける）。
- **テスト**には日本語で「何を検証するか」を `/// <summary>` またはケース名で残す。
- **書かない**: 自明な処理の言い換えコメント、実装と矛盾する説明、機密情報。
- **仕様・運用に影響する変更**では、関連する `docs/` や `.spec-workflow/` を依頼範囲内で整合させる。

### 4.4 リンター・ビルド警告・静的チェック

- **C#**: `dotnet build` / `dotnet test` で出る **コンパイラエラー・Analyzer 警告**は、自分の変更に起因するものは **解消してから** PR に出す。触れていないファイルの既存警告をまとめて直すのは必須ではないが、**新規コードで警告を増やさない**こと。
- **Core-API 厳格 Analyzer**: `api/Directory.Build.props` で `AnalysisMode=AllEnabledByDefault` が有効。Api 配下の本番プロジェクトは **`dotnet build api/statevia-api.sln` で警告 0** を維持する。
- **Core-API テスト向け抑制**: ルート `.editorconfig` に加え、`api/Statevia.Service.Api.Tests/.editorconfig` で xUnit 向け CA（例: CA1812）を抑制している。テストの命名・`ConfigureAwait` 方針は Engine.Tests と同趣旨。
- **Markdown**: リポジトリ直下の **`.markdownlint.json`** に従う。`.spec-workflow/**/*.md` などを編集したときは例として次で確認できる。

  ```bash
  npx markdownlint-cli2 ".spec-workflow/**/*.md"
  ```

- **UI（TypeScript）**: **`npm run lint`**（error 厳格）、**`npm run typecheck`**、**`npm run test:run`** を PR 前の必須チェックとする。設定は `services/ui/eslint.config.js`（`typescript-eslint` strict、`react-hooks`、`jsx-a11y`、`jsdoc`）。
- **SonarQube（Core-API）**: プロジェクトキー **`StateviaCoreAPI`**。新規コードの Quality Gate（`new_coverage ≥ 80%`、`new_violations = 0` 等）を満たすこと。手順は **§5.1**。
- **SonarQube（Service UI）**: プロジェクトキー **`StateviaServiceUI`**。全体・新規コードの Quality Gate（`coverage` / `new_coverage ≥ 80%`、`new_violations = 0` 等）を満たすこと。手順は **§5.2**。

---

## 5. テストと品質ゲート

| 領域 | コマンド（例） |
|------|----------------|
| Engine | `cd engine && dotnet test statevia-engine.sln` |
| Core-API | `cd api && dotnet test statevia-api.sln` |
| UI | `cd services/ui && npm run lint && npm run typecheck && npm run test:run` |
| UI（Sonar 前） | `cd services/ui && npm run test:coverage` |

変更した領域に対応するテストを追加または更新し、ローカルで green を確認してから共有する。UI を Sonar に送る前はカバレッジ付きテストを実行する（**§5.2**）。

### 5.1 Core-API — カバレッジと Sonar（手動）

**前提**

- ローカル SonarQube が起動していること（既定 URL: `http://localhost:9000`）
- 環境変数 **`SONAR_TOKEN`** を設定していること
- グローバルツール: `dotnet-sonarscanner`、`dotnet-coverage`（スクリプトが利用する）

**カバレッジ runsettings（単体テストのみ確認するとき）**

`api/coverage.runsettings` では Engine アセンブリと `Program.cs` / `Migrations/` を cobertura から除外する。Api 本体の行が計測対象に含まれることを確認する。

```powershell
cd api
dotnet test --settings coverage.runsettings --collect:"XPlat Code Coverage"
```

**Sonar スキャン（正本スクリプト）**

リポジトリルートから実行する（カレントディレクトリに依存しない）。

```powershell
# ビルドロック回避（IDE で obj 内ファイルを開いている場合）
dotnet build-server shutdown

# トークン設定後
./sonar/sonar-scanner-api.ps1
```

スクリプトは次を順に実行する。

1. `dotnet sonarscanner begin`（キー `StateviaCoreAPI`、除外は Engine / UI / Program / Migrations 等）
2. `dotnet build api/statevia-api.sln`
3. `dotnet-coverage collect "dotnet test"` → `sonar/core-api-coverage.xml`
4. `dotnet sonarscanner end`

**注意**

- `api/sonar-project.properties` は **Scanner for .NET では使わない**（設定は `begin` の `/d:` で渡す）。
- スキャン直後は Sonar UI の数値が遅れて反映されることがある。Quality Gate 判定は Sonar のプロジェクト画面を正とする。

### 5.2 Service UI — カバレッジと Sonar（手動）

**前提**

- ローカル SonarQube が起動していること（既定 URL: `http://localhost:9000`。`sonar/docker-compose.yaml` 参照）
- 環境変数 **`SONAR_TOKEN`** を設定していること
- Node.js / npm が PATH にあり、`services/ui` で `npm install` 済みであること
- グローバルまたは `npx` で **`sonar-scanner`** が実行できること

**カバレッジ（Vitest のみ確認するとき）**

```powershell
cd services/ui
npm run test:coverage
```

`coverage/lcov.info` が生成される。除外方針は `services/ui/sonar-project.properties` の `sonar.coverage.exclusions` と `vitest.config.ts` の `coverage.exclude` を揃える（正本の説明: `.spec-workflow/specs/ui-quality-refactor/sonar-scan-results.md`）。

**Sonar スキャン（正本スクリプト）**

リポジトリルートから実行する（カレントディレクトリに依存しない）。

```powershell
# トークン設定後
./sonar/sonar-scanner-ui.ps1
```

スクリプトは次を順に実行する。

1. `npm run test:coverage`（`services/ui/coverage/lcov.info` を生成）
2. `npx sonar-scanner`（キー **`StateviaServiceUI`**、`services/ui/sonar-project.properties` を読み込み）

**手動（`services/ui` をカレントに）**

```powershell
cd services/ui
npm run test:coverage
npx --yes sonar-scanner "-Dsonar.token=$($env:SONAR_TOKEN)"
```

**注意**

- HTTP 契約・プロキシ挙動は本リファクタの対象外（`docs/core-api-interface.md` 等は変更しない）。
- スキャン直後は Sonar UI の数値が遅れて反映されることがある。Quality Gate 判定は Sonar のプロジェクト画面を正とする。
- 詳細は `sonar/README.md` も参照。

---

## 6. ブランチ命名

**形式**: `<種別>/<短い説明-kebab-case>`

| 種別 | 用途 | 例 |
|------|------|-----|
| `feature` | 新機能・拡張（デフォルト） | `feature/workflow-cancel-retry` |
| `fix` | 不具合修正 | `fix/command-dedup-race` |
| `chore` | ツール・設定・依存更新（挙動変更が軽微） | `chore/bump-npgsql` |
| `docs` | ドキュメントのみ | `docs/development-guidelines` |
| `test` | テスト追加・整備のみ | `test/engine-scheduler-edge-cases` |

**ルール**

- 説明部は **英語の kebab-case**（小文字・単語はハイフン）。**ASCII** に限定する。
- **1 ブランチ 1 目的**。大きい変更はトピックを分け、必要なら `feature/foo-part-b` のように連番や段階名を付ける。
- **チケット・Issue 番号**を付ける場合は末尾に付与してよい: `feature/io-api-input-STV-402`（任意）。
- **長期エピック**で接頭辞を揃える場合は `feature/<エピック>-<要約>` とする（例: `feature/io-c-api-workflow-start-input`）。`.workspace-docs` のタスク表と揃えてもよい。

**保護ブランチ**（`main` など）へ直接 push せず、PR 経由でマージする。

---

## 7. コミットと説明文

- **コミットメッセージは日本語**で、何を・なぜかが分かる一行以上にする（詳細は `.cursor/rules/Generate-commit-messages.mdc`）。
- PR 説明も日本語で、変更範囲と影響（API 契約・マイグレーション有無）を簡潔に書く。

---

## 8. 変更履歴

| 日付 | 内容 |
|------|------|
| 2026-05-19 | §4.3 / §5 に UI の ESLint・`StateviaServiceUI` Sonar 手順（§5.2・`sonar-scanner-ui.ps1`）を追記 |
| 2026-05-17 | §4.3 / §5.1 に Core-API 厳格ビルド・coverlet runsettings・`sonar-scanner-api.ps1` 手順を追記 |
| 2026-04-05 | §4 再編（4.1 C#・4.2 TypeScript・4.3 静的チェック）、`typescript-standards.mdc` 追加、正本表に TypeScript 細則を追記 |
| 2026-03-28 | 初版・ブランチ命名を追加 |
