using System.Security.Cryptography;
using System.Text;

namespace AstroJint.Conformance.CoreUnits;

/// <summary>
/// Port of <c>packages/astro/test/units/server-islands/encryption.test.ts</c>: the AES-GCM that encrypts the props, the slots and the
/// export of every server island, and the SHA-2 digests of the CSP. All 16 tests are adapted, with the same calls and the same
/// arguments: the page <c>src/pages/probe/[name].ts</c> of the <c>app-server-islands</c> fixture (<c>src/lib/probes/encryption.ts</c>)
/// runs them inside the host, on the <c>core/encryption.js</c> that the server bundle contains, and answers with what each test
/// asserts on; the assertions are here. Nothing in them changes. <c>astro</c> does not export that file, so the probe imports it by
/// its path in the installed package. Where a test only checks that something was or was not equal, the test adds what the result
/// has to be in the format that the rest of the runtime and the tests rely on (the length of the key, the exact digest).
/// </summary>
[Trait("Upstream", "core-units/server-islands/encryption.test.ts")]
public class ServerIslandsEncryptionTests(SiteHost sites)
{
    private const string ProbeName = "encryption";

    private async Task<Probe> RunAsync() => await (await sites.GetServerIslandsSiteAsync()).ProbeAsync(ProbeName);

    private static string Digest(string algorithm, string text)
    {
        var bytes = Encoding.UTF8.GetBytes(text);
        var hash = algorithm switch
        {
            "SHA-256" => SHA256.HashData(bytes),
            "SHA-384" => SHA384.HashData(bytes),
            _ => SHA512.HashData(bytes),
        };

        return $"{algorithm.Replace("-", "", StringComparison.Ordinal).ToLowerInvariant()}-{Convert.ToBase64String(hash)}";
    }

    // #region encryptString / decryptString

    [Fact(DisplayName = "round-trips correctly")]
    public async Task EncryptString_RoundTrips()
    {
        var probe = await RunAsync();

        var result = probe["round-trips correctly"];

        Assert.Equal("hello world", result.Str("original"));
        Assert.Equal(result.Str("original"), result.Str("decrypted"));
    }

    [Fact(DisplayName = "round-trips an empty string")]
    public async Task EncryptString_RoundTripsAnEmptyString()
    {
        var probe = await RunAsync();

        Assert.Equal("", probe["round-trips an empty string"].Str("decrypted"));
    }

    [Fact(DisplayName = "round-trips a JSON payload")]
    public async Task EncryptString_RoundTripsAJsonPayload()
    {
        var probe = await RunAsync();

        var result = probe["round-trips a JSON payload"];

        Assert.Equal("""{"foo":"bar","num":42,"nested":{"a":[1,2]}}""", result.Str("original"));
        Assert.Equal(result.Str("original"), result.Str("decrypted"));
    }

    [Fact(DisplayName = "produces a different ciphertext on each call (IV randomness)")]
    public async Task EncryptString_ProducesADifferentCiphertextOnEachCall()
    {
        var probe = await RunAsync();

        var result = probe["produces a different ciphertext on each call (IV randomness)"];

        // Same plaintext, different ciphertext because each call uses a fresh IV: 12 random bytes, as 24 hex digits.
        var first = result.Str("first");
        var second = result.Str("second");
        Assert.NotEqual(first, second);
        Assert.Matches("^[0-9A-F]{24}[A-Za-z0-9+/]+=*$", first);
        Assert.Matches("^[0-9A-F]{24}[A-Za-z0-9+/]+=*$", second);
        Assert.NotEqual(first[..24], second[..24]);
    }

    [Fact(DisplayName = "both distinct ciphertexts decrypt to the same plaintext")]
    public async Task EncryptString_DistinctCiphertextsDecryptToTheSamePlaintext()
    {
        var probe = await RunAsync();

        var result = probe["both distinct ciphertexts decrypt to the same plaintext"];

        Assert.True(result.Bool("distinct"));
        Assert.Equal("same input", result.Str("first"));
        Assert.Equal("same input", result.Str("second"));
    }

    [Fact(DisplayName = "throws when decrypting a tampered ciphertext")]
    public async Task DecryptString_ThrowsOnATamperedCiphertext()
    {
        var probe = await RunAsync();

        Assert.True(probe.Bool("throws when decrypting a tampered ciphertext"));
    }

