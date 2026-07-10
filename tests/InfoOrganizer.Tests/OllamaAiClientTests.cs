using System.Text.Json;
using InfoOrganizer.Ai;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.PixelFormats;

namespace InfoOrganizer.Tests;

public class OllamaAiClientTests
{
    [Fact]
    public void BuildMappingRequest_sets_schema_temperature_think_and_num_predict()
    {
        var request = OllamaRequestFactory.BuildMappingRequest("qwen3-vl:4b", "table", think: true, numPredict: 2048);
        using var doc = JsonDocument.Parse(JsonSerializer.Serialize(request));
        var root = doc.RootElement;

        Assert.Equal("qwen3-vl:4b", root.GetProperty("model").GetString());
        Assert.False(root.GetProperty("stream").GetBoolean());
        Assert.True(root.GetProperty("think").GetBoolean());
        Assert.Equal("object", root.GetProperty("format").GetProperty("type").GetString());
        Assert.Equal(0, root.GetProperty("options").GetProperty("temperature").GetDouble());
        Assert.Equal(2048, root.GetProperty("options").GetProperty("num_predict").GetInt32());

        var messages = root.GetProperty("messages");
        Assert.Equal("system", messages[0].GetProperty("role").GetString());
        Assert.Equal("user", messages[1].GetProperty("role").GetString());
        Assert.False(messages[1].TryGetProperty("images", out _));
    }

    [Fact]
    public void BuildImageRequest_adds_images_array_and_image_num_predict()
    {
        var request = OllamaRequestFactory.BuildImageRequest("qwen3-vl:4b", "abc123", think: false, numPredict: 4096);
        using var doc = JsonDocument.Parse(JsonSerializer.Serialize(request));
        var root = doc.RootElement;

        Assert.False(root.GetProperty("think").GetBoolean());
        Assert.Equal("object", root.GetProperty("format").GetProperty("type").GetString());
        Assert.Equal(0, root.GetProperty("options").GetProperty("temperature").GetDouble());
        Assert.Equal(4096, root.GetProperty("options").GetProperty("num_predict").GetInt32());

        var messages = root.GetProperty("messages");
        Assert.False(messages[0].TryGetProperty("images", out _));
        var images = messages[1].GetProperty("images");
        Assert.Equal("abc123", images[0].GetString());
    }

    [Fact]
    public void ImageDownscaler_limits_larger_images_to_max_edge()
    {
        var input = CreatePng(width: 1600, height: 800);

        var output = ImageDownscaler.DownscaleToPng(input, maxEdge: 1024);

        using var image = Image.Load(output);
        Assert.Equal(1024, image.Width);
        Assert.Equal(512, image.Height);
    }

    [Fact]
    public void ImageDownscaler_keeps_small_images_unresized()
    {
        var input = CreatePng(width: 320, height: 200);

        var output = ImageDownscaler.DownscaleToPng(input, maxEdge: 1024);

        using var image = Image.Load(output);
        Assert.Equal(320, image.Width);
        Assert.Equal(200, image.Height);
        Assert.True(output.Length > 0);
    }

    private static byte[] CreatePng(int width, int height)
    {
        using var image = new Image<Rgba32>(width, height, new Rgba32(20, 40, 60));
        using var stream = new MemoryStream();
        image.Save(stream, new PngEncoder());
        return stream.ToArray();
    }
}
