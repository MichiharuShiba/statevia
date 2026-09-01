# 開発ガイドライン

本ドキュメントは、リポジトリでコードを書く際の**共通ルールの正本**です。  
アーキテクチャ・契約・利用者向けドキュメントの入口は **[`docs/README.md`](README.md)**。起動・テスト・主要 env は **[`AGENTS.md`](../AGENTS.md)** を参照してください。

Markdown 執筆ルールは [`DOCUMENTATION-STANDARD.md`](DOCUMENTATION-STANDARD.md)。Cursor 等のエージェント向けローカル設定は本書と同内容を参照しますが、**Git 上の正本は `docs/` 配下**です。

---

## 1. 正本と参照先

| 種類 | 場所 |
|------|------|
| ドキュメント索引・知識体系 | [`docs/README.md`](README.md) |
| コーディング共通ルール（C# / TS / コメント） | 本書 **§4** |
| Markdown 執筆 | [`DOCUMENTATION-STANDARD.md`](DOCUMENTATION-STANDARD.md) |
| エージェント向け起動・テスト・env 索引 | [`AGENTS.md`](../AGENTS.md) |
| HTTP 契約・エラー形式 | [`specifications/api-http.md`](specifications/api-http.md), [`specifications/data-integration.md`](specifications/data-integration.md) |
| 作業用仕様・計画（任意） | `.spec/`（`templates/` / `steering/` / `specs/`。旧メモは `.spec/archive/workspace-docs/`） |
| Markdown lint | ルート [`.markdownlint.json`](../.markdownlint.json)（`markdownlint-cli2` で検証） |

---

## 2. 変更の基本方針

- **依頼された範囲に集中する。** 無関係なリファクタ・ファイルの掃除・ドキュメントの拡大は行わない。
- **周辺コードのスタイルに合わせる。** 命名、型の置き方、コメント量、import／using の並びを既存に揃える。
- **契約を壊さない。** API のステータスコード・エラー JSON 形・ヘッダ（`X-Tenant-Id`, `X-Idempotency-Key`）は `docs/` の契約に従う。
- **機微データの露出に注意する。** 一覧／GET で Start 時の `input` や state `output` を広げる変更は、機微 IO 方針（`AGENTS.md`）と整合させる。

---

## 3. コンポーネント別

### 3.1 Service API（`service/api/`）と Application（`core/application/`）

- **Controller**: ルーティング・バインディング・ヘッダ・HTTP ステータスのみ。ビジネスロジックは Application へ。
- **ユースケース（`core/application/`）**: `IExecutionService` は薄い Facade でドメインサービスへ委譲する。コミット境界は `ICoreTransactionExecutor` / `IExecutionMutationPersistence`。Facade は Repository / Engine を直接持たない。`IDbContextFactory` をユースケースから直接使わない。境界の正本は [`architecture/domain-model-boundaries.md`](architecture/domain-model-boundaries.md)。
- **Repository**: 永続化のみ。書き込み API の第一引数は常に `ICoreUnitOfWork`。`SaveChanges` / `BeginTransaction` / `IDbContextFactory` を Repository 内に持たない。
- **UoW**: `IDbContextFactory<CoreDbContext>` は `CoreUnitOfWork` 実装内に閉じる。
- **例外**: `ApiExceptionFilter` と契約に沿ったエラー JSON（404 / 422 / 500）。
- **OpenAPI / Scalar**: Development では `http://localhost:8080/scalar/v1`（起動 URL に依存）。JSON は `/swagger/v1/swagger.json`。コミット用 export はリポジトリルートから `.\scripts\export-core-api-openapi.ps1` → `service/api/openapi/core-api-v1.openapi.json`。本番は既定オフ、`STATEVIA_ENABLE_API_DOCS=true` で有効化可。手書き契約は `docs/specifications/api-http.md`（運用叙述は Markdown に残す）。

### 3.2 Engine（`core/engine/`）

