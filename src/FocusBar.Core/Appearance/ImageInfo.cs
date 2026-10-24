namespace FocusBar.Core.Appearance;

public enum ImageFormat { Png, Jpeg, Gif }

/// <summary>
/// Reads an image's format and size from its first bytes, without decoding it. Used to refuse files that aren't
/// really PNG, JPG, or GIF (whatever their extension says) and images too big to load safely.
/// </summary>
public readonly record struct ImageInfo(ImageFormat Format, int Width, int Height)
{
    public string Extension => Format switch
    {
        ImageFormat.Png => ".png",
        ImageFormat.Jpeg => ".jpg",
        _ => ".gif",
    };

    public static ImageInfo? TryRead(Stream stream)
    {
        try
        {
            var reader = new BinaryReader(stream);
            var start = reader.ReadBytes(10);
            if (start.Length < 10) return null;

            if (start[0] == 0x89 && start[1] == (byte)'P' && start[2] == (byte)'N' && start[3] == (byte)'G') return ReadPng(reader, start);
            if (start[0] == (byte)'G' && start[1] == (byte)'I' && start[2] == (byte)'F' && start[3] == (byte)'8') return ReadGif(start);
            if (start[0] == 0xFF && start[1] == 0xD8 && start[2] == 0xFF) return ReadJpeg(stream, reader);
            return null;
        }
        catch (Exception ex) when (ex is EndOfStreamException or IOException)
        {
            return null;
        }
    }

    // PNG: 8-byte signature, then the IHDR chunk: length (4), "IHDR" (4), width (4, big-endian), height (4).
    private static ImageInfo? ReadPng(BinaryReader reader, byte[] start)
    {
        var rest = reader.ReadBytes(14);
        if (rest.Length < 14) return null;
        var header = start.Concat(rest).ToArray();
        if (header[12] != (byte)'I' || header[13] != (byte)'H' || header[14] != (byte)'D' || header[15] != (byte)'R') return null;
        return Valid(ImageFormat.Png, BigEndian32(header, 16), BigEndian32(header, 20));
    }

    // GIF: "GIF87a"/"GIF89a", then the logical screen width and height (2 bytes each, little-endian).
    private static ImageInfo? ReadGif(byte[] start)
    {
        if (start[4] is not ((byte)'7' or (byte)'9') || start[5] != (byte)'a') return null;
        return Valid(ImageFormat.Gif, start[6] | (start[7] << 8), start[8] | (start[9] << 8));
    }

    // JPEG: a list of segments (FF xx, then a 2-byte big-endian length). The size is in the first
    // start-of-frame segment (SOF0–SOF15 except DHT C4, JPG C8, and DAC CC).
    private static ImageInfo? ReadJpeg(Stream stream, BinaryReader reader)
    {
        stream.Position = 2;
        for (var segments = 0; segments < 1000; segments++)
        {
            var b = reader.ReadByte();
            if (b != 0xFF) return null;
            var marker = reader.ReadByte();
            while (marker == 0xFF) marker = reader.ReadByte(); // fill bytes
            if (marker is 0xD8 or 0x01 || (marker >= 0xD0 && marker <= 0xD7)) continue; // markers without a length
            if (marker is 0xD9 or 0xDA) return null; // end of image or start of scan before any frame header

            var length = (reader.ReadByte() << 8) | reader.ReadByte();
            if (length < 2) return null;

            if (marker >= 0xC0 && marker <= 0xCF && marker is not (0xC4 or 0xC8 or 0xCC))
            {
                var frame = reader.ReadBytes(5); // precision, height (2), width (2)
                if (frame.Length < 5) return null;
                return Valid(ImageFormat.Jpeg, (frame[3] << 8) | frame[4], (frame[1] << 8) | frame[2]);
            }

            stream.Seek(length - 2, SeekOrigin.Current);
        }
        return null;
    }

    private static int BigEndian32(byte[] bytes, int offset) =>
        (bytes[offset] << 24) | (bytes[offset + 1] << 16) | (bytes[offset + 2] << 8) | bytes[offset + 3];

    private static ImageInfo? Valid(ImageFormat format, int width, int height) =>
        width > 0 && height > 0 ? new ImageInfo(format, width, height) : null;
}
