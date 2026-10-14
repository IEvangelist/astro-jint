// Adapted from the upstream fixture: the Jint runtime has no node:crypto, and the host hands the whole request body to
// the script, so the body is read at once and hashed with Web Crypto.
export async function POST({ request }: { request: Request }) {
	const digest = await crypto.subtle.digest('SHA-256', await request.arrayBuffer());

	return new Response(digest, {
		headers: {
			'Content-Type': 'application/octet-stream'
		}
	});
}
