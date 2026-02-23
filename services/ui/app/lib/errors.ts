import type { ApiError } from "./types";

export type ToastState = {
  tone: "success" | "error" | "info";
  message: string;
};

export function toToastError(error: unknown): ToastState {
  const apiError = error as ApiError;
  const status = apiError?.status;
  const code = apiError?.error?.code ?? "UNKNOWN";
  const message = apiError?.error?.message ?? "Unknown error";

  if (status === 409) {
    return { tone: "error", message: `409 状態競合: ${code} - ${message}` };
  }
  if (status === 422) {
    return { tone: "error", message: `422 入力不正: ${code} - ${message}` };
  }
  if (status === 500) {
    return { tone: "error", message: `500 サーバーエラー: ${code} - ${message}` };
  }
  return { tone: "error", message: `${code}: ${message}` };
}
