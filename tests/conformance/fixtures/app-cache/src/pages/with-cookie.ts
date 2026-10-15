import type { APIRoute } from 'astro';

// 'withCookieEndpoint': a response with a Set-Cookie header must not be cached.
export const GET: APIRoute = (ctx) => {
  ctx.cache.set({ maxAge: 300, tags: ['cookie'] });
  const response = Response.json({ timestamp: Date.now(), nonce: Math.random() });
  response.headers.set('Set-Cookie', 'session=test; Path=/; HttpOnly');
  return response;
};
