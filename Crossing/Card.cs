using DeckMakerNeo.Crossing;
using DeckMakerNeo.JSON;
using Emik;
using System.Diagnostics;
using System.Runtime.InteropServices.Marshalling;
using System.Text.Json;

namespace DeckMakerNeo;

public class Card
{
    private static readonly JsonElement Unit = JsonSerializer.Deserialize<JsonElement>("0");

    public string? Name { get; set; }
    internal Dictionary<string, string> Names { get; init; } = [];
    public OrRef<ColorDescription>? Foreground { get; private set; }
    public OrRef<ColorDescription>? Background { get; private set; }
    public List<OrRef<ImageDescription>> Images { get; } = [];
    public List<OrRef<OrRef<ImageDescription>[]>> ImageLists { get; } = [];
    internal Dictionary<string, JsonElement> Variables { get; } = [];
    internal ConditionDescription Condition { get; private set; } = ConditionDescription.OfExcluded(new(
            OrRef<JsonElement>.OfConcrete(Unit),
            OrRef<OrRef<JsonElement>[]>.OfConcrete([])
        ));

    internal virtual Card Clone()
    {
        Card c = new();
        foreach (var p in Names)
            c.Names.Add(p.Key, p.Value);
        c.Foreground = Foreground;
        c.Background = Background;
        c.Images.AddRange(Images.Select(static i => i is null ? throw new UnreachableException() : i.IsReference ? i : i.Concrete.Clone()));
        foreach (var v in Variables)
            c.Variables.Add(v.Key, v.Value);
        c.Condition = Condition;
        return c;
    }

    internal virtual Card Merge(string facetId, BlendItem b)
    {
        if (b.IsFacet && b.Facet is var desc)
        {
            var c = Clone();
            c.Names[facetId] = desc.Name;
            if (desc.Background is not null)
                c.Background = desc.Background;
            if (desc.Foreground is not null)
                c.Foreground = desc.Foreground;
            desc.Images?.Map(
                    concrete => c.Images.AddRange(concrete.Select(i => i.IsReference ? i : i.Concrete.Clone())!),
                    reference => c.ImageLists.Add(reference!));
            if (desc.Set is not null)
                foreach (var v in desc.Set)
                    if (v.Key is ['$', .. var rest])
                        c.Variables[rest] = v.Value;
                    else
                        throw new ArgumentException($"Bad set key \"{v.Key}\"");
            if (desc.If is not null)
                c.Condition = ConditionDescription.OfAnd(new(c.Condition, desc.If));
            return c;
        }
        else if (b.IsBlend && b.Blend is var blend)
            return new BlendCard()
            {
                Maps = [.. blend.Maps],
                Cards = [.. blend.Cards.Select(d => Merge(facetId, BlendItem.OfOneBlend(d)))],
                Names = Names.ToDictionary().Append(facetId, blend.Name!)
            };
        else if (b.OneBlend is var one)
        {
            if (one is BlendCard)
                throw new NotSupportedException("Recursive Blends are not supported.");
            var c = Clone();
            c.Names[facetId] = one!.Name!;
            c.Background = one.Background ?? c.Background;
            c.Foreground = one.Foreground ?? c.Foreground;
            c.Images.AddRange(one.Images);
            c.ImageLists.AddRange(one.ImageLists);
            foreach (var v in one.Variables)
                c.Variables[v.Key] = v.Value;
            c.Condition = ConditionDescription.OfAnd(new(c.Condition, one.Condition));
            return c;
        }
        else
            throw new UnreachableException();
    }

    internal virtual bool Passes() => Condition.Eval(Variables);
    public virtual void Fill(Func<string, bool> fileExists)
    {
        if (Foreground is null) throw new ArgumentException($"Foreground not set for card {Name}");
        Foreground = Foreground.Deref(Variables).Concrete!.Fill(Variables);
        if (Background is null) throw new ArgumentException($"Background not set for card {Name}");
        Background = Background.Deref(Variables).Concrete!.Fill(Variables);
        for (int i = 0; i < ImageLists.Count; i++)
            ImageLists[i].Map(
                conc => Images.AddRange(conc!),
                refe => Images.AddRange(Variables[refe!].Deserialize<OrRef<ImageDescription>[]>()!)
            );
        for (int i = 0; i < Images.Count; i++)
            Images[i] = Images[i].Deref(Variables);
        foreach (var image in Images)
        {
            Debug.Assert(image.IsConcrete);
            image.Concrete.Image = image.Concrete.Image.Deref(Variables);
            if (!fileExists(image.Concrete!.Image.Concrete!))
                throw new ArgumentException($"File '{image.Concrete.Image.Concrete}' not found");
            image.Concrete.Cy = image.Concrete.Cy.Deref(Variables);
            image.Concrete.Cx = image.Concrete.Cx.Deref(Variables);
            image.Concrete.Rotation ??= OrRef<float>.OfConcrete(0);
            image.Concrete.Rotation = image.Concrete.Rotation.Deref(Variables);
            image.Concrete.Recolor ??= OrRef<bool>.OfConcrete(true);
            image.Concrete.Recolor = image.Concrete.Recolor.Deref(Variables);
            image.Concrete.Width = image.Concrete.Width?.Deref(Variables);
            image.Concrete.Height = image.Concrete.Height?.Deref(Variables);
        }
    }
}

[Choice.Facet<FacetMemberDescription>.Blend<BlendCard>.OneBlend<Card>]
internal readonly partial record struct BlendItem;

public class BlendCard : Card
{
    public List<string> Maps { get; init; } = [];
    public Card[] Cards { get; set; } = [];
    internal override Card Clone() => BClone();
    internal BlendCard BClone() => new()
    {
        Maps = [.. Maps],
        Cards = [.. Cards],
        Names = Names.ToDictionary()
    };
    internal override Card Merge(string facetId, BlendItem b)
    {
        if (b.IsFacet && b.Facet is var desc)
        {
            var c = BClone();
            c.Cards = c.Cards.Select(c => c.Merge(facetId, b)).ToArray();
            c.Names[facetId] = desc.Name;
            return c;
        }
        else if (b.IsBlend && b.Blend is var blend)
            return new BlendCard()
            {
                Maps = [.. blend.Maps],
                Cards = [.. blend.Cards.Select(d => Merge(facetId, BlendItem.OfOneBlend(d)))],
                Names = Names.ToDictionary().Append(facetId, blend.Name!)
            };
        else if (b.OneBlend is var one)
        {
            var c = BClone();
            c.Cards = c.Cards.Select(c => c.Merge(facetId, one!)).ToArray();
            return c;
        }
        else
            throw new UnreachableException();
    }

    internal override bool Passes() => Cards.All(static c => c.Passes());
    public override void Fill(Func<string, bool> fileExists)
    {
        foreach (var card in Cards)
            card.Fill(fileExists);
        foreach (var map in Maps)
            if (!fileExists(map))
                throw new ArgumentException($"File '{map}' not found");
    }
}