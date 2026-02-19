import { EventEnvelope, Actor, ExecutionState } from "./types.js";
import { reduce } from "./reducer.js";
import { ensureCancelNotRequested, ensureNotTerminalExecution, ensureNodeStatus, getNodeOrThrow } from "./guards.js";

function nowIso() {
  return new Date().toISOString();
}

function mkEvent(executionId: string, type: string, actor: Actor, payload: any, correlationId?: string): EventEnvelope {
  return {
    eventId: crypto.randomUUID(),
    executionId,
    type,
    occurredAt: nowIso(),
    actor,
    correlationId,
    schemaVersion: 1,
    payload
  };
}

// Node 20+ なら global crypto がある
declare const crypto: { randomUUID(): string };

export function cmdCreateExecution(args: {
  executionId: string;
  graphId: string;
  input?: any;
  actor: Actor;
  correlationId?: string;
}): { events: EventEnvelope[] } {
  return {
    events: [
      mkEvent(args.executionId, "EXECUTION_CREATED", args.actor, { graphId: args.graphId, input: args.input ?? null }, args.correlationId)
    ]
  };
}

export function cmdStartExecution(s: ExecutionState, actor: Actor, correlationId?: string) {
  ensureNotTerminalExecution(s);
  ensureCancelNotRequested(s);
  return { events: [mkEvent(s.executionId, "EXECUTION_STARTED", actor, {}, correlationId)] };
}

export function cmdCancelExecution(s: ExecutionState, actor: Actor, reason?: string, correlationId?: string) {
  // 終端なら No-op（冪等運用）
  if (["COMPLETED", "FAILED", "CANCELED"].includes(s.status)) return { events: [] as EventEnvelope[] };

  const events: EventEnvelope[] = [];
  if (!s.cancelRequestedAt) {
    events.push(mkEvent(s.executionId, "EXECUTION_CANCEL_REQUESTED", actor, { reason: reason ?? null }, correlationId));
  }
  // 最小実装では「即確定」
  events.push(mkEvent(s.executionId, "EXECUTION_CANCELED", actor, { reason: reason ?? null }, correlationId));
  return { events };
}

export function cmdPutNodeWaiting(s: ExecutionState, actor: Actor, nodeId: string, waitKey?: string, prompt?: any, correlationId?: string) {
  ensureNotTerminalExecution(s);
  ensureCancelNotRequested(s);
  const n = getNodeOrThrow(s, nodeId);
  ensureNodeStatus(n.status, ["RUNNING"]);
  return {
    events: [mkEvent(s.executionId, "NODE_WAITING", actor, { nodeId, waitKey: waitKey ?? null, prompt: prompt ?? null }, correlationId)]
  };
}

export function cmdResumeNode(s: ExecutionState, actor: Actor, nodeId: string, resumeKey?: string, correlationId?: string) {
  ensureNotTerminalExecution(s);
  ensureCancelNotRequested(s);
  const n = getNodeOrThrow(s, nodeId);
  ensureNodeStatus(n.status, ["WAITING"]);
  // resumeKey の照合は運用次第（今回は素通し）
  return {
    events: [mkEvent(s.executionId, "NODE_RESUMED", actor, { nodeId, resumeKey: resumeKey ?? null }, correlationId)]
  };
}

// デモ用：ノードが存在しないと wait/resume ができないので、作成APIも用意する
export function cmdCreateNode(s: ExecutionState, actor: Actor, nodeId: string, nodeType: string, correlationId?: string) {
  ensureNotTerminalExecution(s);
  ensureCancelNotRequested(s);
  if (s.nodes[nodeId]) return { events: [] as EventEnvelope[] };
  return {
    events: [mkEvent(s.executionId, "NODE_CREATED", actor, { nodeId, nodeType }, correlationId)]
  };
}

// デモ用：nodeをRUNNINGにするためのstart
export function cmdStartNode(s: ExecutionState, actor: Actor, nodeId: string, attempt: number, workerId?: string, correlationId?: string) {
  ensureNotTerminalExecution(s);
  ensureCancelNotRequested(s);
  const n = getNodeOrThrow(s, nodeId);
  ensureNodeStatus(n.status, ["READY", "IDLE"]); // デモでは甘め
  return {
    events: [mkEvent(s.executionId, "NODE_STARTED", actor, { nodeId, attempt, workerId: workerId ?? null }, correlationId)]
  };
}

// 便利：events を適用して状態を返す
export function applyEvents(s: ExecutionState, events: EventEnvelope[]): ExecutionState {
  return events.reduce((acc, e) => reduce(acc, e), s);
}