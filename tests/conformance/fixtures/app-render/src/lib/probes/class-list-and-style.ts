import { addAttribute } from 'astro/runtime/server/index.js';
import { toStyleString } from 'astro/runtime/server/render/util.js';

// The calls of class-list-and-style.test.ts that need no component: class:list and style values through addAttribute() and
// toStyleString(), with the arguments of the unit tests. A case is named after its test.

const classList = (value: unknown) => addAttribute(value, 'class:list').toString();
const style = (value: unknown) => addAttribute(value, 'style').toString();

export const cases: Record<string, () => unknown> = {
  // class:list
  'handles a plain string': () => classList('test expression'),
  'handles an array of strings': () => classList(['test', 'set']),
  'handles an object with boolean values': () => classList({ test: true, true: true, false: false }),
  'handles an object with truthy/falsy values': () =>
    classList({ test: 1, truthy: '0', noshow1: 0, noshow2: '', noshow3: null, noshow4: undefined }),
  'handles nested arrays and objects': () =>
    classList(['hello goodbye', { hello: true, world: true }, ['hello', 'friend']]),
  'handles conditional expressions in arrays': () => classList(['foo', false && 'bar', true && 'baz']),
  'returns empty string when all values are falsy': () => classList([false && 'empty']),
  'returns empty string for null': () => classList(null),
  'returns empty string for undefined': () => classList(undefined),

  // toStyleString
  'converts camelCase to kebab-case': () => toStyleString({ backgroundColor: 'green' }),
  'preserves CSS custom properties': () => toStyleString({ '--my-var': 'red' }),
  'joins multiple properties with semicolons': () => toStyleString({ backgroundColor: 'green', color: 'red' }),
  'handles numeric values': () => toStyleString({ zIndex: 10 }),
  'handles zero as a value': () => toStyleString({ margin: 0 }),
  'filters out empty string values': () => toStyleString({ color: '', background: 'red' }),
  'filters out whitespace-only string values': () => toStyleString({ color: '  ', background: 'red' }),
  'filters out null/undefined/boolean values': () =>
    toStyleString({ color: null, background: undefined, display: false, margin: 'auto' }),
  'preserves quoted values': () => toStyleString({ backgroundImage: 'url("a")' }),

  // style object via addAttribute
  'converts style object to attribute string': () => style({ backgroundColor: 'green' }),
  'converts style object with multiple properties': () => style({ backgroundColor: 'blue', color: 'white' }),
  'handles url() with quotes in style object': () => style({ backgroundImage: 'url("a")' }),
  'passes through string style values unchanged': () => style('background-color:red'),
  'handles style array [object, string] syntax': () => style([{ color: 'red' }, 'font-size:16px']),
};
