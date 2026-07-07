# Concepts

| 項目 | 値 |
| --- | --- |
| 種別 | Concept |
| Version | 1.0 |
| 更新日 | 2026-07-07 |

---

思想と全体像を、**ストーリーとして**理解するためのドキュメント。用語辞典（Wiki）ではない。Normative 要件は [specifications/](../specifications/README.md) を正とする。

## 読む順

1. [`what-is-statevia.md`](what-is-statevia.md) — プラットフォームの目的と設計原則
2. [`definition.md`](definition.md) — 定義駆動の考え方
3. [`execution-model.md`](execution-model.md) — 実行が進む流れ
4. [`actions.md`](actions.md) — Action / Module による拡張
5. [`durability.md`](durability.md) — 永続化とイベント
6. [`platform.md`](platform.md) — API・セキュリティ・境界

入口は [`../README.md`](../README.md) の Concepts セクション。手順は先に [guides/](../guides/README.md) を読むことを推奨します。

## Specification との対応

| Concept | 主な Specification |
| --- | --- |
| definition | [definition.md](../specifications/definition.md) |
| execution-model | [execution/](../specifications/execution/) |
| actions | [actions/](../specifications/actions/) |
| durability | [data-integration.md](../specifications/data-integration.md) |
| platform | [platform/](../specifications/platform/), [api-http.md](../specifications/api-http.md) |
