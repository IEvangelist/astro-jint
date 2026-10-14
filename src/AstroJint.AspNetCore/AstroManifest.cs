using System.Text.Json;

namespace AstroJint.AspNetCore;

/// <summary>The build contract that <c>astro-adapter-jint</c> writes to <c>astro-jint.json</c>.</summary>
internal sealed class AstroManifest
{
    public const string FileName = "astro-jint.json";
    public const int SupportedVersion = 1;

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public int Version { get; init; } = SupportedVersion;

    public string? AstroVersion { get; init; }

    /// <summary>The Astro <c>base</c>: <c>/</c> or a path such as <c>/docs</c> without a trailing slash.</summary>
    public string Base { get; init; } = "/";

    /// <summary><c>always</c>, <c>never</c> or <c>ignore</c>.</summary>
    public string TrailingSlash { get; init; } = "ignore";

    /// <summary><c>directory</c>, <c>file</c> or <c>preserve</c>.</summary>
    public string BuildFormat { get; init; } = "directory";

    /// <summary>Folder inside <see cref="Client"/> that holds the content-hashed assets.</summary>
    public string AssetsDir { get; init; } = "_astro";

    public string Client { get; init; } = "client";

    public string Server { get; init; } = "server";

    public string Entry { get; init; } = "server/entry.mjs";

    /// <summary><c>security.allowedDomains</c> from the Astro config. When it has entries, only a matching <c>Host</c> is trusted.</summary>
    public IReadOnlyList<AstroAllowedDomain> AllowedDomains { get; init; } = [];

    /// <summary>Response headers that Astro rendered for prerendered pages. They are added when a page's file is served.</summary>
    public IReadOnlyList<AstroStaticHeaders> StaticHeaders { get; init; } = [];

    public IReadOnlyList<AstroManifestRoute> Routes { get; init; } = [];

    public static AstroManifest Load(string distPath)
    {
        var file = Path.Combine(distPath, FileName);
        if (!File.Exists(file))
        {
            throw new FileNotFoundException(
                $"'{FileName}' was not found in '{distPath}'. Build the Astro site with astro-adapter-jint first " +
                "(for example 'pnpm build') and point AstroOptions.DistPath at the output folder.",
                file);
        }

        var manifest = JsonSerializer.Deserialize<AstroManifest>(File.ReadAllText(file), JsonOptions)
            ?? throw new InvalidDataException($"'{file}' is empty.");

        return manifest.Version == SupportedVersion
            ? manifest
            : throw new InvalidDataException(
                $"'{file}' has manifest version {manifest.Version}, but this host supports version {SupportedVersion}. " +
                "Use matching versions of astro-adapter-jint and AstroJint.AspNetCore.");
    }
}

/// <summary>An entry of <c>security.allowedDomains</c>. A part that is not set matches anything.</summary>
internal sealed class AstroAllowedDomain
{
    public string? Protocol { get; init; }

    /// <summary>A host name, or a pattern that starts with <c>*.</c> (one subdomain level) or <c>**.</c> (any number of levels).</summary>
    public string? Hostname { get; init; }

    public string? Port { get; init; }
}

internal sealed class AstroManifestRoute
{
    public string Route { get; init; } = "";

    public string Type { get; init; } = "";

    public bool Prerender { get; init; }

    public string Origin { get; init; } = "";
}

/// <summary>The response headers that go with one prerendered file.</summary>
internal sealed class AstroStaticHeaders
{
    /// <summary>The file, relative to the client folder, using <c>/</c> as the separator.</summary>
    public string File { get; init; } = "";

    public IReadOnlyDictionary<string, string> Headers { get; init; } = new Dictionary<string, string>();
}