- ワークフロー実行・FSM・グラフの責務に留める。HTTP や DB に直接触れない。
- 公開 API の変更は、Service API や契約ドキュメントへの影響を確認する。

### 3.3 UI（`ui/studio/`）

- Service API へは Next.js の route handler 経由でプロキシ（CORS 回避）。環境変数は `AGENTS.md` の表を参照。
- 内部構成は feature-first（薄い `app/` + `features/` + `shared/`）。正本: [`architecture/ui-studio-structure.md`](architecture/ui-studio-structure.md)。
  - `app/**/page.tsx` は薄い wrapper。表示・ロジックは `features/<name>/`。
  - 依存は `app` → `features` → `shared`。`shared` → `features` は禁止（i18n 辞書合成のみ例外）。
  - feature 間の内部 import は禁止。長期の互換 re-export は残さない。
  - Tailwind `content` は `app` / `features` / `shared` を含める（`tailwind.config.ts`）。
- 静的解析: `npm run lint`（ESLint 9 strict）。型チェック: `npm run typecheck`（`tsc --noEmit`）。テスト: `npm run test:run`（Vitest）。Sonar は **§5.2**。

---

## 4. コーディングと静的チェック

- **C#** → **§4.1**。**TypeScript / React** → **§4.2**。**コメント・ドキュメント** → **§4.3**。**リンター・ビルド** → **§4.4**。

### 4.1 .NET（C#）

- **命名**: 意図が伝わる名前。省略しすぎない。
- **コンストラクタ**: プライマリコンストラクタを優先する。
- **分岐**: パターンマッチング・`switch` を優先し、`as` の多用を避ける。
- **反復**: LINQ を主構文とし、`foreach` / `for` は可読性・性能上の理由が明確な場合に限る。
- **XML ドキュメント**: `public` / `internal` の型・メンバーに `/// <summary>` 等を付ける。`private` は意図が自明でない箇所のみ。
- **テスト**: メソッドごとに日本語の `/// <summary>` と **`// Arrange` / `// Act` / `// Assert`** 区切り。1 テスト 1 シナリオ（AAA）。
- **セキュリティ**: 外部入力の検証、認可・テナント境界、機密情報の非出力（ログ・エラー応答含む）。
- **構造化ログ**: `ILogger.Log*`（`LogWarning` / `LogError` 等）を直接呼ばない。`[LoggerMessage]` のソース生成 partial（例: `*LogMessages.cs`）経由で出す。キー命名は [`reference/logging-property-keys.md`](reference/logging-property-keys.md)。
- **Options 検証（Service API）**: `AddOptions<T>().Bind()` した設定は、次の分類で扱う。キー別の許容範囲・不正時挙動は [`reference/environment-variables.md`](reference/environment-variables.md) を参照する。
  - **A（起動失敗）**: 範囲外・矛盾など既定では安全に動けない値 → `.Validate()` + `ValidateOnStart()`。メッセージはキー名と制約のみ（値は出さない）。
  - **B（警告＋既定値）**: 未設定・空で既定値により安全に動ける → Warning ログのうえ既定値で続行。サイレント補正は禁止。
  - **C（機能無効）**: 機能有効化はあるが必須キー欠落 → 起動は継続し、実行時に明示エラーコードで fail-safe。
  - 新規 Options 追加時は上記いずれかを明示し、分類 A なら起動時検証を必須とする。

### 4.2 TypeScript / React（UI）

- **命名**: 意図が伝わる名前。`export` には JSDoc を付ける。
- **型**: ナローイングで表現し、`as` の多用を避ける。
- **分岐**: `switch` / `Record` を優先する。
- **データ変換**: `map` / `filter` / `reduce` 等の宣言的配列操作を主構文とする。
- **i18n**: UI 文言は辞書経由とし、`*.tsx` へハードコードしない。
- **テスト**: Vitest で AAA。ケース名は日本語で検証シナリオが分かるようにする。
- **品質チェック**: **§4.4** の UI 行に従う。

