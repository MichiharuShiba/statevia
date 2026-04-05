# Tasks: API リクエスト基本ログ（STV-403）

実装順に実行する。各タスク完了後は spec-workflow の **`log-implementation`** で記録し、`tasks.md` のチェックを `[x]` に更新する。

---

- [x] 1. TraceId 解決と `HttpContext.Items` キー
  - **Files:** `api/Statevia.Core.Api/Hosting/RequestLogContext.cs`（Items キー定数）、`api/Statevia.Core.Api/Hosting/TraceIdResolver.cs`（静的 `ResolveTraceId(HttpRequest)` または同等）
  - **内容:** `design.md` の優先順位（`traceparent` → `X-Trace-Id` → `X-Request-Id` → `Guid("N")`）。`X-Trace-Id` / `X-Request-Id` は trim、長さ **128** 超は無効として次の手段へ。W3C `traceparent` パース失敗時も次へ。
  - **Purpose:** R4 とミドルウェア本体を分離し単体テストしやすくする。
  - _Leverage: `Microsoft.AspNetCore.Http.HttpRequest`, 既存の `Hosting/` 配置_
  - _Requirements: Requirement 4_
  - _Prompt: Implement the task for spec api-request-basic-logging, first run spec-workflow-guide to get the workflow guide then implement the task: Role: ASP.NET Core 開発者 | Task: `RequestLogContext` に `HttpContext.Items` 用の public 定数キーを定義し、`TraceIdResolver`（または同等の static クラス）で `design.md` の traceId 優先順位を実装する。`api/Statevia.Core.Api/Hosting/` に置く。 | Restrictions: 外部パッケージ追加なし。ミドルウェアはまだ書かない。 | Success: 各入力パターンで一意の traceId 文字列が決まる。単体テスト可能な public API。 | Instructions: `tasks.md` で当該タスクを `[-]` にしてから着手。完了後 `log-implementation` を呼び、`[x]` にする。_

- [x] 2. `RequestLogOptions` と `LogBodyRedactor`
  - **Files:** `api/Statevia.Core.Api/Hosting/RequestLogOptions.cs`, `api/Statevia.Core.Api/Hosting/LogBodyRedactor.cs`（または `Infrastructure/Logging/`）
  - **内容:** `IOptions<RequestLogOptions>` 用 POCO（`LogRequestBody`, `LogResponseBody`, `MaxRequestBodyLogBytes`, `MaxResponseBodyLogBytes`, `MaxQueryStringChars`）。`LogBodyRedactor`: 文字列（JSON テキスト・クエリ文字列）に対し design の代表キーをマスク。単体テストしやすい pure 関数寄りに。
  - **Purpose:** 要件の本文ログ・本番オフ・マスキング（IO-14）をコード境界で固定する。
  - _Leverage: `Microsoft.Extensions.Options`_
  - _Requirements: Requirement 1, 2, Security NFR_
  - _Prompt: Implement the task for spec api-request-basic-logging, first run spec-workflow-guide to get the workflow guide then implement the task: Role: .NET 開発者 | Task: `RequestLogOptions` と `LogBodyRedactor` を実装。design の既定値（本番は本文ログ false 推奨）をコメントで明記。 | Restrictions: 重い JSON パースライブラリを増やさない（必要最小の string/Span 処理でよい）。 | Success: Redactor のユニットテストでマスク対象キーが伏せられる。 | Instructions: 完了後 `log-implementation`。_

- [x] 3. レスポンスバッファリング `Stream` と `RequestLoggingMiddleware`
  - **Files:** `api/Statevia.Core.Api/Hosting/ResponseBodyLoggingStream.cs`（名前は任意）、`api/Statevia.Core.Api/Hosting/RequestLoggingMiddleware.cs`
  - **内容:** ミドルウェア: `InvokeAsync` で経過時間計測。開始ログ: `Path`（クエリなし）, `Query`（長さ `MaxQueryStringChars` で切り詰め）, `RequestBody`（`EnableBuffering` + 読み取り + Redactor、オプションオフ時は省略）, `TenantId`, `UserAgent`, `TraceId`。`Response.Body` をラップし `_next` 後に `ResponseBody` スナップショット（同様に Redactor・オプション）。`_next` 外の例外は Error ログ後に再スロー。ログ処理の try/catch で 500 にしない。
  - **Purpose:** R1–R3 の中核（レビュー反映: クエリ・リクエスト/レスポンス本文）。
  - _Leverage: タスク 1–2, `TenantHeader`, `ILogger`, `IHostEnvironment`_
  - _Requirements: Requirement 1, 2, 3, 4_
  - _Prompt: Implement the task for spec api-request-basic-logging, first run spec-workflow-guide to get the workflow guide then implement the task: Role: ASP.NET Core ミドルウェア担当 | Task: レスポンス用ラッピング `Stream` と `RequestLoggingMiddleware` を実装。design の Path/Query 分離・本文上限・非テキストプレースホルダに従う。 | Restrictions: 既存の `ApiExceptionFilter` と二重 Error を増やさない。 | Success: 正常系で Info×2。ストリームラップ後もレスポンスが壊れない。 | Instructions: `[-]` / `log-implementation` / `[x]`。_

