import { defineConfig } from 'astro/config';
import jint from 'astro-adapter-jint';
import { withVariant } from '../../support/variant.mjs';

// Reproduction of GAP-CF-1: a page that imports a .sql file fails the build, so it cannot share the fixture cf-pages.
export default defineConfig(withVariant({
  output: 'server',
  adapter: jint(),
}));
