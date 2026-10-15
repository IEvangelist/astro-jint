import type { APIRoute } from 'astro';

// '/api' of the CDN-style and the disabled tests (upstream's two endpoints differ in swr and tags; this one has the options of the
// first, which the CDN test asserts, and the disabled and body tests need nothing more).
export const GET: APIRoute = (ctx) => {
  ctx.cache.set({ maxAge: 300, swr: 60, tags: ['api', 'data'] });
  return Response.json({ ok: true });
};
