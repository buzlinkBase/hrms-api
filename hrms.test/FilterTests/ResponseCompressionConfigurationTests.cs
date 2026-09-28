using System.IO.Compression;
using Hrms.Api.Extensions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.ResponseCompression;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace hrms.test.FilterTests;

/// <summary>
/// hrms-api's response compression, exercised through the middleware's own decision logic
/// (IResponseCompressionProvider): Brotli preferred over Gzip, HTTPS included, JSON /
/// problem+json / CSV compressed -- and never MessagePack (already LZ4-compressed), PDFs, or
/// SignalR's event stream.
/// </summary>
public class ResponseCompressionConfigurationTests
{
    private static IServiceProvider BuildProvider()
    {
        var services = new ServiceCollection();
        services.AddSingleton<ILoggerFactory>(NullLoggerFactory.Instance);
        services.AddSingleton(typeof(ILogger<>), typeof(NullLogger<>));
        services.AddApiResponseCompression();
        return services.BuildServiceProvider();
    }

    private static HttpContext Request(string acceptEncoding, bool https = true)
    {
        var context = new DefaultHttpContext();
        context.Request.Scheme = https ? "https" : "http";
        context.Request.IsHttps = https;
        context.Request.Headers.AcceptEncoding = acceptEncoding;
        return context;
    }

    [Theory]
    [InlineData("br, gzip", "br")]
    [InlineData("gzip, deflate", "gzip")]
    public void PicksBrotliFirst_ThenGzip(string acceptEncoding, string expected)
    {
        var compression = BuildProvider().GetRequiredService<IResponseCompressionProvider>();

        compression.GetCompressionProvider(Request(acceptEncoding))!.EncodingName.Should().Be(expected);
    }

    [Fact]
    public void CompressesOverHttps()
    {
        var compression = BuildProvider().GetRequiredService<IResponseCompressionProvider>();

        compression.CheckRequestAcceptsCompression(Request("br")).Should().BeTrue();
    }

    [Theory]
    [InlineData("application/json; charset=utf-8", true)]
    [InlineData("application/problem+json", true)]
    [InlineData("text/csv", true)]
    [InlineData("application/x-msgpack", false)]
    [InlineData("application/pdf", false)]
    [InlineData("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", false)]
    [InlineData("text/event-stream", false)]
    public void CompressesOnlyTheIntendedContentTypes(string contentType, bool compressed)
    {
        var compression = BuildProvider().GetRequiredService<IResponseCompressionProvider>();
        var context = Request("br");
        context.Response.ContentType = contentType;

        compression.ShouldCompressResponse(context).Should().Be(compressed);
    }

    [Fact]
    public void UsesTheFastestLevel()
    {
        var provider = BuildProvider();

        provider.GetRequiredService<IOptions<BrotliCompressionProviderOptions>>().Value.Level.Should().Be(CompressionLevel.Fastest);
        provider.GetRequiredService<IOptions<GzipCompressionProviderOptions>>().Value.Level.Should().Be(CompressionLevel.Fastest);
    }
}
