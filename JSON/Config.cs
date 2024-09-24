using Emik;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace DeckMakerNeo.JSON;

public record class Config(FacetDescription[] Facets, ResultDescription[] Cards);
public partial record class CardDescription(string Name, CardDescription.SubCardDescription[] Card)
{
    [Choice.Facet<string>.Blend<BlendDescription>, JsonConverter(typeof(SubCardDescriptionConverter))]
    public partial class SubCardDescription;
}
public record class ResultDescription(string Name, CardDescription.SubCardDescription[] Card, string Hidden) : CardDescription(Name, Card);
public record class BlendDescription(string Id, string Name, BlendDescription.BlendPartDescription[] Parts, ConditionDescription? If)
{
    public record class BlendPartDescription(string Name, CardDescription.SubCardDescription[] Card, string Map, string Id) : CardDescription(Name, Card);
}
public record class FacetDescription(string Id, FacetMemberDescription[]? Members, string[]? Include);
public partial record class FacetMemberDescription(string Name, OrRef<ColorDescription>? Background, OrRef<ColorDescription>? Foreground, OrRef<OrRef<ImageDescription>[]>? Images, Dictionary<string, JsonElement>? Set, ConditionDescription? If);
public record class ImageDescription(OrRef<string> Image, OrRef<int> Cx, OrRef<int> Cy, OrRef<float>? Rotation, OrRef<bool>? Recolor, OrRef<int>? Width, OrRef<int>? Height);

[Choice
    .And<BinaryCondition>
    .Or<BinaryCondition>
    .Included<InclusionCondition>
    .Excluded<InclusionCondition>,
    JsonConverter(typeof(ConditionDescriptionConverter))]
public partial class ConditionDescription
{
    public record struct BinaryCondition(ConditionDescription Left, ConditionDescription Right);
    public record struct InclusionCondition(OrRef<JsonElement> Left, OrRef<OrRef<JsonElement>[]> Right);
}

[Choice
    .RGB<RGBColor>
    .RGBA<RGBAColor>
    .Axial<AxialGradientColor>
    .Radial<RadialGradientColor>
    .Linear<LinearGradientColor>,
    JsonConverter(typeof(ColorDescriptionConverter))]
public partial class ColorDescription
{
    public record struct RGBColor(OrRef<int> R, OrRef<int> G, OrRef<int> B);
    public record struct RGBAColor(OrRef<int> R, OrRef<int> G, OrRef<int> B, OrRef<int> A);
    public record struct AxialGradientColor(OrRef<int> CX, OrRef<int> CY, GradientStop[] Stops);
    public record struct RadialGradientColor(OrRef<int> CX, OrRef<int> CY, OrRef<float> Theta0, GradientStop[] Stops);
    public record struct LinearGradientColor(OrRef<int> X1, OrRef<int> Y1, OrRef<int> X2, OrRef<int> Y2, GradientStop[] Stops);

    public record struct GradientStop(OrRef<float> T, OrRef<int> R, OrRef<int> G, OrRef<int> B, OrRef<int> A);
}

[Choice, JsonConverter(typeof(OrRefReader))]
public partial class OrRef<T>
{
    private readonly T? _concrete;
    private readonly string? _reference;
}