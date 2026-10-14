import { defineMiddleware } from 'astro:middleware';

// redirect-page.test.ts: a middleware that answers with a redirect of its own, which has to reach the client as it is.
// static-build.test.ts: the same for a redirect that has no body and is matched on the pathname with its trailing slash.
export const onRequest = defineMiddleware(async (context, next) => {
  if (context.url.pathname === '/middleware-redirect/') {
    return new Response(null, { status: 301, headers: { Location: '/test' } });
  }

  if (context.url.pathname === '/mw-redirect') {
    return new Response(null, {
      status: 307,
      headers: {
        location: '/new',
        'content-type': 'text/plain',
        'x-original-response': 'preserved',
      },
    });
  }

  return next();
});
