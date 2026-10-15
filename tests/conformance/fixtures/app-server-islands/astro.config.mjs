import { defineConfig } from 'astro/config';
import jint from 'astro-adapter-jint';
import { withVariant } from '../../support/variant.mjs';

// The pages and the endpoints that the server island unit tests of Astro under packages/astro/test/units/server-islands build
// inline (a stub SSRResult, hand-made components), as real files. Three builds: the default one with `trailingSlash: 'never'`,
// one with a base and `trailingSlash: 'always'`, and one with the CSP and a body size limit for the endpoint of 200 bytes.
//
// The key is pinned: the build bakes it into the manifest and runs in another process than the tests, which have to encrypt what
// they send to the endpoint (AES-GCM, the format of encryptString() of Astro) with the very same key.
process.env.ASTRO_KEY = 'vIXnBp5P+TkrzNkmkCdgwbRmYJZOttak9iJ/WAkULd8=';

export default defineConfig(
  withVariant({
    output: 'server',
    adapter: jint(),
  }),
);
