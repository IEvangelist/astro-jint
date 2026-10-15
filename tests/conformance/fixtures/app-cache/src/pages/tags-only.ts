import type { APIRoute } from 'astro';

// Tags and no maxAge: a CDN-style provider gives Cache-Tag but no CDN-Cache-Control.
export const GET: APIRoute = (ctx) => {
  ctx.cache.set({ tags: ['product', 'sku-123'] });
  return Response.json({ tagged: true });
};
