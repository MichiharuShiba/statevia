import { pool } from "../store/db.js";
import { appendEventsTx } from "../store/eventStore.js";
import { upsertExecutionStateTx, upsertNodeStatesTx } from "../store/projections.js";
import { ExecutionState, EventEnvelope } from "../core/types.js";
import { applyEvents } from "../core/commands.js";
import { loadExecutionStateTx } from "../store/load.js";

export async function executeCommandTx(args: {
  executionId: string;
  commandFn: (state: ExecutionState) => { events: EventEnvelope[] };
}): Promise<ExecutionState> {
  const client = await pool.connect();
  try {
    await client.query("begin");
    
    // FOR UPDATE でロックを取得してから状態を読み込む
    const initialState = await loadExecutionStateTx(client, args.executionId);
    
    const { events } = args.commandFn(initialState);
    const newState = applyEvents(initialState, events);

    await appendEventsTx(client, events);
    await upsertExecutionStateTx(client, newState);
    await upsertNodeStatesTx(client, newState);
    await client.query("commit");
    
    return newState;
  } catch (error) {
    await client.query("rollback");
    throw error;
  } finally {
    client.release();
  }
}
