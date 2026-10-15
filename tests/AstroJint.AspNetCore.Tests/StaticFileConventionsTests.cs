namespace AstroJint.AspNetCore.Tests;

public sealed class StaticFileConventionsTests
{
    [Theory]
    [InlineData("index.html", "text/html; charset=utf-8")]
    [InlineData("about/index.HTML", "text/html; charset=utf-8")]
    [InlineData("_astro/app.abc123.css", "text/css; charset=utf-8")]
    [InlineData("robots.txt", "text/plain; charset=utf-8")]
    [InlineData("data.json", "application/json; charset=utf-8")]
    [InlineData("site.webmanifest", "application/manifest+json; charset=utf-8")]
    [InlineData("favicon.svg", "image/svg+xml; charset=utf-8")]
    [InlineData("logo.png", "image/png")]
    [InlineData("font.woff2", "font/woff2")]
    [InlineData("module.wasm", "application/wasm")]
    [InlineData("blob.bin", "application/octet-stream")]
    [InlineData("no-extension", "application/octet-stream")]
    [InlineData("file.unknown-extension", "application/octet-stream")]
    public void GetContentType_AddsTheCharsetOfTextOnly(string fileName, string expected) =>
        Assert.Equal(expected, StaticFileConventions.GetContentType(fileName));

    [Theory]
    [InlineData("_astro/app.abc123.js")]
    [InlineData("script.mjs")]
    public void GetContentType_TreatsScriptsAsText(string fileName) =>
        Assert.EndsWith("; charset=utf-8", StaticFileConventions.GetContentType(fileName), StringComparison.Ordinal);

    [Fact]
    public void GetLastModified_DropsTheFractionOfASecond()
    {
        var written = new DateTime(2026, 3, 4, 5, 6, 7, DateTimeKind.Utc).AddTicks(9_999_999);

        var lastModified = StaticFileConventions.GetLastModified(written);

        Assert.Equal(new DateTimeOffset(2026, 3, 4, 5, 6, 7, TimeSpan.Zero), lastModified);
        Assert.Equal(TimeSpan.Zero, lastModified.Offset);
    }

    [Fact]
    public void GetETag_IsQuotedAndTellsFilesApart()
    {
        var modified = new DateTimeOffset(2026, 3, 4, 5, 6, 7, TimeSpan.Zero);

        var tag = StaticFileConventions.GetETag(modified, 100);

        Assert.Matches("^\"[0-9a-f]+\"$", tag);
        Assert.Equal(tag, StaticFileConventions.GetETag(modified, 100));
        Assert.NotEqual(tag, StaticFileConventions.GetETag(modified, 101));
        Assert.NotEqual(tag, StaticFileConventions.GetETag(modified.AddSeconds(1), 100));
    }
}
