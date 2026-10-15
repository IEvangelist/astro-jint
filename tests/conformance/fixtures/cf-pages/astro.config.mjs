import { defineConfig } from 'astro/config';
import jint from 'astro-adapter-jint';
import { withVariant } from '../../support/variant.mjs';

// One fixture for the pages of many small upstream fixtures of the Cloudflare adapter tests (build budget). Every page says
// whether it is prerendered, so the site builds the same way with output 'server' as with 'static'.
const config = withVariant({
  output: 'server',
  adapter: jint(),
  redirects: {
    '/redirect-external': 'http://test.invalid/destination',
    '/redirect-internal': '/page2',
  },
  cache: {
    provider: { entrypoint: './cdn-cache-provider.mjs' },
  },
  routeRules: {
    '/cache/tagged': { maxAge: 600, tags: ['products'] },
  },
  image: {
    domains: ['localhost', '127.0.0.1'],
  },
});

// Astro resolves the entrypoint of a session driver from its own folder, so a test names the driver of the fixture by a path
// relative to the fixture ('./src/session-driver.mjs') and this turns it into the URL that Astro takes as it is.
const driver = config.session?.driver;
if (typeof driver?.entrypoint === 'string' && driver.entrypoint.startsWith('./')) {
  driver.entrypoint = new URL(driver.entrypoint, import.meta.url);
}

export default defineConfig(config);
