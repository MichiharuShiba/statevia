/**
 * Actor 値オブジェクト
 * イベントを発行した主体を表す
 */
export type Actor = {
  kind: "system" | "user" | "scheduler" | "external";
  id?: string;
};
