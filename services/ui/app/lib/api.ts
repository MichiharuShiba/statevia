import type { ApiError } from "./types";

function idem(): string {
  return crypto.randomUUID();
}

async function parseJsonSafe(res: Response): Promise<unknown> {
  const t = await res.text();
  try {
    return t ? JSON.parse(t) : null;
  } catch {
    // Return raw text when response is not valid JSON (e.g. plain error message)
    return t;
  }
}

function getErrorMessage(res: Response, json: unknown): string {
  if (typeof json === "object" && json !== null && "error" in json && typeof (json as ApiError).error?.message === "string") {
    return (json as ApiError).error.message;
  }
  if (typeof json === "string") return json;
  return res.statusText;
}

function getErrorCode(res: Response, json: unknown): string {
  if (typeof json === "object" && json !== null && "error" in json && typeof (json as ApiError).error?.code === "string") {
    return (json as ApiError).error.code;
  }
  return `HTTP_${res.status}`;
}

function buildApiError(res: Response, json: unknown): ApiError {
  return {
    status: res.status,
    error: { code: getErrorCode(res, json), message: getErrorMessage(res, json) }
  };
}

async function fetchAndParse<T>(path: string, init?: RequestInit): Promise<T> {
  const res = await fetch(`/api/core${path}`, { cache: "no-store", ...init });
  const json = await parseJsonSafe(res);
  if (!res.ok) {
    throw buildApiError(res, json);
  }
  return json as T;
}

export async function apiGet<T>(path: string): Promise<T> {
  return fetchAndParse<T>(path);
}

export async function apiPost<T>(path: string, body: unknown): Promise<T> {
  return fetchAndParse<T>(path, {
    method: "POST",
    headers: {
      "Content-Type": "application/json",
      "X-Idempotency-Key": idem()
    },
    body: JSON.stringify(body ?? {})
  });
}
