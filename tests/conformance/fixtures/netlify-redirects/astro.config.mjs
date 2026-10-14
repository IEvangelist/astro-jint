import { defineConfig } from 'astro/config';
import jint from 'astro-adapter-jint';
import { withVariant } from '../../support/variant.mjs';

// The redirects fixtures of upstream (test/functions/fixtures/redirects and test/static/fixtures/redirects) in one project.
// The stylesheet is never inlined, so that the build has a file below /_astro/ to ask for headers.
export default defineConfig(withVariant({
	output: 'static',
	adapter: jint(),
	site: 'http://example.com',
	build: {
		inlineStylesheets: 'never',
	},
	redirects: {
		'/other': '/',
		'/two': {
			status: 302,
			destination: '/',
		},
		'/blog/[...slug]': '/team/articles/[...slug]',
	},
}));
