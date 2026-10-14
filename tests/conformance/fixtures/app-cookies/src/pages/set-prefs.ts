import type { APIRoute } from 'astro';

export const POST: APIRoute = (ctx) => {
  const mode = ctx.cookies.get('prefs')!.json().mode;
  ctx.cookies.set('prefs', {
    mode: mode === 'light' ? 'dark' : 'light',
  });
  return new Response(null, {
    status: 302,
    headers: { Location: '/prefs' },
  });
};
