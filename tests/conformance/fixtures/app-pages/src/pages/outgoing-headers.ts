import type { APIRoute } from 'astro';

export const prerender = false;

// The Headers that the tests of createOutgoingHttpHeaders() (app/headers.test.ts) convert, as the headers of a response. The
// host does that conversion itself: what the client receives is what the tests of the converter assert.
export const GET: APIRoute = ({ url }) => {
  const headers = new Headers();

  switch (url.searchParams.get('case')) {
    case 'single-key':
      headers.append('x-test', 'hello world');
      break;
    case 'multiple-keys':
      headers.append('x-test1', 'hello');
      headers.append('x-test2', 'world');
      break;
    case 'multiple-values':
      headers.append('x-test', 'hello');
      headers.append('x-test', 'world');
      break;
    case 'set-cookie':
      headers.append('set-cookie', 'hello');
      headers.append('set-cookie', 'world');
      break;
    case 'set-cookie-case':
      headers.append('Set-cookie', 'hello');
      headers.append('Set-Cookie', 'world');
      break;
    case 'all':
      headers.append('x-single', 'single');
      headers.append('x-triple', 'one');
      headers.append('x-triple', 'two');
      headers.append('x-triple', 'three');
      headers.append('Set-cookie', 'hello');
      headers.append('Set-Cookie', 'world');
      break;
  }

  return new Response('ok', { headers });
};