### 4.3 コメント・ドキュメント（言語横断）

**実装と同時にドキュメントを充実させる**。Markdown 固有の書き方は [`DOCUMENTATION-STANDARD.md`](DOCUMENTATION-STANDARD.md)。

- **公開 API**（C# の `public` / `internal`、TS の `export`）には、目的・引数・戻り値・例外（またはエラー条件）を記載する。
- **クラス / モジュール単位**では、利用コンテキスト・セキュリティ・制限値・上書きなど、コードだけでは分かりにくい契約を `<remarks>` / JSDoc で補足する。
- **定数（上限値など）**には、値の意味と採用理由を書く（推測や誤った技術的根拠は避ける）。
- **テスト**には日本語で「何を検証するか」を `/// <summary>` またはケース名で残す。
- **書かない**: 自明な処理の言い換えコメント、実装と矛盾する説明、機密情報。
- **仕様・運用に影響する変更**では、関連する `docs/` を依頼範囲内で整合させる。

### 4.4 リンター・ビルド警告・静的チェック

- **C#**: `dotnet build` / `dotnet test` で出る **コンパイラエラー・Analyzer 警告**は、自分の変更に起因するものは **解消してから** PR に出す。触れていないファイルの既存警告をまとめて直すのは必須ではないが、**新規コードで警告を増やさない**こと。
- **.NET ソリューションの警告 0**: 次の 6 sln は通常の `dotnet build` で **Warning 0** を維持する。新規のソリューション全体 `NoWarn` や `TreatWarningsAsErrors` は導入しない。意図的な抑制（テストの CA1707 / CA1515、API/ActionHost の CA2007、CLI の CA1303 等）は既存の `.editorconfig` を維持する。
  - `core/engine/statevia-engine.sln`
  - `infrastructure/statevia-infrastructure.sln`
  - `modules/reference/statevia-reference.sln`
  - `service/cli/statevia-cli.sln`
  - `service/action-host/statevia-action-host.sln`
  - `service/api/statevia-api.sln`（`service/api/Directory.Build.props` の `AnalysisMode=AllEnabledByDefault` を含む）
- **IDE 用包括ソリューション**: リポジトリ直下の `statevia.sln` は Cursor / VS Code の IntelliSense 用。CI・Warning 0・Sonar の正本ではない。`.vscode/settings.json` の `dotnet.defaultSolution` はこのファイルを指す。品質ゲートと `dotnet test` は上記 6 sln を使う。
- **Service API テスト向け抑制**: ルート `.editorconfig` に加え、`service/api/Statevia.Service.Api.Tests/.editorconfig` で xUnit 向け CA（例: CA1812）を抑制している。テストの命名・`ConfigureAwait` 方針は Engine.Tests と同趣旨。
- **Markdown**: リポジトリ直下の **`.markdownlint.json`** に従う。編集した Markdown には例として次で確認できる。

  ```bash
  npx markdownlint-cli2 "path/to/edited.md"
  ```

- **UI（TypeScript）**: **`npm run lint`**（error 厳格）、**`npm run typecheck`**、**`npm run test:run`** を PR 前の必須チェックとする。設定は `ui/studio/eslint.config.js`（`typescript-eslint` strict、`react-hooks`、`jsx-a11y`、`jsdoc`）。
- **SonarQube（Service API）**: プロジェクトキー **`StateviaServiceApi`**。新規コードの Quality Gate（`new_coverage ≥ 80%`、`new_violations = 0` 等）を満たすこと。手順は **§5.1**。テストプロジェクトはスキャン対象外（`excludeTestProjects` / `SonarQubeExclude`）。SonarQube for IDE も同じ範囲に揃える（[sonar/README.md](../sonar/README.md)）。
- **SonarQube（UI Studio）**: プロジェクトキー **`StateviaUIStudio`**。全体・新規コードの Quality Gate（`coverage` / `new_coverage ≥ 80%`、`new_violations = 0` 等）を満たすこと。手順は **§5.2**。C# と同様、`tests/` はスキャン対象外（`sonar.exclusions`）。カバレッジは lcov のプロダクションパスだけを見る。

