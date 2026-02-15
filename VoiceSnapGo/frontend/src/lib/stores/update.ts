import { writable } from "svelte/store";

export interface UpdateInfo {
  version: string;
  releaseNotes: string;
  downloadUrl: string;
}

export const updateAvailable = writable<UpdateInfo | null>(null);
export const updateDownloading = writable<boolean>(false);
