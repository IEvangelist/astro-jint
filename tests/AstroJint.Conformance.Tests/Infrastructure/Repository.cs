namespace AstroJint.Conformance;

/// <summary>Where things live in this repository.</summary>
internal static class Repository
{
    public static string Root { get; } = FindRoot();

    public static string FixturesRoot { get; } = Path.Combine(Root, "tests", "conformance", "fixtures");

    public static string AdapterRoot { get; } = Path.Combine(Root, "packages", "astro-adapter-jint");

    private static string FindRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            if (File.Exists(Path.Combine(directory.FullName, "AstroJint.slnx")))
            {
                return directory.FullName;
            }
        }

        throw new InvalidOperationException($"AstroJint.slnx was not found above '{AppContext.BaseDirectory}'.");
    }
}
