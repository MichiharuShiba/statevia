# ログキー命名規約（STV-407）

## 目的

Service API と Engine の構造化ログで、同じ概念に同じキー名を使う。

## 出力手段（必須）

- **`ILogger.LogInformation` / `LogWarning` / `LogError` 等を直接呼ばない。**
- `[LoggerMessage]`（ソース生成）の partial メソッド経由で出す（例: `ExecutionServiceLogMessages`、`ForkExpansionLogMessages`）。
- 新規カテゴリは `*LogMessages.cs` にまとめ、EventId 帯が既存と衝突しないようにする。

## Message テンプレート（Service API）

- **ラベル**（`=` の左）: **PascalCase**（例: `TraceId`, `TenantId`）
- **プレースホルダと引数名**（`{…}` 内および partial メソッド引数）: **camelCase**（例: `{traceId}`, 引数 `traceId`）

例: `TraceId={traceId} TenantId={tenantId}`

## 標準キー一覧

| 概念 | ラベル（PascalCase） | 主な出力箇所 | 備考 |
|------|----------------------|--------------|------|
| 相関 ID | `TraceId` | API / Engine | `traceparent` / `X-Trace-Id` から解決 |
| テナント | `TenantId` | API | 解決済み `tenants.tenant_id`（UUID）。未解決パスでは null |
| ワークフロー ID | `ExecutionId` | API enrich / 実行系 / Engine | route `{id}` 由来（display ID の場合あり） |
| 親ワークフロー ID | `ParentExecutionId` | Fork 展開 | 物理子展開時の親 |
| Fork ノード ID | `ForkNodeId` | Fork 展開 | 親実行グラフ上の Fork 到達インスタンス |
| 状態名 | `StateName` | Engine | State 実行ログ・Warning |
| 定義 ID | `DefinitionId` | API enrich | route `{id}` 由来（display ID の場合あり） |
| グラフ定義 ID | `GraphDefinitionId` | API enrich | route `{graphId}` |
| HTTP メソッド | `Method` | API | - |
| パス | `Path` | API | クエリなし |
| クエリ | `Query` / `QueryForLog` | API | マスク後 |
| ステータス | `StatusCode` | API | - |
| 経過時間 | `ElapsedMs` | API / Engine | ミリ秒 |
| 例外型 | `ExceptionType` / `ErrorType` | API / Engine | - |
| 操作主体 Principal | `ActorPrincipalId` | 管理者パスワード更新 | 監査。平文・ハッシュは出さない |
| 操作対象ユーザー | `TargetUserId` | 管理者パスワード更新 | `users.user_id` |

## Engine

Engine（`ExecutionEngine.LogMessages`）はラベル・プレースホルダ・引数を **PascalCase**（`ExecutionId={ExecutionId}`）で維持する。

## 補足

- ログ基盤側でキーを小文字化する運用は許容する。
