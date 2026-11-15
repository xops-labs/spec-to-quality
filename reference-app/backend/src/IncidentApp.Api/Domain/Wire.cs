using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace IncidentApp.Api.Domain;

/// <summary>Maps C# enum members to the upper-snake-case strings used on the wire (e.g. AtRisk -> AT_RISK).</summary>
public static class Wire
{
    private static class Cache<T> where T : struct, Enum
    {
        public static readonly Dictionary<T, string> ToWire = Enum.GetValues<T>().ToDictionary(v => v, v => Convert(v.ToString()));
        public static readonly Dictionary<string, T> FromWire = ToWire.ToDictionary(kv => kv.Value, kv => kv.Key);
    }

    private static string Convert(string name)
    {
        var sb = new StringBuilder();
        for (var i = 0; i < name.Length; i++)
        {
            if (i > 0 && char.IsUpper(name[i]) && !char.IsUpper(name[i - 1]) && !char.IsDigit(name[i - 1]))
            {
                sb.Append('_');
            }
            sb.Append(char.ToUpperInvariant(name[i]));
        }
        return sb.ToString();
    }

    public static string ToWire<T>(T value) where T : struct, Enum => Cache<T>.ToWire[value];

    /// <summary>Case-sensitive parse of a wire value.</summary>
    public static bool TryParse<T>(string? text, out T value) where T : struct, Enum
    {
        value = default;
        return text is not null && Cache<T>.FromWire.TryGetValue(text, out value);
    }

    public static IEnumerable<string> Names<T>() where T : struct, Enum => Cache<T>.ToWire.Values;
}

public sealed class WireEnumConverterFactory : JsonConverterFactory
{
    public override bool CanConvert(Type typeToConvert) => typeToConvert.IsEnum;

    public override JsonConverter CreateConverter(Type typeToConvert, JsonSerializerOptions options) =>
        (JsonConverter)Activator.CreateInstance(typeof(WireEnumConverter<>).MakeGenericType(typeToConvert))!;

    private sealed class WireEnumConverter<T> : JsonConverter<T> where T : struct, Enum
    {
        public override T Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
            Wire.TryParse<T>(reader.GetString(), out var v) ? v : throw new JsonException("Unknown enum value.");

        public override void Write(Utf8JsonWriter writer, T value, JsonSerializerOptions options) =>
            writer.WriteStringValue(Wire.ToWire(value));
    }
}

/// <summary>ISO-8601 UTC with millisecond precision and a Z suffix.</summary>
public sealed class UtcMillisConverter : JsonConverter<DateTime>
{
    public const string Format = "yyyy-MM-dd'T'HH:mm:ss.fff'Z'";

    public static string FormatUtc(DateTime value) => value.ToUniversalTime().ToString(Format, System.Globalization.CultureInfo.InvariantCulture);

    public override DateTime Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
        DateTime.Parse(reader.GetString()!, System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.AdjustToUniversal | System.Globalization.DateTimeStyles.AssumeUniversal);

    public override void Write(Utf8JsonWriter writer, DateTime value, JsonSerializerOptions options) =>
        writer.WriteStringValue(FormatUtc(value));
}

public sealed class NullableUtcMillisConverter : JsonConverter<DateTime?>
{
    public override DateTime? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
        reader.TokenType == JsonTokenType.Null ? null : new UtcMillisConverter().Read(ref reader, typeToConvert, options);

    public override void Write(Utf8JsonWriter writer, DateTime? value, JsonSerializerOptions options)
    {
        if (value is null) writer.WriteNullValue();
        else writer.WriteStringValue(UtcMillisConverter.FormatUtc(value.Value));
    }
}
