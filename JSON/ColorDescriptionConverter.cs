using System.Text.Json;
using System.Text.Json.Serialization;

namespace DeckMakerNeo.JSON;

internal class ColorDescriptionConverter : JsonConverter<ColorDescription>
{
    private static readonly Type _int = typeof(OrRef<int>), _float = typeof(OrRef<float>);

    public override ColorDescription? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.StartArray)
            throw new JsonException();

        reader.Read();

        var copy = reader;

        int count = 0;
        while (copy.TokenType != JsonTokenType.EndArray)
        {
            if (copy.TokenType is not JsonTokenType.Number
                && (copy.TokenType is not JsonTokenType.String
                    || copy.GetString() is not ['$', ..]))
                throw new JsonException();
            count++;
            copy.Read();
        }

        var intConverter = (JsonConverter<OrRef<int>>)options.GetConverter(_int);

        if (count is 3)
        {
            var r = intConverter.Read(ref reader, _int, options) ?? throw new JsonException();
            reader.Read();
            var g = intConverter.Read(ref reader, _int, options) ?? throw new JsonException();
            reader.Read();
            var b = intConverter.Read(ref reader, _int, options) ?? throw new JsonException();
            reader.Read();
            return ColorDescription.OfRGB(new(r, g, b));
        }

        if (count is 4)
        {
            var r = intConverter.Read(ref reader, _int, options) ?? throw new JsonException();
            reader.Read();
            var g = intConverter.Read(ref reader, _int, options) ?? throw new JsonException();
            reader.Read();
            var b = intConverter.Read(ref reader, _int, options) ?? throw new JsonException();
            reader.Read();
            var a = intConverter.Read(ref reader, _int, options) ?? throw new JsonException();
            reader.Read();
            return ColorDescription.OfRGBA(new(r, g, b, a));
        }

        var floatConverter = (JsonConverter<OrRef<float>>)options.GetConverter(_float);

        if (count % 5 is 2 && count is not 2)
        {
            var cx = intConverter.Read(ref reader, _int, options) ?? throw new JsonException();
            reader.Read();
            var cy = intConverter.Read(ref reader, _int, options) ?? throw new JsonException();
            reader.Read();
            var stops = ReadStops(ref reader, options, intConverter, floatConverter);
            return ColorDescription.OfAxial(new(cx, cy, stops));
        }
        if (count % 5 is 3)
        {
            var cx = intConverter.Read(ref reader, _int, options) ?? throw new JsonException();
            reader.Read();
            var cy = intConverter.Read(ref reader, _int, options) ?? throw new JsonException();
            reader.Read();
            var t0 = floatConverter.Read(ref reader, _float, options) ?? throw new JsonException();
            reader.Read();
            var stops = ReadStops(ref reader, options, intConverter, floatConverter);
            return ColorDescription.OfRadial(new(cx, cy, t0, stops));
        }
        if (count % 5 is 4)
        {
            var x1 = intConverter.Read(ref reader, _int, options) ?? throw new JsonException();
            reader.Read();
            var y1 = intConverter.Read(ref reader, _int, options) ?? throw new JsonException();
            reader.Read();
            var x2 = intConverter.Read(ref reader, _int, options) ?? throw new JsonException();
            reader.Read();
            var y2 = intConverter.Read(ref reader, _int, options) ?? throw new JsonException();
            reader.Read();
            var stops = ReadStops(ref reader, options, intConverter, floatConverter);
            return ColorDescription.OfLinear(new(x1, y1, x2, y2, stops));
        }

        throw new JsonException();
    }

    public override void Write(Utf8JsonWriter writer, ColorDescription value, JsonSerializerOptions options)
    {
        writer.WriteStartArray();
        var intConverter = (JsonConverter<OrRef<int>>)options.GetConverter(_int);
        var floatConverter = (JsonConverter<OrRef<float>>)options.GetConverter(_float);
        value.Map(
            rgb =>
            {
                intConverter.Write(writer, rgb.R, options);
                intConverter.Write(writer, rgb.G, options);
                intConverter.Write(writer, rgb.B, options);
            },
            rgba =>
            {
                intConverter.Write(writer, rgba.R, options);
                intConverter.Write(writer, rgba.G, options);
                intConverter.Write(writer, rgba.B, options);
                intConverter.Write(writer, rgba.A, options);
            },
            axial =>
            {
                intConverter.Write(writer, axial.CX, options);
                intConverter.Write(writer, axial.CY, options);
                WriteStops(writer, axial.Stops, options, intConverter, floatConverter);
            },
            radial =>
            {
                intConverter.Write(writer, radial.CX, options);
                intConverter.Write(writer, radial.CY, options);
                floatConverter.Write(writer, radial.Theta0, options);
                WriteStops(writer, radial.Stops, options, intConverter, floatConverter);
            },
            linear =>
            {
                intConverter.Write(writer, linear.X1, options);
                intConverter.Write(writer, linear.Y1, options);
                intConverter.Write(writer, linear.X2, options);
                intConverter.Write(writer, linear.Y2, options);
                WriteStops(writer, linear.Stops, options, intConverter, floatConverter);
            });
        writer.WriteEndArray();
    }

    private static ColorDescription.GradientStop[] ReadStops(ref Utf8JsonReader reader, JsonSerializerOptions options, JsonConverter<OrRef<int>> intConverter, JsonConverter<OrRef<float>> floatConverter)
    {
        List<ColorDescription.GradientStop> stops = [];
        while (true)
        {
            var t = floatConverter.Read(ref reader, _float, options) ?? throw new JsonException();
            var r = intConverter.Read(ref reader, _int, options) ?? throw new JsonException();
            var g = intConverter.Read(ref reader, _int, options) ?? throw new JsonException();
            var b = intConverter.Read(ref reader, _int, options) ?? throw new JsonException();
            var a = intConverter.Read(ref reader, _int, options) ?? throw new JsonException();
            stops.Add(new(t, r, g, b, a));

            reader.Read();
            if (reader.TokenType == JsonTokenType.EndArray)
                break;
        }

        return [.. stops];
    }

    private static void WriteStops(Utf8JsonWriter writer, ColorDescription.GradientStop[] value, JsonSerializerOptions options, JsonConverter<OrRef<int>> intConverter, JsonConverter<OrRef<float>> floatConverter)
    {
        for (int i = 0; i < value.Length; i++)
        {
            floatConverter.Write(writer, value[i].T, options);
            intConverter.Write(writer, value[i].R, options);
            intConverter.Write(writer, value[i].G, options);
            intConverter.Write(writer, value[i].B, options);
            intConverter.Write(writer, value[i].A, options);
        }
    }
}