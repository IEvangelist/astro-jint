import { defineConfig } from 'astro/config';
import jint from 'astro-adapter-jint';
import { withVariant } from '../../support/variant.mjs';
import fakeIntegration from './fake-astro-library/index.mjs';

// The upstream underscore-in-folder-name fixture. The folder of this fixture starts with an underscore on purpose: the
// route injected by the fake library lives outside src/pages, so Astro must not take the folder name for a private
// path segment of that route.
export default defineConfig(withVariant({
  output: 'static',
  adapter: jint(),
  integrations: [fakeIntegration()],
}));
