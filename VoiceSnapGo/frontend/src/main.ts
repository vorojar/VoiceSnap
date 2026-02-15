import App from "./App.svelte";
import { mount } from "svelte";

const app = mount(App, {
  target: document.getElementById("app")!,
});

// Disable browser context menu (right-click)
document.addEventListener("contextmenu", (e) => e.preventDefault());

export default app;
