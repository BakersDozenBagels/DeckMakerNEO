using Emik;
using System.Diagnostics;
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
public class ImageDescription(OrRef<string> Image, OrRef<int> Cx, OrRef<int> Cy, OrRef<float>? Rotation, OrRef<bool>? Recolor, OrRef<int>? Width, OrRef<int>? Height)
{
    public OrRef<string> Image { get; set; } = Image;
    public OrRef<int> Cx { get; set; } = Cx;
    public OrRef<int> Cy { get; set; } = Cy;
    public OrRef<float>? Rotation { get; set; } = Rotation;
    public OrRef<bool>? Recolor { get; set; } = Recolor;
    public OrRef<int>? Width { get; set; } = Width;
    public OrRef<int>? Height { get; set; } = Height;

    internal ImageDescription Clone() => new(Image, Cx, Cy, Rotation, Recolor, Width, Height);
}

[Choice
    .And<BinaryCondition>
    .Or<BinaryCondition>
    .Included<InclusionCondition>
    .Excluded<InclusionCondition>,
    JsonConverter(typeof(ConditionDescriptionConverter))]
public partial class ConditionDescription
{
    internal bool Eval(Dictionary<string, JsonElement> variables)
    {
        if (IsAnd)
            return And.Left.Eval(variables) && And.Right.Eval(variables);
        if (IsOr)
            return Or.Left.Eval(variables) || Or.Right.Eval(variables);
        if (IsIncluded)
        {
            var l = Included.Left.IsConcrete
                ? Included.Left.Concrete
                : variables[Included.Left.Reference];
            var r = Included.Right.IsConcrete
                ? Included.Right.Concrete
                : variables[Included.Right.Reference].Deserialize<OrRef<JsonElement>[]>();
            return r!
                .Select(e => e.IsConcrete ? e.Concrete : variables[e.Reference])
                .Any(x => JsonElement.DeepEquals(x, l));
        }
        if (IsExcluded)
        {
            var l = Excluded.Left.IsConcrete
                ? Excluded.Left.Concrete
                : variables[Excluded.Left.Reference];
            var r = Excluded.Right.IsConcrete
                ? Excluded.Right.Concrete
                : variables[Excluded.Right.Reference].Deserialize<OrRef<JsonElement>[]>();
            return !r!
                .Select(e => e.IsConcrete ? e.Concrete : variables[e.Reference])
                .Any(x => JsonElement.DeepEquals(x, l));
        }

        throw new UnreachableException();
    }

    public record class BinaryCondition(ConditionDescription Left, ConditionDescription Right);
    public record class InclusionCondition(OrRef<JsonElement> Left, OrRef<OrRef<JsonElement>[]> Right);
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

    public record struct GradientStop(OrRef<float> T, OrRef<int> R, OrRef<int> G, OrRef<int> B, OrRef<int> A)
    {
        public GradientStop Fill(Dictionary<string, JsonElement> variables) => new(
                    T.Deref(variables),
                    R.Deref(variables),
                    G.Deref(variables),
                    B.Deref(variables),
                    A.Deref(variables)
                );
    }

    public ColorDescription Fill(Dictionary<string, JsonElement> variables)
    {
        if (IsRGB && RGB is var x)
            return ColorDescription.OfRGB(new(
                x.R.Deref(variables),
                x.G.Deref(variables),
                x.B.Deref(variables)
            ));
        if (IsRGBA && RGBA is var y)
            return ColorDescription.OfRGBA(new(
                y.R.Deref(variables),
                y.G.Deref(variables),
                y.B.Deref(variables),
                y.A.Deref(variables)
            ));
        if (IsAxial && Axial is var z)
            return ColorDescription.OfAxial(new(
                z.CX.Deref(variables),
                z.CY.Deref(variables),
                z.Stops.Select(s => s.Fill(variables)).ToArray()
            ));
        if (IsRadial && Radial is var w)
            return ColorDescription.OfRadial(new(
                w.CX.Deref(variables),
                w.CY.Deref(variables),
                w.Theta0.Deref(variables),
                w.Stops.Select(s => s.Fill(variables)).ToArray()
            ));
        if (IsLinear && Linear is var v)
            return ColorDescription.OfLinear(new(
                v.X1.Deref(variables),
                v.Y1.Deref(variables),
                v.X2.Deref(variables),
                v.Y2.Deref(variables),
                v.Stops.Select(s => s.Fill(variables)).ToArray()
            ));
        throw new UnreachableException();
    }
}

[Choice, JsonConverter(typeof(OrRefReader))]
public partial class OrRef<T>
{
    private readonly T? _concrete;
    private readonly string? _reference;
}