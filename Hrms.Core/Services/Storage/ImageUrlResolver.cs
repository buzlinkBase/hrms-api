namespace Hrms.Core.Services.Storage;

/// <summary>
/// Turns a stored image (a logo or profile photo) into something an &lt;img&gt; can show: a
/// short-lived signed link from Spaces. Local disk can't hand out links, so in development the
/// image comes back inline as a data: URL instead. Photos saved before storage existed are
/// already data: URLs and pass through unchanged.
/// </summary>
public class ImageUrlResolver(IFileStorage storage)
{
    public async Task<string> DisplayUrlAsync(string? stored, CancellationToken token = default)
    {
        if (string.IsNullOrWhiteSpace(stored)) return string.Empty;
        if (stored.StartsWith("data:", StringComparison.OrdinalIgnoreCase)) return stored;
        if (!StorageKeys.IsValid(stored)) return string.Empty;

        var link = await storage.GetDownloadUrlAsync(stored, downloadFileName: null, token);
        if (link != null) return link.ToString();

        await using var content = await storage.OpenReadAsync(stored, token);
        if (content == null) return string.Empty;
        using var copy = new MemoryStream();
        await content.CopyToAsync(copy, token);
        var contentType = ImageUploads.DetectContentType(copy.GetBuffer().AsSpan(0, (int)Math.Min(copy.Length, 16)));
        return contentType == null ? string.Empty : $"data:{contentType};base64,{Convert.ToBase64String(copy.ToArray())}";
    }
}
