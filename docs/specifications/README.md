# Specifications

| 項目 | 値 |
| --- | --- |
| 種別 | Specification |
| Version | 1.0 |
| 更新日 | 2026-07-07 |

---

**契約・振る舞い** の Normative 正本（必須 / 推奨 / 任意 / 禁止）。背景の「なぜ」は [`../concepts/`](../concepts/) へ。

## 中核

| ドキュメント | 領域 |
| --- | --- |
| [`definition.md`](definition.md) | ワークフロー定義 YAML |
| [`api-http.md`](api-http.md) | Core-API HTTP 契約 |
| [`data-integration.md`](data-integration.md) | Engine / API / UI データ連携 |

## execution/

| ドキュメント | 領域 |
| --- | --- |
| [`execution/fsm.md`](execution/fsm.md) | FSM・状態機械・reducer |
| [`execution/events-and-commands.md`](execution/events-and-commands.md) | イベント・コマンド |
| [`execution/wait-cancel.md`](execution/wait-cancel.md) | Wait / Cancel |
| [`execution/fork-join.md`](execution/fork-join.md) | Fork / Join |
| [`execution/execution-graph.md`](execution/execution-graph.md) | ExecutionGraph JSON |

## actions/

| ドキュメント | 領域 |
| --- | --- |
| [`actions/platform.md`](actions/platform.md) | Catalog / Policy / ModuleHost |
| [`actions/module-zip-layout.md`](actions/module-zip-layout.md) | Module zip レイアウト |

## platform/

| ドキュメント | 領域 |
| --- | --- |
| [`platform/security-runtime.md`](platform/security-runtime.md) | Runtime Security Boundary |
| [`platform/execution-security-snapshot.md`](platform/execution-security-snapshot.md) | Execution Security Snapshot |
| [`platform/io-log-masking.md`](platform/io-log-masking.md) | workflow IO ログマスキング |
| [`platform/audit-and-repro.md`](platform/audit-and-repro.md) | 監査・再現性 |

## ui/

| ドキュメント | 領域 |
| --- | --- |
| [`ui/visual.md`](ui/visual.md) | UI 可視化 |
| [`ui/push-api.md`](ui/push-api.md) | Push API（SSE） |

Concept との対応は [`../concepts/README.md`](../concepts/README.md)。
