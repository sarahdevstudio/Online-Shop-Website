using System.Buffers.Binary;

namespace Shop.Application.Site;

/// <summary>What the bytes of an image really are: format and pixel size, read from the file header only.</summary>
public sealed record ImageInfo(string ContentType, string Extension, int Width, int Height);

/// <summary>
/// Detects PNG, JPEG and WebP by their magic bytes and reads the pixel size from the header (PNG IHDR, JPEG SOF,
/// WebP VP8 / VP8L / VP8X). Anything else (SVG, GIF, HTML, ...) is not an image for the site. The file name and the
/// client's content type are never consulted. No imaging library is involved, so nothing is decoded.
/// </summary>
public static class ImageProbe
{
    private static ReadOnlySpan<byte> PngSignature => [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];

    public static ImageInfo? Probe(ReadOnlySpan<byte> data)
    {
        var info = ProbePng(data) ?? ProbeJpeg(data) ?? ProbeWebp(data);
        return info is { Width: > 0, Height: > 0 } ? info : null;
    }

    private static ImageInfo? ProbePng(ReadOnlySpan<byte> data)
    {
        // signature (8) + chunk length (4) + "IHDR" (4) + width (4) + height (4)
        if (data.Length < 24 || !data[..8].SequenceEqual(PngSignature) || !data.Slice(12, 4).SequenceEqual("IHDR"u8))
        {
            return null;
        }

        var width = BinaryPrimitives.ReadUInt32BigEndian(data.Slice(16, 4));
        var height = BinaryPrimitives.ReadUInt32BigEndian(data.Slice(20, 4));
        return width is 0 or > int.MaxValue || height is 0 or > int.MaxValue
            ? null
            : new ImageInfo("image/png", ".png", (int)width, (int)height);
    }

    private static ImageInfo? ProbeJpeg(ReadOnlySpan<byte> data)
    {
        if (data.Length < 4 || data[0] != 0xFF || data[1] != 0xD8)
        {
            return null;
        }

        var i = 2;
        while (i + 4 <= data.Length)
        {
            if (data[i] != 0xFF)
            {
                return null;
            }

            var marker = data[i + 1];
            if (marker == 0xFF)
            {
                i++; // fill byte
                continue;
            }

            if (marker is 0x01 or (>= 0xD0 and <= 0xD9))
            {
                i += 2; // standalone markers carry no length
                continue;
            }

            var length = BinaryPrimitives.ReadUInt16BigEndian(data.Slice(i + 2, 2));
            var isStartOfFrame = marker is >= 0xC0 and <= 0xCF && marker is not (0xC4 or 0xC8 or 0xCC);
            if (isStartOfFrame)
            {
                if (i + 9 > data.Length)
                {
                    return null;
                }

                var height = BinaryPrimitives.ReadUInt16BigEndian(data.Slice(i + 5, 2));
                var width = BinaryPrimitives.ReadUInt16BigEndian(data.Slice(i + 7, 2));
                return new ImageInfo("image/jpeg", ".jpg", width, height);
            }

            if (marker == 0xDA)
            {
                return null; // start of scan without a frame header: not a valid JPEG
            }

            i += 2 + length;
        }

        return null;
    }

    private static ImageInfo? ProbeWebp(ReadOnlySpan<byte> data)
    {
        if (data.Length < 30 || !data[..4].SequenceEqual("RIFF"u8) || !data.Slice(8, 4).SequenceEqual("WEBP"u8))
        {
            return null;
        }

        var chunk = data.Slice(12, 4);
        int width;
        int height;
        if (chunk.SequenceEqual("VP8X"u8))
        {
            width = 1 + (data[24] | (data[25] << 8) | (data[26] << 16));
            height = 1 + (data[27] | (data[28] << 8) | (data[29] << 16));
        }
        else if (chunk.SequenceEqual("VP8L"u8))
        {
            if (data[20] != 0x2F)
            {
                return null;
            }

            var bits = BinaryPrimitives.ReadUInt32LittleEndian(data.Slice(21, 4));
            width = (int)(bits & 0x3FFF) + 1;
            height = (int)((bits >> 14) & 0x3FFF) + 1;
        }
        else if (chunk.SequenceEqual("VP8 "u8))
        {
            if (data[23] != 0x9D || data[24] != 0x01 || data[25] != 0x2A)
            {
                return null;
            }

            width = BinaryPrimitives.ReadUInt16LittleEndian(data.Slice(26, 2)) & 0x3FFF;
            height = BinaryPrimitives.ReadUInt16LittleEndian(data.Slice(28, 2)) & 0x3FFF;
        }
        else
        {
            return null;
        }

        return new ImageInfo("image/webp", ".webp", width, height);
    }
}
