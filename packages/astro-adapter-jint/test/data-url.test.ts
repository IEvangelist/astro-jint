import assert from 'node:assert/strict';
import { describe, it } from 'node:test';
import { withDataUrls } from '../dist/data-url.js';

/** A `fetch` that records what it is asked and answers with a marker response. */
function networkFetch() {
  const calls: unknown[][] = [];
  const marker = new Response('network');
  const fetchStub = ((...args: unknown[]) => {
    calls.push(args);
    return Promise.resolve(marker);
  }) as typeof fetch;
  return { calls, marker, fetchStub };
}

const load = withDataUrls(networkFetch().fetchStub);

async function bytesOf(response: Response): Promise<number[]> {
  return [...new Uint8Array(await response.arrayBuffer())];
}

describe('withDataUrls', () => {
  describe('requests that are not data: URLs', () => {
    it('are left to the original fetch with the arguments as they came', async () => {
      const { calls, marker, fetchStub } = networkFetch();
      const wrapped = withDataUrls(fetchStub);
      const request = new Request('https://example.com/a');
      const url = new URL('https://example.com/b');
      const init = { redirect: 'manual' } as const;

      assert.equal(await wrapped('https://example.com/', init), marker);
      assert.equal(await wrapped(url), marker);
      assert.equal(await wrapped(request, init), marker);
      assert.equal(await wrapped('/relative'), marker);

      assert.equal(calls.length, 4);
      assert.deepEqual(calls[0], ['https://example.com/', init]);
      assert.equal(calls[1][0], url);
      assert.equal(calls[2][0], request);
      assert.equal(calls[2][1], init);
      assert.equal(calls[3][0], '/relative');
    });

    it('are not confused by a URL that only contains data:', async () => {
      const { calls, fetchStub } = networkFetch();

      await withDataUrls(fetchStub)('https://example.com/data:text/plain,Hi');

      assert.equal(calls.length, 1);
    });
  });

  describe('a data: URL', () => {
    it('never reaches the original fetch', async () => {
      const { calls, fetchStub } = networkFetch();

      await withDataUrls(fetchStub)('data:text/plain,Hi');

      assert.equal(calls.length, 0);
    });

    it('answers 200 OK with the URL that was asked for', async () => {
      const response = await load('data:text/plain,Hi');

      assert.equal(response.status, 200);
      assert.equal(response.statusText, 'OK');
      assert.equal(response.ok, true);
      assert.equal(response.url, 'data:text/plain,Hi');
    });

    it('is read from a string, a URL and a Request alike', async () => {
      const href = 'data:text/plain,Hi';

      assert.equal(await (await load(href)).text(), 'Hi');
      assert.equal(await (await load(new URL(href))).text(), 'Hi');
      assert.equal(await (await load(new Request(href))).text(), 'Hi');
    });

    it('ignores what the URL parser strips and the case of the scheme', async () => {
      assert.equal(await (await load('  data:text/plain,Hi\n')).text(), 'Hi');
      assert.equal(await (await load('DATA:text/plain,Hi')).text(), 'Hi');
    });

    it('leaves the fragment out of the URL of the response and the body', async () => {
      const response = await load('data:text/plain,Hello#fragment');

      assert.equal(response.url, 'data:text/plain,Hello');
      assert.equal(await response.text(), 'Hello');
    });

    it('keeps the query in the body, as the standard has it', async () => {
      assert.equal(await (await load('data:text/plain,a?b=c')).text(), 'a?b=c');
    });
  });

  describe('the body', () => {
    it('is percent-decoded', async () => {
      assert.equal(await (await load('data:text/plain,Hello%2C%20world%21')).text(), 'Hello, world!');
    });

    it('is UTF-8 when it is encoded that way, however the URL was written', async () => {
      assert.deepEqual(await bytesOf(await load('data:text/plain;charset=utf-8,%E2%82%AC')), [0xe2, 0x82, 0xac]);
      assert.deepEqual(await bytesOf(await load('data:text/plain;charset=utf-8,\u20ac')), [0xe2, 0x82, 0xac]);
    });

    it('keeps bytes that are not UTF-8', async () => {
      assert.deepEqual(await bytesOf(await load('data:application/octet-stream,%FF%00a%80')), [0xff, 0x00, 0x61, 0x80]);
    });

    it('keeps a percent sign that is not followed by two hex digits', async () => {
      assert.equal(await (await load('data:text/plain,100%25%zz%4')).text(), '100%%zz%4');
    });

    it('is base64-decoded after a ;base64 suffix of any case, with spaces before it', async () => {
      assert.equal(await (await load('data:text/plain;base64,SGVsbG8=')).text(), 'Hello');
      assert.equal(await (await load('data:text/plain;BASE64,SGVsbG8=')).text(), 'Hello');
      assert.equal(await (await load('data:text/plain; base64,SGVsbG8=')).text(), 'Hello');
    });

    it('is binary when base64 says so', async () => {
      assert.deepEqual(await bytesOf(await load('data:application/octet-stream;base64,AAH/gA==')), [0x00, 0x01, 0xff, 0x80]);
    });

    it('may leave the padding of base64 out, and have it percent-encoded', async () => {
      assert.equal(await (await load('data:text/plain;base64,SGVsbG8')).text(), 'Hello');
      assert.equal(await (await load('data:text/plain;base64,SGVsbG8%3D')).text(), 'Hello');
    });

    it('is empty without data', async () => {
      assert.deepEqual(await bytesOf(await load('data:text/plain,')), []);
    });
  });

  describe('the content type', () => {
    it('is the declared one, lower-cased, with its parameters', async () => {
      const response = await load('data:IMAGE/SVG+XML ; Charset=UTF-8 ;;nonsense; =x,%3Csvg%2F%3E');

      assert.equal(response.headers.get('content-type'), 'image/svg+xml;charset=UTF-8');
    });

    it('is text/plain with the declared parameters when only they are given', async () => {
      assert.equal((await load('data:;charset=utf-8,Hi')).headers.get('content-type'), 'text/plain;charset=utf-8');
    });

    it('is text/plain in US-ASCII when none is given or it is no MIME type', async () => {
      assert.equal((await load('data:,Hi')).headers.get('content-type'), 'text/plain;charset=US-ASCII');
      assert.equal((await load('data:nonsense,Hi')).headers.get('content-type'), 'text/plain;charset=US-ASCII');
      assert.equal((await load('data:text/,Hi')).headers.get('content-type'), 'text/plain;charset=US-ASCII');
    });

    it('is without the base64 marker', async () => {
      assert.equal((await load('data:image/png;base64,AA==')).headers.get('content-type'), 'image/png');
      assert.equal((await load('data:image/png;charset=x;base64,AA==')).headers.get('content-type'), 'image/png;charset=x');
    });
  });

  describe('a data: URL that cannot be loaded', () => {
    for (const url of [
      'data:text/plain',
      'data:text/plain;base64,not base64!',
      'data:text/plain;base64,Y',
      'data:text/plain;base64,SGVsbG8=Zm9v',
    ]) {
      it(`is a network error: ${url}`, async () => {
        await assert.rejects(load(url), TypeError);
      });
    }
  });
});
