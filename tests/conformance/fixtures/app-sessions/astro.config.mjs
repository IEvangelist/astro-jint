import { defineConfig } from 'astro/config';
import jint from 'astro-adapter-jint';
import { withVariant } from '../../support/variant.mjs';

// The tests of packages/astro/test/units/sessions drive the AstroSession class with mocked cookies and storage. Here the
// session is the real one: the adapter wires the session store of the host unless a build says `session: false`, and the
// builds differ only in the `session` option (the cookie name, a cookie with `httpOnly: false`, a ttl, `false`), which the
// tests pass as JSON overrides.
export default defineConfig(withVariant({
  output: 'server',
  adapter: jint(),
}));
