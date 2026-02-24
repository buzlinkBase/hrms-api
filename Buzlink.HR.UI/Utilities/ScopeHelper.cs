using Microsoft.Extensions.DependencyInjection;
namespace Buzlink.HR.UI;

public interface IScopeFactory
{
    IServiceResolver CreateScope();
}

public class ScopeFactory : IScopeFactory
{
    private readonly IServiceScopeFactory _scopeFactory;
    public ScopeFactory(IServiceScopeFactory scopeFactory)
    {
        _scopeFactory = scopeFactory;
    }
    public IServiceResolver CreateScope()
    {
        return new ServiceResolver(_scopeFactory);
    }
}

public interface IServiceResolver : IDisposable
{
    IServiceProvider ServiceProvider { get; }
    bool TryGetService<T>(out T? service) where T : class;
    bool TryGetKeyedService<T>(out T? service, string key) where T : class;
    T? GetService<T>() where T : class;
    T GetRequiredService<T>() where T : class;
    T? GetKeyedService<T>(string key) where T : class;
}
public class ServiceResolver : IServiceResolver, IDisposable
{
    private readonly IServiceScope _scope;
    public ServiceResolver(IServiceScopeFactory scopeFactory)
    {
        _scope = scopeFactory.CreateScope();
    }
    public IServiceProvider ServiceProvider => _scope.ServiceProvider;

    public T? GetService<T>() where T : class
    {
        if (ServiceProvider == null)
            throw new InvalidOperationException("ServiceProvider is not available.");
        return ServiceProvider.GetService<T>();
    }
    public T GetRequiredService<T>() where T : class
    {
        if (ServiceProvider == null)
            throw new InvalidOperationException("ServiceProvider is not available.");
        return ServiceProvider.GetRequiredService<T>();
    }

    public T? GetKeyedService<T>(string key) where T : class
    {
        if (ServiceProvider == null)
            throw new InvalidOperationException("ServiceProvider is not available.");

        return ServiceProvider.GetKeyedService<T>(key);
    }

    public bool TryGetService<T>(out T? service) where T : class
    {
        service = ServiceProvider.GetService<T>();
        return service != null;
    }

    public bool TryGetKeyedService<T>(out T? service, string key) where T : class
    {
        service = ServiceProvider.GetKeyedService<T>(key);
        return service != null;
    }
    public void Dispose() => _scope.Dispose();
}