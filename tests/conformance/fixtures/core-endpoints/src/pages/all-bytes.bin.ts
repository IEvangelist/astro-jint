// Not upstream: every byte value, so that a text encoding of the body anywhere between the engine and the client shows.
export const GET = () => new Response(Uint8Array.from({ length: 256 }, (_, index) => index), {
  headers: { 'Content-Type': 'application/octet-stream' },
});
