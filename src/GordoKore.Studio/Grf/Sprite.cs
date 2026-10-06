using System.Drawing;

namespace GordoKore.Studio.Grf;

/// <summary>
/// Primeiro quadro da acao 0 de um SPR/ACT (o que a UINpcSprWnd do cliente mostra). Sem rotacao nem cor
/// das camadas; espelhamento e escala sim.
/// </summary>
public static class Sprite
{
    public const string NpcDir = "data\\sprite\\npc\\";
    public const string MonsterDir = "data\\sprite\\몬스터\\";

    /// <summary>Procura em npc\ e depois em 몬스터\, como o cliente.</summary>
    public static Bitmap? Load(ResourceStore store, string name)
    {
        foreach (string dir in new[] { NpcDir, MonsterDir })
        {
            byte[]? spr = store.Read(dir + name + ".spr");
            byte[]? act = store.Read(dir + name + ".act");
            if (spr != null && act != null)
                return Render(ReadSpr(spr), act);
        }
        return null;
    }

    private sealed record Image(int Width, int Height, int[] Pixels);

    private static (List<Image> indexed, List<Image> rgba) ReadSpr(byte[] d)
    {
        int version = d[3] << 8 | d[2];
        int indexedCount = BitConverter.ToUInt16(d, 4);
        int rgbaCount = version >= 0x200 ? BitConverter.ToUInt16(d, 6) : 0;
        int o = version >= 0x200 ? 8 : 6;

        var palette = new int[256];
        for (int i = 1; i < 256; i++) // indice 0 = transparente
        {
            int p = d.Length - 1024 + i * 4;
            palette[i] = unchecked((int)0xFF000000) | d[p] << 16 | d[p + 1] << 8 | d[p + 2];
        }

        var indexed = new List<Image>();
        for (int n = 0; n < indexedCount; n++)
        {
            int w = BitConverter.ToUInt16(d, o), h = BitConverter.ToUInt16(d, o + 2);
            o += 4;
            var pixels = new int[w * h];
            if (version >= 0x201) // RLE: 0 seguido da quantidade de zeros
            {
                int size = BitConverter.ToUInt16(d, o);
                o += 2;
                int end = o + size, at = 0;
                while (o < end && at < pixels.Length)
                {
                    byte c = d[o++];
                    if (c == 0)
                        at += d[o++];
                    else
                        pixels[at++] = palette[c];
                }
                o = end;
            }
            else
            {
                for (int i = 0; i < pixels.Length; i++)
                    pixels[i] = palette[d[o + i]];
                o += pixels.Length;
            }
            indexed.Add(new Image(w, h, pixels));
        }

        var rgba = new List<Image>();
        for (int n = 0; n < rgbaCount; n++)
        {
            int w = BitConverter.ToUInt16(d, o), h = BitConverter.ToUInt16(d, o + 2);
            o += 4;
            var pixels = new int[w * h];
            for (int y = 0; y < h; y++) // ABGR, de baixo para cima
                for (int x = 0; x < w; x++, o += 4)
                    pixels[(h - 1 - y) * w + x] = d[o] << 24 | d[o + 3] << 16 | d[o + 2] << 8 | d[o + 1];
            rgba.Add(new Image(w, h, pixels));
        }
        return (indexed, rgba);
    }

    private sealed record Layer(int X, int Y, int Index, bool Mirror, float ScaleX, float ScaleY, int Type);

    // Camadas do 1o quadro da acao 0
    private static List<Layer> ReadFirstFrame(byte[] d)
    {
        int version = d[3] << 8 | d[2];
        int actions = BitConverter.ToUInt16(d, 4);
        int o = 16;
        var layers = new List<Layer>();
        if (actions == 0 || BitConverter.ToInt32(d, o) == 0)
            return layers;
        o += 4 + 32; // quantidade de quadros, retangulos do 1o quadro
        int count = BitConverter.ToInt32(d, o);
        o += 4;
        for (int i = 0; i < count; i++)
        {
            int x = BitConverter.ToInt32(d, o), y = BitConverter.ToInt32(d, o + 4), index = BitConverter.ToInt32(d, o + 8);
            bool mirror = BitConverter.ToInt32(d, o + 12) != 0;
            o += 16;
            float scaleX = 1, scaleY = 1;
            int type = 0;
            if (version >= 0x200)
            {
                o += 4; // cor
                scaleX = BitConverter.ToSingle(d, o);
                o += 4;
                scaleY = scaleX;
                if (version >= 0x204)
                {
                    scaleY = BitConverter.ToSingle(d, o);
                    o += 4;
                }
                o += 4; // rotacao
                type = BitConverter.ToInt32(d, o);
                o += 4;
                if (version >= 0x205)
                    o += 8; // largura, altura
            }
            layers.Add(new Layer(x, y, index, mirror, scaleX, scaleY, type));
        }
        return layers;
    }

    private static Bitmap? Render((List<Image> indexed, List<Image> rgba) spr, byte[] act)
    {
        var placed = new List<(Image image, Layer layer, RectangleF box)>();
        foreach (var layer in ReadFirstFrame(act))
        {
            var images = layer.Type == 1 ? spr.rgba : spr.indexed;
            if (layer.Index < 0 || layer.Index >= images.Count)
                continue;
            var image = images[layer.Index];
            float w = image.Width * Math.Abs(layer.ScaleX), h = image.Height * Math.Abs(layer.ScaleY);
            placed.Add((image, layer, new RectangleF(layer.X - w / 2, layer.Y - h / 2, w, h)));
        }
        if (placed.Count == 0)
            return null;

        var bounds = placed.Select(p => p.box).Aggregate(RectangleF.Union);
        var bitmap = new Bitmap(Math.Max(1, (int)Math.Ceiling(bounds.Width)), Math.Max(1, (int)Math.Ceiling(bounds.Height)));
        using var g = Graphics.FromImage(bitmap);
        g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.NearestNeighbor;
        g.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.Half;
        foreach (var (image, layer, box) in placed)
        {
            using var source = Images.FromPixels(image.Width, image.Height, image.Pixels);
            if (layer.Mirror)
                source.RotateFlip(RotateFlipType.RotateNoneFlipX);
            g.DrawImage(source, box.X - bounds.X, box.Y - bounds.Y, box.Width, box.Height);
        }
        return bitmap;
    }
}
