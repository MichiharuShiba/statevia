# Design: workflow IO ログマスキング（STV-408 / LOG-6）

## Overview

`LogBodyRedactor` 相当のロジックは、**中立配置の小さな共通 `LogRedaction` 静的クラス**へ集約する。配置は `engine/Statevia.Core.Engine/Infrastructure/Logging/` 配下を第一候補とし、`api` / `engine` の双方が同一実装を呼び出す。`STV-408` では固定ルール（`password` / `token` / `secret` / `authorization`）を実装し、ユーザー定義ルールは `STV-412` で拡張する。

### Engine 側

- `WorkflowEngine` が **state 出力をログに出す場合のみ** Redactor を通す（そもそも IO をログしない設計なら、Requirement は「ログする経路が存在しないことの証明」で満たす — **tasks で現状確認**）。

### API 側

- `RequestLoggingMiddleware` の既存パスを拡張し、**workflow 関連エンドポイント**の本文に追加のキーワードを適用するか検討。

## Data Flow

```text
Raw string → LogRedaction.Redact(string) → Logger
```

## Testing

- `LogRedaction` のユニットテストに **ネスト JSON** と **workflowInput 風ペイロード**を追加。

## References

- `.spec-workflow/specs/api-request-basic-logging/requirements.md` — マスキング方針
