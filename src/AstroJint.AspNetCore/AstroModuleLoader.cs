using System.Buffers;
using System.Collections.Concurrent;
using Acornima.Ast;
using Jint;
using Jint.Runtime;
using Jint.Runtime.Modules;

namespace AstroJint.AspNetCore;

/// <summary>
/// Loads the files of the Astro server bundle: the entry and the chunks that the build puts next to it (the pages, the middleware,
/// the server islands, the session driver). Only relative imports of files below the folder of the entry are resolved, so the
/// bundle cannot reach anything else.
/// </summary>
/// <remarks>
/// Every file is parsed once and the result is shared by all engines, which still evaluate their own copy of it.
/// </remarks>
internal sealed class AstroModuleLoader : IModuleLoader
{
    private static readonly SearchValues<char> NotInAFileName = SearchValues.Create("\\:*?\"<>|\0");

    private readonly string _root;
    private readonly ConcurrentDictionary<string, Lazy<Prepared<Module>>> _modules = new(StringComparer.Ordinal);

    public AstroModuleLoader(string entryPath)
    {
        var fullPath = Path.GetFullPath(entryPath);
        _root = Path.GetDirectoryName(fullPath) ?? throw new ArgumentException("The entry has no folder.", nameof(entryPath));
        Entry = "./" + Path.GetFileName(fullPath);
    }

    /// <summary>The specifier that starts the bundle.</summary>
    public string Entry { get; }

    public ResolvedSpecifier Resolve(string? referencingModuleLocation, ModuleRequest moduleRequest)
    {
        var (key, refusal) = KeyOf(moduleRequest, referencingModuleLocation);
        if (key is not null)
        {
            return new ResolvedSpecifier(moduleRequest, key, null, SpecifierType.RelativeOrAbsolute);
        }

        // Not thrown from here: a ModuleResolutionException is not a JavaScript error, so an import() in a page would
        // not get a rejected promise to catch, it would fail the whole request. LoadModule has the engine to raise it.
        // The message reaches the bundle's own code (and so a page that shows it), hence no folders of the host in it.
        return new Refused(moduleRequest, new ModuleResolutionException(refusal!, moduleRequest.Specifier, referencingModuleLocation, filePath: null));
    }

    public ModuleRecord LoadModule(Engine engine, ResolvedSpecifier resolved)
    {
        if (resolved is Refused refused)
        {
            throw new JavaScriptException(engine.Intrinsics.Error, refused.Failure.Message, refused.Failure);
        }

        var key = ModuleFactory.LocationOf(resolved);
        var prepared = _modules
            .GetOrAdd(key, static (key, loader) => new Lazy<Prepared<Module>>(
                () => Engine.PrepareModule(File.ReadAllText(loader.PathOf(key)), key),
                LazyThreadSafetyMode.ExecutionAndPublication), this)
            .Value;

        return ModuleFactory.BuildSourceTextModule(engine, prepared);
    }

    private (string? Key, string? Refusal) KeyOf(ModuleRequest moduleRequest, string? referencingModuleLocation)
    {
        var specifier = moduleRequest.Specifier;
        if (moduleRequest.Attributes is { Length: > 0 })
        {
            return (null, "Only JavaScript files can be imported");
        }

        if (!specifier.StartsWith("./", StringComparison.Ordinal) && !specifier.StartsWith("../", StringComparison.Ordinal))
        {
            return (null, "Only relative imports of files in the server bundle are supported");
        }

        // Keys are paths below the folder of the entry, with forward slashes. A module is named by its key, which
        // is what its own relative imports are resolved against.
        var segments = new List<string>();
        if (referencingModuleLocation is not null)
        {
            segments.AddRange(referencingModuleLocation.Split('/'));
            segments.RemoveAt(segments.Count - 1);
        }

        foreach (var segment in specifier.Split('/'))
        {
            if (segment is "" or ".")
            {
                continue;
            }

            if (segment == "..")
            {
                if (segments.Count == 0)
                {
                    return (null, "The file is outside of the server bundle");
                }

                segments.RemoveAt(segments.Count - 1);
                continue;
            }

            if (segment.AsSpan().IndexOfAny(NotInAFileName) >= 0 || segment[^1] is '.' or ' ')
            {
                return (null, "The file name is not valid");
            }

            segments.Add(segment);
        }

        var key = string.Join('/', segments);
        return !_modules.ContainsKey(key) && !File.Exists(PathOf(key))
            ? (null, "The file does not exist in the server bundle")
            : (key, null);
    }

    private string PathOf(string key) => Path.Combine(_root, key.Replace('/', Path.DirectorySeparatorChar));

    // The key has a colon, which no file name has, so it can never be taken for the key of a module.
    private sealed record Refused : ResolvedSpecifier
    {
        public Refused(ModuleRequest request, ModuleResolutionException failure)
            : base(request, "refused:" + request.Specifier, null, SpecifierType.Error) => Failure = failure;

        public ModuleResolutionException Failure { get; }
    }
}
