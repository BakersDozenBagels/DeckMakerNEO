using DeckMakerNeo.Crossing;
using DeckMakerNeo.JSON;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;

namespace DeckMakerNeo.Drawing;

public static class DrawingUtil
{
    public static void DoSheet(Sheet s, string hidden, Func<string, Bitmap> getImage)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(s.Name)!);
        var subsheets = s.Split();
        foreach (var subsheet in subsheets)
            DoSubsheet(subsheet, hidden, getImage);
    }

    private static void DoSubsheet(SizedSheet s, string hidden, Func<string, Bitmap> getImage)
    {
        using Bitmap bmp = new(s.Dimension.Width * 350, s.Dimension.Height * 500);

        using var gfx = Graphics.FromImage(bmp);

        for (int y = 0; y < s.Dimension.Height; y++)
            for (int x = 0; x < s.Dimension.Width; x++)
                if (y * s.Dimension.Width + x is var ix && ix < s.Cards.Count)
                    DoCard(gfx, s.Cards[ix], x, y, getImage);

        gfx.DrawImage(getImage(hidden), (s.Dimension.Width - 1) * 350, (s.Dimension.Height - 1) * 500, 350, 500);

        bmp.Save(s.Name);
    }

    private static void DoCard(Graphics gfx, Card card, int x, int y, Func<string, Bitmap> getImage)
    {
        if (card is BlendCard bl)
        {
            DoBlend(gfx, bl, x, y, getImage);
            return;
        }

        gfx.FillRectangle(card.Background!.Concrete!.Brush(), x * 350, y * 500, 350, 500);
        Bitmap recolor = new(350, 500, PixelFormat.Format32bppArgb);

        using (var rgfx = Graphics.FromImage(recolor))
        {
            rgfx.FillRectangle(new SolidBrush(Color.FromArgb(0, 0, 0, 0)), new(0, 0, 350, 500));
            foreach (var im in card.Images)
                DoImage(gfx, rgfx, im.Concrete!, x, y, getImage);
        }

        var fg = card.Foreground!.Concrete!.Brush();
        Bitmap recolorBase = new(350, 500, PixelFormat.Format32bppArgb);
        using (var g = Graphics.FromImage(recolorBase))
            g.FillRectangle(fg, new(0, 0, 350, 500));

        CopyAlpha(recolor, recolorBase);
        recolor.Dispose();

        gfx.DrawImage(recolorBase, new Rectangle(x * 350, y * 500, 350, 500));
        recolorBase.Dispose();
    }

    private static void DoImage(Graphics gfx, Graphics rgfx, ImageDescription desc, int x, int y, Func<string, Bitmap> getImage)
    {
        int w = desc.Width(getImage),
            h = desc.Height(getImage),
            hw = w / 2,
            hh = h / 2,
            cx = (desc.Recolor!.Concrete! ? 0 : x * 350) + desc.Cx.Concrete!,
            cy = (desc.Recolor.Concrete! ? 0 : y * 500) + desc.Cy.Concrete!;
        double c = Math.Cos(Math.PI / 180 * desc.Rotation!.Concrete!),
            s = Math.Sin(Math.PI / 180 * desc.Rotation.Concrete!),
            chw = c * hw,
            chh = c * hh,
            shw = s * hw,
            shh = s * hh;

        Point[] dest = [
            new((int)(cx - chw + shh), (int)(cy - chh - shw)),
            new((int)(cx + chw - shh), (int)(cy - chh + shw)),
            new((int)(cx - chw + shh), (int)(cy + chh - shw))
        ];

        (desc.Recolor.Concrete! ? rgfx : gfx).DrawImage(getImage(desc.Image.Concrete!), dest);
    }

    private static void CopyAlpha(Bitmap from, Bitmap to)
    {
        var f = from.LockBits(new(0, 0, 350, 500), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
        var t = to.LockBits(new(0, 0, 350, 500), ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb);
        var alphaByte = BitConverter.IsLittleEndian ? 3 : 0;
        unsafe
        {
            for (int y = 0; y < 500; y++)
            {
                byte* ptrFrom = (byte*)f.Scan0 + y * f.Stride;
                byte* ptrTo = (byte*)t.Scan0 + y * f.Stride;
                for (int x = alphaByte; x < 1400; x += 4)
                    ptrTo[x] = ptrFrom[x];
            }
        }
        from.UnlockBits(f);
        to.UnlockBits(t);
    }

    private static void DoBlend(Graphics gfx, BlendCard card, int x, int y, Func<string, Bitmap> getImage)
    {
        gfx.FillRectangle(new SolidBrush(Color.FromArgb(255, 255, 255, 255)), new Rectangle(x * 350, y * 500, 350, 500));
        for (int i = 0; i < card.Cards.Length; i++)
        {
            var c = card.Cards[i];
            using Bitmap sub = new(350, 500, PixelFormat.Format32bppArgb);
            using Graphics sgfx = Graphics.FromImage(sub);
            DoCard(sgfx, c, 0, 0, getImage);
            var map = getImage(card.Maps[i]);
            CopyAlpha(map, sub);
            gfx.DrawImage(sub, new Rectangle(x * 350, y * 500, 350, 500));
        }
    }

    private static int Width(this ImageDescription desc, Func<string, Bitmap> getImage)
    {
        return (desc.Width, desc.Height) switch
        {
            (null, null) => getImage(desc.Image.Concrete!).Width,
            (null, var h) => (int)((h.Concrete! / (float)getImage(desc.Image.Concrete!).Height) * getImage(desc.Image.Concrete!).Width),
            (var w, _) => w.Concrete!
        };
    }

    private static int Height(this ImageDescription desc, Func<string, Bitmap> getImage)
    {
        return (desc.Width, desc.Height) switch
        {
            (null, null) => getImage(desc.Image.Concrete!).Width,
            (var w, null) => (int)((w.Concrete! / (float)getImage(desc.Image.Concrete!).Width) * getImage(desc.Image.Concrete!).Height),
            (_, var h) => h.Concrete!
        };
    }

    private static Brush Brush(this ColorDescription col)
    {
        if (col.IsRGB)
            return new SolidBrush(Color.FromArgb(255, col.RGB.R.Concrete!, col.RGB.G.Concrete!, col.RGB.B.Concrete!));
        if (col.IsRGBA)
            return new SolidBrush(Color.FromArgb(col.RGBA.A.Concrete!, col.RGBA.R.Concrete!, col.RGBA.G.Concrete!, col.RGBA.B.Concrete!));

        if (col.IsAxial || col.IsLinear || col.IsRadial)
            throw new NotImplementedException();

        throw new UnreachableException();
    }

    public static List<SizedSheet> Split(this Sheet s)
    {
        const int MaxW = 10, MaxH = 7, MaxSheet = MaxW * MaxH - 1;
        if (s.Cards.Count <= MaxSheet)
            return [new(s.Name, s.Cards, SizeOne(s.Cards.Count))];
        List<SizedSheet> ret = [];
        for (int i = 0, j = 1; i < s.Cards.Count; i += MaxSheet, j++)
        {
            if (s.Cards.Count - i <= MaxSheet)
                ret.Add(new(s.Name + j, s.Cards.Slice(i, MaxSheet), SizeOne(MaxSheet)));
            else
                ret.Add(new(s.Name + j, s.Cards.Slice(i, s.Cards.Count - i), SizeOne(s.Cards.Count - i)));
        }
        return ret;
    }

    private static (int Width, int Height) SizeOne(int count)
    {
        return count switch
        {
            1 => (2, 1),
            2 or 3 => (2, 2),
            4 or 5 => (3, 2),
            6 or 7 or 8 => (3, 3),
            9 or 10 or 11 => (4, 3),
            >= 12 and <= 15 => (4, 4),
            >= 16 and <= 19 => (5, 4),
            20 => (7, 3), // For compatibility
            >= 21 and <= 24 => (5, 5),
            >= 25 and <= 29 => (6, 5),
            >= 30 and <= 35 => (6, 6),
            >= 36 and <= 41 => (7, 6),
            >= 42 and <= 48 => (7, 7),
            >= 49 and <= 55 => (8, 7),
            >= 56 and <= 62 => (9, 7),
            >= 63 and <= 69 => (10, 7),
            _ => throw new UnreachableException()
        };
    }

    public record class SizedSheet(string Name, List<Card> Cards, (int Width, int Height) Dimension) : Sheet(Name, Cards);

    public static void GenerateTRBL()
    {
        using var tr = new Bitmap(350, 500, PixelFormat.Format32bppArgb);
        using var bl = new Bitmap(350, 500, PixelFormat.Format32bppArgb);
        for (int y = 0; y < 500; y++)
        {
            for (int x = 0; x < 350; x++)
            {
                const float width = 10;
                var value = (int)Math.Clamp((440f / 350f * x + 30f - y) * 128f / width + 128f, 0f, 255f);
                //value = (int)(Math.Pow(value / 255f, .5f) * 255f);
                //var value2 = (int)(Math.Pow(1f - value / 255f, .5f) * 255f);
                var value2 = 255 - value;
                tr.SetPixel(x, y, Color.FromArgb(value, 255, 255, 255));
                bl.SetPixel(x, y, Color.FromArgb(value2, 0, 0, 0));
            }
        }
        tr.Save("./img/misc/tr.png");
        bl.Save("./img/misc/bl.png");
    }
}
