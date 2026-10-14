import { defineConfig } from 'astro/config';
import jint from 'astro-adapter-jint';
import { withVariant } from '../../support/variant.mjs';

// The middleware fixture of upstream (test/functions/fixtures/middleware) imports a Node built-in with the node: prefix.
export default defineConfig(withVariant({
	output: 'server',
	adapter: jint(),
	site: 'http://example.com',
}));
