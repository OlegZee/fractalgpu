namespace FractalGpu.RenderCli.Media;

public struct RawColor
{
    public readonly byte R, G, B;

    public RawColor(byte r, byte g, byte b)
    {
        (R, G, B) = (r, g, b);
    }

    public static RawColor Black => new RawColor(0, 0, 0);
    public static RawColor White => new RawColor(255, 255, 255);

    public static RawColor FromArgb(byte a, byte r, byte g, byte b)
    {
        return new RawColor(r, g, b);
    }

    public static RawColor Random(Random rand)
    {
        var r = (byte)rand.Next(256);
        var g = (byte)rand.Next(256);
        var b = (byte)rand.Next(256);
        return new RawColor(r, g, b);
    }

    public static RawColor Gray(byte value)
    {
        return new RawColor(value, value, value);
    }
}

public class RawBitmap
{
    private const int BytesPerPixel = 3; // 3 and 4 are supported
    public readonly int Width;
    public readonly int Height;
    private readonly byte[] ImageBytes;

    public RawBitmap(int width, int height)
    {
        Width = width;
        Height = height;
        ImageBytes = new byte[width * height * BytesPerPixel];
    }

    public void SetPixel(int x, int y, RawColor color)
    {
        int offset = ((Height - y - 1) * Width + x) * BytesPerPixel;
        ImageBytes[offset + 0] = color.B;
        ImageBytes[offset + 1] = color.G;
        ImageBytes[offset + 2] = color.R;
    }

    // public byte[] GetBitmapBytes()
    // {
    //     const int imageHeaderSize = 54;
    //     byte[] bmpBytes = new byte[ImageBytes.Length + imageHeaderSize];
    //     bmpBytes[0] = (byte)'B';
    //     bmpBytes[1] = (byte)'M';
    //     bmpBytes[14] = 40;
    //     Array.Copy(BitConverter.GetBytes(bmpBytes.Length), 0, bmpBytes, 2, 4);
    //     Array.Copy(BitConverter.GetBytes(imageHeaderSize), 0, bmpBytes, 10, 4);
    //     Array.Copy(BitConverter.GetBytes(Width), 0, bmpBytes, 18, 4);
    //     Array.Copy(BitConverter.GetBytes(Height), 0, bmpBytes, 22, 4);
    //     Array.Copy(BitConverter.GetBytes(32), 0, bmpBytes, 28, 2);
    //     Array.Copy(BitConverter.GetBytes(ImageBytes.Length), 0, bmpBytes, 34, 4);
    //     Array.Copy(ImageBytes, 0, bmpBytes, imageHeaderSize, ImageBytes.Length);
    //     return bmpBytes;
    // }
    //
    // public void Save1(string filename)
    // {
    //     byte[] bytes = GetBitmapBytes();
    //     File.WriteAllBytes(filename, bytes);
    // }
    
    
    public void Save(string fileName)
    {
        int width = Width, height = Height;

        using var fs = new FileStream(fileName, FileMode.Create);
        using var writer = new BinaryWriter(fs);
        // Write BMP header
        writer.Write((ushort)0x4D42);            // Signature 'BM'
        writer.Write(54 + ImageBytes.Length);      // File size
        writer.Write((ushort)0);                 // Reserved1
        writer.Write((ushort)0);                 // Reserved2
        writer.Write(54);                         // Offset to pixel array
        writer.Write(40);                         // DIB header size
        writer.Write((int)width);                      // Image width
        writer.Write((int)height);                     // Image height
        writer.Write((ushort)1);                  // Number of color planes
        writer.Write((ushort)(8 * BytesPerPixel));                 // Bits per pixel (24-bit color)
        writer.Write(0);                          // Compression method (0 for BI_RGB)
        writer.Write(ImageBytes.Length);           // Image size (including padding)
        writer.Write(2835);                       // Horizontal resolution (pixels per meter)
        writer.Write(2835);                       // Vertical resolution (pixels per meter)
        writer.Write(0);                          // Number of colors in the color palette (0 for BI_RGB)
        writer.Write(0);                          // Number of important colors used (0 for BI_RGB)

        // Write pixel data
        writer.Write(ImageBytes);
    }
}