// The fixture configs of the core-build tests call withCbVariant() instead of withVariant(). A test can pass a `cb` object
// next to the Astro config changes, for example { cb: { assetQueryParams: true }, output: 'server' }. The flags pick
// integrations that only these tests need and are not Astro config, so they are taken out before the changes are applied.

import { withVariant } from '../variant.mjs';

/** The `cb` flags of the variant that is being built. */
export function cbFlags() {
  return JSON.parse(process.env.CONFORMANCE_OVERRIDES || '{}').cb ?? {};
}

/** @param {Record<string, unknown>} config the Astro config of the fixture */
export function withCbVariant(config) {
  const raw = process.env.CONFORMANCE_OVERRIDES;
  const { cb: _flags, ...overrides } = JSON.parse(raw || '{}');
  process.env.CONFORMANCE_OVERRIDES = JSON.stringify(overrides);
  try {
    return withVariant(config);
  } finally {
    if (raw === undefined) {
      delete process.env.CONFORMANCE_OVERRIDES;
    } else {
      process.env.CONFORMANCE_OVERRIDES = raw;
    }
  }
}
