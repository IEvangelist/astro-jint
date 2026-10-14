// A cache provider of a CDN that reads the response headers of Astro: it has no onRequest and no setHeaders, so Astro writes
// CDN-Cache-Control and Cache-Tag itself. With the option waitUntilProbe it is the wait-until probe of the upstream tests:
// it reports whether it is given a waitUntil function.
export default function cdnCacheProvider(config) {
  const provider = {
    name: 'cdn-probe',
    async invalidate() {},
  };
  if (config?.waitUntilProbe) {
    provider.onRequest = async (context, next) => {
      const response = await next();
      response.headers.set('x-cache-provider-waituntil', typeof context.waitUntil);
      return response;
    };
  }
  return provider;
}
