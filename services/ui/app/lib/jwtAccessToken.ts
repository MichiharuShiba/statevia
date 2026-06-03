/**
 * JWT compact 形式（`header.payload.signature`）の構造化表現。
 * 署名検証は行わない（ミドルウェア等での exp 参照用）。
 */

/** JWT compact 形式のセグメント種別。 */
export type JwtSegmentKind = "header" | "payload" | "signature";

const JWT_SEGMENT_KINDS: readonly JwtSegmentKind[] = ["header", "payload", "signature"];

type JwtEncodedParts = Record<JwtSegmentKind, string>;

/**
 * JWT compact 形式の1セグメント（Base64URL）。
 */
export class JwtSegment {
  /** セグメントの役割。 */
  readonly kind: JwtSegmentKind;
  /** compact 形式上のエンコード文字列。 */
  readonly encoded: string;

  /**
   * @param kind セグメント種別
   * @param encoded Base64URL 文字列
   */
  constructor(kind: JwtSegmentKind, encoded: string) {
    this.kind = kind;
    this.encoded = encoded;
  }

  /**
   * UTF-8 文字列へデコードする。
   * @throws 不正な Base64URL のとき
   */
  decodeToUtf8(): string {
    return decodeBase64Url(this.encoded);
  }

  /**
   * ヘッダーまたはペイロードを JSON としてデコードする。
   * @throws 署名セグメント、またはデコード・JSON 解析に失敗したとき
   */
  decodeToJson(): unknown {
    switch (this.kind) {
      case "header":
      case "payload":
        return JSON.parse(this.decodeToUtf8());
      case "signature":
        throw new Error("JWT signature segment is not JSON");
    }
  }

  /**
   * Base64URL としてデコード可能か（構造検証用）。
   */
  static isValidBase64Url(encoded: string): boolean {
    try {
      decodeBase64Url(encoded);
      return true;
    } catch {
      return false;
    }
  }

  /**
   * JSON を載せるセグメント（ヘッダー／ペイロード）を構築する。不正なときは `null`。
   */
  static fromJsonKind(kind: "header" | "payload", encoded: string): JwtSegment | null {
    if (!JwtSegment.isValidBase64Url(encoded)) {
      return null;
    }
    const segment = new JwtSegment(kind, encoded);
    try {
      segment.decodeToJson();
      return segment;
    } catch {
      return null;
    }
  }

  /**
   * 署名セグメントを構築する。不正な Base64URL のときは `null`。
   */
  static fromSignature(encoded: string): JwtSegment | null {
    if (!JwtSegment.isValidBase64Url(encoded)) {
      return null;
    }
    return new JwtSegment("signature", encoded);
  }
}

/**
 * アクセストークン等で使う JWT（compact 3 部構成）。
 */
export class JwtAccessToken {
  /** JWT ヘッダー（alg, typ 等）。 */
  readonly header: JwtSegment;
  /** JWT ペイロード（exp 等のクレーム）。 */
  readonly payload: JwtSegment;
  /** 署名セグメント（検証は行わない）。 */
  readonly signature: JwtSegment;
  /** 元の compact 文字列。 */
  readonly compact: string;

  private constructor(
    header: JwtSegment,
    payload: JwtSegment,
    signature: JwtSegment,
    compact: string
  ) {
    this.header = header;
    this.payload = payload;
    this.signature = signature;
    this.compact = compact;
  }

  /**
   * compact 文字列から JWT を構築する。形式不正のときは `null`。
   * @param accessToken `header.payload.signature` 形式の文字列
   */
  static parse(accessToken: string | undefined | null): JwtAccessToken | null {
    const compact = accessToken?.trim();
    if (!compact) {
      return null;
    }

    const encodedParts = splitCompactJwt(compact);
    if (!encodedParts) {
      return null;
    }

    const header = JwtSegment.fromJsonKind("header", encodedParts.header);
    const payload = JwtSegment.fromJsonKind("payload", encodedParts.payload);
    const signature = JwtSegment.fromSignature(encodedParts.signature);
    if (!header || !payload || !signature) {
      return null;
    }

    return new JwtAccessToken(header, payload, signature, compact);
  }

  /**
   * ペイロードの `exp`（Unix 秒）を読む。
   */
  readExpiryUnixSeconds(): number | null {
    try {
      return readExpClaimUnixSeconds(this.payload.decodeToJson());
    } catch {
      return null;
    }
  }
}

/**
 * compact 文字列を 3 セグメントに分解する。
 */
function splitCompactJwt(compact: string): JwtEncodedParts | null {
  const rawParts = compact.split(".");
  if (rawParts.length !== JWT_SEGMENT_KINDS.length) {
    return null;
  }
  if (rawParts.some((part) => part.length === 0)) {
    return null;
  }

  const [headerEncoded, payloadEncoded, signatureEncoded] = rawParts;
  return {
    header: headerEncoded,
    payload: payloadEncoded,
    signature: signatureEncoded
  };
}

function readExpClaimUnixSeconds(claims: unknown): number | null {
  if (typeof claims !== "object" || claims === null) {
    return null;
  }
  const record = claims as Record<string, unknown>;
  const exp = record.exp;
  return typeof exp === "number" && Number.isFinite(exp) ? exp : null;
}

function decodeBase64Url(segment: string): string {
  const base64 = segment.replaceAll("-", "+").replaceAll("_", "/");
  const padLen = (4 - (base64.length % 4)) % 4;
  return atob(base64 + "=".repeat(padLen));
}
