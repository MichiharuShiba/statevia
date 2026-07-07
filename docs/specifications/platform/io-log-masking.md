# workflow IO ログマスキング運用（STV-408）

| 項目 | 値 |
| --- | --- |
| 種別 | Specification |
| Version | 1.0 |
| 更新日 | 2026-07-07 |
| 関連 | [data-integration.md](../data-integration.md), [api-http.md](../api-http.md) |

---

## Normative 要約

- **MUST**: ログ出力前に `LogRedaction.Redact()` を通す。
- **MUST**: `input` / `output` キーの値はプレースホルダへ置換する。
- **MUST**: 最低限 `password`, `token`, `secret`, `authorization` をマスク対象とする。
- **SHOULD**: Start 時 `input` / state `output` を一覧・GET で既定返却しない（IO-14）。

---

`input`（実行開始入力）および状態 `output` は機微情報を含み得るため、ログ出力時はマスキングを前提とする。
本方針は `AGENTS.md` の IO-14 に整合する。

## 実装方針

- API / Engine は共通 `LogRedaction` を使用する。
- 最低限のマスク対象キーは `password`, `token`, `secret`, `authorization`。
- `input`, `output` キーは値をプレースホルダへ置換する。

## 運用上の注意

- ログ出力の前に必ず `LogRedaction.Redact()` を通す。
- 新しい機微キーが必要な場合、まず `STV-412`（ユーザー定義マスキングと**外部テンプレート化**：ルールを外部ファイルから読み込む方式）で拡張方針を検討する。

## 関連

- `AGENTS.md`（IO-14）
- `docs/specifications/platform/io-log-masking.md`
- `docs/specifications/data-integration.md`
