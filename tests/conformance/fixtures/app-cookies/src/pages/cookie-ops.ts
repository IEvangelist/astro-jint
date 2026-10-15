import type { APIContext, APIRoute } from 'astro';
import {
  attachCookiesToResponse,
  getSetCookiesFromResponse,
} from '../../node_modules/astro/dist/core/cookies/response.js';

// The Astro.cookies calls of the unit tests of cookies (packages/astro/test/units/cookies/{get,set,delete,has,merge,error}.test.ts).
// Upstream builds an AstroCookies object by hand and reads its results; here the request brings the Cookie header and the endpoint
// runs the steps of ?steps=[...] on the cookies of the request, then answers with what each step gave and with cookies.headers().
// The Set-Cookie lines of the response are what the host made of the same cookies.
export const prerender = false;

type Cookies = APIContext['cookies'];

type Step = {
  op: 'get' | 'has' | 'set' | 'delete' | 'merge' | 'headers' | 'roundtrip' | 'markResponseSent';
  name?: string;
  value?: unknown;
  options?: Record<string, unknown>;
  // The decoders of get(): 'base64' is the one of 'gets the cookie value with custom decode', 'record-empty' the spy of
  // 'passes an empty cookie value to a custom decoder' (it answers the values it received).
  decode?: 'base64' | 'record-empty';
  // set() with `encode: (value) => value`.
  encode?: 'identity';
  // The conversion of the cookie that get() found.
  as?: 'json' | 'number' | 'boolean';
  // merge and roundtrip: the steps that fill the AstroCookies object that is merged or attached.
  source?: Step[];
  attach?: boolean;
};

const responseSent = Symbol.for('astro.responseSent');
const base64Alphabet = 'ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789+/';

function decodeBase64(text: string): string {
  let bits = 0;
  let length = 0;
  let result = '';
  for (const char of text) {
    const index = base64Alphabet.indexOf(char);
    if (index < 0) continue;
    bits = (bits << 6) | index;
    length += 6;
    if (length >= 8) {
      length -= 8;
      result += String.fromCharCode((bits >> length) & 0xff);
    }
  }
  return result;
}

// A second AstroCookies object, made with the class of the cookies of the request (merge() reads private fields of it).
function createCookies(cookies: Cookies, request: Request): Cookies {
  const Constructor = cookies.constructor as new (request: Request, logger: { warn(): void }) => Cookies;
  return new Constructor(request, { warn() {} });
}

function apply(cookies: Cookies, step: Step, request: Request): Record<string, unknown> {
  switch (step.op) {
    case 'get': {
      const decoded: string[] = [];
      let options: { decode: (value: string) => string } | undefined;
      if (step.decode === 'base64') {
        options = { decode: decodeBase64 };
      } else if (step.decode === 'record-empty') {
        options = {
          decode(value) {
            decoded.push(value);
            return 'decoded empty value';
          },
        };
      }
      const cookie = cookies.get(step.name!, options);
      if (!cookie) return { found: false };
      const result: Record<string, unknown> = { found: true, value: cookie.value, decoded };
      if (step.as === 'json') {
        const json = cookie.json();
        result.type = typeof json;
        result.json = json;
      } else if (step.as === 'number') {
        const number = cookie.number();
        result.type = typeof number;
        result.isNaN = Number.isNaN(number);
        result.number = Number.isNaN(number) ? null : number;
      } else if (step.as === 'boolean') {
        const boolean = cookie.boolean();
        result.type = typeof boolean;
        result.boolean = boolean;
      }
      return result;
    }
    case 'has':
      return { has: cookies.has(step.name!) };
    case 'set': {
      const options: Record<string, unknown> = { ...step.options };
      if (step.encode === 'identity') options.encode = (value: string) => value;
      cookies.set(step.name!, step.value as string, options);
      return {};
    }
    case 'delete': {
      const options: Record<string, unknown> = { ...step.options };
      if (typeof options.expires === 'string') options.expires = new Date(options.expires);
      cookies.delete(step.name!, options);
      return {};
    }
    case 'merge': {
      const source = createCookies(cookies, request);
      run(source, step.source ?? [], request);
      cookies.merge(source);
      return {};
    }
    case 'headers':
      return { headers: Array.from(cookies.headers()) };
    case 'roundtrip': {
      const source = createCookies(cookies, request);
      run(source, step.source ?? [], request);
      const response = new Response(null);
      if (step.attach) attachCookiesToResponse(response, source);
      return { setCookies: Array.from(getSetCookiesFromResponse(response)) };
    }
    case 'markResponseSent':
      Reflect.set(request, responseSent, true);
      return {};
  }
}

function run(cookies: Cookies, steps: Step[], request: Request) {
  return steps.map((step) => {
    try {
      return apply(cookies, step, request);
    } catch (error) {
      return { error: (error as Error).name };
    }
  });
}

export const GET: APIRoute = ({ cookies, request, url }) => {
  const steps: Step[] = JSON.parse(url.searchParams.get('steps') ?? '[]');
  let results;
  try {
    results = run(cookies, steps, request);
  } finally {
    Reflect.deleteProperty(request, responseSent);
  }
  return new Response(JSON.stringify({ results, headers: Array.from(cookies.headers()) }), {
    headers: { 'Content-Type': 'application/json' },
  });
};
