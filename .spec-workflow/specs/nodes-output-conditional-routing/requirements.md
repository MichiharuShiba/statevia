# Requirements: nodes→states 変換残課題と output 条件遷移

## Introduction

本仕様は、`nodes` から `states` への変換仕様に残っている課題を整理し、既存の `Fork/Join` に加えて **State の output 条件で次遷移先を選択する仕様**を追加定義する。
目的は、定義作者が構造分岐（Fork/Join）とデータ分岐（output 条件）を併用しても、変換結果と実行結果が一意に解釈できる状態を作ることである。

## Alignment with Product Vision

- 定義駆動（definition-driven）の原則に沿って、遷移ロジックを定義時点で明確化する。
- API 利用者・運用者が「なぜその State に遷移したか」を追跡できる契約を整備する。
- 既存の `Fork/Join` 実装を壊さず、段階導入で拡張可能な仕様とする。

## Requirements

### Requirement 1 — 変換処理の決定性と互換性

**User Story:** As a **定義作者**, I want **同一 definition から常に同じ states が生成される** so that **実行結果の再現性を担保できる**。

#### Acceptance Criteria — Requirement 1

1. WHEN 同一 `nodes` 定義を複数回変換する THEN システム SHALL 同一の `states` 構造（state 識別子、遷移順序、優先順位）を生成する。
2. WHEN 条件遷移を定義しないノードを変換する THEN システム SHALL 従来の `on: <Fact> -> next/fork/end` と同等の結果を生成する。
3. WHEN `fork/join` ノードを変換する THEN システム SHALL 既存の構造分岐セマンティクスを変更しない。

### Requirement 2 — output 条件遷移の定義

**User Story:** As a **ワークフロー設計者**, I want **State の output に応じて次の State を選択したい** so that **業務結果に応じた制御フローを記述できる**。

#### Acceptance Criteria — Requirement 2

1. WHEN State 実行が成功し `output` が確定する THEN システム SHALL 条件遷移を評価して次 state を決定する。
2. WHEN 複数の条件が定義される THEN システム SHALL `order` 指定ありの case を `order` 昇順で評価する。
3. WHEN 複数条件が真になる THEN システム SHALL 最初に一致した条件を採用する（first-match wins）。
4. IF `on.<Fact>` に `cases` を定義する THEN システム SHALL 同一階層の `next/fork/end` を禁止する。
5. IF `on.<Fact>` に `next/fork/end` を定義する THEN システム SHALL `cases/default` を禁止する。
6. WHEN 同一 `cases` 配列で `order` 指定ありと未指定が混在する THEN システム SHALL `order` 未指定の case をすべての `order` 指定あり case の後ろで評価する。
7. WHEN `order` が同値の case を評価する THEN システム SHALL 定義順で評価する。
8. WHEN `order` 未指定同士の case を評価する THEN システム SHALL `cases` 配列の記載順で評価する。
9. WHEN `when.path` を指定する THEN システム SHALL 既存仕様と同じ簡易 JSONPath（`$` または `$.seg1.seg2`）のみを許可する。
10. WHEN `op: in` を指定する THEN システム SHALL `value` にリテラル配列を要求する。
11. WHEN `op: between` を指定する THEN システム SHALL `value` に下限・上限の 2 要素リテラル配列を要求する。

### Requirement 3 — 未一致時とエラー時の契約

**User Story:** As an **運用者**, I want **条件未一致時や条件評価不能時の挙動を固定したい** so that **予期しない停止や不整合を防げる**。

#### Acceptance Criteria — Requirement 3

1. WHEN どの条件にも一致しない THEN システム SHALL `on.<Fact>.default` に定義された遷移へフォールバックする。
2. IF 条件遷移を持つ state の `on.<Fact>.cases` に対して `on.<Fact>.default` が存在しない THEN システム SHALL 定義登録時に 422 を返す。
3. WHEN 条件評価で型不整合や参照不能が発生する THEN システム SHALL 当該条件を不一致として扱い、警告ログを出力する。
4. IF すべて不一致かつ `on.<Fact>.default` もない実行状態に入る THEN システム SHALL 実行失敗としてワークフローを終了する。
5. WHEN `on.<Fact>.default` が文字列で指定される THEN システム SHALL `on.<Fact>.default.next` のショートハンドとして扱う。
6. WHEN `on.<Fact>.default` がオブジェクトで指定される THEN システム SHALL `next/fork/end` のいずれか 1 つだけを許可する。
7. IF `on.<Fact>` または `on.<Fact>.default` に `next/fork/end` のいずれも定義されない THEN システム SHALL 定義登録時に 422 を返す。

