# Requirements: UIハイブリッド刷新（一覧・詳細ハブ + 専用画面分離）

## Introduction

本仕様は、UI機能の強化（一覧・詳細参照、テンプレートエディター、ワークフロー実行、実行グラフ）と画面構成の刷新を、ハイブリッド情報設計で段階導入するための要件を定義する。
中核方針は `DefinitionList` と `WorkflowList/Detail` をハブにし、編集・実行・グラフを専用画面として分離することである。

## Alignment with Product Vision

- 定義駆動（definition-driven）運用に合わせ、`Definition` を起点に `Workflow` の参照・実行・可視化へ自然に遷移できるUIにする。
- API契約（`/v1/definitions`, `/v1/workflows`, `/v1/workflows/{id}/graph`）を正とし、UI側で再解釈や独自評価を行わない。
- 既存導線（`/playground`, `/playground/run/[displayId]`）の互換性を維持しつつ、新URL構造へ段階移行可能にする。

## Requirements

### Requirement 1 — ハイブリッド画面遷移の確立

**ユーザーストーリー:** 運用者として、定義と実行の関係に沿った画面遷移で操作したい。なぜなら、必要な情報へ最短で到達したいから。

#### Acceptance Criteria — Requirement 1

| No | アクター | きっかけ（ユースケース） | 期待される結果 |
| --- | --- | --- | --- |
| 1 | 運用者 | `DefinitionList` を開く | `DefinitionDetail` への遷移導線が表示される |
| 2 | 運用者 | `DefinitionDetail` で関連実行を確認する | 定義文脈を維持したまま `WorkflowList` へ遷移できる |
| 3 | 運用者 | `WorkflowList` から対象を選択する | `WorkflowDetail` が URL 主導で表示される |
| 4 | 運用者 | `WorkflowDetail` から可視化を選択する | `WorkflowGraphPage` へ遷移できる |
| 5 | 開発者 | `DefinitionList` から編集を選択する | `DefinitionEditor` へ遷移できる |
| 6 | 運用者 | `DefinitionList` から実行を選択する | 定義起点の新規開始フローで `WorkflowRunPage` を開ける |
| 7 | 運用者 | `WorkflowRunPage` でグラフ確認を選択する | `WorkflowGraphPage` へ遷移できる |

### Requirement 2 — TOPダッシュボード導線の追加

**ユーザーストーリー:** 利用者として、TOPで直近の実行を素早く確認したい。なぜなら、日常運用の再開コストを下げたいから。

#### Acceptance Criteria — Requirement 2

| No | アクター | きっかけ（ユースケース） | 期待される結果 |
| --- | --- | --- | --- |
| 1 | 利用者 | `/` にアクセスする | `redirect()` により `/dashbord` へ遷移する |
| 2 | 利用者 | `/dashbord` を開く | 直近 `WorkflowDetail` 10件が簡易表示される |
| 3 | 利用者 | 一覧項目を選択する | 対応する `WorkflowDetail` へ遷移する |
| 4 | 利用者 | 直近データが0件の状態で `/dashbord` を開く | 空状態メッセージと主要導線（定義一覧・ワークフロー一覧）が表示される |

### Requirement 3 — 一覧・詳細参照のURL主導化

**ユーザーストーリー:** 運用者として、一覧と詳細を共有可能なURLで開きたい。なぜなら、調査結果を他メンバーと共有したいから。

#### Acceptance Criteria — Requirement 3

| No | アクター | きっかけ（ユースケース） | 期待される結果 |
| --- | --- | --- | --- |
| 1 | 運用者 | `WorkflowDetail` を開く | 手入力ID依存ではなく、URLパラメータで対象実行が特定される |
| 2 | 運用者 | `DefinitionDetail` または `WorkflowDetail` を再読込する | 同一対象が再表示される |
| 3 | 運用者 | 不正なIDまたは存在しないIDを指定する | 404相当の案内が表示される |
| 4 | 運用者 | 一覧フィルタ（status/name/definition文脈）を適用する | フィルタ状態がURLまたは復元可能な状態として維持される |

### Requirement 4 — 実行・グラフ・編集の専用画面化

**ユーザーストーリー:** 開発者/運用者として、複雑機能を用途別に分離して使いたい。なぜなら、画面責務を明確にして誤操作を減らしたいから。

#### Acceptance Criteria — Requirement 4

| No | アクター | きっかけ（ユースケース） | 期待される結果 |
| --- | --- | --- | --- |
| 1 | 運用者 | 実行操作を行う | `WorkflowRunPage` で `Cancel/Resume/Event` が完結できる |
| 2 | 運用者 | グラフを確認する | `WorkflowGraphPage` でノード状態とInspectorが表示される |
| 3 | 開発者 | 定義を編集する | `DefinitionEditor` で検証・保存フローが提供される |
| 4 | 開発者/運用者 | 専用画面から戻る操作を行う | 元の `DefinitionDetail` または `WorkflowDetail` へ復帰できる |

### Requirement 5 — 互換導線と段階移行

**ユーザーストーリー:** プロダクトオーナーとして、既存導線を壊さずに新UIへ移行したい。なぜなら、運用停止なしで刷新したいから。

#### Acceptance Criteria — Requirement 5

| No | アクター | きっかけ（ユースケース） | 期待される結果 |
| --- | --- | --- | --- |
| 1 | 運用者 | 移行期間中に `/playground` を開く | 新導線への案内を表示しつつ既存機能が継続提供される |
| 2 | 運用者 | `/playground/run/[displayId]` を開く | 新しい run/graph 導線へ遷移可能な手段が提供される |
| 3 | プロダクトオーナー | 新旧導線が同一データを表示する状態を確認する | 表示整合性が維持される |

## Non-Functional Requirements

### Clarity

- 画面責務と遷移規則を `requirements.md` と `design.md` の両方で追跡可能にする。
- URL命名は画面責務と1対1で対応し、運用ドキュメントから逆引き可能にする。

### Reliability

- 再読込・直接URLアクセスで同一状態を復元できること。
- 404/422/409など主要エラーを画面上で判別可能にすること。

### Performance

- ダッシュボード（直近10件）と一覧画面は初回表示の体感を損なわない取得単位で設計する。
- グラフ画面は必要なデータのみ取得し、重複フェッチを抑制する。

### Security and Data Handling

- `workflowInput` と state `output` は既存方針に沿ってマスク/折りたたみを維持する。
- UIはAPIレスポンスを独自加工しすぎず、機微情報表示ルールを一元化する。

### Observability

- 主要遷移（一覧→詳細、実行→グラフ）の失敗時に追跡可能なエラー情報を表示・記録できること。
- 実行状態更新は通知と再取得の責務を分離し、最終表示はAPI GET結果を正とする。

## Out of Scope

- 認証/認可モデルの新規導入。
- Definitionエディターのエディタ基盤切替（Monaco採用可否の最終決定）。
- API契約そのものの大幅変更（既存 `/v1/*` 契約を前提とする）。

## References

- `.workspace-docs/30_specs/10_in-progress/v2-ui-spec.md`
- `.workspace-docs/30_specs/10_in-progress/ui-playground-design.md`
- `.workspace-docs/40_plans/10_in-progress/v2-modification-plan.md`
- `.workspace-docs/50_tasks/10_in-progress/v2-remaining-tasks.md`
