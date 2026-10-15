import type { APIContext, APIRoute } from 'astro';

type Session = NonNullable<APIContext['session']>;

interface Step {
  op: string;
  key?: string;
  value?: unknown;
}

// The calls that the tests of AstroSession make on the class itself, made on `context.session` of one request: the body is
// `{ "steps": [{ "op": "set", "key": "a", "value": 1 }, { "op": "get", "key": "a" }] }`. Every step reports what it returned
// or the message of the error it threw, so a test can see both, and the response carries the cookie that the session sets.
async function run(session: Session, { op, key, value }: Step): Promise<unknown> {
  switch (op) {
    case 'set':
      session.set(key as string, value);
      return undefined;
    case 'setFunction':
      // An object holding a function is not something that devalue can serialize.
      session.set(key as string, { fun: function () {} });
      return undefined;
    case 'get':
      return session.get(key as string);
    case 'has':
      return session.has(key as string);
    case 'delete':
      session.delete(key as string);
      return undefined;
    case 'keys':
      return [...(await session.keys())];
    case 'regenerate':
      return session.regenerate();
    case 'destroy':
      session.destroy();
      return undefined;
    case 'id':
      return session.sessionID;
    default:
      throw new Error(`Unknown step '${op}'.`);
  }
}

export const POST: APIRoute = async ({ request, session }) => {
  const { steps } = (await request.json()) as { steps: Step[] };
  const results: Record<string, unknown>[] = [];
  for (const step of steps) {
    try {
      const value = await run(session as Session, step);
      results.push({ ok: true, value, isUndefined: value === undefined });
    } catch (error) {
      results.push({ ok: false, error: error instanceof Error ? error.message : String(error) });
    }
  }

  return Response.json({ results });
};
