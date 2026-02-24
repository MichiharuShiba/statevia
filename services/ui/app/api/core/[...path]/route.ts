import { NextRequest, NextResponse } from "next/server";

function base() {
  const b = process.env.CORE_API_INTERNAL_BASE;
  if (!b) throw new Error("Missing CORE_API_INTERNAL_BASE");
  return b.replace(/\/$/, "");
}

function joinUrl(parts: string[]) {
  const p = parts.map(encodeURIComponent).join("/");
  return `${base()}/${p}`;
}

function authAndTenantHeaders(req: NextRequest, pathParts: string[]): Record<string, string> {
  const out: Record<string, string> = {};
  const authFromReq = req.headers.get("authorization");
  const tenantFromReq = req.headers.get("x-tenant-id");
  const tenantFromEnv = process.env.CORE_API_TENANT_ID;
  const authFromEnv = process.env.CORE_API_AUTH_TOKEN;

  if (authFromReq) {
    out["Authorization"] = authFromReq;
  } else if (authFromEnv) {
    out["Authorization"] = authFromEnv.startsWith("Bearer ") ? authFromEnv : `Bearer ${authFromEnv}`;
  }

  if (tenantFromReq) {
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

async function forward(req: NextRequest, method: string, pathParts: string[]) {
  const url = joinUrl(pathParts);
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

  return new NextResponse(outText, {
    status: r.status,
    headers: { "Content-Type": contentType }
  });
}

export async function GET(
  req: NextRequest,
  { params }: { params: Promise<{ path: string[] }> }
) {
  const { path } = await params;
  return forward(req, "GET", path);
}
export async function POST(
  req: NextRequest,
  { params }: { params: Promise<{ path: string[] }> }
) {
  const { path } = await params;
  return forward(req, "POST", path);
}
export async function PUT(
  req: NextRequest,
  { params }: { params: Promise<{ path: string[] }> }
) {
  const { path } = await params;
  return forward(req, "PUT", path);
}
export async function DELETE(
  req: NextRequest,
  { params }: { params: Promise<{ path: string[] }> }
) {
  const { path } = await params;
  return forward(req, "DELETE", path);
}
