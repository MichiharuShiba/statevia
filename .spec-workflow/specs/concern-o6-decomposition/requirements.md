# Requirements: 懸念 O6 の仕様化と分割（STV-410）

## Introduction

`v2-modification-plan.md` に列挙された懸念 **C2, C7, C11, C13, C14** を、実装・起票可能な粒度に分解し、**優先度と依存**を整理する。本 spec は **実装を直接行わない**（分解と文書化が主成果物）。

**紐づくチケット**: `STV-410`（`v2-ticket-backlog.md`）。

**元タスク**: `v2-remaining-tasks.md` の **O6**。

## Alignment with Product Vision

未確定の大きな懸念を放置すると、後続フェーズで手戻りが大きい。先に仕様チケットへ落とす。

## Requirements

### Requirement 1 — 懸念の棚卸し

**User Story:** As a **アーキテクト**, I want **各懸念（C2/C7/C11/C13/C14）の現状と未確定点が 1 ページにまとまる**こと, so that **議論の土台がある**。

#### Acceptance Criteria — Requirement 1

1. WHEN **本仕様が完了する** THEN **各 ID について次が分かる**: 分類（一貫性/仕様/永続化/Engine/Phase5）、現状の決定事項、未確定点、関連ドキュメントへのリンク。
2. WHEN **参照する** THEN **`v2-modification-plan.md` の表と矛盾しない**（更新が必要なら modification-plan 側を追記するタスクを出す）。

### Requirement 2 — サブチケット化

**User Story:** As a **PM**, I want **5 件以上の実装・仕様チケットに分割できる**こと, so that **スプリントに載せられる**。

#### Acceptance Criteria — Requirement 2

1. WHEN **分割が完了する** THEN **少なくとも 5 件のサブチケット**が定義される（ID・タイトル・受け入れの一行）。
2. WHEN **各サブチケット** THEN **優先度（P1–P3 相当）と依存**が記載される。

### Requirement 3 — バックログ連携

**User Story:** As a **保守者**, I want **`v2-ticket-backlog` または `.workspace-docs` に追跡可能な参照がある**こと, so that **実行管理ができる**。

#### Acceptance Criteria — Requirement 3

1. WHEN **完了する** THEN **`v2-ticket-backlog.md` のメモまたは新規タスク表に、サブチケットへの参照が追加される**（または既存 STV に割り当て）。

## Non-Functional Requirements

### Clarity

- 用語は **既存の U1/U2/U7/U10** と整合するよう、必要なら用語集を参照。

## Out of Scope

- 各サブ懸念の**本実装**（サブチケット側で実施）。
- O7（テナント管理・認証連動）。

## References

- `.workspace-docs/40_plans/10_in-progress/v2-modification-plan.md` — C2/C7/C11/C13/C14 表
- `.workspace-docs/50_tasks/10_in-progress/v2-remaining-tasks.md` — O6
