using AngleSharp.Dom;

namespace AstroJint.Conformance;

public static class HttpExtensions
{
    public static async Task<IDocument> ReadHtmlAsync(this HttpResponseMessage response) =>
        ConformanceSite.ParseHtml(await response.Content.ReadAsStringAsync());

    public static Task<string> ReadTextAsync(this HttpResponseMessage response) =>
        response.Content.ReadAsStringAsync();

    /// <summary>The values of a header of the response, or an empty list when it is absent.</summary>
    public static IReadOnlyList<string> HeaderValues(this HttpResponseMessage response, string name) =>
        response.Headers.TryGetValues(name, out var values) || response.Content.Headers.TryGetValues(name, out values)
            ? [.. values]
            : [];
}
