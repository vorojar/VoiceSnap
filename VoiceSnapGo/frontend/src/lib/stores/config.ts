import { writable } from "svelte/store";

export const autoHide = writable<boolean>(true);
export const soundFeedback = writable<boolean>(true);
export const startAtLogin = writable<boolean>(false);
export const hotkeyVK = writable<number>(0xa3);
