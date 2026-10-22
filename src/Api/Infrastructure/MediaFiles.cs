using Microsoft.AspNetCore.StaticFiles;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Options;
using Shop.Infrastructure.Catalog;

namespace Shop.Api.Infrastructure;

internal static class MediaFiles
{
    /// <summary>Serves uploaded images from the media root under /media. Only stored files exist there; no listing.</summary>
    public static IApplicationBuilder UseMedia(this WebApplication app)
    {
        var root = app.Services.GetRequiredService<IOptions<MediaOptions>>().Value.GetFullRoot(app.Environment);
        Directory.CreateDirectory(root);
        return app.UseStaticFiles(new StaticFileOptions
        {
            FileProvider = new PhysicalFileProvider(root),
            // Only the formats uploads are converted to; anything else that ends up in the folder is never served.
            ContentTypeProvider = new FileExtensionContentTypeProvider(new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                [".jpg"] = "image/jpeg",
                [".png"] = "image/png",
                [".webp"] = "image/webp",
            }),
            RequestPath = MediaOptions.RequestPath,
            // File names are random and never reused, so they can be cached for a long time.
            OnPrepareResponse = context => context.Context.Response.Headers.CacheControl = "public, max-age=31536000, immutable",
        });
    }
}
