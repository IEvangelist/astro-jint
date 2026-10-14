import type { APIRoute } from 'astro';

export const GET: APIRoute = (ctx) => {
  ctx.cookies.set('my_cookie', 'value');
  ctx.cookies.set('another', 'set-in-target');
  return new Response('<html><body><h1>Page 2</h1></body></html>', {
    headers: { 'Content-Type': 'text/html' },
  });
};
