namespace Hrms.Core.Services.Storage;

/// <summary>A file to store: its content, where it goes (see <see cref="StorageKeys"/>) and its type.</summary>
public sealed record FileUpload(string Key, Stream Content, string ContentType);

/// <summary>A stored file's key, size and type -- what a record keeps to find it again.</summary>
public sealed record StoredFile(string Key, long Size, string ContentType);

/// <summary>
/// Where the app keeps files (documents, photos, exports). Files are private: a browser gets one
/// through <see cref="GetDownloadUrlAsync"/> (a short-lived link) or through an API endpoint that
/// streams <see cref="OpenReadAsync"/> after its own permission check. Keys always come from
/// <see cref="StorageKeys"/>, which puts every company's files under that company.
/// </summary>
public interface IFileStorage
{
    Task<StoredFile> UploadAsync(FileUpload upload, CancellationToken token = default);

    /// <summary>The file's content, or null when there's no such file. The caller disposes the stream.</summary>
    Task<Stream?> OpenReadAsync(string key, CancellationToken token = default);

    Task<bool> ExistsAsync(string key, CancellationToken token = default);

    /// <summary>Removes the file; does nothing when it's already gone.</summary>
    Task DeleteAsync(string key, CancellationToken token = default);

    /// <summary>
    /// Removes every file in one company's folder (<see cref="StorageKeys.TenantPrefix"/> or
    /// <see cref="StorageKeys.ArchivePrefix"/>) and returns how many there were. Refuses any other
    /// prefix, so it can't be pointed at the whole bucket; does nothing when the folder is empty.
    /// </summary>
    Task<int> DeleteByPrefixAsync(string prefix, CancellationToken token = default);

    /// <summary>
    /// A link the browser can download the file from for a short time, saved as
    /// <paramref name="downloadFileName"/> when given. Null when this storage can't hand out links
    /// (local disk) -- stream <see cref="OpenReadAsync"/> from an endpoint instead.
    /// </summary>
    Task<Uri?> GetDownloadUrlAsync(string key, string? downloadFileName = null, CancellationToken token = default);
}
