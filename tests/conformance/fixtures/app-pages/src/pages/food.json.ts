import type { APIRoute } from 'astro';

export const GET: APIRoute = () =>
  new Response(JSON.stringify([{ name: 'lettuce' }, { name: 'broccoli' }, { name: 'pizza' }]), {
    status: 200,
    statusText: 'tasty',
  });

export const POST: APIRoute = async ({ request }) => {
  const ok = (await request.text()) === 'some data';
  return new Response(ok ? 'ok' : 'not ok', { status: ok ? 200 : 400, statusText: ok ? 'ok' : 'not ok' });
};
