import { defineConfig } from 'astro/config';
import jint from 'astro-adapter-jint';
import { withVariant } from '../../support/variant.mjs';

// The app of the Actions unit tests (packages/astro/test/units/actions: actions-app, action-status, serialize and the
// formDataToObject cases of form-data-to-object). Upstream builds a test App with inline routes and a table of actions;
// here the actions are those of src/actions and the route of the RPC calls (/_actions/[...path]) is the one Astro injects.
// The variants change base, trailingSlash and security.
export default defineConfig(
  withVariant({
    output: 'server',
    adapter: jint(),
  }),
);
