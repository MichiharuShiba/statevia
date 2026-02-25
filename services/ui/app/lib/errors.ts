import type { ApiError } from "./types";

export type ToastState = {
  tone: "success" | "error" | "info";
  message: string;
};

function isApiError(value: unknown): value is ApiError {
  return (
    typeof value === "object" &&
    value !== null &&
    "error" in value &&
    typeof (value as ApiError).error === "object" &&
    (value as ApiError).error !== null
  );
}

export function toToastError(error: unknown): ToastState {
  const status = isApiError(error) ? error.status : undefined;
  const code = isApiError(error) ? error.error?.code ?? "UNKNOWN" : "UNKNOWN";
  const message = isApiError(error) ? error.error?.message ?? "Unknown error" : "Unknown error";

  if (status === 401) {
    return { tone: "error", message: `401 認証が必要です: ${message}` };
  }
  if (status === 403) {
    return { tone: "error", message: `403 権限不足またはテナント未指定: ${message}` };
  }
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
