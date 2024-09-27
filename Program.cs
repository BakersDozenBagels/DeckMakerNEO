using DeckMakerNeo;
using DeckMakerNeo.Crossing;
using DeckMakerNeo.Drawing;
using DeckMakerNeo.JSON;
using System.Diagnostics;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Text.Json.Serialization;

internal class Program
{
    [ThreadStatic]
    private static Dictionary<string, Bitmap>? _imageCache;

    private static readonly JsonSerializerOptions _options = new()
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

    private static void Main(string[] args)
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            Console.WriteLine("Only windows is supported.");
            return;
        }

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

        var data = JsonSerializer.Deserialize<Config>(File.ReadAllText(args[0]), _options);

        HashSet<string> files = [];
        HashSet<string> missing = [];
        bool cachedExists(string file)
        {
            if (files.Contains(file))
                return true;
            if (missing.Contains(file))
                return false;
            if (File.Exists(file))
            {
                files.Add(file);
                return true;
            }
            missing.Add(file);
            return false;
        }

        foreach (var item in data!.Cards)
            if (!cachedExists(item.Hidden))
                throw new ArgumentException($"File '{item.Hidden}' not found");

        Console.WriteLine("Determining cards to generate...");

        var decks = Crosser.Cross(data!);

        Console.WriteLine($"Generating {decks.Sum(d => d.Sheets.Count)} sheets and {decks.Sum(d => d.Sheets.Sum((Sheet s) => s.Cards.Count))} total cards.");
        Console.WriteLine("Validating cards...");

        HashSet<string> overwrite = [];

        foreach (var deck in decks)
        {
            foreach (var sheet in deck.Sheets)
            {
                foreach (var card in sheet.Cards)
                    card.Fill(cachedExists);
                foreach (var ss in sheet.Split())
                    if (cachedExists(ss.Name))
                        overwrite.Add(ss.Name);
            }
        }

        Console.WriteLine("All cards are valid.");

        if (overwrite.Count > 0)
            Console.WriteLine($"{overwrite.Count} files will be overwritten.");

        Bitmap getImage(string path)
        {
            if (_imageCache.TryGetValue(path, out var bitmap))
                return bitmap;
            return _imageCache[path] = new(Image.FromFile(path));
        }

        foreach (var deck in decks)
        {
            Console.WriteLine($"Deck \"{deck.Name}\" has {deck.Sheets.Count} sheets ({deck.Sheets.Sum(s => s.Cards.Count)} total cards)");
            foreach (var sheet in deck.Sheets)
            {
                ThreadPool.QueueUserWorkItem(_ => {
                    _imageCache ??= [];
                    Console.WriteLine($"Sheet \"{sheet.Name}\" has {sheet.Cards.Count} cards.");
                    DrawingUtil.DoSheet(sheet, deck.Hidden, getImage);
                });
            }
        }

        decks = null; // Allow GC to release card descriptions

        int ix = 14;
        while (ThreadPool.PendingWorkItemCount > 0)
        {
            ix++;
            if (ix >= 15)
            {
                ix = 0;
                Console.WriteLine($"{ThreadPool.ThreadCount} sheets generating, {ThreadPool.PendingWorkItemCount} queued");
            }
            Thread.Sleep(1000);
        }
        while (ThreadPool.ThreadCount > 0)
        {
            ix++;
            if (ix >= 50)
            {
                ix = 0;
                Console.WriteLine($"{ThreadPool.ThreadCount} sheets generating, {ThreadPool.PendingWorkItemCount} queued");
            }
            Thread.Sleep(100);
        }

        Console.WriteLine("Generation complete.");
    }

    private static void DisplayHelp()
    {
        Console.WriteLine("Please supply the path of the card spec.");
        Console.WriteLine();
        Console.WriteLine("Library credits:");
        Console.WriteLine("Emik.SourceGenerators.Choices (MPL-2.0) https://github.com/Emik03/Emik.SourceGenerators.Choices");
    }
}