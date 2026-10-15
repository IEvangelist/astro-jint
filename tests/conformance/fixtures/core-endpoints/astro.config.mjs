import { defineConfig } from 'astro/config';
import jint from 'astro-adapter-jint';
import { withVariant } from '../../support/variant.mjs';

// The sources of the upstream fixtures endpoint-routing, api-routes, non-html-pages and astro-public in one project. The tests of
// endpoint-routing run against the dev server, which answers every route per request: here that is output 'server'. The others
// are static builds, where the variant that prerenders their routes (see EndpointsFixture) gives the same output files.
export default defineConfig(withVariant({
  output: 'server',
  adapter: jint(),
  integrations: [
    // Upstream: "SSG only". The endpoint reads its file with node:fs, which the engine does not have.
    { conformanceIntegration: 'prerender-route', componentEndsWith: 'placeholder.png.ts' },
  ],
}));
