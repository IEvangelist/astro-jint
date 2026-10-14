using System.Net;

namespace AstroJint.Sample;

/// <summary>The contact page: a form that posts to itself, validation, a cookie and a redirect after the post.</summary>
public class ContactFormTests(SampleSite site)
{
    private static HttpRequestMessage Post(HttpContent content, string origin = "http://localhost")
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/contact") { Content = content };
        request.Headers.Add("Origin", origin);
        return request;
    }

    private static FormUrlEncodedContent Form(string name, string message) =>
        new([new("name", name), new("message", message)]);

    [Fact]
    public async Task TheFormStartsEmpty()
    {
        using var client = site.CreateClient();

        using var response = await client.GetAsync("/contact");
        var page = await response.ReadHtmlAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(page.Has("form[method=post][action='/contact']"));
        Assert.False(page.Has("#thanks"));
        Assert.False(page.Has(".error"));
    }

    [Fact]
    public async Task AnInvalidSubmissionIsShownAgainWithTheErrorsAndTheInput()
    {
        using var client = site.CreateClient();

        using var response = await client.SendAsync(Post(Form(name: " ", message: "hi")));
        var page = await response.ReadHtmlAsync();

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("Tell us your name.", page.Text("#name-error"));
        Assert.Equal("The message needs at least five characters.", page.Text("#message-error"));
        Assert.Equal("hi", page.Text("textarea[name=message]"));
        Assert.Null(response.SetCookieValue("contact-name"));
    }

    [Fact]
    public async Task AValidSubmissionRedirectsAndRemembersTheNameInACookie()
    {
        using var client = site.CreateClient();

        using var response = await client.SendAsync(Post(Form("Ada", "Hello there, Jint")));

        Assert.Equal(HttpStatusCode.SeeOther, response.StatusCode);
        Assert.Equal("/contact", response.Headers.Location?.OriginalString);
        var cookie = Assert.Single(response.HeaderValues("Set-Cookie"));
        Assert.StartsWith("contact-name=Ada;", cookie);
        Assert.Contains("httponly", cookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("max-age=600", cookie, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("Ada")]
    [InlineData("Zoë & Co")]
    public async Task TheCookieComesBackToThePageThatThanksTheVisitor(string name)
    {
        using var client = site.CreateClient(cookies: true);

        using var post = await client.SendAsync(Post(Form(name, "Hello there, Jint")));
        var page = await (await client.GetAsync(post.Headers.Location)).ReadHtmlAsync();

        Assert.Equal($"Thanks for your message, {name}!", page.Text("#thanks"));
    }

    [Fact]
    public async Task AMultipartFormIsReadLikeAUrlEncodedOne()
    {
        using var client = site.CreateClient();
        using var content = new MultipartFormDataContent
        {
            // A browser quotes the name of a field.
            { new StringContent("Grace"), "\"name\"" },
            { new StringContent("Compilers are a nice idea"), "\"message\"" },
        };

        using var response = await client.SendAsync(Post(content));

        Assert.Equal(HttpStatusCode.SeeOther, response.StatusCode);
        Assert.StartsWith("contact-name=Grace;", Assert.Single(response.HeaderValues("Set-Cookie")));
    }

    [Fact(Skip = "GAP-SAMPLE-1: The multipart parser of Jint 5.0.0-preview-2249 fails on a field name without quotes, and HttpClient writes the name as it is.")]
    public async Task AMultipartFormThatHttpClientWritesIsRead()
    {
        using var client = site.CreateClient();
        using var content = new MultipartFormDataContent
        {
            { new StringContent("Grace"), "name" },
            { new StringContent("Compilers are a nice idea"), "message" },
        };

        using var response = await client.SendAsync(Post(content));

        Assert.Equal(HttpStatusCode.SeeOther, response.StatusCode);
    }

    [Fact]
    public async Task ASubmissionFromAnotherOriginIsForbidden()
    {
        using var client = site.CreateClient();

        using var response = await client.SendAsync(Post(Form("Ada", "Hello there, Jint"), origin: "http://evil.example"));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Null(response.SetCookieValue("contact-name"));
    }
}
