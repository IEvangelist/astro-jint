using System.Globalization;
using System.Text;

namespace AstroJint.Conformance.CoreUnits;

/// <summary>
/// Bodies of form requests, written by hand in the format of a browser (quoted <c>name</c> and <c>filename</c>). HttpClient's
/// <c>MultipartFormDataContent</c> writes unquoted names, which Jint.WebApi's strict <c>Request.formData()</c> rejects
/// (GAP-UNITS-3), so the tests that post forms build the bytes themselves. An empty form is the body that undici writes for
/// <c>new FormData()</c>: only the closing boundary.
/// </summary>
internal static class FormBodies
{
    public const string Boundary = "----AstroJintBoundary7MA4YWxkTrZu0gW";

    public static readonly (string Name, string Value) MultipartType =
        ("Content-Type", $"multipart/form-data; boundary={Boundary}");

    public readonly record struct Part(string Name, string Value, string? FileName = null);

    public static Part Field(string name, string value) => new(name, value);

    public static Part Upload(string name, string fileName, string content = "") => new(name, content, fileName);

    public static byte[] Multipart(params Part[] parts)
    {
        var body = new StringBuilder();
        foreach (var part in parts)
        {
            body.Append(CultureInfo.InvariantCulture, $"--{Boundary}\r\nContent-Disposition: form-data; name=\"{part.Name}\"");
            if (part.FileName is not null)
            {
                body.Append(CultureInfo.InvariantCulture, $"; filename=\"{part.FileName}\"\r\nContent-Type: application/octet-stream");
            }

            body.Append("\r\n\r\n").Append(part.Value).Append("\r\n");
        }

        body.Append(CultureInfo.InvariantCulture, $"--{Boundary}--\r\n");
        return Encoding.UTF8.GetBytes(body.ToString());
    }
}
