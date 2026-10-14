using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace AstroJint.Conformance.Core;

/// <summary>
/// What the tests of the server island endpoint need to talk to it: <c>encryptString()</c> of Astro with the key of the upstream
/// tests, and the request an island sends. The <c>core-server-islands</c> fixtures set that key (<c>ASTRO_KEY</c>) in their Astro
/// config, because the build runs in another process than the test.
/// </summary>
internal static class ServerIslandRequests
{
    private const string AstroKey = "eKBaVEuI7YjfanEXHuJe/pwZKKt3LkAHeMxvTU7aR0M=";

    /// <summary>
    /// The wire format of Astro: 12 random bytes as upper case hex, then the Base64 of the cipher text followed by the 16 bytes of the tag.
    /// </summary>
    public static string Encrypt(string plainText, string additionalData)
    {
        var iv = RandomNumberGenerator.GetBytes(12);
        var data = Encoding.UTF8.GetBytes(plainText);
        var cipherText = new byte[data.Length];
        var tag = new byte[16];

        using var aes = new AesGcm(Convert.FromBase64String(AstroKey), tag.Length);
        aes.Encrypt(iv, data, cipherText, tag, Encoding.UTF8.GetBytes(additionalData));

        return Convert.ToHexString(iv) + Convert.ToBase64String([.. cipherText, .. tag]);
    }

    /// <summary>
    /// The body of a <c>POST /_server-islands/Island</c>: the JSON of upstream, sent as <c>text/plain</c> like the string body of a <c>Request</c>.
    /// </summary>
    public static StringContent IslandBody(string? encryptedProps = null, string encryptedSlots = "") =>
        new(
            JsonSerializer.Serialize(new
            {
                encryptedComponentExport = Encrypt("default", "export:Island"),
                encryptedProps = encryptedProps ?? Encrypt("{}", "props:Island"),
                encryptedSlots,
            }),
            Encoding.UTF8);

    /// <summary>
    /// The <c>Origin</c> of upstream's <c>http://example.com</c> is the origin of the site, because the host builds the URL of a request from its <c>Host</c> header.
    /// </summary>
    public static Task<HttpResponseMessage> PostIslandAsync(ConformanceSite site, HttpContent body) =>
        site.SendAsync(HttpMethod.Post, "/_server-islands/Island", body, ("Origin", site.BaseAddress.GetLeftPart(UriPartial.Authority)));
}
