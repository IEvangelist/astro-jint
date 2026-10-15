import type { APIRoute } from 'astro';
import { isLocalsSerializable, trySerializeLocals } from 'astro/middleware';

// middleware/locals.test.ts calls isLocalsSerializable() and trySerializeLocals() on values it builds. Each case builds
// the value of one upstream test here, inside the runtime, and answers what the call returned or the message it threw.
class Foo {}

type DeepObject = { child?: DeepObject; value?: string };

function buildDeepObject() {
  // 10,000 levels deep: would overflow the call stack with a recursive implementation
  const deep: DeepObject = {};
  let current: DeepObject = deep;
  for (let i = 0; i < 10_000; i++) {
    current.child = {};
    current = current.child;
  }
  current.value = 'leaf';
  return deep;
}

const cases: Record<string, () => unknown> = {
  null: () => isLocalsSerializable(null),
  string: () => isLocalsSerializable('hello'),
  number: () => isLocalsSerializable(42),
  true: () => isLocalsSerializable(true),
  false: () => isLocalsSerializable(false),
  object: () => isLocalsSerializable({ a: 1, b: 'two' }),
  nested: () => isLocalsSerializable({ a: { b: { c: 3 } } }),
  array: () => isLocalsSerializable([1, 'two', null]),
  date: () => isLocalsSerializable(new Date()),
  map: () => isLocalsSerializable(new Map()),
  set: () => isLocalsSerializable(new Set()),
  'class-instance': () => isLocalsSerializable(new Foo()),
  'object-with-date': () => isLocalsSerializable({ date: new Date() }),
  deep: () => isLocalsSerializable(buildDeepObject()),
  'try-serialize': () => trySerializeLocals({ user: 'alice', count: 3 }),
  'try-serialize-date': () => trySerializeLocals({ date: new Date() }),
};

const json = (body: unknown, status = 200) =>
  new Response(JSON.stringify(body), { status, headers: { 'Content-Type': 'application/json' } });

export const GET: APIRoute = ({ params }) => {
  const run = cases[params.case ?? ''];
  if (!run) {
    return json({ error: 'unknown case' }, 404);
  }
  try {
    return json({ value: run() });
  } catch (error) {
    return json({ error: (error as Error).message });
  }
};
