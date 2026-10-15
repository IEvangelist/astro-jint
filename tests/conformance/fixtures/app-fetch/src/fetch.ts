import { FetchState, actions, astro, i18n, middleware, pages, redirects, trailingSlash } from 'astro/fetch';

// The fetch handler of the app. Every request names in `x-pipeline` what to run: one function of `astro/fetch` or one
// composition of them, as the unit tests of astro/fetch do on a FetchState they create themselves. The tests send the
// same requests upstream builds with `new Request(...)` and read what the function returns, or a JSON report of the state.

// What a function that returned undefined ("nothing to do, carry on") looks like to the test.
const nothing = () => new Response('', { headers: { 'x-handler-result': 'undefined' } });

// A step of a composition that returned a response when the test expects it to carry on.
const unexpected = (step: string) => new Response(`unexpected: ${step} returned a response`, { status: 418 });

// What the tests read of a FetchState.
function describeState(state: FetchState) {
  return {
    route: state.routeData?.route ?? null,
    type: state.routeData?.type ?? null,
    prerender: state.routeData?.prerender ?? null,
    pathname: state.pathname,
    protocol: state.url.protocol,
    hostname: state.url.hostname,
    port: state.url.port,
    requestUrl: state.request.url,
    clientAddress: state.clientAddress ?? null,
  };
}

// The members of FetchState that are not part of its public interface but the upstream tests use.
type Internals = FetchState & {
  responseRouteType?: string;
  provide(key: string, provider: { create(): unknown; finalize?(): unknown }): void;
  resolve(key: string): unknown;
  finalizeAll(): Promise<void>;
};

export default {
  async fetch(request: Request): Promise<Response> {
    switch (request.headers.get('x-pipeline') ?? 'astro') {
      case 'state':
        return Response.json(describeState(new FetchState(request)));

      case 'late-forwarded-headers': {
        // A user fetch handler can set the headers before it creates the FetchState.
        request.headers.set('x-forwarded-host', 'example.com');
        request.headers.set('x-forwarded-proto', 'https');
        return Response.json(describeState(new FetchState(request)));
      }

      case 'astro-state': {
        const state = new FetchState(request);
        const described = describeState(state);
        const response = await astro(state);
        return Response.json({
          ...described,
          status: response.status,
          body: await response.text(),
          reconstructed: state.request !== request,
        });
      }

      case 'request-body': {
        const state = new FetchState(request);
        return Response.json({
          method: state.request.method,
          contentType: state.request.headers.get('content-type'),
          requestUrl: state.request.url,
          body: await state.request.text(),
        });
      }

      case 'trailing-slash':
        return trailingSlash(new FetchState(request)) ?? nothing();

      case 'redirects':
        return (await redirects(new FetchState(request))) ?? nothing();

      case 'actions':
        return (await actions(new FetchState(request))) ?? nothing();

      case 'middleware-next':
        return middleware(new FetchState(request), async () => new Response('from next'));

      case 'middleware-page':
        return middleware(new FetchState(request), async () => new Response('page'));

      case 'middleware-pages': {
        const state = new FetchState(request);
        return middleware(state, () => pages(state));
      }

      case 'middleware-throwing-next':
        return middleware(new FetchState(request), async () => {
          throw new Error('boom from next');
        });

      case 'pages':
        return pages(new FetchState(request));

      case 'pages-status': {
        const state = new FetchState(request);
        state.status = 503;
        return pages(state);
      }

      case 'i18n-original': {
        const original = new Response('original body');
        const result = await i18n(new FetchState(request), original);
        return Response.json({ same: result === original, body: await result.text() });
      }

      case 'i18n-page': {
        const state = new FetchState(request) as Internals;
        state.responseRouteType = 'page';
        return i18n(state, new Response('page body'));
      }

      case 'i18n-endpoint': {
        // No responseRouteType: this is what an endpoint response looks like.
        const apiResponse = new Response('{"ok":true}');
        const result = await i18n(new FetchState(request), apiResponse);
        return Response.json({ same: result === apiResponse, status: result.status, body: await result.text() });
      }

      case 'astro':
        return astro(new FetchState(request));

      case 'composed': {
        const state = new FetchState(request);
        if ((await trailingSlash(state)) !== undefined) return unexpected('trailingSlash');
        if ((await redirects(state)) !== undefined) return unexpected('redirects');
        if ((await actions(state)) !== undefined) return unexpected('actions');
        const response = await middleware(state, () => pages(state));
        return i18n(state, response);
      }

      case 'actions-then-middleware': {
        const state = new FetchState(request);
        if ((await actions(state)) !== undefined) return unexpected('actions');
        return middleware(state, () => pages(state));
      }

      case 'response-of-pages': {
        const state = new FetchState(request);
        const before = state.response === undefined;
        const response = await pages(state);
        return Response.json({ before, set: state.response !== undefined, same: state.response === response });
      }

      case 'response-of-middleware': {
        const state = new FetchState(request);
        const response = await middleware(state, () => pages(state));
        return Response.json({ set: state.response !== undefined, same: state.response === response });
      }

      case 'providers': {
        const unregistered = (new FetchState(request) as Internals).resolve('missing') === undefined;

        const counting = new FetchState(request) as Internals;
        let createCalls = 0;
        counting.provide('counter', {
          create() {
            createCalls++;
            return { count: 42 };
          },
        });
        const first = counting.resolve('counter');
        const second = counting.resolve('counter');

        const finalizing = new FetchState(request) as Internals;
        let finalized = false;
        finalizing.provide('session', {
          create: () => ({ data: 'test' }),
          finalize: () => {
            finalized = true;
          },
        });
        finalizing.resolve('session');
        await finalizing.finalizeAll();

        const skipping = new FetchState(request) as Internals;
        let finalizedWithoutResolving = false;
        skipping.provide('unused', {
          create: () => 'value',
          finalize: () => {
            finalizedWithoutResolving = true;
          },
        });
        await skipping.finalizeAll();

        return Response.json({ unregistered, first, cached: first === second, createCalls, finalized, finalizedWithoutResolving });
      }

      default:
        return new Response('unknown pipeline', { status: 400 });
    }
  },
};
