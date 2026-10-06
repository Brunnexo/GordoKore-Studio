using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;

namespace GordoKore.Studio.Grf;

/// <summary>BMP e TGA do jogo como Bitmap 32 bits; #FF00FF (rosa do cliente) vira transparente.</summary>
public static class Images
{
    public static Bitmap? Load(byte[]? data, string path)
    {
        if (data == null || data.Length < 18)
            return null;
        try
        {
            if (path.EndsWith(".tga", StringComparison.OrdinalIgnoreCase))
                return DecodeTga(data);
            using var source = new Bitmap(new MemoryStream(data));
            var bitmap = new Bitmap(source.Width, source.Height, PixelFormat.Format32bppArgb);
            using (var g = Graphics.FromImage(bitmap))
                g.DrawImage(source, 0, 0, source.Width, source.Height);
            Edit(bitmap, pixels =>
            {
                for (int i = 0; i < pixels.Length; i++)
                    if (IsMagenta(pixels[i]))
                        pixels[i] = 0;
            });
            return bitmap;
        }
        catch (Exception e) when (e is ArgumentException or InvalidDataException or IndexOutOfRangeException)
        {
            return null;
        }
    }

    // O cliente trata como rosa transparente qualquer tom muito perto do #FF00FF
    private static bool IsMagenta(int argb) => ((argb >> 16) & 0xFF) >= 0xF0 && ((argb >> 8) & 0xFF) <= 0x0F && (argb & 0xFF) >= 0xF0;

    /// <summary>Le e reescreve os pixels 0xAARRGGBB, de cima para baixo.</summary>
    public static void Edit(Bitmap bitmap, Action<int[]> edit)
    {
        var rect = new Rectangle(0, 0, bitmap.Width, bitmap.Height);
        var lockData = bitmap.LockBits(rect, ImageLockMode.ReadWrite, PixelFormat.Format32bppArgb);
        var pixels = new int[bitmap.Width * bitmap.Height];
        Marshal.Copy(lockData.Scan0, pixels, 0, pixels.Length);
        edit(pixels);
        Marshal.Copy(pixels, 0, lockData.Scan0, pixels.Length);
        bitmap.UnlockBits(lockData);
    }

    public static Bitmap FromPixels(int width, int height, int[] pixels)
    {
        var bitmap = new Bitmap(width, height, PixelFormat.Format32bppArgb);
        Edit(bitmap, target => Array.Copy(pixels, target, pixels.Length));
        return bitmap;
    }

    // TGA tipos 2 (cru) e 10 (RLE), 24 ou 32 bits
    private static Bitmap? DecodeTga(byte[] data)
    {
        int idLength = data[0], type = data[2], width = BitConverter.ToUInt16(data, 12), height = BitConverter.ToUInt16(data, 14);
        int bpp = data[16] / 8;
        bool topDown = (data[17] & 0x20) != 0;
        if ((type != 2 && type != 10) || (bpp != 3 && bpp != 4) || width == 0 || height == 0)
            return null;

        var pixels = new int[width * height];
        int o = 18 + idLength + (data[1] != 0 ? BitConverter.ToUInt16(data, 5) * (data[7] / 8) : 0);
        int Pixel(int at) => bpp == 4
            ? data[at + 3] << 24 | data[at + 2] << 16 | data[at + 1] << 8 | data[at]
            : unchecked((int)0xFF000000) | data[at + 2] << 16 | data[at + 1] << 8 | data[at];

        for (int n = 0; n < pixels.Length && o < data.Length;)
        {
            if (type == 2)
            {
                pixels[n++] = Pixel(o);
                o += bpp;
                continue;
            }
            int header = data[o++];
            int count = (header & 0x7F) + 1;
            if ((header & 0x80) != 0)
            {
                int value = Pixel(o);
                o += bpp;
                for (int i = 0; i < count && n < pixels.Length; i++)
                    pixels[n++] = value;
            }
            else
                for (int i = 0; i < count && n < pixels.Length; i++, o += bpp)
                    pixels[n++] = Pixel(o);
        }

        if (!topDown)
            for (int y = 0; y < height / 2; y++)
                for (int x = 0; x < width; x++)
                    (pixels[y * width + x], pixels[(height - 1 - y) * width + x]) = (pixels[(height - 1 - y) * width + x], pixels[y * width + x]);
        for (int i = 0; i < pixels.Length; i++)
            if (IsMagenta(pixels[i]))
                pixels[i] = 0;
        return FromPixels(width, height, pixels);
    }
}
