using DeckMakerNeo.JSON;
using System.Text.Json;
using System.Text.Json.Serialization;

internal class Program
{
    private static void Main(string[] args)
    {
        if (args.Length != 1)
        {
            DisplayHelp();
            return;
        }

        if (!File.Exists(args[0]))
        {
            Console.WriteLine("Error: That file doesn't exist.");
            Console.ReadKey();
            return;
        }

        var options = new JsonSerializerOptions()
        {
            PropertyNameCaseInsensitive = true,
            AllowTrailingCommas = true,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingDefault,
            NumberHandling = JsonNumberHandling.Strict,
            ReadCommentHandling = JsonCommentHandling.Skip,
            UnknownTypeHandling = JsonUnknownTypeHandling.JsonElement,
            UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
        };

        var data = JsonSerializer.Deserialize<Config>(File.ReadAllText(args[0]), options);

        Console.WriteLine(JsonSerializer.Serialize(data, options));

        Console.ReadKey();
    }

    private static void DisplayHelp()
    {
        Console.WriteLine("Please supply the path of the card spec.");
        Console.WriteLine();
        Console.WriteLine("Library credits:");
        Console.WriteLine("Emik.SourceGenerators.Choices (MPL-2.0) https://github.com/Emik03/Emik.SourceGenerators.Choices");
    }
}