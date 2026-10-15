import { getRequestData } from '../../../node_modules/astro/dist/core/server-islands/endpoint.js';

// The calls of the two tests of endpoint.test.ts that no request can make: getRequestData() with the prototype of Object polluted
// (the properties that the validation has to ignore are inherited ones). `astro` does not export core/server-islands/endpoint.js,
// so it is imported by its path inside the installed package; the bundle of the host then runs the very function that reads the
// request of every server island. A case answers what its test asserts on: the options that the function returned, or the status
// of the response that it returned instead.

function makePostRequest(body: Record<string, unknown>) {
  return new Request('http://localhost/_server-islands/Island', {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify(body),
  });
}

async function readWithInherited(name: string, value: unknown) {
  // Temporarily pollute Object.prototype to simulate inherited properties
  (Object.prototype as any)[name] = value;
  try {
    const result = await getRequestData(
      makePostRequest({
        encryptedComponentExport: 'encExport',
        encryptedProps: 'encProps',
        encryptedSlots: 'encSlots',
      }),
    );
    return result instanceof Response ? { status: result.status, statusText: result.statusText } : { options: result };
  } finally {
    delete (Object.prototype as any)[name];
  }
}

export const cases: Record<string, () => unknown> = {
  'only checks own properties for `slots` validation': () => readWithInherited('slots', { default: 'polluted' }),
  'only checks own properties for `componentExport` validation': () => readWithInherited('componentExport', 'default'),
};
