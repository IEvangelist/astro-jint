import type { APIRoute } from 'astro';

// Registers a context provider whose finalize callback rejects. The state of the request hangs on the context under a symbol of
// the global registry, which is how upstream's test gets at it too.
export const GET: APIRoute = (context) => {
  const state = (context as any)[Symbol.for('astro.fetchState')];
  state.provide('unit-probe', {
    create: () => ({}),
    finalize: () => Promise.reject(new Error('finalize failure')),
  });
  // Resolve the provider so that it is included in finalizeAll().
  state.resolve('unit-probe');
  return new Response('ok');
};
