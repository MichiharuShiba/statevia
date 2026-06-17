import { NextRequest, NextResponse } from "next/server";
import { AUTH_COOKIE_ACCESS, AUTH_COOKIE_TENANT_KEY } from "../../../lib/authSession";

function base() {
  const b = process.env.CORE_API_INTERNAL_BASE;
  if (!b) throw new Error("Missing CORE_API_INTERNAL_BASE");
  return b.replace(/\/$/, "");
}

function joinUrl(parts: string[]) {
  const p = parts.map(encodeURIComponent).join("/");
  return `${base()}/${p}`;
}

/** v2: UI は `/executions` のみ。Core-API の `/v1/executions` に転送する。 */
function pathForBackend(pathParts: string[]): string[] {
  if (pathParts[0] === "executions") return ["v1", "executions", ...pathParts.slice(1)];
  if (pathParts[0] === "definitions") return ["v1", "definitions", ...pathParts.slice(1)];
  if (pathParts[0] === "actions") return ["v1", "actions", ...pathParts.slice(1)];
  if (pathParts[0] === "graphs") return ["v1", "graphs", ...pathParts.slice(1)];
  if (pathParts[0] === "admin") return ["v1", "admin", ...pathParts.slice(1)];
  if (pathParts[0] === "auth") return ["v1", "auth", ...pathParts.slice(1)];
  return pathParts;
}

function authAndTenantHeaders(req: NextRequest, pathParts: string[]): Record<string, string> {
  const out: Record<string, string> = {};
  const tokenFromCookie = req.cookies.get(AUTH_COOKIE_ACCESS)?.value?.trim();
  const tenantFromCookie = req.cookies.get(AUTH_COOKIE_TENANT_KEY)?.value?.trim();
  const authFromReq = req.headers.get("authorization");
  const tenantFromReq = req.headers.get("x-tenant-id");
  const tenantFromEnv = process.env.CORE_API_TENANT_ID;
  const authFromEnv = process.env.CORE_API_AUTH_TOKEN;

  if (tokenFromCookie) {
    out["Authorization"] = `Bearer ${tokenFromCookie}`;
  } else if (authFromReq) {
    out["Authorization"] = authFromReq;
  } else if (authFromEnv) {
    out["Authorization"] = authFromEnv.startsWith("Bearer ") ? authFromEnv : `Bearer ${authFromEnv}`;
  }

  if (tenantFromCookie) {
    out["X-Tenant-Id"] = tenantFromCookie;
  } else if (tenantFromReq) {
    out["X-Tenant-Id"] = tenantFromReq;
  } else if (tenantFromEnv) {
    out["X-Tenant-Id"] = tenantFromEnv;
  }

  const isStream = pathParts.at(-1) === "stream";
  if (isStream) {
    const tenantFromQuery = req.nextUrl.searchParams.get("tenantId");
    if (tenantFromQuery && !out["X-Tenant-Id"]) {
      out["X-Tenant-Id"] = tenantFromQuery;
    }
  }

  return out;
}

/**
 * Core-API への転送先 URL を組み立てる。
 * `limit` / `offset` / `name` / `status` 等はクエリに載るため、`req.nextUrl.search` を必ず付与する。
 */
function buildBackendUrl(req: NextRequest, pathParts: string[]): string {
  const backendPath = pathForBackend(pathParts);
  const baseUrl = joinUrl(backendPath);
  return `${baseUrl}${req.nextUrl.search}`;
}

async function forward(req: NextRequest, method: string, pathParts: string[]) {
  const url = buildBackendUrl(req, pathParts);
  const headers: Record<string, string> = {
    Accept: req.headers.get("accept") ?? "application/json",
    "X-Idempotency-Key": req.headers.get("x-idempotency-key") ?? crypto.randomUUID(),
    "X-Correlation-Id": req.headers.get("x-correlation-id") ?? crypto.randomUUID(),
    "X-Actor-Kind": req.headers.get("x-actor-kind") ?? "user",
    "X-Actor-Id": req.headers.get("x-actor-id") ?? "ui"
  };

  for (const [k, v] of Object.entries(authAndTenantHeaders(req, pathParts))) {
    headers[k] = v;
  }

  let body: string | undefined = undefined;
  if (method !== "GET") {
    headers["Content-Type"] = "application/json";
    const text = await req.text();
    body = text && text.length > 0 ? text : "{}";
  }

  const r = await fetch(url, { method, headers, body, cache: "no-store" });
  const contentType = r.headers.get("content-type") ?? "application/json";

  if (contentType.includes("text/event-stream") && r.body) {
    return new NextResponse(r.body, {
      status: r.status,
      headers: {
        "Content-Type": contentType,
        "Cache-Control": "no-cache, no-transform",
        Connection: "keep-alive"
      }
    });
  }

  const outText = await r.text();

  if (r.status === 401) {
    const unauthorized = new NextResponse(outText, {
      status: 401,
      headers: { "Content-Type": contentType }
    });
    unauthorized.cookies.delete(AUTH_COOKIE_ACCESS);
    unauthorized.cookies.delete(AUTH_COOKIE_TENANT_KEY);
    return unauthorized;
  }

  // 204 / 205 / 304 は本文を持てない。本文付き NextResponse は TypeError になる（Undici/Fetch 準拠）。
  if (r.status === 204 || r.status === 205 || r.status === 304) {
    const headers: Record<string, string> = {};
    const ct = r.headers.get("content-type");
    if (ct) headers["Content-Type"] = ct;
    return new NextResponse(null, { status: r.status, headers });
  }

  return new NextResponse(outText, {
    status: r.status,
    headers: { "Content-Type": contentType }
  });
}

/** Core-API への GET プロキシ（Route Handler）。 */
export async function GET(
  req: NextRequest,
  { params }: { params: Promise<{ path: string[] }> }
) {
  const { path } = await params;
  return forward(req, "GET", path);
}
/** Core-API への POST プロキシ（Route Handler）。 */
export async function POST(
  req: NextRequest,
  { params }: { params: Promise<{ path: string[] }> }
) {
  const { path } = await params;
  return forward(req, "POST", path);
}
/** Core-API への PUT プロキシ（Route Handler）。 */
export async function PUT(
  req: NextRequest,
  { params }: { params: Promise<{ path: string[] }> }
) {
  const { path } = await params;
  return forward(req, "PUT", path);
}
/** Core-API への PATCH プロキシ（Route Handler）。 */
export async function PATCH(
  req: NextRequest,
  { params }: { params: Promise<{ path: string[] }> }
) {
  const { path } = await params;
  return forward(req, "PATCH", path);
}
/** Core-API への DELETE プロキシ（Route Handler）。 */
export async function DELETE(
  req: NextRequest,
  { params }: { params: Promise<{ path: string[] }> }
) {
  const { path } = await params;
  return forward(req, "DELETE", path);
}
