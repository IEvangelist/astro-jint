import { defineConfig } from 'astro/config';
import jint from 'astro-adapter-jint';
import { withVariant } from '../../support/variant.mjs';

// Upstream gives this fixture `base: '/some-base'`, `output: 'static'` and `trailingSlash: 'never'` in its config file.
// Its tests merge their changes over that file, so the "without base" cases still have a base. Here every variant says
// what it needs, and the cases without a base have none.
export default defineConfig(withVariant({ adapter: jint() }));
