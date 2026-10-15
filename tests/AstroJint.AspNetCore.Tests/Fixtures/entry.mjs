// A handwritten stand-in for the bundle that astro-adapter-jint produces. It exercises the host bridge
// (requests, responses, failures and the __astroJint members) without needing Astro.
const host = globalThis.__astroJint;

// Module scope exists once per engine, so this shows when an engine was replaced.
let handled = 0;
const engineId = Math.random().toString(36).slice(2);

const json = (value, init = {}) =>
  new Response(JSON.stringify(value), {
    ...init,
    headers: { 'content-type': 'application/json', ...init.headers },
  });

const busyWait = (ms) => {
  const end = Date.now() + ms;
  while (Date.now() < end) {
    // The engine is single threaded: while this spins, a concurrent request has to use another engine.
  }
};

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

      case '/host-bytes': {
        const buffer = host.readClientBytes(query.get('file') ?? '');
        if (buffer === null) {
          return json({ found: false });
        }

        return json({
          found: true,
          isArrayBuffer: buffer instanceof ArrayBuffer,
          length: buffer.byteLength,
          head: Array.from(new Uint8Array(buffer).subarray(0, 6)),
          sha256: host.sha256(buffer),
        });
      }

      case '/host-headers': {
        const recorded = host.readClientHeaders(query.get('file') ?? '');
        return json({ found: recorded !== null, headers: recorded === null ? null : JSON.parse(recorded) });
      }

      case '/sha256': {
        const bytes = new Uint8Array((query.get('bytes') ?? '').split(',').filter(Boolean).map(Number));
        return json({
          ofBuffer: host.sha256(bytes.buffer),
          ofView: host.sha256(bytes),
          ofText: host.sha256('abc'),
          ofNothing: host.sha256(),
        });
      }

      case '/fetch': {
        try {
          const response = await fetch(query.get('url'), { redirect: query.get('redirect') ?? 'follow' });
          return json({
            ok: true,
            status: response.status,
            url: response.url,
            redirected: response.redirected,
            body: query.has('binary') ? Array.from(new Uint8Array(await response.arrayBuffer())) : await response.text(),
          });
        } catch (error) {
          return json({ ok: false, error: String(error) });
        }
      }

      case '/cookies': {
        const headers = new Headers({ 'content-type': 'text/plain' });
        headers.append('set-cookie', 'a=1; Path=/; HttpOnly');
        headers.append('set-cookie', 'b=2; Path=/; Secure');
        return new Response('ok', { headers });
      }

      case '/redirect':
        return new Response(null, { status: 302, headers: { location: '/target' } });

      case '/console':
        console.debug('debug line');
        console.log('log line');
        console.info('info line');
        console.warn('warn line');
        console.error('error line');
        console.log('%s has %d items', 'cart', 3);
        console.group('group');
        console.log('inside');
        console.groupEnd();
        return new Response('logged');

      case '/status': {
        const init = { status: Number(query.get('code') ?? 200), headers: { 'content-type': 'text/plain' } };
        if (query.has('text')) {
          init.statusText = query.get('text');
        }

        return new Response(query.get('body'), init);
      }

      case '/count':
        return json({ handled });

      case '/session-get': {
        const value = await host.sessionGet(query.get('id'));
        busyWait(Number(query.get('hold') ?? 0));
        return json({ engine: engineId, value });
      }

      case '/session-set':
        await host.sessionSet(query.get('id'), query.get('value'));
        return json({ engine: engineId });

      case '/session-remove':
        await host.sessionRemove(query.get('id'));
        return json({ engine: engineId });

      // The result is not awaited, so the operation settles after the response has been sent.
      case '/session-detached':
        host.sessionSet(query.get('id'), query.get('value'));
        return json({ engine: engineId });

      case '/session-parallel': {
        const count = Number(query.get('count') ?? 10);
        const ids = Array.from({ length: count }, (_, index) => `parallel-${index}`);
        await Promise.all(ids.map((id, index) => host.sessionSet(id, `value-${index}`)));
        return json({ values: await Promise.all(ids.map((id) => host.sessionGet(id))) });
      }

      case '/session-catch':
        try {
          return json({ ok: true, value: await host.sessionGet(query.get('id') ?? 'any') });
        } catch (error) {
          return json({ ok: false, error: String(error), isError: error instanceof Error });
        }

      case '/session-bad-arguments':
        try {
          await host.sessionSet('only-an-id');
          return json({ ok: true });
        } catch (error) {
          return json({ ok: false, error: String(error) });
        }

      case '/throw':
        throw new Error('boom');

      case '/reject':
        return Promise.reject(new Error('rejected'));

      case '/spin': {
        busyWait(Number(query.get('ms') ?? 0));
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
