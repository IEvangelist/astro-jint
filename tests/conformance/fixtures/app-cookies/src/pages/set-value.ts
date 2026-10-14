import type { APIRoute } from 'astro';

export const POST: APIRoute = (ctx) => {
  ctx.cookies.set('admin', 'true', { expires: new Date() });
  return new Response('<html><body><h1>Testing</h1></body></html>', {
    headers: { 'Content-Type': 'text/html' },
  });
};
