# workflow IO ログマスキング運用（STV-408）

## 目的

`workflowInput` および状態 `output` は機微情報を含み得るため、ログ出力時はマスキングを前提とする。
本方針は `AGENTS.md` の IO-14 に整合する。

## 実装方針

- API / Engine は共通 `LogRedaction` を使用する。
- 最低限のマスク対象キーは `password`, `token`, `secret`, `authorization`。
- `workflowInput`, `input`, `output` キーは値をプレースホルダへ置換する。

## 運用上の注意

- ログ出力の前に必ず `LogRedaction.Redact()` を通す。
- 新しい機微キーが必要な場合、まず `STV-412`（ユーザー定義マスキングルール）で拡張方針を検討する。

## 関連

- `AGENTS.md`（IO-14）
- `docs/statevia-data-integration-contract.md`
- `.spec-workflow/specs/workflow-io-log-masking/requirements.md`
