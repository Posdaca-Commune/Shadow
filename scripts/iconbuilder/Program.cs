using System.Buffers.Binary;
using SkiaSharp;
using Svg.Skia;

namespace IconBuilder;

// Renders the Shadow branding SVG into a PNG icon set for Linux (.png) and
// macOS (.icns). Uses Svg.Skia for rasterization and writes the icns container
// by hand so we don't depend on Apple-only `iconutil`.
internal static class Program
{
    private static int Main(string[] args)
    {
        if (args.Length < 3)
        {
            Console.Error.WriteLine("Usage: IconBuilder <input.svg> <output-png-512> <output-icns>");
            return 1;
        }

        var svgPath = args[0];
        var pngPath = args[1];
        var icnsPath = args[2];

        if (!File.Exists(svgPath))
        {
            Console.Error.WriteLine($"SVG not found: {svgPath}");
            return 1;
        }

        using var svg = new SKSvg();
        svg.Load(svgPath);
        if (svg.Picture is null)
        {
            Console.Error.WriteLine($"Failed to load SVG: {svgPath}");
            return 1;
        }

        var picture = svg.Picture;

        // Linux: a single 512px png for the hicolor theme.
        Directory.CreateDirectory(Path.GetDirectoryName(pngPath)!);
        RenderPng(picture, 512, pngPath);
        Console.WriteLine($"Wrote {pngPath}");

        // macOS: .icns needs the full ladder of sizes so the OS can pick the
        // right bitmap for Retina, dock, Finder, etc.
        var sizes = new[] { 16, 32, 64, 128, 256, 512, 1024 };
        var entries = new List<(byte[] Type, byte[] Png)>();

        foreach (var size in sizes)
        {
            var png = RenderPngBytes(picture, size);
            // icns OSType codes: png = single-resolution, png is fine for all
            // sizes; the Retina pair (1024 at @2x) reuses the png code too.
            entries.Add((new byte[] { (byte)'p', (byte)'n', (byte)'g', (byte)' ' }, png));
        }

        Directory.CreateDirectory(Path.GetDirectoryName(icnsPath)!);
        WriteIcns(icnsPath, entries);
        Console.WriteLine($"Wrote {icnsPath}");

        return 0;
    }

    private static void RenderPng(SKPicture picture, int size, string path)
    {
        using var bitmap = RenderBitmap(picture, size);
        using var fs = File.Create(path);
        bitmap.Encode(fs, SKEncodedImageFormat.Png, 100);
    }

    private static byte[] RenderPngBytes(SKPicture picture, int size)
    {
        using var bitmap = RenderBitmap(picture, size);
        using var ms = new MemoryStream();
        bitmap.Encode(ms, SKEncodedImageFormat.Png, 100);
        return ms.ToArray();
    }

    private static SKBitmap RenderBitmap(SKPicture picture, int size)
    {
        var info = new SKImageInfo(size, size, SKColorType.Bgra8888, SKAlphaType.Premul);
        var bitmap = new SKBitmap(info);
        using (var canvas = new SKCanvas(bitmap))
        {
            canvas.Clear(SKColors.Transparent);

            // The SVG viewBox is 414x412; scale uniformly to fit `size`.
            var srcW = picture.CullRect.Width;
            var srcH = picture.CullRect.Height;
            var scale = Math.Min(size / srcW, size / srcH);
            var dx = (size - srcW * scale) / 2f;
            var dy = (size - srcH * scale) / 2f;

            canvas.Translate(dx, dy);
            canvas.Scale(scale, scale);
            canvas.DrawPicture(picture);
        }
        return bitmap;
    }

    // .icns format: 4-byte magic "icns", 4-byte total length (big-endian),
    // then a sequence of members each with a 4-byte OSType + 4-byte length.
    private static void WriteIcns(string path, List<(byte[] Type, byte[] Png)> entries)
    {
        using var fs = File.Create(path);
        using var bw = new BinaryWriter(fs);

        bw.Write(new byte[] { (byte)'i', (byte)'c', (byte)'n', (byte)'s' });

        // Total length = 8 (header) + sum(each entry: 8 + png bytes)
        int total = 8;
        foreach (var entry in entries)
        {
            total += 8 + entry.Png.Length;
        }

        Span<byte> lenBuf = stackalloc byte[4];
        BinaryPrimitives.WriteUInt32BigEndian(lenBuf, (uint)total);
        bw.Write(lenBuf);

        foreach (var entry in entries)
        {
            bw.Write(entry.Type);
            Span<byte> entryLen = stackalloc byte[4];
            BinaryPrimitives.WriteUInt32BigEndian(entryLen, (uint)(8 + entry.Png.Length));
            bw.Write(entryLen);
            bw.Write(entry.Png);
        }
    }
}
