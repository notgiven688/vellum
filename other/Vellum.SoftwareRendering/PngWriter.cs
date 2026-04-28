using System.Buffers.Binary;
using System.IO.Compression;
using System.Text;

namespace Vellum.SoftwareRendering;

internal static class PngWriter
{
    private static readonly byte[] s_signature = [137, 80, 78, 71, 13, 10, 26, 10];

    public static void WriteRgba(string path, byte[] rgba, int width, int height)
    {
        ArgumentNullException.ThrowIfNull(path);
        File.WriteAllBytes(path, EncodeRgba(rgba, width, height));
    }

    public static byte[] EncodeRgba(byte[] rgba, int width, int height)
    {
        if (width < 0) throw new ArgumentOutOfRangeException(nameof(width));
        if (height < 0) throw new ArgumentOutOfRangeException(nameof(height));
        if (rgba.Length != width * height * 4)
            throw new ArgumentException("PNG data must be tightly packed RGBA8.", nameof(rgba));

        using var output = new MemoryStream();
        output.Write(s_signature);

        Span<byte> ihdr = stackalloc byte[13];
        BinaryPrimitives.WriteInt32BigEndian(ihdr[..4], width);
        BinaryPrimitives.WriteInt32BigEndian(ihdr.Slice(4, 4), height);
        ihdr[8] = 8;  // bit depth
        ihdr[9] = 6;  // RGBA
        ihdr[10] = 0; // deflate
        ihdr[11] = 0; // adaptive filters
        ihdr[12] = 0; // no interlace
        WriteChunk(output, "IHDR", ihdr);

        byte[] scanlines = new byte[height * (width * 4 + 1)];
        int src = 0;
        int dst = 0;
        for (int y = 0; y < height; y++)
        {
            scanlines[dst++] = 0; // no filter
            Buffer.BlockCopy(rgba, src, scanlines, dst, width * 4);
            src += width * 4;
            dst += width * 4;
        }

        using var compressed = new MemoryStream();
        using (var zlib = new ZLibStream(compressed, CompressionLevel.SmallestSize, leaveOpen: true))
            zlib.Write(scanlines);
        WriteChunk(output, "IDAT", compressed.ToArray());

        WriteChunk(output, "IEND", ReadOnlySpan<byte>.Empty);
        return output.ToArray();
    }

    private static void WriteChunk(Stream output, string type, ReadOnlySpan<byte> data)
    {
        Span<byte> length = stackalloc byte[4];
        BinaryPrimitives.WriteInt32BigEndian(length, data.Length);
        output.Write(length);

        byte[] typeBytes = Encoding.ASCII.GetBytes(type);
        output.Write(typeBytes);
        output.Write(data);

        uint crc = Crc32(typeBytes, data);
        Span<byte> crcBytes = stackalloc byte[4];
        BinaryPrimitives.WriteUInt32BigEndian(crcBytes, crc);
        output.Write(crcBytes);
    }

    private static uint Crc32(ReadOnlySpan<byte> type, ReadOnlySpan<byte> data)
    {
        uint crc = 0xffffffffu;
        crc = UpdateCrc(crc, type);
        crc = UpdateCrc(crc, data);
        return crc ^ 0xffffffffu;
    }

    private static uint UpdateCrc(uint crc, ReadOnlySpan<byte> data)
    {
        foreach (byte b in data)
        {
            crc ^= b;
            for (int i = 0; i < 8; i++)
            {
                uint mask = 0u - (crc & 1u);
                crc = (crc >> 1) ^ (0xedb88320u & mask);
            }
        }

        return crc;
    }
}
