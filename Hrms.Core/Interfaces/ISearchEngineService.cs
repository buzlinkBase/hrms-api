using Elastic.Clients.Elasticsearch;

namespace Hrms.Core;

public interface ISearchEngineService
{
    Task IndexRecordAsync<T>(T document, string indexName, CancellationToken ct) where T : class;
}

[ServiceRegistration(Microsoft.Extensions.DependencyInjection.ServiceLifetime.Singleton, true)]
public class NullSearchService : ISearchEngineService
{
    public Task IndexRecordAsync<T>(T document, string indexName, CancellationToken ct) where T : class
    {
        return Task.CompletedTask;
    }
}

[ServiceRegistration(Microsoft.Extensions.DependencyInjection.ServiceLifetime.Singleton, true)]
public class ElasticSearchService : ISearchEngineService
{
    private readonly ElasticsearchClient _client;
    public ElasticSearchService(ElasticsearchClient client)
    {
        _client = client;
    }
    public async Task IndexRecordAsync<T>(T document, string indexName, CancellationToken ct) where T : class
    {
        await _client.IndexAsync(document, indexName, ct);
    }
}