import { describe, expect, it } from "vitest";
import { getApiConfig } from "@/shared/api";
import { buildDefinitionsListPath } from "@/features/definitions/api";

describe("shared/api path alias", () => {
  it("exports transport helpers from @/shared/api", () => {
    // Arrange / Act
    const config = getApiConfig();

    // Assert
    expect(config).toEqual(
      expect.objectContaining({
        tenantId: expect.any(String),
        authToken: expect.any(String),
      }),
    );
  });
});

describe("features/definitions api path", () => {
  it("buildDefinitionsListPath を @/features/definitions/api から解決できる", () => {
    // Arrange / Act
    const path = buildDefinitionsListPath({
      pagination: { limit: 20, offset: 0 },
      sort: {},
    });

    // Assert
    expect(path).toContain("/definitions?");
  });
});
