using DeckMakerNeo.Crossing;
using DeckMakerNeo.JSON;
using Emik;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Text;
using System.Text.Json;

namespace DeckMakerNeo;

internal static class Crosser
{
    public static List<Deck> Cross(Config config)
    {
        var facetLookup = config.Facets.ToDictionary(f => f.Id, f => (f.Members, f.Include));
        var facetCache = new Dictionary<string, FacetMemberDescription[]>();
        FacetMemberDescription[] LookupFacet(string id, string[]? visited = null)
        {
            if (visited is not null && visited.Contains(id))
                return [];

            if (visited is null && facetCache.TryGetValue(id, out var cached))
                return cached;

            var (Members, Include) = facetLookup[id];
            List<FacetMemberDescription> members = [];
            if (Members is not null)
                members.AddRange(Members);
            if (Include is not null)
            {
                foreach (var incl in Include)
                {
                    var dot = incl.IndexOf('.');
                    if (dot is not -1)
                        members.Add(facetLookup[incl[..dot]].Members!.Single(m => m!.Name == incl[(1 + dot)..]));
                    else
                        members.AddRange(LookupFacet(incl, [.. visited ?? [], id]));
                }
            }

            var ret = members.ToArray();
            if (visited is null)
                facetCache[id] = ret;
            return ret;
        }

        List<Deck> sheets = [];
        foreach (var card in config.Cards)
            sheets.Add(new(card.Name, card.Hidden, Cross(card, s => LookupFacet(s))));
        return sheets;
    }

    private static List<Sheet> Cross(CardDescription card, Func<string, FacetMemberDescription[]> facetLookup)
    {
        if (!ValidateCardName(card.Name, out var deps, out var fill))
            throw new ArgumentException($"Invalid card name string \"{card.Name}\"");

        var info = card.Card.Select(c => (c, c.IsFacet ? c.Facet : c.Blend.Id)).ToArray();
        if (info.DistinctBy(i => i.Item2).Count() != info.Length)
            throw new ArgumentException($"Non-unique facets specified for card name string \"{card.Name}\"");

        if (!deps.All(d => info.Any(i => i.Item2 == d)))
            throw new ArgumentException($"Not all variables filled for card name string \"{card.Name}\"");

        CardDescription.SubCardDescription[] stages =
            info.Select(i => i.c).ToArray();

        Axis[] stageLookup = stages
            .Select(s => s.IsFacet ?
                Axis.OfFacet(new(facetLookup(s.Facet))) :
                Axis.OfBlend(new(Blend(s.Blend, facetLookup))))
            .ToArray();
        string[] stageNames = stages.Select(s => s.IsFacet ? s.Facet : s.Blend.Id).ToArray();

        int[] mod = stageLookup.Select(st => st.Length).ToArray();
        int[] div = new int[mod.Length];
        div[mod.Length - 1] = 1;
        for (int i = mod.Length - 2; i >= 0; i--)
            div[i] = div[i + 1] * mod[i + 1];

        int potentials = mod.Aggregate((a, b) => a * b);
        int[] lastChosen = Enumerable.Repeat(-1, mod.Length).ToArray();
        int[] chosen = new int[mod.Length];
        Card[] working = new Card[mod.Length + 1];
        working[0] = new Card();

        Dictionary<string, List<Card>> cards = [];

        for (int candidate = 0; candidate < potentials; candidate++)
        {
            for (int i = 0; i < chosen.Length; i++)
                chosen[i] = candidate / div[i] % mod[i];
            for (int i = 0; i < chosen.Length; i++)
            {
                if (chosen[i] != lastChosen[i])
                {
                    for (int j = i; j < chosen.Length; j++)
                    {
                        BlendItem toMerge = stageLookup[j].IsFacet ?
                            stageLookup[j].Facet!.Array[chosen[j]] :
                            stageLookup[j].Blend!.Array[chosen[j]];
                        working[j + 1] = working[j].Merge(stageNames[j], toMerge);
                    }
                    break;
                }
            }

            var newCard = working[chosen.Length];
            (lastChosen, chosen) = (chosen, lastChosen);

            if (!newCard.Passes())
                continue;

            var newName = fill(newCard.Names);
            newCard.Name = newName;

            if (cards.TryGetValue(newName, out List<Card>? value))
                value.Add(newCard);
            else
                cards[newName] = [newCard];
        }

        return cards.Select(row => new Sheet(row.Key, row.Value)).ToList();
    }

