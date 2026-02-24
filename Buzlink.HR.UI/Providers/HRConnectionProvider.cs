using Encryption;
using Shared.Contracts;

namespace Buzlink.HR.UI.Providers;

public interface IHRConnectionResolver
{
    string GetConnectionString();
    void SetConnectionString(string server, string user, int port);
}
public class HRConnectionResolver : IHRConnectionResolver
{
    private readonly IConnectionStringReaderProvider _reader;
    private readonly IConnectionStringWriterProvider _writer;
    private readonly IKeyProvider _keyProvider;
    private const string db = "hrms";

    public HRConnectionResolver(IConnectionStringReaderProvider reader,
        IConnectionStringWriterProvider writer,
        IKeyProvider keyProvider)
    {
        _reader = reader;
        _writer = writer;
        _keyProvider = keyProvider;
    }
    public string GetConnectionString()
    {
        return _reader.GetConnectionString(db, _keyProvider.Key);
    }
    public void SetConnectionString(string server, string user, int port)
    {
        var conStr = $"Server={server};Database={db};user={user}{_keyProvider.Key};port={port}";
        _writer.SetConnectionString(db, conStr, _keyProvider.Key);
    }
}
