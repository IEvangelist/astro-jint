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

    /// <summary>The value of the cookie that the response sets with the given name, or <see langword="null"/> when it sets none.</summary>
    public static string? SetCookieValue(this HttpResponseMessage response, string name)
    {
        foreach (var header in response.HeaderValues("Set-Cookie"))
        {
            var pair = header.Split(';', 2)[0];
            var separator = pair.IndexOf('=');
            if (separator > 0 && pair[..separator].Trim() == name)
            {
                return pair[(separator + 1)..].Trim();
            }
        }

        return null;
    }
}
