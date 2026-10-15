using System.Text.Json;
using static AstroJint.Conformance.CoreUnits.AppActions;
using static AstroJint.Conformance.CoreUnits.FormBodies;

namespace AstroJint.Conformance.CoreUnits;

/// <summary>
/// Port of <c>packages/astro/test/units/actions/form-data-to-object.test.ts</c>: how a posted form becomes the input of an action,
/// by the shape of its zod schema (strings, numbers, booleans, optional, nullable and default values, files, arrays, nested
/// objects with dotted keys, discriminated unions and <c>.passthrough()</c>).
/// <para>
/// Differences from upstream: <c>formDataToObject(formData, schema)</c> is not reachable from a test of the host, so each test posts
/// a real <c>multipart/form-data</c> body to the form action of the <c>app-actions</c> fixture whose schema is the one of the
/// upstream test (<c>forms.*</c>), and the action answers with its parsed input as JSON. What is observed is therefore
/// <c>schema.parse(formDataToObject(formData, schema))</c>, not the intermediate object: <c>undefined</c> values are absent from the
/// JSON (upstream asserted <c>undefined</c>), a File is shown by its name (upstream asserted <c>instanceof File</c> and the same
/// instances) and the NaN of 'should pass NaN for invalid numbers' is observed as the zod error that rejects a NaN
/// (status 400 with the issue of <c>age</c>, whose message names NaN). The forms are written by hand with
/// <see cref="FormBodies"/>, and the file parts are empty parts named like upstream's <c>new File([''], name)</c>. The loop of
/// 'should coerce mixed boolean array values correctly' stays inside one test, as upstream's.
/// </para>
/// </summary>
[Trait("Upstream", "core-units/actions/form-data-to-object.test.ts")]
public class FormDataToObjectTests(SiteHost sites)
{
    // Posts the form to the action `forms.<action>` and gives the JSON that the action answered with.
    private async Task<string> RunAsync(string action, params Part[] parts)
    {
        var site = await sites.GetActionsSiteAsync();
        var response = await PostFormAsync(site, $"/_actions/forms.{action}", parts);
        if (response.StatusCode != 200)
        {
            Assert.Fail($"Status {response.StatusCode}: {response.Text}");
        }

        return Shown(response);
    }

    [Fact(DisplayName = "should handle strings")]
    public async Task Strings_AreKept() => Assert.Equal(
        """{"name":"Ben","email":"test@test.test"}""",
        await RunAsync("strings", Field("name", "Ben"), Field("email", "test@test.test")));

    [Fact(DisplayName = "should handle numbers")]
    public async Task Numbers_AreParsed() =>
        Assert.Equal("""{"age":25}""", await RunAsync("numbers", Field("age", "25")));

    [Fact(DisplayName = "should pass NaN for invalid numbers")]
    public async Task Numbers_Invalid_ReachZodAsNaN()
    {
        var site = await sites.GetActionsSiteAsync();

        var response = await PostFormAsync(site, "/_actions/forms.numbers", Field("age", "twenty-five"));

        Assert.Equal(400, response.StatusCode);
        var issue = JsonDocument.Parse(response.Text).RootElement.GetProperty("issues")[0];
        Assert.Equal("age", issue.GetProperty("path")[0].GetString());
        Assert.Contains("NaN", issue.GetProperty("message").GetString());
    }

    [Fact(DisplayName = "should handle boolean checks")]
    public async Task Booleans_AreTrueWhenPresentAndNotFalse() => Assert.Equal(
        """{"isCool":true,"isNotCool":false,"isTrue":true,"isFalse":false,"falseString":false}""",
        await RunAsync(
            "booleans", Field("isCool", "yes"), Field("isTrue", "true"), Field("isFalse", "false"), Field("falseString", "false")));

    [Fact(DisplayName = "should handle optional values")]
    public async Task Optional_MissingValues_AreUndefined() =>
        Assert.Equal("""{"name":"Ben"}""", await RunAsync("optional", Field("name", "Ben")));

    [Fact(DisplayName = "should handle null values")]
    public async Task Nullable_MissingValues_AreNull() => Assert.Equal(
        """{"name":"Ben","email":null,"age":null}""", await RunAsync("nullable", Field("name", "Ben")));

    [Fact(DisplayName = "should handle zod default values")]
    public async Task Defaults_FillTheMissingValues() => Assert.Equal(
        """{"name":"test","email":"test@test.test","favoriteNumbers":[1,2]}""", await RunAsync("defaults"));

    [Fact(DisplayName = "should handle zod chaining of optional, default, and nullish values")]
    public async Task Chained_DefaultsAndNullish_AreApplied() => Assert.Equal(
        """{"name":"test","email":"test@test.test","favoriteNumbers":[1,2]}""",
        await RunAsync("chained", Field("email", "test@test.test")));

    [Fact(DisplayName = "should handle File objects")]
    public async Task File_IsAFile() =>
        Assert.Equal("""{"file":"__File__:test.txt"}""", await RunAsync("file", Upload("file", "test.txt")));

    [Fact(DisplayName = "should handle string arrays")]
    public async Task StringArray_CollectsEveryValue() => Assert.Equal(
        """{"contact":["Ben","Jane","John"]}""",
        await RunAsync("stringArray", Field("contact", "Ben"), Field("contact", "Jane"), Field("contact", "John")));

