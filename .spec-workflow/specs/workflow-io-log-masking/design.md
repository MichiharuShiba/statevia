# Design: workflow IO ログマスキング（STV-408 / LOG-6）

## Overview

`LogBodyRedactor` を **単一の真実源**とし、必要なら **`Statevia.Core` 共有プロジェクト**または **Engine から参照可能な小さな `LogRedaction` 静的クラス**を `api` からリンク共有する（プロジェクト分割の都合で **ソースリンク**や **複製 + テスト同期**は tasks で決定）。

### Engine 側

- `WorkflowEngine` が **state 出力をログに出す場合のみ** Redactor を通す（そもそも IO をログしない設計なら、Requirement は「ログする経路が存在しないことの証明」で満たす — **tasks で現状確認**）。

### API 側

- `RequestLoggingMiddleware` の既存パスを拡張し、**workflow 関連エンドポイント**の本文に追加のキーワードを適用するか検討。

## Data Flow

```text
Raw string → LogBodyRedactor.Redact(string) → Logger
```

## Testing

- `LogBodyRedactor` のユニットテストに **ネスト JSON** と **workflowInput 風ペイロード**を追加。

## References

- `.spec-workflow/specs/api-request-basic-logging/requirements.md` — マスキング方針
