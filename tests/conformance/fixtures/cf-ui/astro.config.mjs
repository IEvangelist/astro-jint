import mdx from '@astrojs/mdx';
import react from '@astrojs/react';
import solidJs from '@astrojs/solid-js';
import svelte from '@astrojs/svelte';
import vue from '@astrojs/vue';
import tailwindcss from '@tailwindcss/vite';
import { defineConfig } from 'astro/config';
import jint from 'astro-adapter-jint';
import { withVariant } from '../../support/variant.mjs';

// The UI-framework fixtures of the upstream Cloudflare tests (with-react, with-solid-js, with-svelte, with-vue,
// prerender-styles, server-island-prerender-framework) in one site, so that they are built once. React and Solid both
// compile .tsx, so each one only takes the components in its own folder.
export default defineConfig(
  withVariant({
    output: 'server',
    adapter: jint(),
    integrations: [react({ include: ['**/react/*'] }), solidJs({ include: ['**/solid/*'] }), svelte(), vue(), mdx()],
    vite: {
      plugins: [tailwindcss()],
    },
  }),
);
