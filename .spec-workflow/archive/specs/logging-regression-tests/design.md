# Design: ログ関連テスト（STV-409 / LOG-7）

## Overview

### テスト手法

- **Microsoft.Extensions.Logging.Testing** の `FakeLogger` / `FakeLoggerProvider`、または **`LoggerFactory` + カスタム `InMemoryLogger`**。
- HTTP ミドルウェア: 既存 `RequestLoggingMiddlewareTests` を拡張。
- Engine: `WorkflowEngine` に `ILogger` を注入し、**ログ条数・メッセージテンプレート・プロパティ**を検証。

### マトリクス（草案）

| 領域 | テストファイル（既存 or 新規） | 検証内容 |
|------|-------------------------------|----------|
| TraceId | `TraceIdResolver` / ミドルウェア | 優先順位 |
| Redactor | `LogBodyRedactorTests` | マスク |
| Engine | `WorkflowEngineTests` | 実行ログ |

## CI

- `dotnet test` を `api` / `engine` で実行。ログテストは **デフォルトで有効**。

## References

- 各先行 spec の `tasks.md` 完了チェック
