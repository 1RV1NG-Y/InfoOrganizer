using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.Processing;

namespace InfoOrganizer.Ai;

internal static class ImageDownscaler
{
    public static byte[] DownscaleToPng(byte[] imageBytes, int maxEdge)
    {
        if (imageBytes.Length == 0)
            return imageBytes;

        using var image = Image.Load(imageBytes);
        if (maxEdge > 0 && Math.Max(image.Width, image.Height) > maxEdge)
        {
            var scale = (double)maxEdge / Math.Max(image.Width, image.Height);
            var width = Math.Max(1, (int)Math.Round(image.Width * scale));
            var height = Math.Max(1, (int)Math.Round(image.Height * scale));
            image.Mutate(x => x.Resize(new ResizeOptions
            {
                Size = new Size(width, height),
                Sampler = KnownResamplers.Lanczos3
            }));
        }

        using var output = new MemoryStream();
        image.Save(output, new PngEncoder());
        return output.ToArray();
    }
}
