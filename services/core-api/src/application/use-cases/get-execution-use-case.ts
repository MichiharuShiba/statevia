/**
 * GetExecution ユースケース
 * 実行状態の参照（参照用・ロックなし）
 */
import { ExecutionRepository } from "../../infrastructure/persistence/repositories/execution-repository.js";
import { ExecutionState } from "../../domain/value-objects/execution-state.js";

export async function getExecutionUseCase(executionId: string): Promise<ExecutionState | null> {
  return ExecutionRepository.loadOptional(executionId);
}
