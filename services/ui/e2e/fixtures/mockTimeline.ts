/**
 * 実行履歴タイムライン・リプレイ機能の e2e 用テストデータ
 */
import type { ExecutionEventWithSeq, WorkflowView } from "../../app/lib/types";
import { mockExecution } from "./mockExecution";

const EXECUTION_ID = "ex-1";
const at = (iso: string) => iso;

/** タイムライン用: 1ページ目（seq 1-3）。続きを読み込むで2ページ目を返す想定 */
export const timelineEventsPage1: ExecutionEventWithSeq[] = [
  {
    seq: 1,
    type: "GraphUpdated",
    executionId: EXECUTION_ID,
    patch: {
      nodes: [
        { nodeId: "start", nodeType: "Start", status: "IDLE" },
        { nodeId: "task-a", nodeType: "Task", status: "IDLE" },
      ],
    },
    at: at("2026-02-28T10:00:00.000Z"),
  },
  {
    seq: 2,
    type: "ExecutionStatusChanged",
    executionId: EXECUTION_ID,
    to: "ACTIVE",
    at: at("2026-02-28T10:00:01.000Z"),
  },
  {
    seq: 3,
    type: "GraphUpdated",
    executionId: EXECUTION_ID,
    patch: {
      nodes: [
        { nodeId: "start", nodeType: "Start", status: "READY" },
      ],
    },
    at: at("2026-02-28T10:00:02.000Z"),
  },
];

/** タイムライン用: 2ページ目（seq 4-5）。afterSeq=3 で取得 */
export const timelineEventsPage2: ExecutionEventWithSeq[] = [
  {
    seq: 4,
    type: "GraphUpdated",
    executionId: EXECUTION_ID,
    patch: {
      nodes: [
        { nodeId: "start", nodeType: "Start", status: "SUCCEEDED" },
        { nodeId: "task-a", nodeType: "Task", status: "RUNNING", attempt: 1 },
      ],
    },
    at: at("2026-02-28T10:00:03.000Z"),
  },
  {
    seq: 5,
    type: "GraphUpdated",
    executionId: EXECUTION_ID,
    patch: {
      nodes: [
        { nodeId: "task-a", nodeType: "Task", status: "SUCCEEDED" },
      ],
    },
    at: at("2026-02-28T10:00:04.000Z"),
  },
];

/** リプレイ用: seq 1 時点の状態（ノード少なめ・IDLE） */
export const stateAtSeq1: WorkflowView = {
  ...mockExecution,
  displayId: EXECUTION_ID,
  nodes: [
    { nodeId: "start", nodeType: "Start", status: "IDLE", attempt: 0, workerId: null, waitKey: null, canceledByExecution: false },
    { nodeId: "task-a", nodeType: "Task", status: "IDLE", attempt: 0, workerId: null, waitKey: null, canceledByExecution: false },
  ],
};

/** リプレイ用: seq 2 時点の状態 */
export const stateAtSeq2: WorkflowView = {
  ...stateAtSeq1,
  nodes: [
    { nodeId: "start", nodeType: "Start", status: "IDLE", attempt: 0, workerId: null, waitKey: null, canceledByExecution: false },
    { nodeId: "task-a", nodeType: "Task", status: "IDLE", attempt: 0, workerId: null, waitKey: null, canceledByExecution: false },
  ],
};

/** リプレイ用: seq 3 時点の状態（start が READY） */
export const stateAtSeq3: WorkflowView = {
  ...stateAtSeq1,
  nodes: [
    { nodeId: "start", nodeType: "Start", status: "READY", attempt: 0, workerId: null, waitKey: null, canceledByExecution: false },
    { nodeId: "task-a", nodeType: "Task", status: "IDLE", attempt: 0, workerId: null, waitKey: null, canceledByExecution: false },
  ],
};

/** リプレイ用: seq 4 時点の状態（start SUCCEEDED, task-a RUNNING） */
export const stateAtSeq4: WorkflowView = {
  ...stateAtSeq1,
  nodes: [
    { nodeId: "start", nodeType: "Start", status: "SUCCEEDED", attempt: 1, workerId: null, waitKey: null, canceledByExecution: false },
    { nodeId: "task-a", nodeType: "Task", status: "RUNNING", attempt: 1, workerId: "w1", waitKey: null, canceledByExecution: false },
  ],
};

/** seq に応じて返すリプレイ用状態 */
export function getStateAtSeq(seq: number): WorkflowView {
  switch (seq) {
    case 1:
      return stateAtSeq1;
    case 2:
      return stateAtSeq2;
    case 3:
      return stateAtSeq3;
    case 4:
      return stateAtSeq4;
    default:
      return mockExecution;
  }
}
