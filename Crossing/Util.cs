using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace DeckMakerNeo.Crossing;

internal static class Util
{
    public static Dictionary<T, U> Append<T, U>(this Dictionary<T, U> dic, T key, U val) where T : notnull
    {
        dic[key] = val;
        return dic;
    }

    public static int SplitCount(this Sheet sheet) => (int)Math.Ceiling(sheet.Cards.Count / 69f);
}
