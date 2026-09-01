# 機微 IO のログマスキング

| 項目 | 値 |
| --- | --- |
| 種別 | Specification |
| Version | 1.1 |
| 更新日 | 2026-09-01 |
| 関連 | [data-integration.md](../data-integration.md), [api-http.md](../api-http.md) |

---

## Normative 要約

- **MUST**: ログ出力前に `LogRedaction.Redact()` を通す。
- **MUST**: `input` / `output` キーの値はプレースホルダへ置換する。
- **MUST**: 最低限 `password`, `token`, `secret`, `authorization` をマスク対象とする。
- **SHOULD**: Start 時 `input` / state `output` を一覧・GET で既定返却しない。

---

`input`（実行開始入力）および状態 `output` は機微情報を含み得るため、ログ出力時はマスキングを前提とする。一覧・GET で返さない方針とログマスキングをあわせて **機微 IO 方針** とする。本書がその正本である。

**Version 1.1（2026-09-01）**: 機微 IO 方針の正本とする。内部作業番号の参照を外す。

## 実装方針

- API / Engine は共通 `LogRedaction` を使用する。
- 最低限のマスク対象キーは `password`, `token`, `secret`, `authorization`。
- `input`, `output` キーは値をプレースホルダへ置換する。

## 運用上の注意

- ログ出力の前に必ず `LogRedaction.Redact()` を通す。
- 新しい機微キーの外部テンプレート化は未実装。

## 関連

- [data-integration.md](../data-integration.md)
- [api-http.md](../api-http.md)
