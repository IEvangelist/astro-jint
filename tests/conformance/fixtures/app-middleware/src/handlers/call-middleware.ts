import { localsOf, type Rules } from './rules';

// middleware/call-middleware.test.ts, which calls callMiddleware(onRequest, mockContext, responseFunction). Here the
// response function is the endpoint src/pages/call/[...path].ts (or the page of the request) and the middleware is the
// handler of the URL. The comment above each handler is the title of the upstream test it serves.
export const callRules: Rules = {
  // returns the middleware return value when next() is called and a Response is returned
  '/call/replace': async (_context, next) => {
    const response = await next();
    return new Response('modified', { status: 200, headers: response.headers });
  },

  // returns the responseFunction result when next() is called but middleware returns undefined
  '/call/next-undefined': async (_context, next) => {
    await next();
    // deliberately returns undefined
  },

  // throws MiddlewareNotAResponse when next() is called but middleware returns a non-Response
  '/call/next-non-response': async (_context, next) => {
    await next();
    return 'not a response' as unknown as Response;
  },

  // returns the Response when middleware short-circuits without calling next()
  '/call/short-circuit': async () => new Response('short-circuit', { status: 200 }),

  // returns a 500 Response when middleware short-circuits with an error status
  '/call/short-circuit-500': async () => new Response(null, { status: 500 }),

  // throws MiddlewareNoDataOrNextCalled when middleware returns undefined without calling next()
  '/call/no-data': async () => {
    // returns undefined, never calls next
  },

  // throws MiddlewareNotAResponse when middleware returns a non-Response without calling next()
  '/call/non-response': async () => 'not a response' as unknown as Response,

  // locals mutations are visible in the response function
  '/call/locals': async (context, next) => {
    localsOf(context).name = 'bar';
    return next();
  },

  // middleware can set response headers after calling next()
  '/call/headers': async (_context, next) => {
    const response = await next();
    response.headers.set('X-Custom', 'value');
    return response;
  },

  // middleware can clone the response, modify body, and return a new Response
  '/call/clone': async (_context, next) => {
    const response = await next();
    const cloned = response.clone();
    const html = await cloned.text();
    const modified = html.replace('testing', 'it works');
    return new Response(modified, { status: 200, headers: response.headers });
  },

  // middleware can intercept a JSON response, modify it, and return a new Response
  '/call/json': async (_context, next) => {
    const response = await next();
    const data = await response.json();
    data.name = 'REDACTED';
    return new Response(JSON.stringify(data), {
      headers: { 'Content-Type': 'application/json' },
    });
  },

  // works with a synchronous middleware that calls next()
  '/call/sync-next': (_context, next) => {
    return next();
  },

  // works with a synchronous middleware that returns a Response
  '/call/sync-response': () => {
    return new Response('sync short-circuit');
  },
};
