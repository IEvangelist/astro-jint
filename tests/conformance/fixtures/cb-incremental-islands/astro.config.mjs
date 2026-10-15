import { defineConfig } from 'astro/config';
import jint from 'astro-adapter-jint';
import { withCbVariant } from '../../support/cb/variant.mjs';

// The upstream incremental-build-islands fixture, built by the adapter under test instead of the test adapter. A test builds
// it twice into the one cache of the fixture and tells the builds apart with a `cb` flag, which moves the output folder of
// the second build and is not part of the config that the cache is keyed by.
export default defineConfig(withCbVariant({
  output: 'static',
  adapter: jint(),
  experimental: { incrementalBuild: true },
}));
