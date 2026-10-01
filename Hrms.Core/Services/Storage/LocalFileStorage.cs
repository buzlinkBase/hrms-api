using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace Hrms.Core.Services.Storage;

/// <summary>
/// Files in a folder on this machine, for development and tests -- no Spaces keys needed. It
/// can't hand out download links (<see cref="GetDownloadUrlAsync"/> returns null); don't use it
/// in production, where a container's disk is lost on every deploy.
/// </summary>
public class LocalFileStorage : IFileStorage
{
    private readonly string _root;

    public LocalFileStorage(IOptions<StorageOptions> options, IHostEnvironment environment)
        : this(Path.IsPathRooted(options.Value.Local.RootPath)
            ? options.Value.Local.RootPath
            : Path.Combine(environment.ContentRootPath, options.Value.Local.RootPath))
    {
    }

    public LocalFileStorage(string rootPath) =>
        _root = Path.GetFullPath(rootPath).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

    /// <summary>The file's path, which must stay inside the root folder.</summary>
    private string PathOf(string key)
    {
        if (!StorageKeys.IsValid(key)) throw new ArgumentException($"Invalid storage key \"{key}\".", nameof(key));
        var path = Path.GetFullPath(Path.Combine(_root, key));
        if (!path.StartsWith(_root + Path.DirectorySeparatorChar, StringComparison.Ordinal))
            throw new ArgumentException($"Invalid storage key \"{key}\".", nameof(key));
        return path;
    }

    public async Task<StoredFile> UploadAsync(FileUpload upload, CancellationToken token = default)
    {
        var path = PathOf(upload.Key);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        await using (var file = File.Create(path))
            await upload.Content.CopyToAsync(file, token);
        return new StoredFile(upload.Key, new FileInfo(path).Length, upload.ContentType);
    }

    public Task<Stream?> OpenReadAsync(string key, CancellationToken token = default)
    {
        var path = PathOf(key);
        return Task.FromResult<Stream?>(File.Exists(path) ? File.OpenRead(path) : null);
    }

    public Task<bool> ExistsAsync(string key, CancellationToken token = default) =>
        Task.FromResult(File.Exists(PathOf(key)));

    public Task DeleteAsync(string key, CancellationToken token = default)
    {
        var path = PathOf(key);
        if (File.Exists(path)) File.Delete(path);
        return Task.CompletedTask;
    }

    public Task<Uri?> GetDownloadUrlAsync(string key, string? downloadFileName = null, CancellationToken token = default) =>
        Task.FromResult<Uri?>(null);
}
