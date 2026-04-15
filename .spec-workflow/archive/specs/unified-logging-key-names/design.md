# Design: ログキー名の統一（STV-407 / LOG-5）

## Overview

### 命名方針（草案 — tasks で最終確定）

| 概念 | 推奨キー | 備考 |
|------|----------|------|
| W3C / 相関 | `TraceId` | HTTP / Engine 共通 |
| テナント | `TenantId` | API のみ |
| ワークフロー | `WorkflowId` | Engine。API ではルート解決後に enrich する場合あり |
| 状態 | `StateName` | Engine |
| HTTP | `Method`, `Path`, `Query`, `StatusCode`, `ElapsedMs` | API |

**ケース**: 構造化ログの **テンプレート引数名**は **PascalCase**（既存 `RequestLoggingMiddleware` と整合）を推奨。Serilog 等への射影後のキーはインフラ側で小文字化されてもよい。

## Implementation Approach

1. **棚卸し**: `RequestLoggingMiddleware`、`WorkflowEngine`（および `TraceContextEnrichmentMiddleware`）の全 `Log*` 呼び出しを列挙。
2. **表の確定**: `docs/core-api-observability.md` 新規、または `AGENTS.md` へ節追加。
3. **リネーム**: 定数化して置換（可能なら `partial class LogKeys` を API / Engine それぞれに、同一文字列）。

## References

- `api/Statevia.Core.Api/Hosting/RequestLoggingMiddleware.cs`
