using System.Security.Cryptography;
using System.Text;

namespace AstroJint.Conformance.Core;

internal static class CspDigest
{
    /// <summary>
    /// What <c>generateCspDigest(content, 'SHA-256')</c> of Astro returns: <c>sha256-</c> and the Base64 of the hash of the UTF-8 text.
    /// </summary>
    public static string Sha256(string content) => "sha256-" + Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes(content)));
}
