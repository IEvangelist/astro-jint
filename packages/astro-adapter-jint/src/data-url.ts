/**
 * `data:` URLs for `fetch()`, which the one of the engine cannot load: it speaks HTTP and HTTPS and rejects
 * everything else with "Failed to fetch". The Fetch standard has `data:` URLs answered by the fetch itself, and
 * Astro's image endpoint relies on that for an SVG that `image.remotePatterns` allows as a `data:` URL.
 *
 * Nothing is requested from anywhere, so such a URL never reaches the host's `FetchUrlFilter`.
 */

const DATA_SCHEME = /^[\0-\x20]*data:/i;
const BASE64_SUFFIX = /;[ ]*base64$/i;
const DEFAULT_MIME_TYPE = 'text/plain;charset=US-ASCII';
const MIME_ESSENCE = /^[!#$%&'*+.^_`|~0-9a-z-]+\/[!#$%&'*+.^_`|~0-9a-z-]+$/i;

const encoder = new TextEncoder();

function hexValue(byte: number | undefined): number {
  if (byte === undefined) {
    return -1;
  }
  if (byte >= 0x30 && byte <= 0x39) {
    return byte - 0x30;
  }
  const lower = byte | 0x20;
  return lower >= 0x61 && lower <= 0x66 ? lower - 0x57 : -1;
}

/** The percent-decoding of the URL standard, which leaves a `%` alone unless two hex digits follow it. */
function percentDecode(text: string): Uint8Array<ArrayBuffer> {
  try {
    // UTF-8 survives the round trip, so for the usual text this decodes the same and runs natively.
    return encoder.encode(decodeURIComponent(text));
  } catch {
    // A stray `%` or bytes that are not UTF-8 need the byte by byte way.
  }

  const input = encoder.encode(text);
  const output = new Uint8Array(input.length);
  let length = 0;
  for (let index = 0; index < input.length; index++) {
    const high = input[index] === 0x25 ? hexValue(input[index + 1]) : -1;
    const low = high < 0 ? -1 : hexValue(input[index + 2]);
    if (low >= 0) {
      output[length++] = (high << 4) | low;
      index += 2;
    } else {
      output[length++] = input[index];
    }
  }
  return output.slice(0, length);
}

/** `atob` is the forgiving-base64 decode of the standard and throws for text that is not base64. */
function decodeBase64(text: string): Uint8Array<ArrayBuffer> {
  const binary = atob(
    text.includes('%') ? text.replace(/%([0-9a-f]{2})/gi, (_, hex: string) => String.fromCharCode(parseInt(hex, 16))) : text,
  );
  const bytes = new Uint8Array(binary.length);
  for (let index = 0; index < binary.length; index++) {
    bytes[index] = binary.charCodeAt(index);
  }
  return bytes;
}

/** The type and subtype are lower-cased and checked; the parameters stay as written, but for their names and the spaces around them. */
function mimeTypeOf(declared: string): string {
  const [essence, ...parameters] = (declared.startsWith(';') ? `text/plain${declared}` : declared)
    .split(';')
    .map((part) => part.trim());
  if (!MIME_ESSENCE.test(essence)) {
    return DEFAULT_MIME_TYPE;
  }

  const kept = parameters.flatMap((parameter) => {
    const equals = parameter.indexOf('=');
    return equals > 0 ? [parameter.slice(0, equals).toLowerCase() + parameter.slice(equals)] : [];
  });
  return [essence.toLowerCase(), ...kept].join(';');
}

function loadDataUrl(text: string): Response {
  const url = new URL(text);
  url.hash = '';
  const href = url.href;

  const rest = href.slice('data:'.length);
  const comma = rest.indexOf(',');
  if (comma < 0) {
    throw new TypeError('Failed to fetch');
  }

  const declared = rest.slice(0, comma).trim();
  const base64 = BASE64_SUFFIX.test(declared);
  let body: Uint8Array<ArrayBuffer>;
  try {
    body = base64 ? decodeBase64(rest.slice(comma + 1)) : percentDecode(rest.slice(comma + 1));
  } catch {
    throw new TypeError('Failed to fetch');
  }

  const response = new Response(body, {
    status: 200,
    statusText: 'OK',
    headers: { 'content-type': mimeTypeOf(base64 ? declared.replace(BASE64_SUFFIX, '') : declared) },
  });
  // The URL of a response is the one that was requested. Astro asks for it to check what it loaded.
  Object.defineProperty(response, 'url', { value: href });
  return response;
}

/** Wraps `fetch` so that it answers `data:` URLs itself and leaves every other request to the original. */
export function withDataUrls(original: typeof fetch): typeof fetch {
  return (input: RequestInfo | URL, init?: RequestInit) => {
    const text = input instanceof Request ? input.url : String(input);
    if (!DATA_SCHEME.test(text)) {
      return original(input, init);
    }

    try {
      return Promise.resolve(loadDataUrl(text));
    } catch (error) {
      return Promise.reject(error);
    }
  };
}
