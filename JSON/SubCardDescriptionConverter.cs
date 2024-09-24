using System.Text.Json;
using System.Text.Json.Serialization;

namespace DeckMakerNeo.JSON;

internal class SubCardDescriptionConverter : JsonConverter<CardDescription.SubCardDescription>
{
    private readonly Type _type = typeof(BlendDescription);

    public override CardDescription.SubCardDescription Read(
        ref Utf8JsonReader reader,
        Type typeToConvert,
        JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.String)
            return reader.GetString()!;
        else
            return ((JsonConverter<BlendDescription>)options.GetConverter(typeof(BlendDescription)))
                .Read(ref reader, _type, options)!;
    }

    public override void Write(
        Utf8JsonWriter writer,
        CardDescription.SubCardDescription obj,
        JsonSerializerOptions options)
    {
        if (obj.IsFacet)
            writer.WriteStringValue(obj.Facet);
        else
            ((JsonConverter<BlendDescription>)options.GetConverter(typeof(BlendDescription)))
                .Write(writer, obj.Blend, options);
    }
}
