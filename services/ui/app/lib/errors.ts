import type { ApiError } from "./types";
import { DEFAULT_LOCALE, type Locale } from "./i18n";
import { getUiText } from "./uiTextLocale";

export type ToastState = {
  tone: "success" | "error" | "info";
  message: string;
};

function isApiError(value: unknown): value is ApiError {
  return (
    typeof value === "object" &&
    value !== null &&
    "error" in value &&
    typeof (value as ApiError).error === "object" &&
    (value as ApiError).error !== null
  );
}

export function toToastError(error: unknown, locale: Locale = DEFAULT_LOCALE): ToastState {
  const uiText = getUiText(locale);
  const status = isApiError(error) ? error.status : undefined;
  const code = isApiError(error) ? error.error?.code ?? "UNKNOWN" : "UNKNOWN";
  const message = isApiError(error) ? error.error?.message ?? "Unknown error" : "Unknown error";

  if (status === 401) {
    return { tone: "error", message: `${uiText.errorPrefixes.unauthorized401}: ${message}` };
  }
  if (status === 403) {
    return { tone: "error", message: `${uiText.errorPrefixes.forbidden403}: ${message}` };
  }
  if (status === 409) {
    return { tone: "error", message: `${uiText.errorPrefixes.conflict409}: ${code} - ${message}` };
  }
  if (status === 422) {
    return { tone: "error", message: `${uiText.errorPrefixes.unprocessable422}: ${code} - ${message}` };
  }
  if (status === 500) {
    return { tone: "error", message: `${uiText.errorPrefixes.server500}: ${code} - ${message}` };
  }
  return { tone: "error", message: `${code}: ${message}` };
}