    [Fact(DisplayName = "throws when decrypting with the wrong key")]
    public async Task DecryptString_ThrowsWithTheWrongKey()
    {
        var probe = await RunAsync();

        Assert.True(probe.Bool("throws when decrypting with the wrong key"));
    }

    [Fact(DisplayName = "throws when decrypting with mismatched additionalData")]
    public async Task DecryptString_ThrowsWithMismatchedAdditionalData()
    {
        var probe = await RunAsync();

        Assert.True(probe.Bool("throws when decrypting with mismatched additionalData"));
    }

    // #endregion

    // #region encodeKey / decodeKey

    [Fact(DisplayName = "round-trips a CryptoKey through base64")]
    public async Task EncodeKey_RoundTripsACryptoKey()
    {
        var probe = await RunAsync();

        var result = probe["round-trips a CryptoKey through base64"];

        Assert.Equal("verify key works", result.Str("plain"));
        Assert.Equal(result.Str("plain"), result.Str("decrypted"));
    }

    [Fact(DisplayName = "produces a string from encodeKey")]
    public async Task EncodeKey_ProducesAString()
    {
        var probe = await RunAsync();

        var result = probe["produces a string from encodeKey"];

        Assert.Equal("string", result.Str("type"));
        var encoded = result.Str("encoded");
        Assert.NotEmpty(encoded);
        // It is the raw key that createKey() generates, AES-256, in Base64: the format of the ASTRO_KEY variable.
        Assert.Equal(32, Convert.FromBase64String(encoded).Length);
    }

    [Fact(DisplayName = "a key encoded then decoded can decrypt ciphertexts made with the original key")]
    public async Task DecodeKey_DecryptsWhatTheOriginalKeyEncrypted()
    {
        var probe = await RunAsync();

        var result = probe["a key encoded then decoded can decrypt ciphertexts made with the original key"];

        Assert.Equal("cross-key decrypt", result.Str("plain"));
        Assert.Equal(result.Str("plain"), result.Str("decrypted"));
    }

    // #endregion

    // #region generateCspDigest

    [Fact(DisplayName = "produces a sha256- prefixed base64 hash for SHA-256 algorithm")]
    public async Task GenerateCspDigest_ProducesASha256Hash()
    {
        var probe = await RunAsync();

        var hash = probe.Str("produces a sha256- prefixed base64 hash for SHA-256 algorithm");

        Assert.StartsWith("sha256-", hash, StringComparison.Ordinal);
        Assert.Equal(Digest("SHA-256", "alert(1)"), hash);
    }

    [Fact(DisplayName = "produces a sha384- prefixed hash for SHA-384 algorithm")]
    public async Task GenerateCspDigest_ProducesASha384Hash()
    {
        var probe = await RunAsync();

        var hash = probe.Str("produces a sha384- prefixed hash for SHA-384 algorithm");

        Assert.StartsWith("sha384-", hash, StringComparison.Ordinal);
        Assert.Equal(Digest("SHA-384", "alert(1)"), hash);
    }

    [Fact(DisplayName = "produces a sha512- prefixed hash for SHA-512 algorithm")]
    public async Task GenerateCspDigest_ProducesASha512Hash()
    {
        var probe = await RunAsync();

        var hash = probe.Str("produces a sha512- prefixed hash for SHA-512 algorithm");

        Assert.StartsWith("sha512-", hash, StringComparison.Ordinal);
        Assert.Equal(Digest("SHA-512", "alert(1)"), hash);
    }

    [Fact(DisplayName = "produces a deterministic hash for the same input")]
    public async Task GenerateCspDigest_IsDeterministic()
    {
        var probe = await RunAsync();

        var result = probe["produces a deterministic hash for the same input"];

        Assert.Equal(result.Str("a"), result.Str("b"));
        Assert.Equal(Digest("SHA-256", "hello"), result.Str("a"));
    }

    [Fact(DisplayName = "produces different hashes for different inputs")]
    public async Task GenerateCspDigest_DiffersForDifferentInputs()
    {
        var probe = await RunAsync();

        var result = probe["produces different hashes for different inputs"];

        Assert.NotEqual(result.Str("a"), result.Str("b"));
        Assert.Equal(Digest("SHA-256", "hello"), result.Str("a"));
        Assert.Equal(Digest("SHA-256", "world"), result.Str("b"));
    }

    // #endregion
}
