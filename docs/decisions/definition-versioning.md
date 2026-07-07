# Immutable 定義版（Definition Versioning）

| 項目 | 値 |
| --- | --- |
| 種別 | Decision |
| 更新日 | 2026-07-07 |
| ステータス | 採用 |

## Context

ワークフロー定義は実行中に書き換わると、再現性・監査・ロールバックの説明が困難になる。クライアントが「最新定義」で常に実行すると、開始直後に定義が変わり挙動が揺れる。

## Decision

- 定義は **`definitions` + `definition_versions`** で **append-only** の版管理とする
- 各 **Execution** は開始時に **`definition_version_id` を固定**する
- publish は `PUT /v1/definitions/{id}` で新版を追加し、既存版は上書きしない

## Consequences

- 実行の再現は「どの版で動いたか」に集約できる
- 版レンジ解決（`@LATEST` 等）は Compiler 責務として段階導入し、Runtime は exact lookup のみとする
- ストレージと API の複雑さは増えるが、契約の明確さが優先される

関連: [data-integration.md](../specifications/data-integration.md)、[definition.md](../specifications/definition.md)
