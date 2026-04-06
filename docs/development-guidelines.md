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
| TypeScript / React 細則（命名・JSDoc・型絞り込み・テスト体裁） | `.cursor/rules/typescript-standards.mdc`（Cursor 利用時） |
| Markdown（`.spec-workflow` 等） | ルート `.markdownlint.json`（`markdownlint-cli2` で検証可能） |

---

## 2. 変更の基本方針

- **依頼された範囲に集中する。** 無関係なリファクタ・ファイルの掃除・ドキュメントの拡大は行わない。
- **周辺コードのスタイルに合わせる。** 命名、型の置き方、コメント量、import／using の並びを既存に揃える。
- **契約を壊さない。** API のステータスコード・エラー JSON 形・ヘッダ（`X-Tenant-Id`, `X-Idempotency-Key`）は `docs/` の契約に従う。
- **機微データの露出に注意する。** 一覧／GET で `workflowInput` や state `output` を広げる変更は、IO-14（`AGENTS.md`）と整合させる。

---

## 3. コンポーネント別

### 3.1 Core-API（`api/`）

- **Controller**: ルーティング・バインディング・ヘッダ・HTTP ステータスのみ。ビジネスロジックは Service へ。
- **Service**: ユースケース。Repository・DisplayId・command dedup・`IWorkflowEngine` を組み合わせる。
- **Repository**: 永続化のみ。`IDbContextFactory<CoreDbContext>` は Repository 実装内に閉じる。
- **例外**: `ApiExceptionFilter` と契約に沿ったエラー JSON（404 / 422 / 500）。

### 3.2 Engine（`engine/`）

- ワークフロー実行・FSM・グラフの責務に留める。HTTP や DB に直接触れない。
- 公開 API の変更は、Core-API や契約ドキュメントへの影響を確認する。

### 3.3 UI（`services/ui/`）

- Core-API へは Next.js の route handler 経由でプロキシ（CORS 回避）。環境変数は `AGENTS.md` の表を参照。
- 型チェック: `tsc --noEmit`（プロジェクト方針）。テスト: `npm run test:run`（Vitest）。

---

## 4. コーディングと静的チェック

- **C#** の細則は **`.cursor/rules/csharp-standards.mdc`**（§4.1）。**TypeScript / React** は **`.cursor/rules/typescript-standards.mdc`**（§4.2）。**リンター・ビルド**は §4.3。

### 4.1 C#（コメント・XML・テストの要約）

- **モダン C# のパターンマッチ**（`is` パターン、switch 式）を優先する。粒度の詳細は **`csharp-standards.mdc`**。
- **public / internal** の型・メンバーには **XML ドキュメント**（`/// <summary>` 等）を付ける。
- **private** は、意図がコードだけでは分かりにくい箇所（ヘルパー・複雑な分岐・仕様参照）に `/// <summary>` や行コメントを付ける。
- **単体テスト**では、メソッドごとに **日本語の `/// <summary>`**（何を検証するか）と、本体の **`// Arrange` / `// Act` / `// Assert`** 区切りを付ける。
- 上記の粒度・例は **`csharp-standards.mdc`** に従う。

### 4.2 TypeScript（UI）

- 詳細は **`.cursor/rules/typescript-standards.mdc`**（変数名・JSDoc・型の絞り込み・React の props・Vitest の AAA と日本語ケース名）。
- 品質チェックは **§4.3** の UI 行に従う。

### 4.3 リンター・ビルド警告・静的チェック

- **C#**: `dotnet build` / `dotnet test` で出る **コンパイラエラー・Analyzer 警告**は、自分の変更に起因するものは **解消してから** PR に出す。触れていないファイルの既存警告をまとめて直すのは必須ではないが、**新規コードで警告を増やさない**こと。
- **Markdown**: リポジトリ直下の **`.markdownlint.json`** に従う。`.spec-workflow/**/*.md` などを編集したときは例として次で確認できる。

  ```bash
  npx markdownlint-cli2 ".spec-workflow/**/*.md"
  ```

- **UI（TypeScript）**: **`tsc --noEmit`** と **`npm run test:run`** を品質の主なチェックとする（ESLint は未設定。既知の軽微な警告については `AGENTS.md` の Lint 節を参照）。
- **SonarQube 等の外部解析**を使う場合は、**変更したファイル**について指摘があれば、ルールと工数のバランスで可能な範囲で対応する。

---

## 5. テストと品質ゲート

| 領域 | コマンド（例） |
|------|----------------|
| Engine | `cd engine && dotnet test statevia-engine.sln` |
| Core-API | `cd api && dotnet test statevia-api.sln` |
| UI | `cd services/ui && npm run test:run` |

変更した領域に対応するテストを追加または更新し、ローカルで green を確認してから共有する。

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
| 2026-04-05 | §4 再編（4.1 C#・4.2 TypeScript・4.3 静的チェック）、`typescript-standards.mdc` 追加、正本表に TypeScript 細則を追記 |
| 2026-03-28 | 初版・ブランチ命名を追加 |
