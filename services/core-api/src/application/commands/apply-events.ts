/**
 * イベント適用
 * 状態にイベント列を適用して新状態を返す
 */
import { EventEnvelope } from "../../domain/value-objects/event-envelope.js";
import { ExecutionState } from "../../domain/value-objects/execution-state.js";
import { reduce } from "../../domain/domain-services/reducer.js";

export function applyEvents(s: ExecutionState, events: EventEnvelope[]): ExecutionState {
  return events.reduce((acc, e) => reduce(acc, e), s);
}
