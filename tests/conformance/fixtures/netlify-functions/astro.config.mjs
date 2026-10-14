import { defineConfig } from 'astro/config';
import jint from 'astro-adapter-jint';
import { withVariant } from '../../support/variant.mjs';

// The cookies and the middleware fixtures of upstream (test/functions/fixtures) in one project.
export default defineConfig(withVariant({
	output: 'server',
	adapter: jint(),
	site: 'http://example.com',
	security: {
		checkOrigin: false,
	},
}));
