import { defineConfig } from 'astro/config';
import jint from 'astro-adapter-jint';
import { withVariant } from '../../support/variant.mjs';

// The endpoints of the App.render() unit tests of cookies (packages/astro/test/units/cookies/app-cookies.test.ts), which
// read and write cookies directly and through rewrites.
export default defineConfig(
  withVariant({
    output: 'server',
    adapter: jint(),
  }),
);
