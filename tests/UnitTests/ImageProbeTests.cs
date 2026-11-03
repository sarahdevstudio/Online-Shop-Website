using System.Buffers.Binary;
using Shop.Application.Site;

namespace Shop.UnitTests;

public class ImageProbeTests
{
    // Real 1x1 files.
    private static readonly byte[] RealPng = Convert.FromBase64String(
        "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mNk+M9QDwADhgGAWjR9awAAAABJRU5ErkJggg==");

    private static byte[] Png(uint width, uint height)
    {
        var bytes = new List<byte> { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0, 0, 0, 13, (byte)'I', (byte)'H', (byte)'D', (byte)'R' };
        var dims = new byte[8];
        BinaryPrimitives.WriteUInt32BigEndian(dims.AsSpan(0), width);
        BinaryPrimitives.WriteUInt32BigEndian(dims.AsSpan(4), height);
        bytes.AddRange(dims);
        bytes.AddRange(new byte[] { 8, 6, 0, 0, 0 });
        return [.. bytes];
    }

    private static byte[] Jpeg(ushort width, ushort height, bool withApp0 = true)
    {
        var bytes = new List<byte> { 0xFF, 0xD8 };
        if (withApp0)
        {
            bytes.AddRange(new byte[] { 0xFF, 0xE0, 0x00, 0x10, (byte)'J', (byte)'F', (byte)'I', (byte)'F', 0, 1, 1, 0, 0, 1, 0, 1, 0, 0 });
        }

        bytes.AddRange(new byte[] { 0xFF, 0xC0, 0x00, 0x0B, 8, (byte)(height >> 8), (byte)height, (byte)(width >> 8), (byte)width, 1, 1, 0x11, 0 });
        bytes.AddRange(new byte[] { 0xFF, 0xDA, 0x00, 0x02 });
        return [.. bytes];
    }

    private static byte[] Webp(string chunk, byte[] payload)
    {
        var bytes = new List<byte>();
        bytes.AddRange("RIFF"u8.ToArray());
        bytes.AddRange(BitConverter.GetBytes((uint)(4 + 8 + payload.Length)));
        bytes.AddRange("WEBP"u8.ToArray());
        bytes.AddRange(System.Text.Encoding.ASCII.GetBytes(chunk));
        bytes.AddRange(BitConverter.GetBytes((uint)payload.Length));
        bytes.AddRange(payload);
        return [.. bytes];
    }

    [Fact]
    public void RealPng_IsRecognized()
    {
        var info = ImageProbe.Probe(RealPng);

        Assert.Equal(new ImageInfo("image/png", ".png", 1, 1), info);
    }

    [Fact]
    public void Png_ReadsLargeDimensions() =>
        Assert.Equal(new ImageInfo("image/png", ".png", 1920, 1080), ImageProbe.Probe(Png(1920, 1080)));

    [Theory]
    [InlineData(1200, 630, true)]
    [InlineData(300, 70, false)]
    public void Jpeg_ReadsTheFrameHeader_WithOrWithoutApp0(ushort width, ushort height, bool withApp0) =>
        Assert.Equal(new ImageInfo("image/jpeg", ".jpg", width, height), ImageProbe.Probe(Jpeg(width, height, withApp0)));

    [Fact]
    public void Webp_VP8X_ReadsTheCanvasSize()
    {
        var payload = new byte[10];
        var w = 1600 - 1;
        var h = 700 - 1;
        payload[4] = (byte)w; payload[5] = (byte)(w >> 8); payload[6] = (byte)(w >> 16);
        payload[7] = (byte)h; payload[8] = (byte)(h >> 8); payload[9] = (byte)(h >> 16);

        Assert.Equal(new ImageInfo("image/webp", ".webp", 1600, 700), ImageProbe.Probe(Webp("VP8X", payload)));
    }

    [Fact]
    public void Webp_VP8L_ReadsTheLosslessHeader()
    {
        var payload = new byte[10];
        payload[0] = 0x2F;
        var bits = (uint)(512 - 1) | ((uint)(256 - 1) << 14);
        BitConverter.GetBytes(bits).CopyTo(payload, 1);

        Assert.Equal(new ImageInfo("image/webp", ".webp", 512, 256), ImageProbe.Probe(Webp("VP8L", payload)));
    }

    [Fact]
    public void Webp_VP8_ReadsTheLossyHeader()
    {
        var payload = new byte[14];
        payload[3] = 0x9D; payload[4] = 0x01; payload[5] = 0x2A;
        BitConverter.GetBytes((ushort)800).CopyTo(payload, 6);
        BitConverter.GetBytes((ushort)600).CopyTo(payload, 8);

        Assert.Equal(new ImageInfo("image/webp", ".webp", 800, 600), ImageProbe.Probe(Webp("VP8 ", payload)));
    }

    [Theory]
    [InlineData("<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"10\" height=\"10\"></svg>")]
    [InlineData("GIF89a\u0001\u0000\u0001\u0000")]
    [InlineData("<html><script>alert(1)</script></html>")]
    [InlineData("")]
    public void NonImages_AreRefused(string text) =>
        Assert.Null(ImageProbe.Probe(System.Text.Encoding.UTF8.GetBytes(text)));

    [Fact]
    public void ZeroSizeOrTruncatedHeaders_AreRefused()
    {
        Assert.Null(ImageProbe.Probe(Png(0, 10)));
        Assert.Null(ImageProbe.Probe(RealPng.AsSpan(0, 20)));
        Assert.Null(ImageProbe.Probe(new byte[] { 0xFF, 0xD8, 0xFF }));
        Assert.Null(ImageProbe.Probe(Jpeg(0, 0)));
    }

    [Fact]
    public void NamesAndContentTypesAreNotTrusted_OnlyBytesCount()
    {
        // A PNG stays a PNG whatever it is called; an HTML payload never becomes an image.
        Assert.Equal(".png", ImageProbe.Probe(Png(10, 10))!.Extension);
        Assert.Null(ImageProbe.Probe("<?xml version=\"1.0\"?><svg/>"u8));
    }
}
