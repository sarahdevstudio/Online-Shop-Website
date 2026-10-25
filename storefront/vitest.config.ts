import { fileURLToPath } from 'node:url';
import { defineConfig } from 'vitest/config';

// Same `@/` alias as tsconfig, so tests can import app modules that use it.
export default defineConfig({
  resolve: { alias: { '@': fileURLToPath(new URL('./src', import.meta.url)) } },
});
