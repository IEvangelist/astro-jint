import { defineConfig } from 'astro/config';
import jint from 'astro-adapter-jint';
import { withVariant } from '../../support/variant.mjs';

// The upstream build-readonly-file fixture (a page with a hoisted script and a file in public/), built by the adapter under
// test. The test makes public/test.txt read-only before it builds.
export default defineConfig(withVariant({
  output: 'static',
  adapter: jint(),
}));
