namespace DeckMakerNeo.Crossing;

public record class Sheet(string Name, List<Card> Cards);
public record class Deck(string Name, string Hidden, List<Sheet> Sheets);