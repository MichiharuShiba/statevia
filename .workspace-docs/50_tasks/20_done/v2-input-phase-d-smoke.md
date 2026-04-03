# Input フェーズD スモーク手順（IO-15）

目的: Core-API の `POST /v1/workflows` で渡した `input` が実行グラフ側へ伝播することを最小手順で確認する。

## 前提

- PostgreSQL 起動済み
- Core-API 起動済み（`api/Statevia.Core.Api`）
- `curl` と `jq` が利用可能

## 手順

1. 定義を登録する（初期状態 `A` から `B` に遷移し、`B` は受け取った input を output に返す想定の最小定義）。
2. `POST /v1/workflows` に `definitionId` と `input` を渡して起動する。
3. 返却された `displayId` で `GET /v1/workflows/{id}/graph` を取得する。
4. グラフ JSON のノード `B`（または対応ノード）の `input`/`output` 断面に、起動時 `input` 値が含まれることを確認する。

## 実行例

```bash
# 1) 定義登録
DEF_RES=$(curl -sS -X POST "http://localhost:8080/v1/definitions" \
  -H "Content-Type: application/json" \
  -d '{
    "name":"io15-smoke",
    "yaml":"id: io15-smoke\ninitial: A\nstates:\n  A:\n    action: noop\n    on:\n      done:\n        next: B\n  B:\n    action: noop\n"
  }')
DEF_ID=$(echo "$DEF_RES" | jq -r '.displayId')

# 2) input 付きで workflow 起動
WF_RES=$(curl -sS -X POST "http://localhost:8080/v1/workflows" \
  -H "Content-Type: application/json" \
  -H "X-Idempotency-Key: io15-smoke-1" \
  -d "{
    \"definitionId\":\"$DEF_ID\",
    \"input\": {\"orderId\":\"ORD-001\", \"amount\": 1200}
  }")
WF_ID=$(echo "$WF_RES" | jq -r '.displayId')

# 3) graph 取得
curl -sS "http://localhost:8080/v1/workflows/$WF_ID/graph" | jq '.'
```

## 判定基準

- `POST /v1/workflows` が `201` を返すこと。
- `GET /v1/workflows/{id}/graph` が `200` を返すこと。
- 同一 `X-Idempotency-Key` でも `input` が異なる 2 リクエストが **別 workflow** として作成されること（`IO-12` 経由で `input` を識別できることの確認）。

## 実行ログ（2026-03-24）

最新コードで Core-API を起動し、同一 `definitionId` + 同一 `X-Idempotency-Key` で `input` だけを変えて 2 回 `POST /v1/workflows` を実行した。

```text
DEF_ID=6OUKVQBNUy
WF1_ID=Y989fxwHoB
WF2_ID=VEIks7Tfdo
DIFFERENT_WORKFLOWS=True
```

上記より、`input` 差分がワークフロー開始リクエストの識別に反映されることを確認した。
