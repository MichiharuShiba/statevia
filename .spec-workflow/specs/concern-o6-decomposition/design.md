# Design: 懸念 O6 の仕様化と分割（STV-410）

## Overview

### 懸念一覧（出典: modification-plan）

| ID | 分類 | 要約 |
|----|------|------|
| C2 | 一貫性 | projection の更新タイミング |
| C7 | 仕様 | EventEnvelope と event_store、Engine のイベント発行タイミング |
| C11 | 永続化 | コールバック失敗時のリトライ・再送・べき等 |
| C13 | Engine | GetSnapshot と reducer 出力の関係 |
| C14 | Phase 5 | nodes→states 変換で未カバーの要素・仕様ギャップ |

### 成果物

1. **`docs/` または `.workspace-docs/30_specs/`** に **O6 分解メモ**（ファイル名は tasks で決定）。
2. **サブチケット一覧**（Markdown 表）— `requirements.md` の Acceptance を満たす。
3. **次アクション**: 各サブチケットを `v2-ticket-backlog` に STV-4xx として起票するか、既存計画ドキュメントに ID を振る。

### プロセス

- 既存の `v2-u7-reducer-placement.md` 等の完了ドキュメントを参照し、**C13/C7 は Engine・reducer・event_store の横断**で整理。
- **C14** は `v2-nodes-to-states-conversion-spec.md` と突き合わせ。

## References

- `.workspace-docs/30_specs/10_in-progress/v2-nodes-to-states-conversion-spec.md`
- `.workspace-docs/50_tasks/20_done/v2-u7-reducer-placement.md`
