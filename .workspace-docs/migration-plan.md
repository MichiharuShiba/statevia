# .workspace-docs 整理記録（移行完了）

更新日: 2026-03-28  
目的: `.exclude` から `.workspace-docs` への再配置結果を記録する

---

## 1. 作成済みフォルダ

```text
.workspace-docs/00_inbox
.workspace-docs/10_notes
.workspace-docs/30_specs/10_in-progress
.workspace-docs/30_specs/20_done
.workspace-docs/30_specs/30_archived
.workspace-docs/40_plans/10_in-progress
.workspace-docs/40_plans/20_done
.workspace-docs/40_plans/30_archived
.workspace-docs/50_tasks/10_in-progress
.workspace-docs/50_tasks/20_done
.workspace-docs/50_tasks/30_archived
```

---

## 2. ファイル移行マップ（実施済み）

## 2.1 ルート直下

| 現在 | 移行先 | 区分 |
| --- | --- | --- |
| `.workspace-docs/playground-A.md` | `.workspace-docs/10_notes/playground-A.md` | notes |
| `.workspace-docs/philosophy.md` | `.workspace-docs/10_notes/philosophy.md` | notes |

## 2.2 codex 系

| 現在 | 移行先 | 区分 |
| --- | --- | --- |
| `.workspace-docs/codex/statevia-codex-skills-spec.ja.md` | `.workspace-docs/30_specs/20_done/statevia-codex-skills-spec.ja.md` | spec |
| `.workspace-docs/codex/statevia-codex-usage-spec.ja.md` | `.workspace-docs/30_specs/20_done/statevia-codex-usage-spec.ja.md` | spec |
| `.workspace-docs/codex/ui-implementation-instructions.ja.md` | `.workspace-docs/40_plans/30_archived/ui-implementation-instructions.ja.md` | archived plan |
| `.workspace-docs/codex/ui-implementation-instructions.v1.1.ja.md` | `.workspace-docs/40_plans/30_archived/ui-implementation-instructions.v1.1.ja.md` | archived plan |
| `.workspace-docs/codex/ui-implementation-instructions.v1.2.ja.md` | `.workspace-docs/40_plans/30_archived/ui-implementation-instructions.v1.2.ja.md` | archived plan |
| `.workspace-docs/codex/ui-implementation-instructions.v1.3.ja.md` | `.workspace-docs/40_plans/20_done/ui-implementation-instructions.v1.3.ja.md` | current plan |

## 2.3 v2 仕様系

| 現在 | 移行先 | 区分 |
| --- | --- | --- |
| `.workspace-docs/v2/architecture.md` | `.workspace-docs/30_specs/20_done/v2-architecture.md` | spec |
| `.workspace-docs/v2/core-api-spec.md` | `.workspace-docs/30_specs/20_done/v2-core-api-spec.md` | spec |
| `.workspace-docs/v2/core-engine-spec.md` | `.workspace-docs/30_specs/20_done/v2-core-engine-spec.md` | spec |
| `.workspace-docs/v2/db-schema.md` | `.workspace-docs/30_specs/20_done/v2-db-schema.md` | spec |
| `.workspace-docs/v2/definition-spec.md` | `.workspace-docs/30_specs/20_done/v2-definition-spec.md` | spec |
| `.workspace-docs/v2/engine-runtime-spec.md` | `.workspace-docs/30_specs/20_done/v2-engine-runtime-spec.md` | spec |
| `.workspace-docs/v2/execution-graph-spec.md` | `.workspace-docs/30_specs/20_done/v2-execution-graph-spec.md` | spec |
| `.workspace-docs/v2/ui-spec.md` | `.workspace-docs/30_specs/10_in-progress/v2-ui-spec.md` | spec |
| `.workspace-docs/v2/workflow-definition-spec.md` | `.workspace-docs/30_specs/20_done/v2-workflow-definition-spec.md` | spec |
| `.workspace-docs/v2/workflow-input-output-spec.md` | `.workspace-docs/30_specs/20_done/v2-workflow-input-output-spec.md` | spec |
| `.workspace-docs/v2/statevia-system-diagram.md` | `.workspace-docs/30_specs/20_done/v2-statevia-system-diagram.md` | spec |

## 2.4 v2 計画・ロードマップ系

