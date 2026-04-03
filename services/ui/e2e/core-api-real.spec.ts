import { test, expect } from "@playwright/test";
import { e2eWaitWorkflowYaml } from "./fixtures/e2eWaitWorkflowYaml";

/**
 * Core-API を直接叩くオプション E2E（STV-401/402）。
 * 実行例:
 *   CORE_API_E2E_URL=http://localhost:8080 npx playwright test e2e/core-api-real.spec.ts
 */
const apiBase = process.env.CORE_API_E2E_URL?.replace(/\/$/, "") ?? "";

const tenantHeaders = { "X-Tenant-Id": "default", "Content-Type": "application/json" };

async function waitForWorkflowStatus(
  request: import("@playwright/test").APIRequestContext,
  displayId: string,
  status: string,
  timeoutMs: number
): Promise<void> {
  const deadline = Date.now() + timeoutMs;
  while (Date.now() < deadline) {
    const res = await request.get(`${apiBase}/v1/workflows/${encodeURIComponent(displayId)}`, {
      headers: { "X-Tenant-Id": "default" }
    });
    if (res.ok()) {
      const j = (await res.json()) as { status?: string };
      if (j.status === status) {
        return;
      }
    }
    await new Promise((r) => setTimeout(r, 200));
  }
  throw new Error(`timeout waiting for workflow status ${status}`);
}

test.describe("Core API (direct)", () => {
  test.describe.configure({ mode: "serial" });
  test.skip(!apiBase, "CORE_API_E2E_URL が未設定のためスキップ（例: http://localhost:8080）");

  test("GET /v1/health は ok を返す", async ({ request }) => {
    const res = await request.get(`${apiBase}/v1/health`);
    expect(res.ok()).toBeTruthy();
    const json = (await res.json()) as { status?: string };
    expect(json.status).toBe("ok");
  });

  test("GET /v1/workflows は 200（テナント default）", async ({ request }) => {
    const res = await request.get(`${apiBase}/v1/workflows`, {
      headers: { "X-Tenant-Id": "default" }
    });
    expect(res.status()).toBe(200);
  });

  test("STV-401: ワークフロー開始 → Cancel → Cancelled", async ({ request }) => {
    const suffix = `${Date.now()}-${Math.random().toString(16).slice(2)}`;
    const defName = `e2e-wait-${suffix}`;
    const yaml = e2eWaitWorkflowYaml(defName);

    const createDef = await request.post(`${apiBase}/v1/definitions`, {
      headers: tenantHeaders,
      data: { name: defName, yaml }
    });
    expect(createDef.status(), await createDef.text()).toBe(201);
    const defJson = (await createDef.json()) as { displayId: string };

    const start = await request.post(`${apiBase}/v1/workflows`, {
      headers: tenantHeaders,
      data: { definitionId: defJson.displayId, input: {} }
    });
    expect(start.status(), await start.text()).toBe(201);
    const wf = (await start.json()) as { displayId: string; status: string };
    expect(wf.status).toBe("Running");

    const cancel = await request.post(`${apiBase}/v1/workflows/${encodeURIComponent(wf.displayId)}/cancel`, {
      headers: tenantHeaders,
      data: { reason: "e2e" }
    });
    expect(cancel.status()).toBe(204);

    await waitForWorkflowStatus(request, wf.displayId, "Cancelled", 15_000);
  });

  test("STV-402: 同一 X-Idempotency-Key + 同一ボディの再送は同一ワークフローを返す", async ({ request }) => {
    const suffix = `${Date.now()}-${Math.random().toString(16).slice(2)}`;
    const defName = `e2e-idem-${suffix}`;
    const yaml = e2eWaitWorkflowYaml(defName);

    const createDef = await request.post(`${apiBase}/v1/definitions`, {
      headers: tenantHeaders,
      data: { name: defName, yaml }
    });
    expect(createDef.ok()).toBeTruthy();
    const defJson = (await createDef.json()) as { displayId: string };

    const idemKey = `idem-${suffix}`;
    const body = { definitionId: defJson.displayId, input: { k: 1 } };

    const start1 = await request.post(`${apiBase}/v1/workflows`, {
      headers: { ...tenantHeaders, "X-Idempotency-Key": idemKey },
      data: body
    });
    expect(start1.status(), await start1.text()).toBe(201);
    const w1 = (await start1.json()) as { displayId: string; resourceId: string };

    const start2 = await request.post(`${apiBase}/v1/workflows`, {
      headers: { ...tenantHeaders, "X-Idempotency-Key": idemKey },
      data: body
    });
    expect(start2.status(), await start2.text()).toBe(201);
    const w2 = (await start2.json()) as { displayId: string; resourceId: string };

    expect(w2.displayId).toBe(w1.displayId);
    expect(w2.resourceId).toBe(w1.resourceId);
  });

  test("STV-402: 同一 X-Idempotency-Key でボディが異なると 409", async ({ request }) => {
    const suffix = `${Date.now()}-${Math.random().toString(16).slice(2)}`;
    const defNameA = `e2e-idem-a-${suffix}`;
    const defNameB = `e2e-idem-b-${suffix}`;
    const yamlA = e2eWaitWorkflowYaml(defNameA);
    const yamlB = e2eWaitWorkflowYaml(defNameB);

    const createA = await request.post(`${apiBase}/v1/definitions`, {
      headers: tenantHeaders,
      data: { name: defNameA, yaml: yamlA }
    });
    const createB = await request.post(`${apiBase}/v1/definitions`, {
      headers: tenantHeaders,
      data: { name: defNameB, yaml: yamlB }
    });
    expect(createA.ok()).toBeTruthy();
    expect(createB.ok()).toBeTruthy();
    const defA = (await createA.json()) as { displayId: string };
    const defB = (await createB.json()) as { displayId: string };

    const idemKey = `idem-conflict-${suffix}`;

    const start1 = await request.post(`${apiBase}/v1/workflows`, {
      headers: { ...tenantHeaders, "X-Idempotency-Key": idemKey },
      data: { definitionId: defA.displayId, input: {} }
    });
    expect(start1.status()).toBe(201);

    const start2 = await request.post(`${apiBase}/v1/workflows`, {
      headers: { ...tenantHeaders, "X-Idempotency-Key": idemKey },
      data: { definitionId: defB.displayId, input: {} }
    });
    expect(start2.status()).toBe(409);
    const err = (await start2.json()) as { error?: { code?: string } };
    expect(err.error?.code).toBe("IDEMPOTENCY_KEY_CONFLICT");
  });
});
