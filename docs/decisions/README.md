# Decisions（ADR）

| 項目 | 値 |
| --- | --- |
| 種別 | Decision |
| Version | 1.0 |
| 更新日 | 2026-07-07 |

---

**なぜその設計にしたか** を残す Architecture Decision Record。仕様（Specification）でも Concept でもない。

## 運用

- 重要な設計判断が確定したときに **1 件ずつ追加**する（件数は事前に計画しない）
- ファイル名は `short-title.md`（kebab-case）。番号付けは任意

## ADR テンプレート

```markdown
# タイトル

| 項目 | 値 |
| --- | --- |
| 種別 | Decision |
| 更新日 | YYYY-MM-DD |
| ステータス | 採用 / 置換 / 廃止 |

## Context

（背景・課題）

## Decision

（採用した判断）

## Consequences

（トレードオフ・結果）
```

## 採用済み

| ドキュメント | 概要 |
| --- | --- |
| [`definition-versioning.md`](definition-versioning.md) | immutable 定義版と実行への版固定 |
| [`event-store.md`](event-store.md) | projection を read-model 正本とする |
| [`action-module-signing.md`](action-module-signing.md) | 署名・TrustLevel・Policy |

入口は [`../README.md`](../README.md) の Decisions セクション。
