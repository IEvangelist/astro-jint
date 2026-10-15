import type { APIRoute } from 'astro';

// 'withAstroCookieEndpoint': a cookie set through Astro.cookies must not be cached either.
export const GET: APIRoute = (ctx) => {
  ctx.cache.set({ maxAge: 300, tags: ['astro-cookie'] });
  ctx.cookies.set('session', 'test', { path: '/', httpOnly: true });
  return Response.json({ nonce: Math.random() });
};
