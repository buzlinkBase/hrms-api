using System.Net.Http.Json;
using System.Text.Json;

namespace Hrms.Core.Services.ClientPortal;

public interface IClientPortalClient
{
    Task<PortalIngestResult> PutAsync<T>(ClientPortalCredentials credentials, string resource, IReadOnlyList<T> rows, CancellationToken token);
}

/// <summary>
/// Pushes rows to the portal's /ingest endpoints with the company's API key. The address and key differ per company, so
/// they are set on each request rather than on the shared HttpClient. Rows go in batches the portal accepts.
/// </summary>
public class ClientPortalClient : IClientPortalClient
{
    public const int BatchSize = 500;
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private readonly HttpClient _http;

    public ClientPortalClient(HttpClient http)
    {
        _http = http;
    }

    public async Task<PortalIngestResult> PutAsync<T>(ClientPortalCredentials credentials, string resource, IReadOnlyList<T> rows, CancellationToken token)
    {
        var accepted = 0;
        var errors = new List<PortalRowError>();

        foreach (var chunk in rows.Chunk(BatchSize))
        {
            using var request = new HttpRequestMessage(HttpMethod.Put, $"{credentials.PortalUrl.TrimEnd('/')}/api/v1/ingest/{resource}")
            {
                Content = JsonContent.Create(chunk, options: Json),
            };
            request.Headers.Add("X-Api-Key", credentials.ApiKey);

            using var response = await _http.SendAsync(request, token);
            if (!response.IsSuccessStatusCode)
            {
                // Throwing hands the failure to the message retry (the portal may just be down); the key is never logged.
                throw new HttpRequestException($"The client portal answered {(int)response.StatusCode} for /ingest/{resource}.", null, response.StatusCode);
            }

            var envelope = await response.Content.ReadFromJsonAsync<IngestEnvelope>(Json, token);
            var result = envelope?.Data ?? throw new HttpRequestException($"The client portal sent an unreadable answer for /ingest/{resource}.");
            accepted += result.Accepted;
            errors.AddRange(result.Errors ?? []);
        }

        return new PortalIngestResult(accepted, errors.Count, errors);
    }

    private sealed record IngestEnvelope(PortalIngestResult? Data);
}