---

## 5. テストと品質ゲート

| 領域 | コマンド（例） |
|------|----------------|
| Engine | `cd core/engine && dotnet test statevia-engine.sln` |
| Service API | `cd service/api && dotnet test statevia-api.sln` |
| UI | `cd ui/studio && npm run lint && npm run typecheck && npm run test:run` |
| UI（Sonar 前） | `cd ui/studio && npm run test:coverage` |
| Sonar（一括） | `./sonar/sonar-scanner-all.ps1`（`-Projects engine,api` で複数指定可。[sonar/README.md](../sonar/README.md)） |

変更した領域に対応するテストを追加または更新し、ローカルで green を確認してから共有する。UI を Sonar に送る前はカバレッジ付きテストを実行する（**§5.2**）。C# の Scanner for .NET スクリプトはすべて `sonar.scanner.scanAll=false` とし、`ui/studio` は **`StateviaUIStudio`** だけが解析する。

### 5.1 Service API — カバレッジと Sonar（手動）

**前提**

- ローカル SonarQube が起動していること（既定 URL: `http://localhost:9000`）
- 環境変数 **`SONAR_TOKEN`** を設定していること
- グローバルツール: `dotnet-sonarscanner`、`dotnet-coverage`（スクリプトが利用する）

**カバレッジ runsettings（単体テストのみ確認するとき）**

`service/api/coverage.runsettings` では Engine アセンブリと `Program.cs` / `Migrations/` を cobertura から除外する。Api 本体の行が計測対象に含まれることを確認する。

```powershell
cd service/api
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

1. `dotnet sonarscanner begin`（キー `StateviaServiceApi`、除外は Engine / UI / Program / Migrations / `modules/reference` 等。`sonar.scanner.scanAll=false` で `ui/studio` を JS/TS 解析に混ぜない）
2. `dotnet build service/api/statevia-api.sln`
3. `dotnet-coverage collect "dotnet test"` → `sonar/service-api-coverage.xml`
4. `dotnet sonarscanner end`

**注意**

- `service/api/sonar-project.properties` は **Scanner for .NET では使わない**（設定は `begin` の `/d:` で渡す）。
- `sonar.exclusions` だけでは `scanAll` が `ui/studio/tsconfig.json` を拾う。UI は **`StateviaUIStudio`**（§5.2）で解析する。
- スキャン直後は Sonar UI の数値が遅れて反映されることがある。Quality Gate 判定は Sonar のプロジェクト画面を正とする。

### 5.2 UI Studio — カバレッジと Sonar（手動）

**前提**

- ローカル SonarQube が起動していること（既定 URL: `http://localhost:9000`。`sonar/docker-compose.yaml` 参照）
- 環境変数 **`SONAR_TOKEN`** を設定していること
- Node.js / npm が PATH にあり、`ui/studio` で `npm install` 済みであること
- グローバルまたは `npx` で **`sonar-scanner`** が実行できること

**カバレッジ（Vitest のみ確認するとき）**

```powershell
cd ui/studio
npm run test:coverage
```

`coverage/lcov.info` が生成される。除外方針は `ui/studio/sonar-project.properties` の `sonar.coverage.exclusions` と `vitest.config.ts` の `coverage.exclude` を揃える（本書 §5.2）。

**Sonar スキャン（正本スクリプト）**

リポジトリルートから実行する（カレントディレクトリに依存しない）。

```powershell
# トークン設定後
./sonar/sonar-scanner-ui.ps1
```

スクリプトは次を順に実行する。

1. `npm run test:coverage`（`ui/studio/coverage/lcov.info` を生成）
2. `npx sonar-scanner`（キー **`StateviaUIStudio`**、`ui/studio/sonar-project.properties` を読み込み）

