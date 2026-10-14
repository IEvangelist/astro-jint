// Adapted from the upstream fixture: ?delay= sets the pause before each chunk, in milliseconds (1000 upstream).
export const GET = ({ locals, url }) => {
    const delay = Number(url.searchParams.get('delay') ?? 1000);
    let sentChunks = 0;

    const readableStream = new ReadableStream({
        async pull(controller) {
            if (sentChunks === 3) return controller.close();
            else sentChunks++;

            await new Promise(resolve => setTimeout(resolve, delay));
            controller.enqueue(new TextEncoder().encode('hello\n'));
        },
        cancel() {
            locals.cancelledByTheServer = true;
        }
    });

    return new Response(readableStream, {
        headers: {
            "Content-Type": "text/event-stream"
        }
    });
}
