import {
  escapeHTML,
  escapeStyleText,
  isHTMLString,
  markHTMLString,
  stringifyForScript,
  unescapeHTML,
} from 'astro/runtime/server/escape.js';

// The calls of escape.test.ts, with the arguments of the unit tests. A case is named after its test; the em dash of the titles of
// the unescapeHTML tests is written \u2014 here and in the test class.

async function collect(chunks: AsyncIterable<unknown>, decode = false): Promise<string> {
  const decoder = new TextDecoder();
  const parts: string[] = [];
  for await (const chunk of chunks) {
    parts.push(decode && chunk instanceof Uint8Array ? decoder.decode(chunk) : String(chunk));
  }
  return parts.join('');
}

export const cases: Record<string, () => unknown> = {
  'wraps a string in an HTMLString instance': () => {
    const result = markHTMLString('hello');
    return { isHTMLString: isHTMLString(result), text: String(result) };
  },

  'returns the value unchanged if already an HTMLString': () => {
    const original = markHTMLString('hello');
    const again = markHTMLString(original);
    return { isHTMLString: isHTMLString(again), text: String(again) };
  },

  'returns true for HTMLString instances': () => isHTMLString(markHTMLString('hello')),

  'returns false for plain strings': () => isHTMLString('hello'),

  'returns false for null and undefined': () => [isHTMLString(null), isHTMLString(undefined)],

  'returns false for numbers and objects': () => [isHTMLString(42), isHTMLString({})],

  'escapes < and > to &lt; and &gt;': () => String(escapeHTML('<script>alert("xss")</script>')),

  'escapes & to &amp;': () => String(escapeHTML('a & b')),

  'escapes " to &quot;': () => String(escapeHTML('"hello"')),

  "escapes ' to &#39;": () => String(escapeHTML("it's")),

  'escapes a plain string and returns a string': () => {
    const result = escapeHTML('<b>test</b>');
    return { type: typeof result, value: result };
  },

  'escapes less-than signs in inline style text as CSS': () =>
    escapeStyleText('animation-name: </STYLE><script>test</script>'),

  'preserves ordinary inline style text': () => escapeStyleText('animation-duration: 300ms'),

  'serializes values without literal less-than signs for inline scripts': () =>
    stringifyForScript({ value: '</SCRIPT><script>test</script>' }),

  'can take a string of HTML \u2014 Migrated from test/set-html.test.js': () => {
    const result = unescapeHTML('<b>bold</b>');
    return { isHTMLString: isHTMLString(result), text: String(result) };
  },

  'can take a Promise to a string of HTML \u2014 Migrated from test/set-html.test.js': async () => {
    const result = await unescapeHTML(Promise.resolve('<b>bold</b>'));
    return { isHTMLString: isHTMLString(result), text: String(result) };
  },

  'can take an Iterator \u2014 Migrated from test/set-html.test.js': async () => {
    function* gen() {
      yield '<li>1</li>';
      yield '<li>2</li>';
    }
    return await collect(unescapeHTML(gen()) as AsyncIterable<unknown>);
  },

  'can take an AsyncIterator': async () => {
    async function* gen() {
      yield '<li>a</li>';
      yield '<li>b</li>';
    }
    return await collect(unescapeHTML(gen()) as AsyncIterable<unknown>);
  },

  'can take a Response': async () => {
    const response = new Response('<p>hello</p>', { headers: { 'content-type': 'text/html' } });
    return await collect(unescapeHTML(response) as AsyncIterable<unknown>, true);
  },

  'can take a ReadableStream': async () => {
    const stream = new ReadableStream({
      start(controller) {
        controller.enqueue('<span>stream</span>');
        controller.close();
      },
    });
    return await collect(unescapeHTML(stream) as AsyncIterable<unknown>);
  },
};
