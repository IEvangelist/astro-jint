// Lets a test ask for a variant of a fixture without a second copy of it. The upstream tests pass the changes they
// need (output, base, trailingSlash, ...) to loadFixture(); the .NET test host passes the same changes as JSON in
// CONFORMANCE_OVERRIDES and names the folder to build into in CONFORMANCE_OUT_DIR. Entries of `integrations` that are
// { conformanceIntegration: '<name>', ... } become the integration of that name from integrations.mjs.

import { createIntegration } from './integrations.mjs';

/** @param {Record<string, unknown>} config the Astro config of the fixture */
export function withVariant(config) {
  const overrides = JSON.parse(process.env.CONFORMANCE_OVERRIDES || '{}');
  const merged = merge(config, overrides);
  if (process.env.CONFORMANCE_OUT_DIR) {
    merged.outDir = process.env.CONFORMANCE_OUT_DIR;
  }

  if (Array.isArray(merged.integrations)) {
    merged.integrations = merged.integrations.map((entry) =>
      isPlainObject(entry) && 'conformanceIntegration' in entry ? createIntegration(entry) : entry);
  }

  return merged;
}

function merge(target, source) {
  const result = { ...target };
  for (const [key, value] of Object.entries(source)) {
    result[key] = isPlainObject(value) && isPlainObject(result[key]) ? merge(result[key], value) : value;
  }

  return result;
}

function isPlainObject(value) {
  return value !== null && typeof value === 'object' && !Array.isArray(value);
}
