/**
 * Node コマンドハンドラ
 */
import { EventEnvelope } from "../../domain/value-objects/event-envelope.js";
import { ExecutionState } from "../../domain/value-objects/execution-state.js";
import { Actor } from "../../domain/value-objects/actor.js";
import { ensureCancelNotRequested, ensureNotTerminalExecution, ensureNodeStatus, getNodeOrThrow } from "../../domain/domain-services/guards.js";
import { mkEvent } from "./event-factory.js";

export function cmdCreateNode(
  s: ExecutionState,
  actor: Actor,
  nodeId: string,
  nodeType: string,
  correlationId?: string
): { events: EventEnvelope[] } {
  ensureNotTerminalExecution(s);
  ensureCancelNotRequested(s);
  if (s.nodes[nodeId]) return { events: [] };
  return {
    events: [mkEvent(s.executionId, "NODE_CREATED", actor, { nodeId, nodeType }, correlationId)]
  };
}

export function cmdStartNode(
  s: ExecutionState,
  actor: Actor,
  nodeId: string,
  attempt: number,
  workerId?: string,
  correlationId?: string
): { events: EventEnvelope[] } {
  ensureNotTerminalExecution(s);
  ensureCancelNotRequested(s);
  const n = getNodeOrThrow(s, nodeId);
  ensureNodeStatus(n.status, ["READY", "IDLE"]);
  return {
    events: [mkEvent(s.executionId, "NODE_STARTED", actor, { nodeId, attempt, workerId: workerId ?? null }, correlationId)]
  };
}

export function cmdPutNodeWaiting(
  s: ExecutionState,
  actor: Actor,
  nodeId: string,
  waitKey?: string,
  prompt?: unknown,
  correlationId?: string
): { events: EventEnvelope[] } {
  ensureNotTerminalExecution(s);
  ensureCancelNotRequested(s);
  const n = getNodeOrThrow(s, nodeId);
  ensureNodeStatus(n.status, ["RUNNING"]);
  return {
    events: [
      mkEvent(s.executionId, "NODE_WAITING", actor, { nodeId, waitKey: waitKey ?? null, prompt: prompt ?? null }, correlationId)
    ]
  };
}

export function cmdResumeNode(
  s: ExecutionState,
  actor: Actor,
  nodeId: string,
  resumeKey?: string,
  correlationId?: string
): { events: EventEnvelope[] } {
  ensureNotTerminalExecution(s);
  ensureCancelNotRequested(s);
  const n = getNodeOrThrow(s, nodeId);
  ensureNodeStatus(n.status, ["WAITING"]);
  return {
    events: [mkEvent(s.executionId, "NODE_RESUMED", actor, { nodeId, resumeKey: resumeKey ?? null }, correlationId)]
  };
}
