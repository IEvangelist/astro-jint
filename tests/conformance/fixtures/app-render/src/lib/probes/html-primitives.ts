import {
  Fragment,
  render as renderTemplate,
  renderComponent,
  renderHTMLElement,
} from 'astro/runtime/server/index.js';
import {
  addAttribute,
  defineScriptVars,
  formatList,
  internalSpreadAttributes,
  renderElement,
  toAttributeString,
  toStyleString,
} from 'astro/runtime/server/render/util.js';

// The calls of html-primitives.test.ts that need no component or route, with the arguments of the unit tests. A case is named
// after its test (prefixed with its describe where two tests share a title).

const scriptVars = (vars: Record<string, unknown>) => String(defineScriptVars(vars));

// renderHTMLElement() resolves the tag name through customElements.getName(), a global of browsers that the unit test stubs.
async function withCustomElements<T>(run: () => Promise<T>): Promise<T> {
  const original = (globalThis as any).customElements;
  (globalThis as any).customElements = { getName: () => 'my-el' };
  try {
    return await run();
  } finally {
    (globalThis as any).customElements = original;
  }
}

async function fragmentStreaming() {
  let resolveAsync!: () => void;
  const asyncChild = new Promise<void>((resolve) => {
    resolveAsync = resolve;
  });
  const DEFAULT_RESULT = { clientDirectives: new Map() };

  // A Fragment whose default slot has a sync <p> followed by an async <p>.
  const renderInstance = renderComponent(
    DEFAULT_RESULT as any,
    'Fragment',
    Fragment,
    {},
    {
      default: (_result: any) =>
        renderTemplate`<p id="sync">sync</p>${asyncChild.then(() => renderTemplate`<p id="async">async</p>`)}`,
    },
  );

  const chunks: string[] = [];
  const destination = {
    write(chunk: unknown) {
      chunks.push(String(chunk));
    },
  };

  // Start rendering without awaiting it, so that the state in the middle can be seen.
  const instance = await Promise.resolve(renderInstance);
  const renderPromise = (instance as any).render(destination);
  await Promise.resolve();
  const syncFlushed = chunks.join('').includes('sync');

  resolveAsync();
  await renderPromise;
  const html = chunks.join('');
  return {
    syncFlushed,
    hasSync: html.includes('sync'),
    hasAsync: html.includes('async'),
    syncBeforeAsync: html.indexOf('sync') < html.indexOf('async'),
  };
}

