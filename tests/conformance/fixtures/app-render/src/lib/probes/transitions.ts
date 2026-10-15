import {
  ViewTransitionStyleSheet,
  reEncode,
  renderTransition,
  stringifyAnimation,
  stringifyAnimations,
  toTimeValue,
} from 'astro/runtime/server/transition.js';

// The calls of transitions.test.ts, with its arguments. A case answers the value(s) that its test asserts on; the second test of
// 'reEncode' asserts three inputs, so it answers three outputs.

function renderedStyle(animation: unknown): string {
  const result = { _metadata: { extraHead: [] as unknown[] } } as any;
  renderTransition(result, 'hash', animation as any, 'name');
  return String(result._metadata.extraHead[0]);
}

export const cases: Record<string, () => unknown> = {
  // reEncode
  'passes through simple alphanumeric strings unchanged': () => [
    reEncode('hello'),
    reEncode('my-transition'),
    reEncode('Hero123'),
  ],
  'prepends underscore when string starts with a digit': () => reEncode('1hero'),
  'prepends underscore when string starts with a minus sign': () => reEncode('-hero'),
  'replaces spaces with their hex escape': () => reEncode('my hero'),
  'replaces dots with their hex escape': () => reEncode('hero.image'),
  'doubles underscores to avoid collision, and prepends _ if starts with underscore': () => reEncode('_hero'),
  'handles non-ASCII characters by copying them through': () => reEncode('h\u00e9ro'),

  // toTimeValue
  'converts 0 to "0ms"': () => toTimeValue(0),
  'converts positive number to "{n}ms"': () => [toTimeValue(300), toTimeValue(1000)],
  'passes string values through unchanged': () => [toTimeValue('0.3s'), toTimeValue('300ms')],

  // stringifyAnimation
  'serializes animation-name': () => stringifyAnimation({ name: 'fade-in' }),
  'serializes duration as ms when numeric': () => stringifyAnimation({ name: 'slide', duration: 300 }),
  'serializes easing, direction, delay, and fillMode': () =>
    stringifyAnimation({
      name: 'fade',
      easing: 'ease-in-out',
      direction: 'reverse',
      delay: 100,
      fillMode: 'both',
    }),
  'escapes CSS delimiters in animation values': () =>
    stringifyAnimation({ name: 'fade', duration: '1ms;}custom{value' }),
  'preserves CSS functions in animation values': () =>
    stringifyAnimation({ name: 'fade', duration: 'calc(1s + var(--transition-delay, 200ms))' }),
  'accepts an array of animations': () => stringifyAnimation([{ name: 'fade-in' }, { name: 'slide-in' }]),

  // stringifyAnimations
  'merges multiple animation names into one animation-name declaration': () =>
    stringifyAnimations([{ name: 'fade-in' }, { name: 'slide-in' }]),
  'merges multiple durations into one animation-duration declaration': () =>
    stringifyAnimations([
      { name: 'a', duration: 200 },
      { name: 'b', duration: 400 },
    ]),
  'handles a single animation identical to stringifyAnimation': () => ({
    single: stringifyAnimations([{ name: 'fade', duration: 300, easing: 'ease' }]),
    viaWrapper: stringifyAnimation({ name: 'fade', duration: 300, easing: 'ease' }),
  }),
  'omits animation properties that are not set': () => stringifyAnimations([{ name: 'slide' }]),
  'handles an empty array producing only animation-name with empty value': () => stringifyAnimations([]),

  // ViewTransitionStyleSheet
  'toString() includes view-transition-name declaration': () =>
    new ViewTransitionStyleSheet('astro-abc-1', 'hero').toString(),
  'addModern() adds a modern ::view-transition rule inside @layer': () => {
    const sheet = new ViewTransitionStyleSheet('astro-abc-1', 'hero');
    sheet.addModern('old', 'animation: fade-out 300ms;');
    return sheet.toString();
  },
  'addFallback() adds fallback selectors outside @layer': () => {
    const sheet = new ViewTransitionStyleSheet('astro-abc-1', 'hero');
    sheet.addFallback('old', 'animation: fade-out 300ms;');
    return sheet.toString();
  },
  'addAnimationPair() adds both modern and fallback rules for forwards direction': () => {
    const sheet = new ViewTransitionStyleSheet('astro-xyz-1', 'banner');
    sheet.addAnimationPair('forwards', 'old', { name: 'fade-out', duration: 300 });
    return sheet.toString();
  },
  'addAnimationPair() adds [data-astro-transition=back] prefix for backwards direction': () => {
    const sheet = new ViewTransitionStyleSheet('astro-xyz-1', 'banner');
    sheet.addAnimationPair('backwards', 'old', { name: 'slide-out' });
    return sheet.toString();
  },
  'addAnimationPair() quotes custom direction names in the selector': () => {
    const sheet = new ViewTransitionStyleSheet('astro-xyz-1', 'banner');
    sheet.addAnimationPair('downward', 'old', { name: 'slide-out' });
    return sheet.toString();
  },
  'addAnimationPair() supports custom direction names that are not CSS identifiers': () => {
    const sheet = new ViewTransitionStyleSheet('astro-xyz-1', 'banner');
    sheet.addAnimationPair('2nd step', 'new', { name: 'slide-in' });
    sheet.addAnimationPair('a"b]c{d}', 'old', { name: 'slide-out' });
    return sheet.toString();
  },
  'addAnimationRaw() adds same rule to both modern and fallback': () => {
    const sheet = new ViewTransitionStyleSheet('astro-abc-1', 'hero');
    sheet.addAnimationRaw('new', 'animation: none; mix-blend-mode: normal;');
    return sheet.toString();
  },
  'produces empty @layer block when no modern rules added': () => {
    const sheet = new ViewTransitionStyleSheet('astro-abc-1', 'hero');
    sheet.addFallback('old', 'animation: none;');
    return sheet.toString();
  },

  // renderTransition
  'keeps animation values inside the generated style element': () =>
    renderedStyle({
      forwards: {
        old: { name: 'fade', duration: '</style><script>test</script>' },
        new: { name: 'fade' },
      },
      backwards: {
        old: { name: 'fade' },
        new: { name: 'fade' },
      },
    }),
  'keeps animation values inside their generated CSS declarations': () =>
    renderedStyle({
      forwards: {
        old: { name: 'fade', duration: '1ms;}custom{value' },
        new: { name: 'fade' },
      },
      backwards: {
        old: { name: 'fade' },
        new: { name: 'fade' },
      },
    }),
};
