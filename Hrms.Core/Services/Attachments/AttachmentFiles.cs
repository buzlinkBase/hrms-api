using Hrms.Core.Services.Storage;
using Onepunch.Common.Lib.Exceptions;

namespace Hrms.Core.Services.Attachments;

/// <summary>
/// What an attachment may be: a PNG, JPEG, WebP image or a PDF, at most <see cref="MaxBytes"/>.
/// The type is read from the file's own first bytes -- the browser's claimed content type and the
/// file name are never trusted. SVG and Office files are deliberately not allowed.
/// </summary>
public static class AttachmentFiles
{
    public const long MaxBytes = 5 * 1024 * 1024;
    public const string MaxSizeLabel = "5 MB";
    public const int MaxPerRecord = 5;

    /// <summary>The detected content type, or null when the bytes aren't an allowed file.</summary>
    public static string? DetectContentType(ReadOnlySpan<byte> header) =>
        ImageUploads.DetectContentType(header)
        ?? (header.Length >= 5 && header[..5].SequenceEqual("%PDF-"u8) ? "application/pdf" : null);

    public static string ExtensionFor(string contentType) =>
        contentType == "application/pdf" ? ".pdf" : ImageUploads.ExtensionFor(contentType);

    /// <summary>
    /// Reads the upload into memory (it's small) and checks it. Throws
    /// <see cref="InvalidAttachmentException"/> with a message the user can act on.
    /// </summary>
    public static async Task<(MemoryStream Content, string ContentType)> ReadAsync(
        Stream upload, long? declaredLength, CancellationToken token)
    {
        if (declaredLength is 0) throw new InvalidAttachmentException("The file is empty.");
        if (declaredLength > MaxBytes) throw TooLarge();

        var content = new MemoryStream();
        var buffer = new byte[81920];
        int read;
        while ((read = await upload.ReadAsync(buffer, token)) > 0)
        {
            if (content.Length + read > MaxBytes) throw TooLarge();
            content.Write(buffer, 0, read);
        }

        var contentType = DetectContentType(content.GetBuffer().AsSpan(0, (int)Math.Min(content.Length, 16)));
        if (contentType == null)
            throw new InvalidAttachmentException("Upload a PNG, JPG, WebP image or a PDF.");

        content.Position = 0;
        return (content, contentType);
    }

    private static InvalidAttachmentException TooLarge() =>
        new($"The file is too large. The limit is {MaxSizeLabel}.");
}

/// <summary>An upload that isn't acceptable; the message is safe to show the user (a 400).</summary>
public class InvalidAttachmentException(string message) : GuardException(message);
