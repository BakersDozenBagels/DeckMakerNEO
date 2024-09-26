using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace DeckMakerNeo.JSON;

public class OrRefReader : JsonConverterFactory
{
    public override bool CanConvert(Type typeToConvert) =>
        typeToConvert.IsGenericType && typeToConvert.GetGenericTypeDefinition() == typeof(OrRef<>);

    public override JsonConverter? CreateConverter(Type type, JsonSerializerOptions options)
    {
        Type[] typeArguments = type.GetGenericArguments();
        Type typeParam = typeArguments[0];

        JsonConverter converter = (JsonConverter)Activator.CreateInstance(
            typeof(OrRefConverter<>).MakeGenericType([typeParam]),
            BindingFlags.Instance | BindingFlags.Public,
            binder: null,
            args: [options],
            culture: null)!;

        return converter;
    }

    private class OrRefConverter<T>(JsonSerializerOptions options) : JsonConverter<OrRef<T>>
    {
        private readonly JsonConverter<T> _valueConverter = (JsonConverter<T>)options
                .GetConverter(typeof(T));
        private readonly Type _type = typeof(T);

        public override OrRef<T> Read(
            ref Utf8JsonReader reader,
            Type typeToConvert,
            JsonSerializerOptions options)
        {
            if (reader.TokenType == JsonTokenType.String)
            {
                var str = reader.GetString();
                if (str is { } && str is ['$', ..var rest])
                    return OrRef<T>.OfReference(rest);
            }
            return OrRef<T>.OfConcrete(_valueConverter.Read(ref reader, _type, options));
        }

        public override void Write(
            Utf8JsonWriter writer,
            OrRef<T> obj,
            JsonSerializerOptions options)
        {
            obj.Map(
                concrete => _valueConverter.Write(writer, concrete!, options),
                reference => writer.WriteStringValue($"${reference}")
            );
        }
    }
}
