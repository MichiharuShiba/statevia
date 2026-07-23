import { describe, expect, it } from "vitest";
import { FEATURES_SCAFFOLD } from "@/features/scaffold";
import { SHARED_SCAFFOLD } from "@/shared/scaffold";

describe("Phase 0 path aliases", () => {
  it("resolves @/features and @/shared", () => {
    // Arrange / Act / Assert
    expect(FEATURES_SCAFFOLD).toBe(true);
    expect(SHARED_SCAFFOLD).toBe(true);
  });
});
