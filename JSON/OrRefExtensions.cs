using System.Text.Json;

namespace DeckMakerNeo.JSON;

internal static class OrRefExtensions
{
    public static OrRef<U> Fold<T, U>(this OrRef<T> self, Func<T, U> selector) =>
        self.IsReference ? (OrRef<U>)self.Reference : (OrRef<U>)selector(self.Concrete);

    public static OrRef<T> Deref<T>(this OrRef<T> self, Dictionary<string, JsonElement> variables) =>
        self.IsConcrete ? self : OrRef<T>.OfConcrete(variables[self.Reference].Deserialize<T>());
}
