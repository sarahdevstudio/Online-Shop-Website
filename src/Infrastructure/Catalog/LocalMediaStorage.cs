using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Shop.Application.Catalog;

namespace Shop.Infrastructure.Catalog;

public sealed class MediaOptions
{
    public const string SectionName = "Media";
    public const string RequestPath = "/media";

    /// <summary>
    /// Folder for uploaded files, relative to the API content root or absolute. Git-ignored; must be backed up in
    /// production. Must not be inside the folder the application's binaries run from (see <see cref="GetFullRoot"/>).
    /// </summary>
    public string RootPath { get; set; } = "media";

    /// <summary>Largest accepted image upload in bytes (default 5 MB).</summary>
    public long MaxImageBytes { get; set; } = 5 * 1024 * 1024;

    /// <summary>Resolves the media root and refuses one inside the application's binary folder.</summary>
    public string GetFullRoot(IHostEnvironment environment)
    {
        var root = Path.GetFullPath(Path.Combine(environment.ContentRootPath, RootPath));
        EnsureOutsideApplication(root, AppContext.BaseDirectory);
        return root;
    }

    /// <summary>
    /// Uploads must never land next to the executables (a published app's content root is its binary folder, so the
    /// relative default is only valid in development: set an absolute <c>Media__RootPath</c> elsewhere).
    /// </summary>
    public static void EnsureOutsideApplication(string mediaRoot, string applicationDirectory)
    {
        static string WithSeparator(string path) => Path.TrimEndingDirectorySeparator(Path.GetFullPath(path)) + Path.DirectorySeparatorChar;

        var media = WithSeparator(mediaRoot);
        var app = WithSeparator(applicationDirectory);
        if (media.StartsWith(app, StringComparison.OrdinalIgnoreCase) || app.StartsWith(media, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"Media:RootPath '{mediaRoot}' overlaps the application folder '{applicationDirectory}'. Set Media__RootPath to a folder outside the application.");
        }
    }
}

/// <summary>Files on the local disk under the media root. A cloud/S3 adapter can replace it behind <see cref="IMediaStorage"/>.</summary>
internal sealed class LocalMediaStorage(IOptions<MediaOptions> options, IHostEnvironment environment) : IMediaStorage
{
    private readonly string _root = options.Value.GetFullRoot(environment);

    public async Task<string> SaveAsync(Stream content, string folder, string extension, CancellationToken ct)
    {
        var relative = $"{folder.Trim('/')}/{Guid.NewGuid():N}{extension}";
        var fullPath = Resolve(relative);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);

        await using var file = new FileStream(fullPath, FileMode.CreateNew, FileAccess.Write);
        await content.CopyToAsync(file, ct);
        return relative;
    }

    public Task DeleteAsync(string path, CancellationToken ct)
    {
        var fullPath = Resolve(path);
        if (File.Exists(fullPath))
        {
            File.Delete(fullPath);
        }

        return Task.CompletedTask;
    }

    public string GetUrl(string path) => $"{MediaOptions.RequestPath}/{path}";

    private string Resolve(string relative)
    {
        var fullPath = Path.GetFullPath(Path.Combine(_root, relative));
        // Paths come from our own database, but never let one escape the media root.
        if (!fullPath.StartsWith(_root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Media path is outside the media root.");
        }

        return fullPath;
    }
}
