namespace Hrms.Core.Services.Storage;

/// <summary>
/// What an uploaded image may be: PNG, JPEG or WebP, at most <see cref="MaxBytes"/>. The type is
/// read from the file's own first bytes -- the browser's claimed content type and the file name
/// are never trusted. SVG is deliberately not allowed: it can carry script.
/// </summary>
public static class ImageUploads
{
    public const long MaxBytes = 2 * 1024 * 1024;
    public const string MaxSizeLabel = "2 MB";

    /// <summary>"image/png", "image/jpeg" or "image/webp" when the bytes are one of those; otherwise null.</summary>
    public static string? DetectContentType(ReadOnlySpan<byte> header)
    {
        if (header.Length >= 8 && header[..8].SequenceEqual(new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A }))
            return "image/png";
        if (header.Length >= 3 && header[0] == 0xFF && header[1] == 0xD8 && header[2] == 0xFF)
            return "image/jpeg";
        if (header.Length >= 12 &&
            header[..4].SequenceEqual("RIFF"u8) &&
            header[8..12].SequenceEqual("WEBP"u8))
            return "image/webp";
        return null;
    }

    /// <summary>
    /// Reads the upload into memory (it's small by now) and checks it. Throws
    /// <see cref="InvalidImageException"/> with a message the user can act on.
    /// </summary>
    public static async Task<(MemoryStream Content, string ContentType)> ReadAsync(Stream upload, long? declaredLength, CancellationToken token)
    {
        if (declaredLength is 0) throw new InvalidImageException("The file is empty.");
        if (declaredLength > MaxBytes) throw new InvalidImageException($"The image is too large. The limit is {MaxSizeLabel}.");

        var content = new MemoryStream();
        var buffer = new byte[81920];
        int read;
        while ((read = await upload.ReadAsync(buffer, token)) > 0)
        {
            if (content.Length + read > MaxBytes)
                throw new InvalidImageException($"The image is too large. The limit is {MaxSizeLabel}.");
            content.Write(buffer, 0, read);
        }

        var contentType = DetectContentType(content.GetBuffer().AsSpan(0, (int)Math.Min(content.Length, 16)));
        if (contentType == null)
            throw new InvalidImageException("Upload a PNG, JPG or WebP image.");

        content.Position = 0;
        return (content, contentType);
    }

    /// <summary>The file extension for a detected content type.</summary>
    public static string ExtensionFor(string contentType) => contentType switch
    {
        "image/png" => ".png",
        "image/webp" => ".webp",
        _ => ".jpg",
    };
}

/// <summary>A stored image's link to show it right away.</summary>
public sealed record ImageUploadResult(string Url);

/// <summary>An upload that isn't an acceptable image; the message is safe to show the user (a 400).</summary>
public class InvalidImageException(string message) : Onepunch.Common.Lib.Exceptions.GuardException(message);
