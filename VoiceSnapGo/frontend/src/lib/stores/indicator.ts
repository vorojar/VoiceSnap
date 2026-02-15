import { writable } from "svelte/store";

export type IndicatorStatus =
  | "loading"
  | "ready"
  | "recording"
  | "processing"
  | "done"
  | "cancelled"
  | "no_voice"
  | "no_content"
  | "error"
  | "hidden";

export const indicatorVisible = writable<boolean>(false);
export const indicatorStatus = writable<IndicatorStatus>("hidden");
export const indicatorVolume = writable<number>(0);
export const hotkeyName = writable<string>("Ctrl");
