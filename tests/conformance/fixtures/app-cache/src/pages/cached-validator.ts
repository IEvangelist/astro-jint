import type { APIRoute } from 'astro';

// 'cachedValidatorEndpoint': a response with a Last-Modified validator and no explicit Cache-Control.
export const GET: APIRoute = (ctx) => {
  ctx.cache.set({ maxAge: 300, lastModified: new Date('2025-06-01T12:00:00Z') });
  return Response.json({ timestamp: Date.now() });
};
