import type { APIRoute } from 'astro';
import { handleImageRequest, type LocalImageLoadResult } from 'astro/assets/endpoint/shared';
import { host } from './host.js';

/**
 * Replacement for Astro's image endpoint, which the adapter wires up as `image.endpoint.entrypoint`.
 *
 * The stock endpoints cannot run here. The generic one fetches local images from its own origin, and
 * the Node one reads them from the file system, while this engine has neither a network route to
 * itself nor a file system. The checks that matter (allowed remote domains, redirect validation,
 * SVG conversion) all live in Astro's shared `handleImageRequest`, so that is reused as is and only
 * the way a local image is loaded differs: the ASP.NET Core host hands out the bytes of any file
 * below `dist/client`.
 *
 * The configured image service still decides what happens to those bytes. With the default
 * passthrough service they are served unchanged, because Sharp does not run in Jint.
 */

// Local paths are resolved against a fixed origin so the URL parser normalizes them the same way
// `new URL('.' + src, outDir)` does in the Node endpoint. The client output is a directory below that
// origin's root, as it is below the root of a disk, so a path that climbs out of it ends outside of the
// directory and is refused. Against the root itself the parser would drop the surplus `..` segments.
const CLIENT_DIRECTORY = '/client/';
const LOCAL_ORIGIN = `http://local.invalid${CLIENT_DIRECTORY}`;

/**
 * A `Uint8Array` that is cheap to hash. Astro derives the `ETag` of a processed image from
 * `data.toString()`, which for a plain typed array is every byte as decimal text; hashing that
 * in an interpreter takes seconds for a photo. The host computes a real digest of the bytes instead.
 */
function toImageBuffer(data: ArrayBuffer): Buffer {
  const bytes = new Uint8Array(data);
  Object.defineProperty(bytes, 'toString', {
    configurable: true,
    value: () => host?.sha256?.(data) ?? Array.prototype.join.call(bytes, ','),
  });
  return bytes as unknown as Buffer;
}

/** The only `Buffer` member Astro's shared image code uses: `Buffer.from(await res.arrayBuffer())`. */
const bufferShim = {
  from(value: ArrayBuffer | ArrayBufferView): Buffer {
    const data = ArrayBuffer.isView(value)
      ? value.buffer.slice(value.byteOffset, value.byteOffset + value.byteLength)
      : value;
    return toImageBuffer(data as ArrayBuffer);
  },
};

/**
 * Jint has no `Buffer`. The shim is only there while an image request is being handled, so no
 * other code ever sees a `Buffer` that is not the real thing. That is safe because an engine
 * handles one request at a time.
 */
async function withBuffer<T>(run: () => Promise<T>): Promise<T> {
  const global = globalThis as { Buffer?: unknown };
  if (global.Buffer !== undefined) {
    return run();
  }
  global.Buffer = bufferShim;
  try {
    return await run();
  } finally {
    delete global.Buffer;
  }
}

async function loadLocalImage(src: string): Promise<LocalImageLoadResult> {
  let pathname: string;
  try {
    if (!URL.canParse(`.${src}`, LOCAL_ORIGIN)) {
      return { kind: 'invalid-path' };
    }
    pathname = new URL(`.${src}`, LOCAL_ORIGIN).pathname;
  } catch {
    return { kind: 'invalid-path' };
  }

  // Outside of the client output or the output itself, which Node refuses too: it is no file to load.
  if (pathname.length <= CLIENT_DIRECTORY.length || !pathname.startsWith(CLIENT_DIRECTORY)) {
    return { kind: 'invalid-path' };
  }

  // The host resolves the path the way it serves static files, base included, and refuses anything
  // that is hidden, outside `dist/client` or not an existing file.
  const bytes = host?.readClientBytes?.(pathname.slice(CLIENT_DIRECTORY.length - 1));
  return bytes ? { kind: 'loaded', buffer: toImageBuffer(bytes) } : { kind: 'not-found' };
}

const SIGNATURES: ReadonlyArray<readonly [type: string, offset: number, bytes: readonly number[]]> = [
  ['image/png', 0, [0x89, 0x50, 0x4e, 0x47, 0x0d, 0x0a, 0x1a, 0x0a]],
  ['image/jpeg', 0, [0xff, 0xd8, 0xff]],
  ['image/gif', 0, [0x47, 0x49, 0x46, 0x38]],
  ['image/webp', 8, [0x57, 0x45, 0x42, 0x50]],
  ['image/avif', 4, [0x66, 0x74, 0x79, 0x70, 0x61, 0x76, 0x69]],
  ['image/bmp', 0, [0x42, 0x4d]],
  ['image/x-icon', 0, [0x00, 0x00, 0x01, 0x00]],
  ['image/tiff', 0, [0x49, 0x49, 0x2a, 0x00]],
  ['image/tiff', 0, [0x4d, 0x4d, 0x00, 0x2a]],
];

const EXTENSIONS: Record<string, string> = {
  avif: 'image/avif',
  bmp: 'image/bmp',
  gif: 'image/gif',
  ico: 'image/x-icon',
  jpeg: 'image/jpeg',
  jpg: 'image/jpeg',
  png: 'image/png',
  svg: 'image/svg+xml',
  tif: 'image/tiff',
  tiff: 'image/tiff',
  webp: 'image/webp',
};

function sniffImageType(bytes: Uint8Array): string | undefined {
  return SIGNATURES.find(([, offset, signature]) =>
    signature.every((byte, index) => bytes[offset + index] === byte),
  )?.[0];
}

function imageTypeFromExtension(source: string | null): string | undefined {
  const path = (source ?? '').split(/[?#]/)[0];
  const extension = path.slice(path.lastIndexOf('/') + 1).split('.').pop()?.toLowerCase();
  return extension ? EXTENSIONS[extension] : undefined;
}

/**
 * Nothing here converts an image, so what is served is the source, whatever format was asked for.
 * Astro labels the response with the requested format, or `image/null` when the passthrough service
 * picked none, so the label is corrected to the type of the bytes that are actually sent.
 */
async function withImageContentType(request: Request, response: Response): Promise<Response> {
  if (response.status !== 200) {
    return response;
  }

  const declared = response.headers.get('content-type');
  const body = await response.arrayBuffer();
  const type =
    sniffImageType(new Uint8Array(body)) ??
    (declared !== 'image/null' && declared !== 'image/undefined' ? declared : null) ??
    imageTypeFromExtension(new URL(request.url).searchParams.get('href')) ??
    'application/octet-stream';

  const headers = new Headers(response.headers);
  headers.set('content-type', type);
  return new Response(body, { status: response.status, headers });
}

export const GET: APIRoute = async ({ request, logger }) => {
  try {
    const response = await withBuffer(() => handleImageRequest({ request, loadLocalImage, logger }));
    return await withImageContentType(request, response);
  } catch (error) {
    logger.error(`Could not process image request: ${error}`);
    return new Response('Internal Server Error', { status: 500 });
  }
};
