using System.Text.Json;
using System.Text.Json.Serialization;

namespace TPXSoft.Documents.Api.Contracts;

// Tri-state PATCH binding (documentation/README.md's "PATCH is tri-state" rule). A plain
// `Guid?`/`string?` property cannot tell "absent from the body" (leave alone) apart from
// "explicit null" (clear/move to root) -- Patch<T> can. Reusable across every module PATCH
// endpoint, not folder-specific: UpdateFolderRequest is just the first consumer.

/// <summary>
/// IsSet is true whenever the JSON property was present in the payload, including when its value
/// was explicitly null. Value is only meaningful when IsSet is true.
/// </summary>
public readonly struct Patch<T>
{
    public bool IsSet { get; }

    public T? Value { get; }

    private Patch(bool isSet, T? value)
    {
        IsSet = isSet;
        Value = value;
    }

    /// <summary>Default value of the struct -- "the property was absent from the JSON body".</summary>
    public static Patch<T> Unset => default;

    public static Patch<T> Set(T? value) => new(true, value);
}

/// <summary>
/// Registered once in Program.cs's ConfigureHttpJsonOptions. Patch{T} is a struct, so
/// System.Text.Json always invokes the matching converter for a present property -- even when
/// its JSON value is null -- which is exactly the signal IsSet needs. A genuinely absent property
/// never reaches Read at all, leaving the constructor parameter's default(Patch{T}) (IsSet=false)
/// in place.
/// </summary>
public sealed class PatchJsonConverterFactory : JsonConverterFactory
{
    public override bool CanConvert(Type typeToConvert) =>
        typeToConvert.IsGenericType && typeToConvert.GetGenericTypeDefinition() == typeof(Patch<>);

    public override JsonConverter CreateConverter(Type typeToConvert, JsonSerializerOptions options)
    {
        var valueType = typeToConvert.GetGenericArguments()[0];
        var converterType = typeof(PatchJsonConverter<>).MakeGenericType(valueType);
        return (JsonConverter)Activator.CreateInstance(converterType)!;
    }

    private sealed class PatchJsonConverter<T> : JsonConverter<Patch<T>>
    {
        public override Patch<T> Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            var value = JsonSerializer.Deserialize<T>(ref reader, options);
            return Patch<T>.Set(value);
        }

        public override void Write(Utf8JsonWriter writer, Patch<T> value, JsonSerializerOptions options)
        {
            JsonSerializer.Serialize(writer, value.Value, options);
        }
    }
}
