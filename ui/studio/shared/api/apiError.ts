/**
 * Core-API / BFF のエラー JSON 形状（クライアント側）。
 */
export type ApiError = {
  status?: number;
  error: {
    code: string;
    message: string;
    details?: Record<string, unknown>;
  };
};
