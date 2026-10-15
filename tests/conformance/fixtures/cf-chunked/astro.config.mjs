import { defineConfig } from 'astro/config';
import jint from 'astro-adapter-jint';
import { withVariant } from '../../support/variant.mjs';

// The tests of content-collections-chunked.test.ts build this with experimental.collectionStorage set to "chunked".
export default defineConfig(
  withVariant({
    output: 'server',
    adapter: jint(),
  }),
);
