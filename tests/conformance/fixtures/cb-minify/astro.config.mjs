import { defineConfig } from 'astro/config';
import jint from 'astro-adapter-jint';
import { withVariant } from '../../support/variant.mjs';

// The upstream fixtures minification-html (compressHTML: true), minification-html-default (compressHTML unset) and
// minification-html-jsx (compressHTML: 'jsx') in one: this config leaves compressHTML unset and the tests pass `compressHTML`
// (and `output: 'server'`) as config variants. Like upstream, the stylesheets are linked and not inlined ("test suite was
// authored when inlineStylesheets defaulted to never").
export default defineConfig(withVariant({
  adapter: jint(),
  build: { inlineStylesheets: 'never' },
}));
