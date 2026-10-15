// The provider of the 'cdn' build: a CDN-style provider, which has no onRequest, so Astro only adds the cache headers.
export default function createCdnProvider() {
  return {
    name: 'mock-cdn-cache',
    async invalidate() {},
  };
}