- [x] 4. `Program.cs` で DI とパイプライン登録
  - **Files:** `api/Statevia.Core.Api/Program.cs`
  - **内容:** `Configure<RequestLogOptions>`（環境: Development なら本文ログ true 等）。`app.Build()` 後、`UseCors` **より前**に `UseMiddleware<RequestLoggingMiddleware>()`。
  - **Purpose:** 全 HTTP 呼び出しへ適用。
  - _Leverage: 既存 `Program.cs`_
  - _Requirements: Requirement 1, 2_
  - _Prompt: Implement the task for spec api-request-basic-logging, first run spec-workflow-guide to get the workflow guide then implement the task: Role: ASP.NET Core ホスティング | Task: `RequestLogOptions` の登録とミドルウェア追加。 | Restrictions: 他ミドルウェア順序を壊さない。 | Success: 起動後リクエストでミドルウェアが動作。 | Instructions: `log-implementation`。_

- [x] 5. 単体テスト（TraceId / Redactor / Stream / ミドルウェア）
  - **Files:** `api/Statevia.Core.Api.Tests/Hosting/*.cs`
  - **内容:** `TraceIdResolver`、`LogBodyRedactor`、可能なら `ResponseBodyLoggingStream` の転送、ミドルウェアの FakeLogger で開始・完了・主要フィールド（Path と Query が別）を検証。
  - **Purpose:** R5。
  - _Leverage: xUnit, `DefaultHttpContext`_
  - _Requirements: Requirement 5_
  - _Prompt: Implement the task for spec api-request-basic-logging, first run spec-workflow-guide to get the workflow guide then implement the task: Role: .NET テストエンジニア | Task: 新規コンポーネントの単体テストを追加。 | Restrictions: フレークなし。 | Success: `dotnet test` green。 | Instructions: `log-implementation`。_

- [x] 6. 運用ドキュメント追記
  - **Files:** `AGENTS.md` または `docs/core-api-observability.md`
  - **内容:** ログフィールド一覧（`Path`/`Query`/本文スナップショット）、**本番では本文ログ既定オフ**、IO-14 との関係（外部送信前のマスキング注意）。
  - **Purpose:** R5。
  - _Leverage: `AGENTS.md`_
  - _Requirements: Requirement 5_
  - _Prompt: Implement the task for spec api-request-basic-logging, first run spec-workflow-guide to get the workflow guide then implement the task: Role: 技術ライター | Task: 観測性の短い節を追記。 | Restrictions: 冗長にしない。 | Success: 運用者が本文ログのリスクを理解できる。 | Instructions: `tasks.md` 更新。_

- [x] 7. （任意）応答ヘッダ `X-Trace-Id`
  - **Files:** `RequestLoggingMiddleware.cs` 変更
  - **内容:** traceId 決定後、応答に `X-Trace-Id` を付与（design 既存の任意項）。
  - **Purpose:** クライアントデバッグ。
  - _Leverage: 実装済みミドルウェア_
  - _Requirements: Requirement 4（補助）_
  - _Prompt: Implement the task for spec api-request-basic-logging, first run spec-workflow-guide to get the workflow guide then implement the task: Role: ASP.NET Core 開発者 | Task: 任意。応答ヘッダ `X-Trace-Id`。 | Restrictions: ヘッダインジェクションなし。 | Success: curl で確認可能。 | Instructions: 不要ならスキップ。_

- [x] 8. （任意）ルート解決後の ID enrich と `tracestate`（レビュー `comment_1775318191580_u2gvs3fdn`）
  - **Files:** `UseRouting` より後のミドルウェア、または Filter + 必要なら `tracestate` マージヘルパ
  - **内容:** `WorkflowDisplayId` / `DefinitionDisplayId` を **ログスコープまたは構造化ログ**に付与。さらに任意で W3C **`tracestate`** にベンダーメンバーを追加（design の「W3C tracestate と定義 ID / ワークフロー ID」節）。
  - **Purpose:** 分散トレースとドメイン ID の結合。
  - _Leverage: `design.md` 該当節、`Activity`_
  - _Requirements: Requirement 4（拡張解釈）_
  - _Prompt: Implement the task for spec api-request-basic-logging, first run spec-workflow-guide to get the workflow guide then implement the task: Role: ASP.NET Core + 分散トレース | Task: ルート確定後にのみ ID を取得しログへ。tracestate は既存ヘッダとマージし長さを抑える。 | Restrictions: CORS 前の単一ミドルウェアだけでは完結させない。 | Success: 代表 API でログに ID が付く。tracestate 実装する場合はヘッダが壊れない。 | Instructions: 優先度低。別チケットに逃がしてもよい。_

---

## 完了チェック（STV-403 受け入れ）

実装マージ前に人手で確認すること（チェックリストではなくメモ）:

1. 主要エンドポイントで開始・完了ログが出る（ミドルウェア全域）
2. `path` と `query` が別フィールドで出る。本文は方針どおりマスク・上限付き
3. `traceId` で開始と完了が相関できる
4. 単体テストまたは手順で検証可能
5. `.workspace-docs` の `STV-403` を完了に更新し、`log-implementation` / コミット方針に従う
