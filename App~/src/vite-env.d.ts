/// <reference types="vite/client" />

// Ambient declarations for non-TS imports. Lets us `import "./styles.css"`
// or `import logo from "./logo.svg"` without TypeScript complaining.

declare module "*.css";
declare module "*.scss";

declare module "*.svg" {
  const content: string;
  export default content;
}

declare module "*.png" {
  const content: string;
  export default content;
}

declare module "*.jpg" {
  const content: string;
  export default content;
}