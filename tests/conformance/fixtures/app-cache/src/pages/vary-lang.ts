import type { APIRoute } from 'astro';

// 'varyLangEndpoint': the language of the request varies the response, as the Vary header says.
export const GET: APIRoute = (ctx) => {
  const lang = ctx.request.headers.get('Accept-Language') || 'en';
  ctx.cache.set({ maxAge: 300, tags: ['vary-test'] });
  return new Response(JSON.stringify({ lang, timestamp: Date.now() }), {
    headers: {
      'Content-Type': 'application/json',
      Vary: 'Accept-Language',
    },
  });
};
