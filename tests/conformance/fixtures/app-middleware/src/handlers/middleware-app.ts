import { localsOf, type Rules } from './rules';

// middleware/middleware-app.test.ts. The comment above each handler is the title of the upstream test it serves.
export const appRules: Rules = {
  // should render locals data set by middleware
  '/locals/set': async (context, next) => {
    localsOf(context).name = 'bar';
    return next();
  },

  // should change locals data based on URL
  '/locals/by-url': async (context, next) => {
    localsOf(context).name = 'bar';
    return next();
  },
  '/locals/by-url/lorem': async (context, next) => {
    localsOf(context).name = 'ipsum';
    return next();
  },

  // should call a second middleware in a sequence via manifest: first() answers here and second(), in src/middleware.ts,
  // runs after it for /sequence/second.
  '/sequence': async (context, next) => {
    localsOf(context).name = 'first';
    return next();
  },
  '/sequence/second': async (context, next) => {
    localsOf(context).name = 'first';
    return next();
  },

  // should successfully create a new response bypassing the page
  '/rewrite': async () => new Response('<span>New content!!</span>', { status: 200 }),

  // should return a new response that is a 500
  '/broken-500': async () => new Response(null, { status: 500 }),

  // should return 200 if middleware returns a 200 Response for a non-existent route
  '/no-route-but-200': async () => new Response("It's OK!", { status: 200 }),

  // should throw when middleware returns undefined without calling next()
  '/undefined': async () => undefined as unknown as Response,

  // should render 500.astro when middleware throws an error
  '/throw': async () => {
    throw new Error('middleware error');
  },

  // should successfully redirect to another page
  '/redirect': async (context) => context.redirect('/', 302),

  // should allow middleware to set cookies
  '/cookies/middleware': async (context, next) => {
    context.cookies.set('foo', 'bar');
    return next();
  },

  // should forward cookies set in a component when middleware returns a new response
  '/cookies/component': async (_context, next) => {
    const response = await next();
    const html = await response.text();
    return new Response(html, { status: 200, headers: response.headers });
  },

  // should be able to clone the response and modify it
  '/clone': async (_context, next) => {
    const response = await next();
    const cloned = response.clone();
    const html = await cloned.text();
    const modified = html.replace('testing', 'it works');
    return new Response(modified, { status: 200, headers: response.headers });
  },

  // should correctly manipulate the response coming from API endpoints. Upstream answers /api/endpoint with a second
  // endpoint; here it is /api/redacted, as /api/endpoint is the one of the test before.
  '/api/redacted': async (_context, next) => {
    const response = await next();
    const data = await response.json();
    data.name = 'REDACTED';
    return new Response(JSON.stringify(data), { headers: response.headers });
  },

  // should correctly call middleware for 404 routes: nothing matches /unknown-page
  '/unknown-page': async (context, next) => {
    localsOf(context).name = 'bar';
    return next();
  },

  // should allow accessing /admin with valid auth header, should redirect /admin without auth header
  '/admin': async (context, next) => {
    const authToken = context.request.headers.get('Authorization');
    if (!authToken) {
      return context.redirect('/');
    }
    return next();
  },

  // should preserve cookies set by middleware when returning Response(null, { status: 404 })
  '/api/guarded/': async (context) => {
    context.cookies.set('session', 'abc123', { path: '/' });
    return new Response(null, { status: 404 });
  },

  // should preserve cookies set by middleware when returning Response(null, { status: 500 })
  '/api/error/': async (context) => {
    context.cookies.set('csrf', 'token456', { path: '/' });
    return new Response(null, { status: 500 });
  },

  // should preserve multiple cookies from sequenced middleware during error page rerouting
  '/api/multi/': async (context) => {
    context.cookies.set('session', 'abc123', { path: '/' });
    context.cookies.set('csrf', 'token456', { path: '/' });
    context.cookies.set('auth_attempt', 'failed', { path: '/' });
    return new Response(null, { status: 404 });
  },

  // should not preserve Content-Length from middleware when rendering 404 error page. Upstream counts the calls (the first
  // one answers with the 404, the one that renders the error page passes it through), which module state in a pool of
  // engines cannot do; the status of what next() returned tells them apart: 200 for the page, 404 for 404.astro.
  '/api/framing-404/': async (_context, next) => {
    const response = await next();
    if (response.status !== 404) {
      return new Response(null, {
        status: 404,
        headers: { 'Content-Length': '999', 'X-Custom': 'keep-me' },
      });
    }
    return response;
  },

  // should not preserve Transfer-Encoding from middleware when rendering 500 error page
  '/api/framing-500/': async (_context, next) => {
    const response = await next();
    if (response.status !== 500) {
      return new Response(null, {
        status: 500,
        headers: { 'Transfer-Encoding': 'chunked', 'X-Error-Source': 'middleware' },
      });
    }
    return response;
  },

  // should correctly set custom headers in middleware
  '/headers': async (_context, next) => {
    const response = await next();
    response.headers.set('X-Custom-Header', 'custom-value');
    return response;
  },
};
