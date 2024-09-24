using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace DeckMakerNeo.JSON;

internal partial class ConditionDescriptionConverter : JsonConverter<ConditionDescription>
{
    private static readonly Type
        _type = typeof(OrRef<JsonElement>),
        _elemType = typeof(JsonElement),
        _elemsType = typeof(OrRef<OrRef<JsonElement>[]>);
    private static readonly string[]
        _keys = ["left", "right", "mode"],
        _modes = ["and", "or", "in", "not in"];

    public override ConditionDescription? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var elemConverter = (JsonConverter<JsonElement>)options.GetConverter(_elemType);
        JsonElement? left = null, right = null;
        string? mode = null;

        if (reader.TokenType != JsonTokenType.StartObject)
            throw new JsonException();

        for (int i = 0; i < 3; i++)
        {
            reader.Read();
            if (reader.TokenType != JsonTokenType.PropertyName)
                throw new JsonException();
            var key = reader.GetString() ?? throw new JsonException();
            var n_key = key.ToLowerInvariant();
            if (!_keys.Contains(n_key))
                throw new JsonException();

            reader.Read();
            switch (n_key)
            {
                case "left":
                    if (left is not null) throw new JsonException();
                    left = elemConverter.Read(ref reader, _elemType, options);
                    if (left is null) throw new JsonException();
                    break;
                case "right":
                    if (right is not null) throw new JsonException();
                    right = elemConverter.Read(ref reader, _elemType, options);
                    if (right is null) throw new JsonException();
                    break;
                case "mode":
                    if (left is not null) throw new JsonException();
                    mode = reader.GetString()?.ToLowerInvariant();
                    if (mode is null || !_modes.Contains(mode))
                        throw new JsonException();
                    break;
            }
        }

        Debug.Assert(left is not null);
        Debug.Assert(right is not null);
        Debug.Assert(mode is not null);

        reader.Read();
        if (reader.TokenType != JsonTokenType.EndObject)
            throw new JsonException();

        return mode switch
        {
            "and" => ConditionDescription.OfAnd(new(
                                left.Value.Deserialize<ConditionDescription>(options) ?? throw new JsonException(),
                                right.Value.Deserialize<ConditionDescription>(options) ?? throw new JsonException()
                            )),
            "or" => ConditionDescription.OfOr(new(
                                left.Value.Deserialize<ConditionDescription>(options) ?? throw new JsonException(),
                                right.Value.Deserialize<ConditionDescription>(options) ?? throw new JsonException()
                            )),
            "in" => ConditionDescription.OfIncluded(new(
                                left.Value.Deserialize<OrRef<JsonElement>>(options) ?? throw new JsonException(),
                                right.Value.Deserialize<OrRef<OrRef<JsonElement>[]>>(options) ?? throw new JsonException()
                            )),
            "not in" => ConditionDescription.OfExcluded(new(
                                left.Value.Deserialize<OrRef<JsonElement>>(options) ?? throw new JsonException(),
                                right.Value.Deserialize<OrRef<OrRef<JsonElement>[]>>(options) ?? throw new JsonException()
                            )),
            _ => throw new UnreachableException(),
        };
    }

    public override void Write(Utf8JsonWriter writer, ConditionDescription value, JsonSerializerOptions options)
    {
        var key = options.PropertyNamingPolicy?.ConvertName("Mode") ?? "mode";
        var left = options.PropertyNamingPolicy?.ConvertName("left") ?? "left";
        var right = options.PropertyNamingPolicy?.ConvertName("right") ?? "right";
        writer.WriteStartObject();
        value.Map(
            and =>
            {
                writer.WriteString(key, "and");
                writer.WritePropertyName(left);
                Write(writer, and.Left, options);
                writer.WritePropertyName(right);
                Write(writer, and.Right, options);
            },
            or =>
            {
                writer.WriteString(key, "or");
                writer.WritePropertyName(left);
                Write(writer, or.Left, options);
                writer.WritePropertyName(right);
                Write(writer, or.Right, options);
            },
            incl =>
            {
                writer.WriteString(key, "in");
                writer.WritePropertyName(left);
                ((JsonConverter<OrRef<JsonElement>>)options.GetConverter(_type))
                    .Write(writer, incl.Left, options);
                writer.WritePropertyName(right);
                ((JsonConverter<OrRef<OrRef<JsonElement>[]>>)options.GetConverter(_elemsType))
                    .Write(writer, incl.Right, options);
            },
            excl =>
            {
                writer.WriteString(key, "not in");
                writer.WritePropertyName(left);
                ((JsonConverter<OrRef<JsonElement>>)options.GetConverter(_type))
                    .Write(writer, excl.Left, options);
                writer.WritePropertyName(right);
                ((JsonConverter<OrRef<OrRef<JsonElement>[]>>)options.GetConverter(_elemsType))
                    .Write(writer, excl.Right, options);
            }
        );
        writer.WriteEndObject();
    }
}