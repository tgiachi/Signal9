using System.Text.Json;
using System.Text.Json.Serialization;

namespace SignalNine.Web.Services.Json;

/// <summary>
/// Accepts enum payloads as either a number or a case-insensitive string
/// on the read side, but serializes back as the numeric value so existing
/// HTTP test clients (and the React TS types that already expect numbers
/// like dayOfWeek=1) keep working.
/// </summary>
public sealed class LenientNumericEnumConverter : JsonConverterFactory
{
    public override bool CanConvert(Type typeToConvert)
    {
        return typeToConvert.IsEnum;
    }

    public override JsonConverter CreateConverter(Type typeToConvert, JsonSerializerOptions options)
    {
        var converterType = typeof(InnerConverter<>).MakeGenericType(typeToConvert);
        return (JsonConverter)Activator.CreateInstance(converterType)!;
    }

    private sealed class InnerConverter<T> : JsonConverter<T>
        where T : struct, Enum
    {
        public override T Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType == JsonTokenType.Number)
            {
                if (reader.TryGetInt64(out var raw))
                {
                    var underlying = Enum.GetUnderlyingType(typeToConvert);
                    var boxed = Convert.ChangeType(raw, underlying);
                    return (T)Enum.ToObject(typeToConvert, boxed);
                }
                throw new JsonException($"Invalid numeric value for {typeToConvert.Name}.");
            }
            if (reader.TokenType == JsonTokenType.String)
            {
                var s = reader.GetString();
                if (string.IsNullOrEmpty(s))
                {
                    throw new JsonException($"Empty string is not a valid {typeToConvert.Name}.");
                }
                if (Enum.TryParse<T>(s, ignoreCase: true, out var parsed))
                {
                    return parsed;
                }
                throw new JsonException($"'{s}' is not a valid value for {typeToConvert.Name}.");
            }
            throw new JsonException($"Unexpected token {reader.TokenType} for enum {typeToConvert.Name}.");
        }

        public override void Write(Utf8JsonWriter writer, T value, JsonSerializerOptions options)
        {
            var underlying = Enum.GetUnderlyingType(typeof(T));
            if (underlying == typeof(int))
            {
                writer.WriteNumberValue((int)(object)value);
            }
            else if (underlying == typeof(long))
            {
                writer.WriteNumberValue((long)(object)value);
            }
            else if (underlying == typeof(byte))
            {
                writer.WriteNumberValue((byte)(object)value);
            }
            else if (underlying == typeof(short))
            {
                writer.WriteNumberValue((short)(object)value);
            }
            else
            {
                writer.WriteNumberValue(Convert.ToInt64(value, System.Globalization.CultureInfo.InvariantCulture));
            }
        }
    }
}
