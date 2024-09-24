namespace DeckMakerNeo.JSON;

internal static class OrRefExtensions
{
    public static OrRef<U> Fold<T, U>(this OrRef<T> self, Func<T, U> selector) =>
        self.IsReference ? (OrRef<U>)self.Reference : (OrRef<U>)selector(self.Concrete);
}
