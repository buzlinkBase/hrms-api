namespace Hrms.Core.Services.AI;

/// <summary>
/// A document (PDF, image, plain text, ...) attached to an extraction request.
/// Created only through the factory methods so instances are always valid.
/// </summary>
public sealed class AiDocument
{
    public byte[] Bytes { get; }
    public string MimeType { get; }

    private AiDocument(byte[] bytes, string mimeType)
    {
        Bytes = bytes;
        MimeType = mimeType;
    }

    public static AiDocument FromBytes(byte[] bytes, string mimeType)
    {
        if (bytes == null || bytes.Length == 0)
            throw new ArgumentException("Document bytes are required.", nameof(bytes));
        if (string.IsNullOrWhiteSpace(mimeType))
            throw new ArgumentException("A mime type is required (e.g. application/pdf).", nameof(mimeType));

        return new AiDocument(bytes, mimeType);
    }

    public static AiDocument FromBase64(string base64, string mimeType)
    {
        if (string.IsNullOrWhiteSpace(base64))
            throw new ArgumentException("Base64 content is required.", nameof(base64));

        return FromBytes(Convert.FromBase64String(base64), mimeType);
    }

    public static async Task<AiDocument> FromStreamAsync(Stream stream, string mimeType, CancellationToken cancellationToken = default)
    {
        if (stream == null)
            throw new ArgumentNullException(nameof(stream));

        using var memory = new MemoryStream();
        await stream.CopyToAsync(memory, cancellationToken);
        return FromBytes(memory.ToArray(), mimeType);
    }

    public static class MimeTypes
    {
        public const string Pdf = "application/pdf";
        public const string Png = "image/png";
        public const string Jpeg = "image/jpeg";
        public const string Webp = "image/webp";
        public const string PlainText = "text/plain";
    }
}
