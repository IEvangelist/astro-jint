import { defineConfig } from 'astro/config';
import jint from 'astro-adapter-jint';
import { withVariant } from '../../support/variant.mjs';

// Error pages of the App.render() unit tests that app-pages cannot host: there is no 404.astro here, so a 404 gets
// Astro's built-in page, and the 500 page is prerendered, so it is a file the host reads instead of a page it renders.
export default defineConfig(
  withVariant({
    output: 'server',
    adapter: jint(),
  }),
);