    [Fact(DisplayName = "should handle number arrays")]
    public async Task NumberArray_ParsesEveryValue() => Assert.Equal(
        """{"age":[25,30,35]}""", await RunAsync("numberArray", Field("age", "25"), Field("age", "30"), Field("age", "35")));

    [Fact(DisplayName = "should preserve \"false\" string values in boolean arrays")]
    public async Task BooleanArray_KeepsFalseStrings() => Assert.Equal(
        """{"flags":[false,true,false]}""",
        await RunAsync("booleanArray", Field("flags", "false"), Field("flags", "true"), Field("flags", "false")));

    [Fact(DisplayName = "should coerce mixed boolean array values correctly")]
    public async Task BooleanArray_MixedValues_AreCoerced()
    {
        foreach (var (values, expected) in new[]
        {
            (new[] { "true", "true" }, """{"flags":[true,true]}"""),
            (new[] { "false", "false" }, """{"flags":[false,false]}"""),
            (new[] { "true", "false", "true" }, """{"flags":[true,false,true]}"""),
        })
        {
            var shown = await RunAsync("booleanArray", values.Select(value => Field("flags", value)).ToArray());

            Assert.Equal(expected, shown);
        }
    }

    [Fact(DisplayName = "should handle an array of File objects")]
    public async Task FileArray_CollectsEveryFile() => Assert.Equal(
        """{"files":["__File__:test1.txt","__File__:test2.txt"]}""",
        await RunAsync("files", Upload("files", "test1.txt"), Upload("files", "test2.txt")));

    [Fact(DisplayName = "should handle nested objects with dot notation keys")]
    public async Task Nested_DottedKeys_BuildObjects() => Assert.Equal(
        """{"a":"hello","bc":{"b":"hoge","c":"world"}}""",
        await RunAsync("nested", Field("a", "hello"), Field("bc.b", "hoge"), Field("bc.c", "world")));

    [Fact(DisplayName = "should parse nested objects with superRefine wrapping")]
    public async Task Nested_WithSuperRefine_IsParsed() => Assert.Equal(
        """{"a":"hello","bc":{"b":"huga","c":"test"}}""",
        await RunAsync("nestedSuperRefine", Field("a", "hello"), Field("bc.b", "huga"), Field("bc.c", "test")));

    [Fact(DisplayName = "should handle deeply nested objects")]
    public async Task DeeplyNested_DottedKeys_BuildObjects() => Assert.Equal(
        """{"a":{"b":{"c":"deep","d":42}}}""", await RunAsync("deeplyNested", Field("a.b.c", "deep"), Field("a.b.d", "42")));

    [Fact(DisplayName = "should return undefined for optional nested objects when no keys present")]
    public async Task OptionalNested_WithoutKeys_IsUndefined() =>
        Assert.Equal("""{"name":"Ben"}""", await RunAsync("optionalNested", Field("name", "Ben")));

    [Fact(DisplayName = "should return null for nullable nested objects when no keys present")]
    public async Task NullableNested_WithoutKeys_IsNull() =>
        Assert.Equal("""{"name":"Ben","address":null}""", await RunAsync("nullableNested", Field("name", "Ben")));

    [Fact(DisplayName = "should parse optional nested objects when keys are present")]
    public async Task OptionalNested_WithKeys_IsParsed() => Assert.Equal(
        """{"name":"Ben","address":{"street":"123 Main","city":"Springfield"}}""",
        await RunAsync(
            "optionalNested", Field("name", "Ben"), Field("address.street", "123 Main"), Field("address.city", "Springfield")));

    [Fact(DisplayName = "should use default value for nested objects when no keys present")]
    public async Task DefaultNested_WithoutKeys_UsesTheDefault() => Assert.Equal(
        """{"name":"Ben","settings":{"theme":"dark"}}""", await RunAsync("defaultNested", Field("name", "Ben")));

    [Fact(DisplayName = "should override default for nested objects when keys are present")]
    public async Task DefaultNested_WithKeys_OverridesTheDefault() => Assert.Equal(
        """{"name":"Ben","settings":{"theme":"light"}}""",
        await RunAsync("defaultNested", Field("name", "Ben"), Field("settings.theme", "light")));

    [Fact(DisplayName = "should handle nested discriminatedUnion")]
    public async Task DiscriminatedUnion_Email_IsParsed() => Assert.Equal(
        """{"name":"Ben","contact":{"type":"email","email":"ben@test.test"}}""",
        await RunAsync(
            "discriminatedUnion", Field("name", "Ben"), Field("contact.type", "email"), Field("contact.email", "ben@test.test")));

    [Fact(DisplayName = "should handle nested discriminatedUnion with alternate variant")]
    public async Task DiscriminatedUnion_Phone_IsParsed() => Assert.Equal(
        """{"name":"Ben","contact":{"type":"phone","phone":"555-1234"}}""",
        await RunAsync(
            "discriminatedUnion", Field("name", "Ben"), Field("contact.type", "phone"), Field("contact.phone", "555-1234")));

    [Fact(DisplayName = "should allow object passthrough when chaining .passthrough() on root object")]
    public async Task Passthrough_KeepsUnknownKeys() => Assert.Equal(
        """{"expected":42,"unexpected":"42"}""",
        await RunAsync("passthrough", Field("expected", "42"), Field("unexpected", "42")));
}
