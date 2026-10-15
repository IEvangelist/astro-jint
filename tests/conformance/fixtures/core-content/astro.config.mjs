import mdx from '@astrojs/mdx';
import { defineConfig } from 'astro/config';
import jint from 'astro-adapter-jint';
import { withVariant } from '../../support/variant.mjs';

export default defineConfig(withVariant({
  adapter: jint(),
  integrations: [mdx()],
  // The upstream suite was authored when inlineStylesheets defaulted to never.
  build: { inlineStylesheets: 'never' },
}));
