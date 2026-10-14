using Jint;

// Quick JS-compat probe of things SSR code in Astro sites commonly relies on.
internal static class Probes
{
    private static readonly (string Label, string Js)[] Snippets =
    [
        ("error.stack first line", "(() => { try { throw new Error('boom'); } catch (e) { return JSON.stringify(String(e.stack).split('\\n')[0]); } })()"),
        ("Error own props / stack kind", "(() => { const e = new Error('x'); const own = Object.getOwnPropertyDescriptor(e, 'stack'); const proto = Object.getOwnPropertyDescriptor(Error.prototype, 'stack'); return JSON.stringify(Object.getOwnPropertyNames(e)) + ' own:' + (own ? ('get' in own ? 'accessor' : 'data') : 'none') + ' proto:' + (proto ? ('get' in proto ? 'accessor' : 'data') : 'none') + ' captureStackTrace:' + typeof Error.captureStackTrace; })()"),
        ("missing globals", "['process','Buffer','setTimeout','setImmediate','queueMicrotask','structuredClone','TextEncoder','TextDecoder','URL','URLSearchParams','URLPattern','Blob','File','FormData','ReadableStream','TransformStream','CompressionStream','AbortController','performance','crypto','navigator','WebAssembly','Intl','fetch','Request','Response','Headers','atob','btoa','global','self','window'].filter(n => typeof globalThis[n] === 'undefined').join(',')"),
        ("Intl.DateTimeFormat long", "new Intl.DateTimeFormat('en-US', { dateStyle: 'long', timeZone: 'UTC' }).format(new Date(0))"),
        ("Date.toLocaleDateString", "new Date(0).toLocaleDateString('en-US', { year: 'numeric', month: 'short', day: 'numeric', timeZone: 'UTC' })"),
        ("Number.toLocaleString", "(1234567.891).toLocaleString('en-US')"),
        ("Intl.NumberFormat de-DE EUR", "new Intl.NumberFormat('de-DE', { style: 'currency', currency: 'EUR' }).format(1234.5)"),
        ("Intl.PluralRules", "new Intl.PluralRules('en-US').select(1) + '/' + new Intl.PluralRules('en-US').select(2)"),
        ("Intl.RelativeTimeFormat", "new Intl.RelativeTimeFormat('en', { numeric: 'auto' }).format(-1, 'day')"),
        ("Intl.ListFormat", "new Intl.ListFormat('en', { style: 'long', type: 'conjunction' }).format(['a', 'b', 'c'])"),
        ("Intl.Segmenter words", "[...new Intl.Segmenter('en', { granularity: 'word' }).segment('Hello world')].length"),
        ("Intl default time zone", "Intl.DateTimeFormat().resolvedOptions().timeZone"),
        ("regex lookbehind", "/(?<=\\$)\\d+/.exec('cost $42')[0]"),
        ("regex unicode property", "/\\p{L}+/u.exec('héllo wörld')[0]"),
        ("regex named groups", "'2026-10-14'.match(/(?<y>\\d+)-(?<m>\\d+)/).groups.y"),
        ("ES2023 toSorted/at/groupBy", "[[3,1,2].toSorted().join(), [1,2,3].at(-1), JSON.stringify(Object.groupBy([1,2,3], x => x % 2 ? 'odd' : 'even'))].join(' ')"),
        ("ES2024+ typeof checks", "[typeof Promise.withResolvers, typeof Array.fromAsync, typeof Set.prototype.union, typeof Iterator, typeof Uint8Array.fromBase64].join(' ')"),
        ("structuredClone Date", "structuredClone({ a: new Date(0) }).a instanceof Date"),
        ("crypto uuid + random", "crypto.randomUUID().length + ' ' + crypto.getRandomValues(new Uint8Array(4)).length"),
        ("URL resolution", "new URL('../a?x=1#h', 'http://localhost/b/c').href"),
        ("Response.json()", "typeof Response.json === 'function' ? Response.json({ a: 1 }).headers.get('content-type') : 'missing'"),
        ("Headers.getSetCookie", "(() => { const h = new Headers(); h.append('set-cookie', 'a=1'); h.append('set-cookie', 'b=2'); return JSON.stringify(h.getSetCookie ? h.getSetCookie() : 'no getSetCookie'); })()"),
        ("fetch denied by UrlFilter", "fetch('http://example.com/').then(() => 'ALLOWED', e => 'rejected: ' + String(e && e.message || e).slice(0, 90))"),
        ("setTimeout + UnwrapIfPromise", "new Promise(r => setTimeout(() => r('fired'), 15))"),
        ("multipart round-trip (Jint)", """
            (async () => {
              try {
                const fd = new FormData();
                fd.append('field', 'v1');
                fd.append('upload', new Blob(['file-bytes']), 'a.txt');
                const req = new Request('http://localhost/x', { method: 'POST', body: fd });
                const parsed = await req.formData();
                return [...parsed.entries()].map(([k, v]) => k + '=' + (typeof v === 'string' ? v : v.name + ':' + v.size)).join(',');
              } catch (e) { return 'ERR ' + e.name + ': ' + e.message; }
            })()
            """),
        ("multipart unquoted boundary", MultipartProbe("boundary=abc", Browser)),
        ("multipart quoted boundary", MultipartProbe("boundary=\"abc\"", Browser)),
        ("multipart unquoted name", MultipartProbe("boundary=abc", "`--abc\\r\\nContent-Disposition: form-data; name=field\\r\\n\\r\\nv1\\r\\n--abc--\\r\\n`")),
        ("multipart part Content-Type", MultipartProbe("boundary=abc", "`--abc\\r\\nContent-Type: text/plain; charset=utf-8\\r\\nContent-Disposition: form-data; name=\"field\"\\r\\n\\r\\nv1\\r\\n--abc--\\r\\n`")),
        ("multipart filename* param", MultipartProbe("boundary=abc", "`--abc\\r\\nContent-Disposition: form-data; name=\"upload\"; filename=\"a.txt\"; filename*=utf-8''a.txt\\r\\n\\r\\nfile-bytes\\r\\n--abc--\\r\\n`")),
        ("multipart unquoted filename", MultipartProbe("boundary=abc", "`--abc\\r\\nContent-Disposition: form-data; name=upload; filename=a.txt\\r\\n\\r\\nfile-bytes\\r\\n--abc--\\r\\n`")),
    ];

