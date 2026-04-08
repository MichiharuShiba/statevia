# ログキー命名規約（STV-407）

## 目的

Core-API と Engine の構造化ログで、同じ概念に同じキー名を使う。
テンプレート引数名は PascalCase に統一する。

## 標準キー一覧

| 概念 | 標準キー | 主な出力箇所 | 備考 |
|------|----------|--------------|------|
| 相関 ID | `TraceId` | API | `traceparent` / `X-Trace-Id` から解決 |
| テナント | `TenantId` | API | `X-Tenant-Id` |
| ワークフロー ID | `WorkflowId` | Engine / API enrich | API は route `{id}` 由来（display ID の場合あり） |
| 状態名 | `StateName` | Engine | State 実行ログ・Warning |
| 定義 ID | `DefinitionId` | API enrich | route `{id}` 由来（display ID の場合あり） |
| グラフ定義 ID | `GraphDefinitionId` | API enrich | route `{graphId}` |
| HTTP メソッド | `Method` | API | - |
| パス | `Path` | API | クエリなし |
| クエリ | `Query` | API | マスク後 |
| ステータス | `StatusCode` | API | - |
| 経過時間 | `ElapsedMs` | API / Engine | ミリ秒 |
| 例外型 | `ExceptionType` / `ErrorType` | API / Engine | 既存実装を維持 |

## 補足

- ログ基盤側でキーを小文字化する運用は許容するが、アプリ実装のテンプレート名は
  PascalCase を維持する。
