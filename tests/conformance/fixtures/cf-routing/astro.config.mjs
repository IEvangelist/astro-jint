import { defineConfig } from 'astro/config';
import jint from 'astro-adapter-jint';
import { withVariant } from '../../support/variant.mjs';
import integration from './integration.mjs';

// The upstream fixture routing-priority (a static site) plus the section /od/, whose pages are rendered on demand.
export default defineConfig(withVariant({
  adapter: jint(),
  integrations: [integration()],
}));
