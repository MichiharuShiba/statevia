# HTTPリクエスト例

```bash
# 1) Execution 作成
curl -s -X POST http://localhost:8080/executions -H "Content-Type: application/json" -H "X-Idempotency-Key: 11111111-1111-1111-1111-111111111111" -d '{"executionId":"ex-1","graphId":"hello"}'

# 2) Node 作成
curl -s -X POST http://localhost:8080/executions/ex-1/nodes/task-1/create -H "Content-Type: application/json" -H "X-Idempotency-Key: 22222222-2222-2222-2222-222222222222" -d '{"nodeType":"Task"}'

# 3) Node start（RUNNINGへ）
curl -s -X POST http://localhost:8080/executions/ex-1/nodes/task-1/start -H "Content-Type: application/json" -H "X-Idempotency-Key: 33333333-3333-3333-3333-333333333333" -d '{"attempt":1,"workerId":"w1"}'

# 4) wait（WAITINGへ）
curl -s -X POST http://localhost:8080/executions/ex-1/nodes/task-1/wait -H "Content-Type: application/json" -H "X-Idempotency-Key: 44444444-4444-4444-4444-444444444444" -d '{"waitKey":"approval-1"}'

# 5) cancel（Execution CANCELED、normalizeで node も CANCELED 収束）
curl -s -X POST http://localhost:8080/executions/ex-1/cancel -H "Content-Type: application/json" -H "X-Idempotency-Key: 55555555-5555-5555-5555-555555555555" -d '{"reason":"user"}'

# 6) resume（cancelRequestedAtがあるので 409）
curl -i -s -X POST http://localhost:8080/executions/ex-1/nodes/task-1/resume -H "Content-Type: application/json" -H "X-Idempotency-Key: 66666666-6666-6666-6666-666666666666" -d '{"resumeKey":"approval-1"}'

# 7) state確認
curl -s http://localhost:8080/executions/ex-1 | jq .
```