    private const string Browser = "`--abc\\r\\nContent-Disposition: form-data; name=\"field\"\\r\\n\\r\\nv1\\r\\n--abc\\r\\nContent-Disposition: form-data; name=\"upload\"; filename=\"a.txt\"\\r\\nContent-Type: application/octet-stream\\r\\n\\r\\nfile-bytes\\r\\n--abc--\\r\\n`";

    private static string MultipartProbe(string boundaryParam, string bodyJs) => $$"""
        (async () => {
          try {
            const body = {{bodyJs}};
            const req = new Request('http://localhost/x', { method: 'POST', headers: { 'content-type': 'multipart/form-data; {{boundaryParam.Replace("\"", "\\\"")}}' }, body });
            const fd = await req.formData();
            return [...fd.entries()].map(([k, v]) => k + '=' + (typeof v === 'string' ? v : v.name + ':' + v.size)).join(',');
          } catch (e) { return 'ERR ' + e.name + ': ' + e.message; }
        })()
        """;

    public static void Run()
    {
        var engine = new Engine(options =>
        {
            options.UseWebApis();
            options.UseFetch(f => f.UrlFilter = _ => false);
            options.UseConsole(Console.Out);
        });

        foreach (var (label, js) in Snippets)
        {
            try
            {
                var value = engine.Evaluate(js).UnwrapIfPromise();
                Console.WriteLine($"{label,-32} {value}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"{label,-32} !! {ex.GetType().Name}: {ex.Message.ReplaceLineEndings(" ")}");
            }
        }

        var withProcess = new Engine(options =>
        {
            options.UseWebApis();
            options.UseNodeProcess();
        });
        const string processJs = "[typeof process, typeof process.env, String(process.env.NODE_ENV), process.platform, JSON.stringify(process.versions ?? null), typeof process.cwd, typeof process.nextTick, typeof process.hrtime, typeof process.emitWarning].join(' | ')";
        Console.WriteLine($"{"UseNodeProcess()",-32} {withProcess.Evaluate(processJs)}");
    }
}
