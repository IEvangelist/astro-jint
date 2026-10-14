import { defineMiddleware } from 'astro:middleware';

// The inline middlewares of the upstream App.render() unit tests under packages/astro/test/units/app, merged into one.
// Their rules never overlap: each one looks at a path prefix that only the tests it came from request.
export const onRequest = defineMiddleware(async (context, next) => {
  const { pathname } = context.url;

  // double-slash-bypass.test.ts
  if (['/admin', '/dashboard'].some((prefix) => pathname.startsWith(prefix))) {
    return new Response('Forbidden', { status: 403 });
  }

  // encoded-backslash-bypass.test.ts
  if (pathname === '/users/admin' || pathname.startsWith('/users/admin/')) {
    return new Response('Forbidden', { status: 403 });
  }

  // base-prefix-boundary.test.ts, which runs with base: '/app'
  if (pathname === '/app/admin' || pathname.startsWith('/app/admin/')) {
    return new Response('Forbidden', { status: 403 });
  }

  // double-encoding-bypass.test.ts has three middlewares: this one blocks /api/admin, the rewriting one also sends every
  // other request through routing again with next(context.url), and the passthrough one blocks nothing. The header picks one.
  const mode = context.request.headers.get('x-middleware-mode');
  if (pathname.startsWith('/api/admin') && mode !== 'passthrough') {
    return new Response(JSON.stringify({ error: 'Unauthorized' }), {
      status: 401,
      headers: { 'Content-Type': 'application/json' },
    });
  }
  if (pathname.startsWith('/api/') && mode === 'rewrite') {
    return next(context.url);
  }

  // locals.test.ts: the error pages have to see the locals this middleware mutated even when it fails for them
  if (pathname.startsWith('/mutated/')) {
    if (context.routePattern.includes('/500')) {
      throw new Error('middleware error on 500');
    }
    context.locals.foo = 'mutated-property';
  }

  return next();
});
