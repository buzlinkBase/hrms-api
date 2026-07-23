using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using System.Net;

namespace Hrms.Core.Services.AI;

public class StreamResult : IDisposable
{
    public Stream Stream { get; set; }
    public string ContentType { get; set; }
    public void Dispose()
    {
        Stream?.Dispose();
    }
}

public class StreamLoader
{
    private readonly HttpClient _httpClient;
    private readonly IWebHostEnvironment _env;
    public StreamLoader(HttpClient httpClient, IWebHostEnvironment env)
    {
        _httpClient = httpClient;
        _env = env;
    }
    /// <summary>
    /// Loads a stream directly from an uploaded IFormFile.
    /// </summary>
    public StreamResult FromFormFile(IFormFile file)
    {
        if (file == null || file.Length == 0)
            throw new ArgumentException("The provided file is empty or null.");

        return new StreamResult
        {
            Stream = file.OpenReadStream(),
            ContentType = file.ContentType
        };
    }

    /// <summary>
    /// Captures a stream automatically determining if the source is a URL or a physical/relative disk path.
    /// </summary>
    public async Task<StreamResult> FromPathOrUrlAsync(string source, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(source))
            throw new ArgumentException("Source path or URL cannot be empty.", nameof(source));

        // Case 1: Source is a URL (http:// or https://)
        if (Uri.TryCreate(source, UriKind.Absolute, out Uri uriResult)
            && (uriResult.Scheme == Uri.UriSchemeHttp || uriResult.Scheme == Uri.UriSchemeHttps))
        {
            // Optimize: If the URL points to our local host, load it locally instead of making an HTTP call!
            if (uriResult.IsLoopback || uriResult.Host.Equals("localhost", StringComparison.OrdinalIgnoreCase))
            {
                var relativeDiskPath = uriResult.AbsolutePath.TrimStart('/');
                var localPhysicalPath = Path.Combine(_env.WebRootPath, relativeDiskPath);

                if (File.Exists(localPhysicalPath))
                {
                    return FromPhysicalPath(localPhysicalPath);
                }
            }

            // Fallback: It's an external URL, retrieve it via HttpClient
            var response = await _httpClient.GetAsync(uriResult, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            response.EnsureSuccessStatusCode();

            var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            var contentType = response.Content.Headers.ContentType?.MediaType ?? "application/octet-stream";

            return new StreamResult
            {
                Stream = stream,
                ContentType = contentType
            };
        }
        // Case 2: Source is a relative or absolute local disk path
        string targetPath = Path.IsPathRooted(source) ? source : Path.Combine(_env.WebRootPath, source);
        return FromPhysicalPath(targetPath);
    }

    private StreamResult FromPhysicalPath(string physicalPath)
    {
        // 1. URL Decode the path in case the URL had %20 or special characters
        string decodedPath = WebUtility.UrlDecode(physicalPath);

        if (!File.Exists(decodedPath))
            throw new FileNotFoundException($"The system could not find the file at: {decodedPath}");

        // 2. Use FileShare.ReadWrite so it bypasses exclusive locks from other processes
        var stream = new FileStream(
            decodedPath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.ReadWrite // <-- This stops the "used by another process" crash
        );

        var contentType = GetMimeType(decodedPath);

        return new StreamResult
        {
            Stream = stream,
            ContentType = contentType
        };
    }

    private string GetMimeType(string filePath)
    {
        var extension = Path.GetExtension(filePath).ToLowerInvariant();
        return extension switch
        {
            // Documents & Text
            ".pdf" => "application/pdf",
            ".txt" => "text/plain",
            ".csv" => "text/csv",

            // Microsoft Word
            ".docx" => "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
            ".doc" => "application/msword",

            // Microsoft Excel
            ".xlsx" => "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            ".xls" => "application/vnd.ms-excel",

            // Microsoft PowerPoint
            ".pptx" => "application/vnd.openxmlformats-officedocument.presentationml.presentation",
            ".ppt" => "application/vnd.ms-powerpoint",

            // Web & Data
            ".json" => "application/json",
            ".xml" => "application/xml",

            // Images
            ".png" => "image/png",
            ".jpg" => "image/jpeg",
            ".jpeg" => "image/jpeg",
            ".gif" => "image/gif",
            ".webp" => "image/webp",
            ".tiff" => "image/tiff",
            ".bmp" => "image/bmp",

            // Fallback generic binary stream
            _ => "application/octet-stream"
        };
    }
}