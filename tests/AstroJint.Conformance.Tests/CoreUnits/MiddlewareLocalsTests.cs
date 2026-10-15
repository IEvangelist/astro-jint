using System.Text.Json;

namespace AstroJint.Conformance.CoreUnits;

/// <summary>
/// Adapted port of <c>packages/astro/test/units/middleware/locals.test.ts</c>: <c>isLocalsSerializable()</c> accepts null,
/// strings, numbers, booleans, plain objects (nested too) and arrays, refuses a <c>Date</c>, a <c>Map</c>, a <c>Set</c>, a class
/// instance and a plain object that contains one of them, does not overflow the stack on an object 10,000 levels deep, and
/// <c>trySerializeLocals()</c> returns the JSON of a serializable value and throws for another.
/// <para>
/// Differences from upstream: the functions are public API of the <c>astro/middleware</c> package, and upstream imports them
/// from its <c>dist</c> and calls them in the test process. Here the endpoint
/// <c>src/pages/locals-serialization/[case].ts</c> of the <c>app-middleware</c> fixture imports them from
/// <c>astro/middleware</c>, builds the value of each upstream test (the class, the dates, the maps and the deep object
/// too) and calls the function inside the runtime that runs the site, answering the JSON <c>{ "value": … }</c> or
/// <c>{ "error": message }</c>, so every test is Adapted: the same calls on the same values, with their result read over
/// HTTP. The one with <c>throws</c> matches the message of the error with the upstream <c>/serialized/i</c>.
/// </para>
/// </summary>
[Trait("Upstream", "core-units/middleware/locals.test.ts")]
public class MiddlewareLocalsTests(SiteHost sites)
{
    private async Task<JsonElement> CallAsync(string name)
    {
        var site = await sites.GetMiddlewareSiteAsync();
        var response = await site.SendRawAsync($"/locals-serialization/{name}");

        Assert.Equal(200, response.StatusCode);
        using var json = JsonDocument.Parse(response.Text);
        return json.RootElement.Clone();
    }

    private async Task<bool> IsSerializableAsync(string name)
    {
        var result = await CallAsync(name);
        return result.GetProperty("value").GetBoolean();
    }

    [Fact(DisplayName = "returns true for null")]
    public async Task IsLocalsSerializable_Null() => Assert.True(await IsSerializableAsync("null"));

    [Fact(DisplayName = "returns true for string")]
    public async Task IsLocalsSerializable_String() => Assert.True(await IsSerializableAsync("string"));

    [Fact(DisplayName = "returns true for number")]
    public async Task IsLocalsSerializable_Number() => Assert.True(await IsSerializableAsync("number"));

    [Fact(DisplayName = "returns true for boolean")]
    public async Task IsLocalsSerializable_Boolean()
    {
        Assert.True(await IsSerializableAsync("true"));
        Assert.True(await IsSerializableAsync("false"));
    }

    [Fact(DisplayName = "returns true for a plain object")]
    public async Task IsLocalsSerializable_PlainObject() => Assert.True(await IsSerializableAsync("object"));

    [Fact(DisplayName = "returns true for a nested plain object")]
    public async Task IsLocalsSerializable_NestedPlainObject() => Assert.True(await IsSerializableAsync("nested"));

    [Fact(DisplayName = "returns true for an array")]
    public async Task IsLocalsSerializable_Array() => Assert.True(await IsSerializableAsync("array"));

    [Fact(DisplayName = "returns false for a Date")]
    public async Task IsLocalsSerializable_Date() => Assert.False(await IsSerializableAsync("date"));

    [Fact(DisplayName = "returns false for a Map")]
    public async Task IsLocalsSerializable_Map() => Assert.False(await IsSerializableAsync("map"));

    [Fact(DisplayName = "returns false for a Set")]
    public async Task IsLocalsSerializable_Set() => Assert.False(await IsSerializableAsync("set"));

    [Fact(DisplayName = "returns false for a class instance")]
    public async Task IsLocalsSerializable_ClassInstance() => Assert.False(await IsSerializableAsync("class-instance"));

    [Fact(DisplayName = "returns false for a plain object containing a non-serializable value")]
    public async Task IsLocalsSerializable_PlainObjectWithANonSerializableValue() =>
        Assert.False(await IsSerializableAsync("object-with-date"));

    [Fact(DisplayName = "handles deeply nested objects without stack overflow (iterative implementation)")]
    public async Task IsLocalsSerializable_DeeplyNestedObject() => Assert.True(await IsSerializableAsync("deep"));

    [Fact(DisplayName = "returns a JSON string for a serializable object")]
    public async Task TrySerializeLocals_ReturnsAJsonString()
    {
        var result = await CallAsync("try-serialize");

        var value = result.GetProperty("value");
        Assert.Equal(JsonValueKind.String, value.ValueKind);
        using var parsed = JsonDocument.Parse(value.GetString()!);
        Assert.Equal("alice", parsed.RootElement.GetProperty("user").GetString());
        Assert.Equal(3, parsed.RootElement.GetProperty("count").GetInt32());
        Assert.Equal(2, parsed.RootElement.EnumerateObject().Count());
    }

    [Fact(DisplayName = "throws for a non-serializable value")]
    public async Task TrySerializeLocals_ThrowsForANonSerializableValue()
    {
        var result = await CallAsync("try-serialize-date");

        Assert.False(result.TryGetProperty("value", out _));
        Assert.Matches("(?i)serialized", result.GetProperty("error").GetString());
    }
}
