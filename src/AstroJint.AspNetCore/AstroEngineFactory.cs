using System.Security.Cryptography;
using Jint;
using Jint.Native;
using Jint.Native.Object;
using Jint.Runtime.Interop;
using Jint.WebApi;

namespace AstroJint.AspNetCore;

/// <summary>Creates engines that run the Astro server bundle. Its files are parsed once and shared by all engines.</summary>
internal sealed class AstroEngineFactory(
    AstroOptions options,
    string entryPath,
    Func<string, string?> readClientFile,
    Func<string, byte[]?> readClientBytes,
    AstroSessionStore sessions,
    ConsoleSink consoleSink)
{
    // Error.stack in Jint starts with the frames, but Astro's error pages (and most libraries) expect the
    // "Name: message" line first. See docs/spikes/02-jint-astro-runtime.md.
    private const string Prelude = """
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
        """;

    private readonly AstroModuleLoader _modules = new(entryPath);

    public async Task<AstroEngineSlot> CreateAsync()
    {
        var context = new AstroRequestContext();

        var engine = new Engine(engineOptions =>
        {
            engineOptions.UseModules(_modules);
            // The messages of the loader name files of the bundle only, relative to its entry.
            engineOptions.Modules.ExposeDetailedLoadErrors = true;
            engineOptions.UseWebApis();
            engineOptions.UseConsole(consoleSink);
            engineOptions.UseFetch(fetch => fetch.UrlFilter = options.FetchUrlFilter ?? (_ => false));
            engineOptions.TimeZone = options.TimeZone;
            if (options.MemoryLimitBytes is { } limit)
            {
                engineOptions.LimitMemory(limit);
            }
        });

        try
        {
            engine.Execute(Prelude);
            engine.SetValue("__astroJint", CreateHostObject(engine, context));
            var entryExports = await engine.Modules.ImportAsync(_modules.Entry).ConfigureAwait(false);
            engine.WebApi.SetFetchHandler(entryExports.Get("default"));
            return new AstroEngineSlot(engine, context);
        }
        catch
        {
            engine.Dispose();
            throw;
        }
    }

    // The bundle reads this object while it is evaluated (see packages/astro-adapter-jint/src/server.ts), so it
    // has to exist before the module is imported. It only holds plain functions, no .NET objects.
    private ObjectInstance CreateHostObject(Engine engine, AstroRequestContext context)
    {
        var host = new JsObject(engine);
        Add("clientAddress", _ => ToJs(context.ClientAddress));
        Add("locals", _ => ToJs(context.LocalsJson));
        Add("env", name => ToJs(name is not null && options.Env.TryGetValue(name, out var value) ? value : null));
        Add("readClientFile", pathname => ToJs(pathname is null ? null : readClientFile(pathname)));
        Add("readClientBytes", pathname => pathname is not null && readClientBytes(pathname) is { } bytes
            ? engine.Intrinsics.ArrayBuffer.Construct(bytes)
            : JsValue.Null);

        // A digest is cheap here and expensive in an interpreter, which matters for the ETag of an image.
        host.Set("sha256", new ClrFunction(engine, "sha256", (_, arguments) =>
            arguments is [{ } value, ..] && value.IsArrayBuffer() && value.AsArrayBuffer() is { } data
                ? new JsString(Convert.ToHexStringLower(SHA256.HashData(data)))
                : JsValue.Null));

        // Session data has to outlive the engines and be shared by them, so the host keeps it. The caches that suit
        // it (Redis, SQL Server) are asynchronous, hence promises.
        AddAsync("sessionGet", (id, _) => sessions.GetAsync(Required(id), context.Cancellation));
        AddAsync("sessionSet", async (id, data) =>
        {
            await sessions.SetAsync(Required(id), Required(data), context.Cancellation).ConfigureAwait(false);
            return null;
        });
        AddAsync("sessionRemove", async (id, _) =>
        {
            await sessions.RemoveAsync(Required(id), context.Cancellation).ConfigureAwait(false);
            return null;
        });
        return host;

        void Add(string name, Func<string?, JsValue> implementation) =>
            host.Set(name, new ClrFunction(engine, name, (_, arguments) => implementation(StringAt(arguments, 0))));

        void AddAsync(string name, Func<string?, string?, Task<string?>> operation) =>
            host.Set(name, new ClrFunction(engine, name, (_, arguments) =>
            {
                var (first, second) = (StringAt(arguments, 0), StringAt(arguments, 1));
                return ToPromise(engine, () => operation(first, second));
            }));

        static string Required(string? value) => value ?? throw new ArgumentException("A string was expected.");
    }

    private static string? StringAt(JsValue[] arguments, int index) =>
        arguments.Length > index && arguments[index].IsString() ? arguments[index].AsString() : null;

    // The work runs on the .NET side and settles the promise on the engine's own event loop when it is done.
    // Failures reach JavaScript without detail: the stores log what went wrong themselves.
    private static JsValue ToPromise(Engine engine, Func<Task<string?>> operation)
    {
        var promise = engine.Tasks.RegisterPromise();
        _ = SettleAsync();
        return promise.Promise;

        async Task SettleAsync()
        {
            try
            {
                var result = await operation().ConfigureAwait(false);
                Post(() => promise.Resolve(ToJs(result)));
            }
            catch (Exception)
            {
                Post(() => promise.Reject(engine.Intrinsics.Error.Construct("The host failed to complete the operation.")));
            }
        }

        void Post(Action settle)
        {
            try
            {
                engine.Tasks.Post(settle);
            }
            catch (ObjectDisposedException)
            {
                // The engine was replaced while it waited, and nothing waits for the answer any more.
            }
        }
    }

    private static JsValue ToJs(string? value) => value is null ? JsValue.Null : new JsString(value);
}