### Requirement 4 — nodes→states 変換ルールの正規化

**User Story:** As an **実装者**, I want **ノード遷移を実行時に扱いやすい形へ正規化したい** so that **評価器とバリデータを単純化できる**。

#### Acceptance Criteria — Requirement 4

1. WHEN 条件付き edge を変換する THEN システム SHALL `states.<state>.on.Completed.cases[]` に `{ when, next/fork/end, order }` として格納する。
2. WHEN 無条件 edge を変換する THEN システム SHALL `states.<state>.on.Completed.default` として格納する。
3. IF 無条件 edge が複数定義される THEN システム SHALL 定義登録時にエラーを返す。
4. IF 遷移先 state が存在しない THEN システム SHALL 定義登録時にエラーを返す。
5. WHEN `nodes.next` が定義される THEN システム SHALL `nodes.edges: [{ to: { id: <next> } }]` と等価に扱う。
6. WHEN `nodes.edges.to.id` のみで 1 本の無条件遷移が定義される THEN システム SHALL `nodes.next` と等価に扱う。
7. WHEN `nodes.next` と単一無条件 `nodes.edges.to.id` が同時に定義される THEN システム SHALL 同一遷移先である場合のみ受理し、不一致時は定義登録時にエラーを返す。
8. WHEN states 形式で複数の `end: true` 遷移を定義する THEN システム SHALL それらを有効な終端遷移として受理する。
9. WHEN nodes 形式を登録する THEN システム SHALL 現行互換として `type: end` ノードをちょうど 1 つ要求する。
10. WHEN nodes 形式で複数の終了パターンを表現したい THEN システム SHALL 単一の `type: end` ノードへ集約する構造を要求する。
11. WHEN states 形式を登録する THEN システム SHALL 少なくとも 1 つの `end: true` 遷移を要求する。
12. IF `end: true` を持つ state の同一 `on.<Fact>` または `on.<Fact>.default` に `next/fork` も併記される THEN システム SHALL 定義登録時に 422 を返す。

### Requirement 5 — Fork/Join と条件遷移の併用ルール

**User Story:** As an **ワークフロー設計者**, I want **Fork/Join と output 条件遷移を同一定義内で併用したい** so that **構造分岐とデータ分岐を使い分けられる**。

#### Acceptance Criteria — Requirement 5

1. WHEN 通常 state から条件評価で次遷移先を決定する THEN 遷移先として `fork` state を指定可能である。
2. WHEN `fork/join` state を評価する THEN システム SHALL output 条件評価を行わない。
3. WHEN 仕様利用者が判定ロジックを確認する THEN システム SHALL 「構造分岐は Fork/Join、データ分岐は output 条件」の責務分離を文書上で明示する。

## Non-Functional Requirements

### Clarity

- 条件評価順、default フォールバック、エラー契約を図と文章の両方で説明すること。
- 条件遷移の表記は既存 `on: <Fact>` を崩さず、拡張キー（`cases` / `default`）で表現すること。

### Reliability

- 条件遷移導入後も既存定義の実行結果が変化しない後方互換性を担保すること。

### Observability

- 条件評価ログに `workflowId`、`stateName`、`matchedTransition`（または no-match）を出力可能であること。
- Engine は既存のエラー配列返却方針を維持し、条件評価エラーを同方針で扱うこと。
- API はデバッグ用途で条件評価詳細（評価した case、採用結果、no-match 理由）を返却可能であること。
- UI は API が返した条件評価結果をそのまま表示し、独自再評価を行わないこと。

### Documentation Lifecycle

- `docs/` 配下の契約ドキュメント更新は実装完了後に実施し、本 spec フェーズでは要件・設計への反映を正とすること。

## Out of Scope

- 条件式 DSL の高度化（論理演算のネスト、関数呼び出し、スクリプト実行）。
- `onError` / `timeout` / `controls` の実装導入（本 spec では契約のみ扱う）。
- 範囲リテラル短縮記法（例: `[1...9]`）。
- 以下の比較演算子は今回の対象外とする: `contains`, `notContains`, `startsWith`, `endsWith`, `matches`, `notMatches`, `anyOf`, `allOf`, `noneOf`, `notIn`, `isNull`, `isNotNull`, `isEmpty`, `isNotEmpty`, `before`, `after`, `onOrBefore`, `onOrAfter`, `overlaps`, `intersects`

## References

- `.workspace-docs/30_specs/10_in-progress/v2-nodes-to-states-conversion-spec.md`
- `AGENTS.md`
