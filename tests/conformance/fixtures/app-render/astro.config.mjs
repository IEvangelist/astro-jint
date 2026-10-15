import { defineConfig } from 'astro/config';
import jint from 'astro-adapter-jint';
import { withVariant } from '../../support/variant.mjs';

// The pages and the endpoints that the rendering unit tests of Astro under packages/astro/test/units/render build inline
// (compiled components and the runtime helpers of astro/runtime/server), as real files. Two builds: the default one, and one
// with `site` and `base` (the astro-global tests). `inlineStylesheets: 'never'` gives the pages with a <style> a stylesheet link.
export default defineConfig(
  withVariant({
    output: 'server',
    adapter: jint(),
    build: { inlineStylesheets: 'never' },
  }),
);