**手動（`ui/studio` をカレントに）**

```powershell
cd ui/studio
npm run test:coverage
npx --yes sonar-scanner "-Dsonar.token=$($env:SONAR_TOKEN)"
```

**注意**

- HTTP 契約・プロキシ挙動は本リファクタの対象外（`docs/specifications/api-http.md` 等は変更しない）。
- スキャン直後は Sonar UI の数値が遅れて反映されることがある。Quality Gate 判定は Sonar のプロジェクト画面を正とする。
- `tests/` は `sonar.exclusions` でスキャン対象外にする（C# の `excludeTestProjects` と同趣旨）。カバレッジは `coverage/lcov.info` のプロダクションパスだけを使う。
- 詳細は `sonar/README.md` も参照。

### 5.3 リファレンス Module — Sonar（手動）

first-party Module（`modules/reference/`）は **`StateviaModulesReference`** で解析する。API スキャンには含めない。

```powershell
dotnet build-server shutdown
./sonar/sonar-scanner-reference.ps1
```

スクリプトは `modules/reference/statevia-reference.sln` を build / test し、プロダクションコードだけを当該キーへ送る。`sonar.scanner.scanAll=false` で `ui/studio` など MSBuild 対象外のファイルが CPD に混ざらないようにする。ActionPublication の JSON スキーマ定型は独立 Module 間の意図的な重複のため、`sonar.cpd.exclusions` で `*Publication.cs` を CPD 対象外にする。

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
- **チケット・Issue 番号**を付ける場合は末尾に付与してよい: `feature/io-api-input-idempotency`（任意）。
- **長期エピック**で接頭辞を揃える場合は `feature/<エピック>-<要約>` とする（例: `feature/io-c-api-workflow-start-input`）。

**保護ブランチ**（`main` など）へ直接 push せず、PR 経由でマージする。

---

## 7. コミットと説明文

- **コミットメッセージは日本語**で、何を・なぜかが分かるように書く（英語や他言語は使わない）。
- 1 行で簡潔に。例: バグ修正「〇〇のバグを修正」、機能追加「〇〇機能を追加」、リファクタ「〇〇をリファクタ」。
- PR 説明も日本語で、変更範囲と影響（API 契約・マイグレーション有無）を簡潔に書く。

---

## 8. 変更履歴

| 日付 | 内容 |
|------|------|
| 2026-09-01 | §4.3 / §5.2 に UI `tests/` のスキャン除外（`sonar.exclusions`。C# テストプロジェクト除外と同趣旨）を追記 |
| 2026-08-31 | §5 / §5.1 に C# スキャナの `scanAll=false`（`ui/studio` 混入防止）を追記 |
| 2026-08-30 | §5 に全コンポーネント一括 Sonar スクリプト（`sonar-scanner-all.ps1`）を追記 |
| 2026-08-24 | §5.3 に scanAll 無効化（UI 混入防止）と Publication JSON の CPD 除外を追記 |
| 2026-08-23 | §5.3 にリファレンス Module（`StateviaModulesReference`）の Sonar 手順を追記 |
| 2026-08-13 | §3.1 を Execution Facade / ドメインサービス分割後の責務に合わせて更新 |
| 2026-07-07 | §1 / §4 / §7 を Git 上の正本として自己完結化（`.cursor` パス参照を廃止） |
| 2026-05-19 | §4.3 / §5 に UI の ESLint・`StateviaUIStudio` Sonar 手順（§5.2・`sonar-scanner-ui.ps1`）を追記 |
| 2026-05-17 | §4.3 / §5.1 に Service API 厳格ビルド・coverlet runsettings・`sonar-scanner-api.ps1` 手順を追記 |
| 2026-04-05 | §4 再編（4.1 C#・4.2 TypeScript・4.3 静的チェック）、正本表に TypeScript 細則を追記 |
| 2026-03-28 | 初版・ブランチ命名を追加 |