| 現在 | 移行先 | 区分 |
| --- | --- | --- |
| `.workspace-docs/v2/statevia-roadmap.md` | `.workspace-docs/40_plans/10_in-progress/v2-roadmap.md` | plan |
| `.workspace-docs/v2/statevia-architecture-v1.md` | `.workspace-docs/40_plans/30_archived/v2-statevia-architecture-v1.md` | archived plan |
| `.workspace-docs/v2/statevia-playground-architecture.md` | `.workspace-docs/40_plans/30_archived/v2-statevia-playground-architecture.md` | archived plan |
| `.workspace-docs/v2/statevia-playground-spec.md` | `.workspace-docs/30_specs/30_archived/v2-statevia-playground-spec.md` | archived spec |
| `.workspace-docs/v2/playground-min-ui-spec.md` | `.workspace-docs/30_specs/30_archived/v2-playground-min-ui-spec.md` | archived spec |
| `.workspace-docs/v2/README.md` | `.workspace-docs/40_plans/10_in-progress/v2-readme.md` | plan/meta |

## 2.5 planning 配下（タスク主体）

| 現在 | 移行先 | 区分 |
| --- | --- | --- |
| `.workspace-docs/v2/planning/remaining-tasks.md` | `.workspace-docs/50_tasks/10_in-progress/v2-remaining-tasks.md` | task |
| `.workspace-docs/v2/planning/ticket-backlog.md` | `.workspace-docs/50_tasks/10_in-progress/v2-ticket-backlog.md` | task |
| `.workspace-docs/v2/planning/logging-v1-tasks.md` | `.workspace-docs/50_tasks/10_in-progress/v2-logging-v1-tasks.md` | task |
| `.workspace-docs/v2/planning/input-implementation-tasks.md` | `.workspace-docs/50_tasks/20_done/v2-input-implementation-tasks.md` | task |
| `.workspace-docs/v2/planning/input-phase-d-smoke.md` | `.workspace-docs/50_tasks/20_done/v2-input-phase-d-smoke.md` | task |
| `.workspace-docs/v2/planning/input-future-backlog.md` | `.workspace-docs/50_tasks/10_in-progress/v2-input-future-backlog.md` | task |
| `.workspace-docs/v2/planning/action-registry-v1-plan.md` | `.workspace-docs/40_plans/20_done/v2-action-registry-v1-plan.md` | plan |
| `.workspace-docs/v2/planning/modification-plan.md` | `.workspace-docs/40_plans/10_in-progress/v2-modification-plan.md` | plan |
| `.workspace-docs/v2/planning/open-decisions.md` | `.workspace-docs/40_plans/10_in-progress/v2-open-decisions.md` | plan |
| `.workspace-docs/v2/planning/u1-event-ordering-and-transactions.md` | `.workspace-docs/50_tasks/20_done/v2-u1-event-ordering-and-transactions.md` | task |
| `.workspace-docs/v2/planning/u2-event-store-schema.md` | `.workspace-docs/50_tasks/20_done/v2-u2-event-store-schema.md` | task |
| `.workspace-docs/v2/planning/u3-display-id-table.md` | `.workspace-docs/50_tasks/20_done/v2-u3-display-id-table.md` | task |
| `.workspace-docs/v2/planning/u4-get-id-and-response.md` | `.workspace-docs/50_tasks/20_done/v2-u4-get-id-and-response.md` | task |
| `.workspace-docs/v2/planning/u5-api-engine-reference-and-ci.md` | `.workspace-docs/50_tasks/20_done/v2-u5-api-engine-reference-and-ci.md` | task |
| `.workspace-docs/v2/planning/u6-cli-and-samples-placement.md` | `.workspace-docs/50_tasks/20_done/v2-u6-cli-and-samples-placement.md` | task |
| `.workspace-docs/v2/planning/u7-reducer-placement.md` | `.workspace-docs/50_tasks/20_done/v2-u7-reducer-placement.md` | task |
| `.workspace-docs/v2/planning/u8-restart-policy.md` | `.workspace-docs/50_tasks/20_done/v2-u8-restart-policy.md` | task |
| `.workspace-docs/v2/planning/u9-publish-event-endpoint.md` | `.workspace-docs/50_tasks/20_done/v2-u9-publish-event-endpoint.md` | task |
| `.workspace-docs/v2/planning/u10-nodes-states-discrimination.md` | `.workspace-docs/50_tasks/20_done/v2-u10-nodes-states-discrimination.md` | task |
| `.workspace-docs/v2/planning/u11-legacy-branch-or-tag.md` | `.workspace-docs/50_tasks/20_done/v2-u11-legacy-branch-or-tag.md` | task |

---

## 3. 実施手順（記録）

1. 状態別フォルダを作成  
2. `notes` / `tasks` / `specs` / `plans` の順に移動  
3. 参照リンクを一括更新（`.exclude/` → `.workspace-docs/`）  
4. 主要ドキュメント（`README` / `remaining-tasks` / `ticket-backlog` / `modification-plan`）の参照を整合

---

## 4. 運用メモ

- `done` は「将来更新不要」ではなく「現時点の正本」を意味する
- `archived` は削除しない（差分確認のため）
- 迷うファイルは `inbox` に一時配置し、週次で仕分けする
