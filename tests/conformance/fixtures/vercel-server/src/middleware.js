export const hello = async () => 'hello world';

// A middleware module with a top-level await, as the one of the upstream edge middleware fixture.
const message = await hello();

/**
 * Sets a cookie before `next()` and reads the response after it: `next()` has to hand one back.
 *
 * @type {import("astro").MiddlewareHandler}
 */
export const onRequest = async (context, next) => {
  context.cookies.set('foo', 'bar');
  const response = await next();
  response.headers.set('x-astro-middleware', 'ran');
  response.headers.set('x-message', message);
  return response;
};
