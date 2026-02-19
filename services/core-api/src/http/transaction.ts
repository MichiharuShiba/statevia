import { PoolClient } from "pg";
import { pool } from "../store/db.js";
import { appendEventsTx } from "../store/eventStore.js";
import { upsertExecutionStateTx, upsertNodeStatesTx } from "../store/projections.js";
import { ExecutionState, EventEnvelope } from "../core/types.js";
import { applyEvents } from "../core/commands.js";

export async function executeCommandTx(args: {
  initialState: ExecutionState;
  commandFn: (state: ExecutionState) => { events: EventEnvelope[] };
}): Promise<ExecutionState> {
  const { events } = args.commandFn(args.initialState);
  const newState = applyEvents(args.initialState, events);

  const client = await pool.connect();
  try {
    await client.query("begin");
    await appendEventsTx(client, events);
    await upsertExecutionStateTx(client, newState);
    await upsertNodeStatesTx(client, newState);
    await client.query("commit");
  } catch (error) {
    await client.query("rollback");
    throw error;
  } finally {
    client.release();
  }

  return newState;
}
