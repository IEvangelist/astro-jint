using System.Diagnostics;
using System.Text;
using Jint;

// Usage: JintSpike <serverDir> [entry.mjs] [--process]   |   JintSpike --probe
if (args.Length > 0 && args[0] == "--probe")
{
    Probes.Run();
    return;
}

var serverDir = Path.GetFullPath(args.Length > 0 ? args[0] : throw new ArgumentException("server dir required"));
var entry = args.Length > 1 && !args[1].StartsWith("--") ? args[1] : "entry.mjs";
var withProcess = args.Contains("--process");

static void Log(string label, Stopwatch sw) => Console.WriteLine($"[{sw.Elapsed.TotalMilliseconds,8:N1} ms] {label}");
static string Truncate(string s, int max = 300) => (s.Length <= max ? s : s[..max] + "...").ReplaceLineEndings(" ");

var sw = Stopwatch.StartNew();
var engine = new Engine(options =>
{
    options.UseWebApis();
    options.UseFetch(f => f.UrlFilter = _ => false);
    options.UseConsole(Console.Out);
    options.UseModules(serverDir);
    if (withProcess)
    {
        options.UseNodeProcess();
    }
});
Log("engine created", sw);

if (args.Contains("--prelude"))
{
    engine.Execute("""
        (() => {
          const original = Object.getOwnPropertyDescriptor(Error.prototype, 'stack');
          if (!original || !original.get) return;
          Object.defineProperty(Error.prototype, 'stack', {
            configurable: true,
            enumerable: false,
            get() {
              const frames = String(original.get.call(this)).replace(/\r\n/g, '\n');
              return Error.prototype.toString.call(this) + '\n' + frames;
            },
            set(value) {
              Object.defineProperty(this, 'stack', { value, writable: true, configurable: true, enumerable: false });
            },
          });
        })();
        """);
}

sw.Restart();
var ns = await engine.Modules.ImportAsync("./" + entry);
Log($"module graph imported ({entry})", sw);

engine.WebApi.SetFetchHandler(ns.Get("default"));
Console.WriteLine($"HasFetchHandler = {engine.WebApi.HasFetchHandler}");

async Task Send(string label, HttpMethod method, string path, HttpContent? content = null, params (string Name, string Value)[] headers)
{
    var t = Stopwatch.StartNew();
    try
    {
        using var req = new HttpRequestMessage(method, "http://localhost" + path) { Content = content };
        req.Headers.TryAddWithoutValidation("User-Agent", "jint-spike/1.0");
        foreach (var (name, value) in headers)
        {
            req.Headers.TryAddWithoutValidation(name, value);
        }

        using var res = await engine.WebApi.InvokeFetchHandlerAsync(req);
        var text = await res.Content.ReadAsStringAsync();
        Console.WriteLine($"--- {label}: {method} {path} -> {(int)res.StatusCode} in {t.Elapsed.TotalMilliseconds:N1} ms");
        foreach (var h in res.Headers.Concat(res.Content.Headers))
        {
            Console.WriteLine($"    {h.Key}: {string.Join(" | ", h.Value)}");
        }

        Console.WriteLine($"    body[{text.Length}]: {Truncate(text)}");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"--- {label}: {method} {path} -> EXCEPTION in {t.Elapsed.TotalMilliseconds:N1} ms");
        Console.WriteLine($"    {ex.GetType().Name}: {Truncate(ex.Message, 600)}");
    }
}

await Send("dynamic page", HttpMethod.Get, "/time");
await Send("dynamic page again", HttpMethod.Get, "/time");
await Send("prerendered route", HttpMethod.Get, "/");
await Send("not found", HttpMethod.Get, "/nope");
await Send("dynamic param + query", HttpMethod.Get, "/items/42?q=hello%20world");
await Send("redirect", HttpMethod.Get, "/redirect");
await Send("server error", HttpMethod.Get, "/boom");
await Send("web crypto", HttpMethod.Get, "/crypto");
await Send("endpoint POST + cookies", HttpMethod.Post, "/api/echo", new StringContent("{\"hello\":\"world\"}", Encoding.UTF8, "application/json"));
await Send("endpoint wrong method", HttpMethod.Get, "/api/echo");
await Send("HEAD dynamic", HttpMethod.Head, "/time");
await Send("async timer in page", HttpMethod.Get, "/delay");
await Send("form urlencoded + cookie", HttpMethod.Post, "/api/form", new FormUrlEncodedContent([new("name", "Ada Lovelace"), new("tags", "x&y")]), ("Cookie", "a=1; z=9"));
var dotnetMultipart = new MultipartFormDataContent
{
    { new StringContent("v1"), "field" },
    { new ByteArrayContent(Encoding.UTF8.GetBytes("file-bytes")), "upload", "a.txt" },
};
await Send("form multipart (.NET-style, unquoted)", HttpMethod.Post, "/api/form", dotnetMultipart);

var browserMultipart = new StringContent(
    "--abc\r\nContent-Disposition: form-data; name=\"field\"\r\n\r\nv1\r\n--abc\r\nContent-Disposition: form-data; name=\"upload\"; filename=\"a.txt\"\r\nContent-Type: application/octet-stream\r\n\r\nfile-bytes\r\n--abc--\r\n",
    Encoding.UTF8);
browserMultipart.Headers.ContentType = System.Net.Http.Headers.MediaTypeHeaderValue.Parse("multipart/form-data; boundary=abc");
await Send("form multipart (browser-style, quoted)", HttpMethod.Post, "/api/form", browserMultipart);
