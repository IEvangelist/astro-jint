import { defineMiddleware } from 'astro:middleware';

// The guard of 'Actions RPC middleware handling' in actions-app.test.ts: the `locked` action answers 401 unless the request
// has the cookie `actionCookie`. A request with the header `x-fail-middleware` makes the middleware throw, which is how the
// 'skipMiddleware' test of render-context.test.ts gets Astro to render an error page a second time without the middleware.
// Every other request goes on.
export const onRequest = defineMiddleware(async (context, next) => {
  if (context.url.pathname.includes('/_actions/locked') && !context.cookies.has('actionCookie')) {
    return new Response('Unauthorized', { status: 401 });
  }
  if (context.request.headers.has('x-fail-middleware')) {
    throw new Error('The middleware failed');
  }
  return next();
});
