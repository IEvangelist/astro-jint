import { defineConfig } from 'astro/config';
import jint from 'astro-adapter-jint';
import { withVariant } from '../../support/variant.mjs';

// The upstream test configures the `fs` driver. The adapter wires the session store of the ASP.NET Core host in
// its place, which keeps `ttl`.
export default defineConfig(withVariant({
  output: 'server',
  adapter: jint(),
  session: {
    ttl: 20,
  },
}));
