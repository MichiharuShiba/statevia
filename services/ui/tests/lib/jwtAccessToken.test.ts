import { describe, expect, it } from "vitest";
import { JwtAccessToken, JwtSegment } from "../../app/lib/jwtAccessToken";
import { testJwt } from "../helpers/testJwt";

function encodeJsonSegment(value: unknown): string {
  return Buffer.from(JSON.stringify(value)).toString("base64url");
}

describe("JwtAccessToken", () => {
  it("compact 文字列を header / payload / signature に分解する", () => {
    const compact = testJwt(1_700_000_000);
    const token = JwtAccessToken.parse(compact);

    expect(token).not.toBeNull();
    expect(token?.header.kind).toBe("header");
    expect(token?.payload.kind).toBe("payload");
    expect(token?.signature.kind).toBe("signature");
    expect(token?.compact).toBe(compact);
    expect(token?.header.decodeToJson()).toEqual({ alg: "HS256", typ: "JWT" });
    expect(token?.payload.decodeToJson()).toEqual({ exp: 1_700_000_000 });
  });

  it("readExpiryUnixSeconds はペイロードの exp を返す", () => {
    const exp = Math.floor(Date.now() / 1000) + 120;
    const token = JwtAccessToken.parse(testJwt(exp));

    expect(token?.readExpiryUnixSeconds()).toBe(exp);
  });

  it("セグメントが3つでない、空セグメント、JSON でないペイロードは null", () => {
    const header = encodeJsonSegment({ alg: "HS256" });
    const signature = "c2ln";

    expect(JwtAccessToken.parse("only-one")).toBeNull();
    expect(JwtAccessToken.parse(`${header}..${signature}`)).toBeNull();
    expect(JwtAccessToken.parse(`${header}.not-json.${signature}`)).toBeNull();
    expect(JwtAccessToken.parse(null)).toBeNull();
  });

  it("JwtSegment.fromJsonKind は不正 Base64URL で null", () => {
    expect(JwtSegment.fromJsonKind("payload", "###")).toBeNull();
  });

  it("exp が無い・非数のペイロードは readExpiryUnixSeconds が null", () => {
    const header = encodeJsonSegment({ alg: "HS256" });
    const payload = encodeJsonSegment({ sub: "user" });
    const signature = Buffer.from("sig").toString("base64url");
    const token = JwtAccessToken.parse(`${header}.${payload}.${signature}`);

    expect(token?.readExpiryUnixSeconds()).toBeNull();
  });

  it("JwtSegment.fromSignature は不正 Base64URL で null", () => {
    expect(JwtSegment.fromSignature("@@")).toBeNull();
  });

  it("署名セグメントの decodeToJson は例外", () => {
    const signature = JwtSegment.fromSignature("c2ln");
    expect(signature?.kind).toBe("signature");
    expect(() => signature?.decodeToJson()).toThrow("JWT signature segment is not JSON");
  });
});
