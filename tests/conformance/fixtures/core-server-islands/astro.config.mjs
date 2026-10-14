import mdx from '@astrojs/mdx';
import svelte from '@astrojs/svelte';
import { defineConfig } from 'astro/config';
import jint from 'astro-adapter-jint';
import { withVariant } from '../../support/variant.mjs';

// The upstream test sets ASTRO_KEY in its own process before building. The build runs in a child
// process here, and Astro bakes the key into the manifest, so the fixture pins it.
process.env.ASTRO_KEY = 'eKBaVEuI7YjfanEXHuJe/pwZKKt3LkAHeMxvTU7aR0M=';

export default defineConfig(withVariant({
  output: 'server',
  adapter: jint(),
  integrations: [svelte(), mdx()],
}));
