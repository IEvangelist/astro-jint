using Acornima.Ast;
using Jint;
using Jint.Native;
using Jint.Native.Object;
using Jint.Runtime.Interop;

namespace AstroJint.AspNetCore;

/// <summary>Creates engines that run the Astro server bundle. The bundle is parsed once and shared by all engines.</summary>
internal sealed class AstroEngineFactory(
    AstroOptions options,
    string entryPath,
    Func<string, string?> readClientFile)
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

    private readonly Lazy<Prepared<Module>> _entry = new(
        () => Engine.PrepareModule(File.ReadAllText(entryPath), "entry.mjs"),
        LazyThreadSafetyMode.ExecutionAndPublication);

    public async Task<AstroEngineSlot> CreateAsync()
    {
        var entry = _entry.Value;
        var context = new AstroRequestContext();

        var engine = new Engine(engineOptions =>
        {
            engineOptions.UseWebApis();
            engineOptions.UseFetch(fetch => fetch.UrlFilter = _ => false);
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
            engine.Modules.Add("entry", builder => builder.AddModule(entry));
            var entryExports = await engine.Modules.ImportAsync("entry").ConfigureAwait(false);
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
        return host;

        void Add(string name, Func<string?, JsValue> implementation) =>
            host.Set(name, new ClrFunction(engine, name, (_, arguments) => implementation(
                arguments.Length > 0 && arguments[0].IsString() ? arguments[0].AsString() : null)));
    }

    private static JsValue ToJs(string? value) => value is null ? JsValue.Null : new JsString(value);
}
