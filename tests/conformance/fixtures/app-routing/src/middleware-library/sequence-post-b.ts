import { sequence } from 'astro:middleware';
import type { MiddlewareHandler } from 'astro';

// Sends every request to the post-b page from the first of two chained middleware. The second one reports, in headers, the
// request it was given, once the page has rendered: upstream keeps that request and reads it after the render.
const first: MiddlewareHandler = async (_context, next) => next('/post/post-b');

const second: MiddlewareHandler = async (context, next) => {
  const sequenced = context.request;
  const response = await next();
  response.headers.set('X-Sequenced-Method', sequenced.method);
  response.headers.set('X-Sequenced-Body', sequenced.body === null ? 'null' : 'stream');
  if (sequenced.method === 'POST') {
    response.headers.set('X-Sequenced-Json', JSON.stringify(await sequenced.json()));
  }
  return response;
};

export const onRequest = sequence(first, second);
