using Microsoft.AspNetCore.ResponseCompression;
using System.IO.Compression;

namespace Hrms.Api.Extensions;

public static class ResponseCompressionExtensions
{
    // Content types compressed on top of ResponseCompressionDefaults.MimeTypes (which already
    // covers application/json, text/*, xml, etc.): error responses and the CSV exports.
    //
    // Deliberately NOT compressed:
    // - application/x-msgpack: the MessagePack formatter already LZ4-compresses its payload
    //   (MessagePackCompression.Lz4BlockArray), so a second pass costs CPU for nothing.
    // - application/pdf and xlsx: already-compressed file formats.
    // - text/event-stream: compression would buffer SignalR's SSE fallback.
    private static readonly string[] AdditionalMimeTypes =
    [
        "application/problem+json",
        "text/csv",
    ];

    /// <summary>
    /// Brotli (preferred) or Gzip for JSON-heavy responses, at the fastest level so the CPU cost
    /// stays small. Enabled for HTTPS: this API issues no credentials (AuthApi does), so BREACH's
    /// secret-in-a-compressed-response precondition doesn't apply here.
    /// </summary>
    public static IServiceCollection AddApiResponseCompression(this IServiceCollection services)
    {
        services.AddResponseCompression(options =>
        {
            options.EnableForHttps = true;
            options.Providers.Add<BrotliCompressionProvider>();
            options.Providers.Add<GzipCompressionProvider>();
            options.MimeTypes = ResponseCompressionDefaults.MimeTypes.Concat(AdditionalMimeTypes);
        });

        services.Configure<BrotliCompressionProviderOptions>(options => options.Level = CompressionLevel.Fastest);
        services.Configure<GzipCompressionProviderOptions>(options => options.Level = CompressionLevel.Fastest);
        return services;
    }
}
