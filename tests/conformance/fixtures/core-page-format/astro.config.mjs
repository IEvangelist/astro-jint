import { defineConfig } from 'astro/config';
import jint from 'astro-adapter-jint';
import { withVariant } from '../../support/variant.mjs';

// Upstream builds the fixture without an adapter and gives each describe its own build.format, base, trailingSlash and i18n;
// the tests ask for the same changes as variants.
export default defineConfig(withVariant({
  adapter: jint(),
}));
