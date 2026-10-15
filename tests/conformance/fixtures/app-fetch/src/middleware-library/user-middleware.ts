import type { MiddlewareHandler } from 'astro';

// The user middleware of the unit tests of astro/fetch: it marks the response, puts a value in locals, reports the props
// it was given and, when the request asks for it, throws.
export const onRequest: MiddlewareHandler = async (context, next) => {
  if (context.request.headers.get('x-middleware') === 'throw') {
    throw new Error('boom from middleware');
  }
  (context.locals as Record<string, unknown>).from = 'middleware';
  const response = await next();
  response.headers.set('x-user-middleware', 'true');
  response.headers.set('x-middleware-props', JSON.stringify(context.props));
  return response;
};
