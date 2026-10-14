using System.Net;
using System.Text;
using System.Text.Json;

namespace AstroJint.Sample;

/// <summary>The pages and endpoints with <c>prerender = false</c>, which Astro renders for every request inside the engine pool.</summary>
public class DynamicPageTests(SampleSite site)
{
    [Fact]
    public async Task AnOnDemandPageIsRenderedForEveryRequest()
    {
        using var client = site.CreateClient();

        using var firstResponse = await client.GetAsync("/time");
        var first = await firstResponse.ReadHtmlAsync();
        await Task.Delay(50);
        var second = await (await client.GetAsync("/time")).ReadHtmlAsync();

        Assert.Equal(HttpStatusCode.OK, firstResponse.StatusCode);
        Assert.Equal("text/html", firstResponse.Content.Headers.ContentType?.MediaType);
        Assert.Equal("on-demand", first.QuerySelector("footer .badge")?.GetAttribute("data-mode"));
        Assert.NotEqual(first.Text("#rendered-at"), second.Text("#rendered-at"));
        Assert.Null(firstResponse.Headers.ETag);
    }

    [Fact]
    public async Task TheScriptOfAnOnDemandPageIsAStaticAsset()
    {
        using var client = site.CreateClient();
        var page = await (await client.GetAsync("/time")).ReadHtmlAsync();
        var src = page.QuerySelector("script[type=module]")?.GetAttribute("src");

        Assert.StartsWith("/_astro/", src);
        using var script = await client.GetAsync(src);

        Assert.Equal(HttpStatusCode.OK, script.StatusCode);
        Assert.Equal(["immutable", "max-age=31536000", "public"], script.CacheControl());
        Assert.Contains("/api/time", await script.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task TheTimeEndpointIsNotCached()
    {
        using var client = site.CreateClient();

        using var response = await client.GetAsync("/api/time");
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("application/json", response.Content.Headers.ContentType?.MediaType);
        Assert.Equal(["no-store"], response.CacheControl());
        Assert.InRange(json.RootElement.GetProperty("now").GetDateTimeOffset(), DateTimeOffset.UtcNow.AddMinutes(-1), DateTimeOffset.UtcNow.AddMinutes(1));
    }

    [Theory]
    [InlineData("1", "", "USD 249.00")]
    [InlineData("2", "", "USD 89.00")]
    [InlineData("2", "?currency=eur", "EUR 81.88")]
    [InlineData("3", "?currency=EUR", "EUR 41.40")]
    [InlineData("3", "?currency=usd", "USD 45.00")]
    public async Task AProductIsPricedInTheRequestedCurrency(string id, string query, string price)
    {
        using var client = site.CreateClient();

        using var response = await client.GetAsync($"/products/{id}{query}");
        var page = await response.ReadHtmlAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(price, page.Text("#price"));
        Assert.Equal(["max-age=60", "public"], response.CacheControl());
    }

    [Fact]
    public async Task AProductPageNamesTheProduct()
    {
        using var client = site.CreateClient();

        var page = await (await client.GetAsync("/products/1")).ReadHtmlAsync();

        Assert.Equal("Espresso machine", page.Text("h1"));
        Assert.Contains("Espresso machine", page.Title);
    }

    [Theory]
    [InlineData("99")]
    [InlineData("0")]
    [InlineData("espresso")]
    public async Task AnUnknownProductIsNotFound(string id)
    {
        using var client = site.CreateClient();

        using var response = await client.GetAsync($"/products/{id}");
        var page = await response.ReadHtmlAsync();

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("Page not found", page.Text("h1"));
    }

    [Fact]
    public async Task TheEchoEndpointDescribesAGetRequest()
    {
        using var client = site.CreateClient();

        using var response = await client.GetAsync("/api/echo?x=1&y=two");
        using var echo = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var root = echo.RootElement;

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("GET", root.GetProperty("method").GetString());
        Assert.Equal("/api/echo", root.GetProperty("path").GetString());
        Assert.Equal("1", root.GetProperty("query").GetProperty("x").GetString());
        Assert.Equal(JsonValueKind.Null, root.GetProperty("body").ValueKind);
        Assert.Equal("127.0.0.1", root.GetProperty("clientAddress").GetString());
        Assert.Equal(response.Header("X-Request-Id"), root.GetProperty("traceId").GetString());
    }

    [Theory]
    [InlineData("POST")]
    [InlineData("PUT")]
    [InlineData("PATCH")]
    [InlineData("DELETE")]
    public async Task TheEchoEndpointAnswersEveryMethodWithTheBody(string method)
    {
        using var client = site.CreateClient();
        using var request = new HttpRequestMessage(new HttpMethod(method), "/api/echo")
        {
            Content = new StringContent("""{"greeting":"héllo ☕"}""", Encoding.UTF8, "application/json"),
        };

        using var response = await client.SendAsync(request);
        using var echo = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var root = echo.RootElement;

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(method, root.GetProperty("method").GetString());
        Assert.StartsWith("application/json", root.GetProperty("contentType").GetString());
        Assert.Equal("""{"greeting":"héllo ☕"}""", root.GetProperty("body").GetString());
    }

    [Fact]
    public async Task TheTimeEndpointOnlyAnswersGet()
    {
        using var client = site.CreateClient();

        using var response = await client.PostAsync("/api/time", content: null);

        Assert.True(response.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.MethodNotAllowed, $"Unexpected {response.StatusCode}.");
    }
}
