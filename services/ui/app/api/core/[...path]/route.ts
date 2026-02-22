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

async function forward(req: NextRequest, method: string, pathParts: string[]) {
  const url = joinUrl(pathParts);
  const headers: Record<string, string> = {
    "Content-Type": "application/json",
    "X-Idempotency-Key": req.headers.get("x-idempotency-key") ?? crypto.randomUUID(),
    "X-Correlation-Id": req.headers.get("x-correlation-id") ?? crypto.randomUUID(),
    "X-Actor-Kind": req.headers.get("x-actor-kind") ?? "user",
    "X-Actor-Id": req.headers.get("x-actor-id") ?? "ui"
  };

  let body: string | undefined = undefined;
  if (method !== "GET") {
    const text = await req.text();
    body = text && text.length > 0 ? text : "{}";
  }

  const r = await fetch(url, { method, headers, body, cache: "no-store" });
  const contentType = r.headers.get("content-type") ?? "application/json";
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
