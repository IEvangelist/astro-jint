import { createApp } from 'astro/app/entrypoint';
import { setGetEnv } from 'astro/env/setup';
import { withDataUrls } from './data-url.js';
import { host } from './host.js';

if (host?.env) {
  setGetEnv((name) => host?.env?.(name) ?? undefined);
}

// The `fetch` of the engine speaks HTTP and HTTPS only, and the Fetch standard also loads `data:` URLs.
// They never leave the engine, so they bypass the host's `FetchUrlFilter`.
if (typeof globalThis.fetch === 'function') {
  (globalThis as { fetch: typeof fetch }).fetch = withDataUrls(globalThis.fetch);
}

const app = createApp({ streaming: false });

// Prerendered 404/500 pages live in dist/client. The default implementation fetches them over
// the network, which a sandboxed engine cannot do, so the host reads them from disk instead. Astro keeps the
// headers of that response when it serves the page as a 404 or 500, so the ones recorded for the file (the
// policy that `staticHeaders` moves out of the markup) are carried over here, as the host would send them.
async function fetchPrerenderedErrorPage(url: string): Promise<Response> {
  const { pathname } = new URL(url);
  const html = host!.readClientFile!(pathname);
  if (html === null || html === undefined) {
    return new Response(null, { status: 404 });
  }
  const recorded = host?.readClientHeaders?.(pathname);
  const headers = new Headers(recorded ? JSON.parse(recorded) : undefined);
  headers.set('content-type', 'text/html; charset=utf-8');
  return new Response(html, { headers });
}

const PRERENDERED_FILE_ROUTE_TYPES = new Set(['page', 'endpoint']);

// Redirects configured for a static site are prerendered routes, but they have no HTML file
// (`build.redirects` is off), so they must be answered here with a real 3xx. A prerendered page
// or endpoint is a file the host serves before it ever gets here, so only on-demand routes count.
function matchRoute(request: Request) {
  const route = app.match(request, true);
  return route?.prerender && PRERENDERED_FILE_ROUTE_TYPES.has(route.type) ? app.match(request) : route;
}

export default {
  async fetch(request: Request): Promise<Response> {
    const locals = host?.locals?.();
    const response = await app.render(request, {
      clientAddress: host?.clientAddress?.() ?? undefined,
      locals: locals ? JSON.parse(locals) : undefined,
      prerenderedErrorPageFetch: host?.readClientFile ? fetchPrerenderedErrorPage : undefined,
      routeData: matchRoute(request),
    });

    // With `addCookieHeader` Astro appends the cookies to the headers of the response it got, and that
    // throws when they are immutable, as those of `Response.redirect()` are. They are read here instead and
    // added to the copy below, whose headers are always mutable. The lookup needs the response as returned.
    const cookies = [...app.setCookieHeaders(response)];
    if (response.body === null && cookies.length === 0) {
      return response;
    }

    // Jint's fetch handler hosting cannot read stream-backed bodies and Astro re-wraps
    // `response.body` when it merges error pages, so every body is buffered here.
    const buffered = new Response(response.body === null ? null : await response.arrayBuffer(), {
      status: response.status,
      statusText: response.statusText,
      headers: response.headers,
    });
    for (const cookie of cookies) {
      buffered.headers.append('set-cookie', cookie);
    }
    return buffered;
  },
};
