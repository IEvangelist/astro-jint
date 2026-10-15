import { defineConfig } from 'astro/config';
import jint from 'astro-adapter-jint';
import { withVariant } from '../../support/variant.mjs';

// The middlewares of the App.render() unit tests under packages/astro/test/units/middleware (middleware-app,
// call-middleware, sequence and locals), which build theirs inline, as one src/middleware.ts that answers each test's URLs.
export default defineConfig(
  withVariant({
    output: 'server',
    adapter: jint(),
  }),
);
