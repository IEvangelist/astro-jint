import { defineMiddleware, sequence } from 'astro:middleware';
import { callRules } from './handlers/call-middleware';
import { appRules } from './handlers/middleware-app';
import { dispatch, localsOf } from './handlers/rules';
import { sequenceRules } from './handlers/sequence';

// first() carries the handlers of every upstream test, each for the URLs of its own; second() is the second middleware of
// 'should call a second middleware in a sequence via manifest'. The export is a sequence of both, as in that test.
const first = dispatch(appRules, callRules, sequenceRules);

const second = defineMiddleware(async (context, next) => {
  if (context.url.pathname === '/sequence/second') {
    localsOf(context).name = 'second';
  }
  return next();
});

export const onRequest = sequence(first, second);
