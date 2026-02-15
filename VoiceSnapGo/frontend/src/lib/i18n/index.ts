import zh from "./zh.json";
import en from "./en.json";

type Locale = "zh" | "en";

const translations: Record<Locale, Record<string, any>> = { zh, en };

// Detect system language, default to Chinese
function detectLocale(): Locale {
  const lang = navigator.language.toLowerCase();
  if (lang.startsWith("zh")) return "zh";
  return "en";
}

let currentLocale: Locale = detectLocale();

export function setLocale(locale: Locale) {
  currentLocale = locale;
}

export function getLocale(): Locale {
  return currentLocale;
}

/**
 * Translate a key path like "status.ready" with optional interpolation.
 * Example: t("indicator.holdToSpeak", { key: "Ctrl" }) => "按住Ctrl说话"
 */
export function t(key: string, params?: Record<string, string>): string {
  const parts = key.split(".");
  let value: any = translations[currentLocale];

  for (const part of parts) {
    if (value == null) return key;
    value = value[part];
  }

  if (typeof value !== "string") return key;

  if (params) {
    for (const [k, v] of Object.entries(params)) {
      value = value.replace(`{${k}}`, v);
    }
  }

  return value;
}
