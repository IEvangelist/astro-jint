import svelte from '@astrojs/svelte';
import { defineConfig } from 'astro/config';
import jint from 'astro-adapter-jint';
import { withVariant } from '../../support/variant.mjs';

export default defineConfig(withVariant({
  output: 'static',
  adapter: jint(),
  integrations: [svelte()],
}));
