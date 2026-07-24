import { describe, expect, it } from "vitest";
import { buildDefinitionsListPath, getApiConfig } from "@/shared/api";

describe("shared/api path alias", () => {
  it("exports transport helpers from @/shared/api", () => {
    // Arrange / Act
    const path = buildDefinitionsListPath({
      pagination: { limit: 20, offset: 0 },
      sort: {},
    });
    const config = getApiConfig();

    // Assert
    expect(path).toContain("/definitions?");
    expect(config).toEqual(
      expect.objectContaining({
        tenantId: expect.any(String),
        authToken: expect.any(String),
      }),
    );
  });
});
