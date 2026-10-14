// A handwritten stand-in for the bundle that astro-adapter-jint produces. It exercises the host bridge
// (requests, responses, failures and the __astroJint members) without needing Astro.
const host = globalThis.__astroJint;

// Module scope exists once per engine, so this shows when an engine was replaced.
let handled = 0;

const json = (value, init = {}) =>
  new Response(JSON.stringify(value), {
    ...init,
    headers: { 'content-type': 'application/json', ...init.headers },
  });

export default {
  async fetch(request) {
    handled++;
    const url = new URL(request.url);
    const query = url.searchParams;

    switch (url.pathname) {
      case '/echo': {
        const headers = {};
        for (const [name, value] of request.headers) {
          headers[name] = value;
        }

        return json({
          method: request.method,
          url: request.url,
          headers,
          body: await request.text(),
        });
      }

      case '/host':
        return json({
          clientAddress: host.clientAddress(),
          locals: host.locals(),
          env: host.env(query.get('env') ?? ''),
          file: host.readClientFile(query.get('file') ?? ''),
        });

      case '/cookies': {
        const headers = new Headers({ 'content-type': 'text/plain' });
        headers.append('set-cookie', 'a=1; Path=/; HttpOnly');
        headers.append('set-cookie', 'b=2; Path=/; Secure');
        return new Response('ok', { headers });
      }

      case '/redirect':
        return new Response(null, { status: 302, headers: { location: '/target' } });

      case '/status':
        return new Response(query.get('body'), {
          status: Number(query.get('code') ?? 200),
          headers: { 'content-type': 'text/plain' },
        });

      case '/count':
        return json({ handled });

      case '/throw':
        throw new Error('boom');

      case '/reject':
        return Promise.reject(new Error('rejected'));

      case '/spin': {
        const end = Date.now() + Number(query.get('ms') ?? 0);
        while (Date.now() < end) {
          // Busy wait: the engine is single threaded and has no timers worth relying on.
        }

        return json({ handled });
      }

      case '/forever':
        while (true) {
          // Never returns; only the host's cancellation can end this.
        }

      case '/alloc': {
        const hoard = [];
        while (true) {
          hoard.push(new Array(100000).fill(handled));
        }
      }

      case '/binary':
        return new Response(new Uint8Array([0, 1, 2, 253, 254, 255]), {
          headers: { 'content-type': 'application/octet-stream' },
        });

      case '/big':
        return new Response('x'.repeat(Number(query.get('size') ?? 100000)), {
          headers: { 'content-type': 'text/plain' },
        });

      default:
        return new Response('not found', { status: 404, headers: { 'content-type': 'text/plain' } });
    }
  },
};