    private static BlendCard[] Blend(BlendDescription blend, Func<string, FacetMemberDescription[]> facetLookup)
    {
        if (!ValidateCardName(blend.Name, out var deps, out var fill))
            throw new ArgumentException($"Invalid blend name string \"{blend.Name}\"");

        var stageNames = blend.Parts.Select(p => p.Id).ToArray();
        (string Name, Card Card)[][] partLookup = blend.Parts
            .Select(s => Cross(new(s.Name, s.Card), facetLookup).SelectMany(h => h.Cards.Select(x => (h.Name, x))).ToArray())
            .ToArray();

        int[] mod = partLookup.Select(st => st.Length).ToArray();
        int[] div = new int[mod.Length];
        div[mod.Length - 1] = 1;
        for (int i = mod.Length - 2; i >= 0; i--)
            div[i] = div[i + 1] * mod[i + 1];

        int potentials = mod.Aggregate((a, b) => a * b);
        int[] lastChosen = Enumerable.Repeat(-1, mod.Length).ToArray();
        int[] chosen = new int[mod.Length];
        BlendCard[] working = new BlendCard[mod.Length + 1];
        working[0] = new BlendCard()
        {
            Maps = blend.Parts.Select(p => p.Map).ToList(),
            Cards = new Card[blend.Parts.Length],
        };

        List<BlendCard> cards = [];

        for (int candidate = 0; candidate < potentials; candidate++)
        {
            for (int i = 0; i < chosen.Length; i++)
                chosen[i] = candidate / div[i] % mod[i];
            for (int i = 0; i < chosen.Length; i++)
            {
                if (chosen[i] != lastChosen[i])
                {
                    for (int j = i; j < chosen.Length; j++)
                    {
                        var toMerge = partLookup[j][chosen[j]];
                        var newItem = working[j].BClone();
                        newItem.Names[stageNames[ j]] = toMerge.Name;
                        newItem.Cards[j] = toMerge.Card;
                        working[j + 1] = newItem;
                    }
                    break;
                }
            }

            var newCard = working[chosen.Length];
            (chosen, lastChosen) = (lastChosen, chosen);

            if (blend.If is not null && !blend.If.Eval(newCard.Names.ToDictionary(k => k.Key, k => JsonSerializer.Deserialize<JsonElement>($"\"{k.Value}\""))))
                continue;
            newCard.Name = fill(newCard.Names);
            cards.Add(newCard);
        }

        return [.. cards];
    }

    private static bool ValidateCardName(
        string name,
        [NotNullWhen(true)] out List<string>? deps,
        [NotNullWhen(true)] out Func<Dictionary<string, string>, string>? fill)
    {
        deps = [];
        fill = null;
        var allParts = name.Split('$');
        if (allParts is not [var first, .. var parts])
        {
            fill = _ => name;
            return true;
        }

        var literals = new List<string>();

        foreach (var part in parts)
        {
            if (part is ['$', ..])
            {
                literals[^1] += part;
                continue;
            }
            if (part is not ['{', .. var rest])
                return false;
            var close = rest.IndexOf('}');
            if (close is 0 or -1)
                return false;
            deps.Add(rest[..close]);
            literals.Add(rest[(close + 1)..]);
        }

        Debug.Assert(deps.Count == literals.Count);
        var fills = deps.ToArray();
        fill = dict =>
        {
            StringBuilder sb = new(first);
            for (int i = 0; i < fills.Length; i++)
                sb.Append(dict[fills[i]]).Append(literals[i]);
            return sb.ToString();
        };
        return true;
    }
}

[Choice.Facet<FMDA>.Blend<BCA>]
internal readonly partial record struct Axis
{
    public int Length => IsFacet ? Facet.Array.Length : Blend.Array.Length;

    internal readonly record struct FMDA(FacetMemberDescription[] Array);
    internal readonly record struct BCA(BlendCard[] Array);
}