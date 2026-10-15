import react from '@astrojs/react';
import svelte from '@astrojs/svelte';
import { defineConfig } from 'astro/config';
import jint from 'astro-adapter-jint';
import { withVariant } from '../../support/variant.mjs';

// The upstream astro-client-only fixture. Upstream builds it with output 'static', with output 'server' and with a `base`
// and a `site`; the tests here pass these as config variants. The default output is 'static', as in upstream.
export default defineConfig(withVariant({
  adapter: jint(),
  integrations: [svelte(), react()],
  // upstream: test suite was authored when inlineStylesheets defaulted to never
  build: { inlineStylesheets: 'never' },
}));
