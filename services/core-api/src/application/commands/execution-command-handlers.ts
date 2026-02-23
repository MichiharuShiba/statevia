/**
 * Execution コマンドハンドラ
 */
import { EventEnvelope } from "../../domain/value-objects/event-envelope.js";
import { ExecutionState } from "../../domain/value-objects/execution-state.js";
import { Actor } from "../../domain/value-objects/actor.js";
import { ensureCancelNotRequested, ensureNotTerminalExecution } from "../../domain/domain-services/guards.js";
import { mkEvent } from "./event-factory.js";

export function cmdCreateExecution(args: {
  executionId: string;
  graphId: string;
  input?: unknown;
  actor: Actor;
  correlationId?: string;
}): { events: EventEnvelope[] } {
  return {
    events: [
      mkEvent(
        args.executionId,
        "EXECUTION_CREATED",
        args.actor,
        { graphId: args.graphId, input: args.input ?? null },
        args.correlationId
      )
    ]
  };
}

export function cmdStartExecution(s: ExecutionState, actor: Actor, correlationId?: string): { events: EventEnvelope[] } {
  ensureNotTerminalExecution(s);
  ensureCancelNotRequested(s);
  return { events: [mkEvent(s.executionId, "EXECUTION_STARTED", actor, {}, correlationId)] };
}

export function cmdCancelExecution(
  s: ExecutionState,
  actor: Actor,
  reason?: string,
  correlationId?: string
): { events: EventEnvelope[] } {
  if (["COMPLETED", "FAILED", "CANCELED"].includes(s.status)) return { events: [] };

  const events: EventEnvelope[] = [];
  if (!s.cancelRequestedAt) {
    events.push(mkEvent(s.executionId, "EXECUTION_CANCEL_REQUESTED", actor, { reason: reason ?? null }, correlationId));
  }
  return { events };
}
