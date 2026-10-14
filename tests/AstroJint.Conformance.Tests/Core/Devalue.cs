using System.Text.Json;
using System.Text.Json.Nodes;

namespace AstroJint.Conformance.Core;

/// <summary>
/// Reads what <c>devalue.stringify()</c> writes, which is how Astro answers an action call: a flat JSON array whose first
/// element is the root value and in which every object and array holds the indexes of its members. It handles what the
/// actions of the core fixtures return (strings, numbers, booleans, <see langword="null"/>, arrays and plain objects)
/// and refuses the tagged types (<c>Date</c>, <c>Map</c>, <c>URL</c>, ...) rather than guess at them.
/// </summary>
internal static class Devalue
{
    public static JsonNode? Parse(string text)
    {
        using var document = JsonDocument.Parse(text);
        var values = document.RootElement.EnumerateArray().Select(value => value.Clone()).ToArray();
        return Hydrate(values, 0);
    }

    private static JsonNode? Hydrate(JsonElement[] values, int index)
    {
        // devalue encodes undefined as -1; NaN, the infinities and -0 (-2 to -5) do not occur in the fixtures.
        if (index == -1)
        {
            return null;
        }

        if (index < 0)
        {
            throw new NotSupportedException($"The devalue constant {index} is not supported.");
        }

        var value = values[index];
        switch (value.ValueKind)
        {
            case JsonValueKind.Array:
                if (value.GetArrayLength() > 0 && value[0].ValueKind == JsonValueKind.String)
                {
                    throw new NotSupportedException($"The devalue type '{value[0].GetString()}' is not supported.");
                }

                return new JsonArray([.. value.EnumerateArray().Select(item => Hydrate(values, item.GetInt32()))]);
            case JsonValueKind.Object:
                var result = new JsonObject();
                foreach (var property in value.EnumerateObject())
                {
                    result[property.Name] = Hydrate(values, property.Value.GetInt32());
                }

                return result;
            default:
                return JsonNode.Parse(value.GetRawText());
        }
    }
}
