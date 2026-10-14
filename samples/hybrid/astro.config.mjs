import { defineConfig } from 'astro/config';
import jint from 'astro-adapter-jint';

// Output stays 'static': pages are prerendered unless they set `export const prerender = false`.
export default defineConfig({
  adapter: jint(),
});