export const cases: Record<string, () => unknown> = {
  // toAttributeString
  'escapes & to &amp;': () => toAttributeString('a&b'),
  'escapes " to &quot;': () => toAttributeString('say "hello"'),
  'escapes both & and " in the same string': () => toAttributeString('"a&b"'),
  'passes through normal strings unchanged': () => toAttributeString('hello world'),
  'does not escape when shouldEscape is false': () => toAttributeString('a&b', false),
  'coerces non-string values to string': () => [toAttributeString(42), toAttributeString(true)],

  // toStyleString
  'converts camelCase properties to kebab-case': () => toStyleString({ backgroundColor: 'red' }),
  'leaves lowercase properties unchanged': () => toStyleString({ color: 'blue' }),
  'leaves CSS custom properties (--) unchanged': () => toStyleString({ '--my-var': '10px' }),
  'joins multiple properties with semicolons': () => toStyleString({ color: 'red', fontSize: '16px' }),
  'includes numeric values': () => [toStyleString({ opacity: 0 }), toStyleString({ zIndex: 99 })],
  'filters out empty string values': () => toStyleString({ color: '' }),
  'filters out null and undefined values': () => toStyleString({ color: null, background: undefined }),
  'returns empty string for empty object': () => toStyleString({}),

  // defineScriptVars
  'generates const declaration for a string value': () => scriptVars({ name: 'Astro' }),
  'generates const declaration for a number value': () => scriptVars({ count: 42 }),
  'generates const declaration for an object value': () => scriptVars({ config: { debug: true } }),
  'generates multiple const declarations': () => scriptVars({ a: 1, b: 2 }),
  'sanitizes </script> to prevent XSS injection': () => scriptVars({ evil: '</script>' }),
  'sanitizes case-insensitive </script> variants': () =>
    ['</Script>', '</SCRIPT>', '</sCrIpT>'].map((tag) => scriptVars({ evil: tag })),
  'sanitizes </script> with trailing whitespace before >': () =>
    ['</script >', '</script\t>', '</script\n>'].map((tag) => scriptVars({ evil: tag })),
  'sanitizes self-closing </script/>': () => scriptVars({ evil: '</script/>' }),
  'handles undefined values without throwing': () => scriptVars({ undef: undefined }),
  'converts keys with spaces to valid JS identifiers': () => scriptVars({ 'my key': 'value' }),

  // formatList
  'returns the single item for a one-element array': () => formatList(['only']),
  'joins two items with "or"': () => formatList(['a', 'b']),
  'joins three items with comma and "or"': () => formatList(['a', 'b', 'c']),
  'joins four or more items correctly': () => formatList(['a', 'b', 'c', 'd']),

  // renderElement
  'renders a void element without a closing tag': () => renderElement('br', { props: {}, children: '' }),
  'renders a void element with attributes': () =>
    renderElement('img', { props: { src: '/hero.png', alt: 'Hero' }, children: '' }),
  'renders a non-void element with children': () => renderElement('div', { props: {}, children: 'Hello' }),
  'renders a non-void element with attributes and children': () =>
    renderElement('span', { props: { class: 'bold' }, children: 'text' }),
  'renders a script element with children': () =>
    renderElement('script', { props: {}, children: 'console.log(1)' }),
  'does not render lang, data-astro-id props': () =>
    renderElement('style', { props: { lang: 'scss', 'data-astro-id': 'abc' }, children: 'body{}' }),
  'injects defineVars into script children': () =>
    renderElement('script', { props: { 'define:vars': { count: 5 } }, children: 'console.log(count)' }),

  // addAttribute rejects invalid attribute keys
  'drops keys containing double quotes': () => String(addAttribute('val', 'x" onmousemove="alert(1)" y')),
  'drops keys containing spaces': () => String(addAttribute('val', ' onerror=alert(1) ')),
  'drops keys containing >': () => String(addAttribute('val', 'x><script>alert(1)</script')),
  'drops keys containing single quotes': () => String(addAttribute('val', "x' onclick='alert(1)")),
  'drops keys containing =': () => String(addAttribute('val', 'x=y')),
  'allows normal attribute names': () => [
    String(addAttribute('v', 'id')),
    String(addAttribute('v', 'data-foo')),
    String(addAttribute('v', 'aria-label')),
  ],
  'allows namespaced attributes': () => [
    String(addAttribute('v', 'on:click')),
    String(addAttribute('v', 'xmlns:happy')),
  ],

  // spreadAttributes rejects invalid attribute keys
  'spreadAttributes rejects invalid attribute keys: drops malicious keys while keeping valid ones': () =>
    String(
      internalSpreadAttributes({ class: 'safe', 'x" onclick="alert(1)" y': 'bad', id: 'ok' }, true, 'div'),
    ),

  // renderHTMLElement rejects invalid attribute keys
  'renderHTMLElement rejects invalid attribute keys: drops malicious keys while keeping valid ones': () =>
    withCustomElements(async () =>
      String(
        await renderHTMLElement(
          {} as any,
          class {} as any,
          {
            'onmouseover=alert(document.domain) x': 'y',
            'x><script>alert(1)</script>': 'z',
            'data-safe': 'ok',
          },
          {},
        ),
      ),
    ),
  'preserves namespaced and normal attribute names': () =>
    withCustomElements(async () =>
      String(
        await renderHTMLElement(
          {} as any,
          class {} as any,
          { id: 'a', 'data-foo': 'b', 'on:click': 'c' },
          {},
        ),
      ),
    ),

  // Correctly serializes boolean attributes (#astro-basic)
  'renders empty-value attribute for data-* attr with no value': () =>
    String(addAttribute('', 'data-something')),
  'renders empty-value attribute for arbitrary attr with no value': () =>
    String(addAttribute('', 'not-data-ok')),

  // Allows using the Fragment element: the Fragment of the runtime, mid-flight
  'streams sync siblings before async children resolve (issue #13283)': fragmentStreaming,
};
