import type { APIContext, MiddlewareHandler, MiddlewareNext } from 'astro';
import { sequence } from 'astro:middleware';
import { localsOf, type Rules } from './rules';

// middleware/sequence.test.ts, which runs sequence(...handlers) through callMiddleware(combined, mockContext, responseFn).
// Here the combined handler runs for the URL of its test and the response function is the endpoint
// src/pages/seq/[...path].ts. What upstream reads from the mock context and from closures after the call (the locals
// and the order the handlers ran in), a header of the response tells. The comment above each handler is the title of the
// upstream test it serves.
async function observed(
  chain: MiddlewareHandler,
  context: APIContext,
  next: MiddlewareNext,
  order?: number[],
) {
  const response = (await chain(context, next)) as Response;
  response.headers.set('x-locals', JSON.stringify(context.locals));
  if (order) {
    response.headers.set('x-order', order.join(','));
  }
  return response;
}

const recording =
  (order: number[], id: number): MiddlewareHandler =>
  async (_context, next) => {
    order.push(id);
    return next();
  };

export const sequenceRules: Rules = {
  // returns a passthrough middleware when called with no handlers
  '/seq/none': (context, next) => sequence()(context, next),

  // works with a single handler
  '/seq/single': (context, next) => {
    const handler: MiddlewareHandler = async (ctx, n) => {
      localsOf(ctx).touched = true;
      return n();
    };
    return observed(sequence(handler), context, next);
  },

  // executes handlers in order
  '/seq/order': (context, next) => {
    const order: number[] = [];
    return observed(
      sequence(recording(order, 1), recording(order, 2), recording(order, 3)),
      context,
      next,
      order,
    );
  },

  // propagates context mutations across handlers
  '/seq/propagate': (context, next) => {
    const first: MiddlewareHandler = async (ctx, n) => {
      localsOf(ctx).first = 'a';
      return n();
    };
    const second: MiddlewareHandler = async (ctx, n) => {
      const locals = localsOf(ctx);
      // should see mutation from first
      locals.second = (locals.first as string) + 'b';
      return n();
    };
    return sequence(first, second)(context, next);
  },

  // allows the last handler to modify the response from the page
  '/seq/upper': (context, next) => {
    const handler1: MiddlewareHandler = async (_ctx, n) => {
      return n();
    };
    const handler2: MiddlewareHandler = async (_ctx, n) => {
      const response = await n();
      const text = await response.text();
      return new Response(text.toUpperCase());
    };
    return sequence(handler1, handler2)(context, next);
  },

  // supports mixed sync and async handlers
  '/seq/mixed': (context, next) => {
    const syncHandler: MiddlewareHandler = (_ctx, n) => {
      return n();
    };
    const asyncHandler: MiddlewareHandler = async (ctx, n) => {
      localsOf(ctx).async = true;
      return await n();
    };
    return observed(sequence(syncHandler, asyncHandler), context, next);
  },

  // filters out falsy handlers
  '/seq/falsy': (context, next) => {
    const order: number[] = [];
    const combined = sequence(recording(order, 1), null as any, undefined as any, recording(order, 2));
    return observed(combined, context, next, order);
  },

  // allows earlier handlers to short-circuit the chain
  '/seq/short-circuit': (context, next) => {
    const order: number[] = [];
    const handler1: MiddlewareHandler = async () => {
      order.push(1);
      return new Response('short-circuit');
    };
    return observed(sequence(handler1, recording(order, 2)), context, next, order);
  },

  // accumulates cookies set by multiple handlers
  '/seq/cookies': (context, next) => {
    const handler1: MiddlewareHandler = async (ctx, n) => {
      ctx.cookies.set('cookie1', 'value1');
      return n();
    };
    const handler2: MiddlewareHandler = async (ctx, n) => {
      ctx.cookies.set('cookie2', 'value2');
      return n();
    };
    return sequence(handler1, handler2)(context, next);
  },

  // handles a chain where middle handler returns a redirect
  '/seq/redirect': (context, next) => {
    const handler1: MiddlewareHandler = async (ctx, n) => {
      localsOf(ctx).beforeRedirect = true;
      return n();
    };
    const handler2: MiddlewareHandler = async (ctx) => {
      return ctx.redirect('/login');
    };
    const handler3: MiddlewareHandler = async (_ctx, n) => {
      // should never be called
      return n();
    };
    return observed(sequence(handler1, handler2, handler3), context, next);
  },
};
