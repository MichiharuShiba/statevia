# Contracts

言語間の齟齬防止のための JSON Schema 定義（refactoring-tasks-v2 タスク 0.2）。

| ファイル | 説明 |
|----------|------|
| `execution-readmodel.schema.json` | UI 向け Execution Read Model（data-integration-contract §2.1） |
| `decide-request.schema.json` | Core-API → Core Engine の Decide リクエスト（architecture.v2 §4.1） |
| `decide-response.schema.json` | Core Engine → Core-API の Decide レスポンス（Accepted / Rejected） |

JSON Schema は draft 2020-12。バリデーションには [ajv](https://github.com/ajv-validator/ajv) 等を利用可能。
