import type { ApiError } from "./types";

function idem() {
  return crypto.randomUUID();
}

async function parseJsonSafe(res: Response) {
  const t = await res.text();
  try {
    return t ? JSON.parse(t) : null;
  } catch {
    return t;
  }
}

export async function apiGet<T>(path: string): Promise<T> {
  const res = await fetch(`/api/core${path}`, { cache: "no-store" });
  const json = await parseJsonSafe(res);
  if (!res.ok) {
    const fallback: ApiError = {
      status: res.status,
      error: {
        code: `HTTP_${res.status}`,
        message: typeof json === "string" ? json : res.statusText
      }
    };
    if (json && typeof json === "object" && "error" in (json as Record<string, unknown>)) {
      throw { status: res.status, ...(json as object) } as ApiError;
    }
    throw fallback;
  }
  return json as T;
}

export async function apiPost<T>(path: string, body: unknown): Promise<T> {
  const res = await fetch(`/api/core${path}`, {
    method: "POST",
    headers: {
      "Content-Type": "application/json",
      "X-Idempotency-Key": idem()
    },
    body: JSON.stringify(body ?? {})
  });
  const json = await parseJsonSafe(res);
  if (!res.ok) {
    const fallback: ApiError = {
      status: res.status,
      error: {
        code: `HTTP_${res.status}`,
        message: typeof json === "string" ? json : res.statusText
      }
    };
    if (json && typeof json === "object" && "error" in (json as Record<string, unknown>)) {
      throw { status: res.status, ...(json as object) } as ApiError;
    }
    throw fallback;
  }
  return json as T;
}
