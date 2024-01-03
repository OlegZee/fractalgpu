namespace OlegZee.Fractal.Media;

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
        byte r = (byte)rand.Next(256);
        byte g = (byte)rand.Next(256);
        byte b = (byte)rand.Next(256);
        return new RawColor(r, g, b);
    }

    public static RawColor Gray(byte value)
    {
        return new RawColor(value, value, value);
    }
}

public class RawBitmap
{
    public readonly int Width;
    public readonly int Height;
    private readonly byte[] ImageBytes;

    public RawBitmap(int width, int height)
    {
        Width = width;
        Height = height;
        ImageBytes = new byte[width * height * 4];
    }

    public void SetPixel(int x, int y, RawColor color)
    {
        int offset = ((Height - y - 1) * Width + x) * 4;
        ImageBytes[offset + 0] = color.B;
        ImageBytes[offset + 1] = color.G;
        ImageBytes[offset + 2] = color.R;
    }

    public byte[] GetBitmapBytes()
    {
        const int imageHeaderSize = 54;
        byte[] bmpBytes = new byte[ImageBytes.Length + imageHeaderSize];
        bmpBytes[0] = (byte)'B';
        bmpBytes[1] = (byte)'M';
        bmpBytes[14] = 40;
        Array.Copy(BitConverter.GetBytes(bmpBytes.Length), 0, bmpBytes, 2, 4);
        Array.Copy(BitConverter.GetBytes(imageHeaderSize), 0, bmpBytes, 10, 4);
        Array.Copy(BitConverter.GetBytes(Width), 0, bmpBytes, 18, 4);
        Array.Copy(BitConverter.GetBytes(Height), 0, bmpBytes, 22, 4);
        Array.Copy(BitConverter.GetBytes(32), 0, bmpBytes, 28, 2);
        Array.Copy(BitConverter.GetBytes(ImageBytes.Length), 0, bmpBytes, 34, 4);
        Array.Copy(ImageBytes, 0, bmpBytes, imageHeaderSize, ImageBytes.Length);
        return bmpBytes;
    }

    public void Save(string filename)
    {
        byte[] bytes = GetBitmapBytes();
        File.WriteAllBytes(filename, bytes);
    }
}