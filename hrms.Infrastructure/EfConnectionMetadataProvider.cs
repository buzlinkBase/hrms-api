namespace Hrms.Infrastructure;
public interface IDbConnectionProvider
{
    string? GetConnectionString(Guid tenantId);
} 

public class EfConnectionMetadataProvider : IDbConnectionProvider
{
    public string? GetConnectionString(Guid tenantId)
    {
        return  null;
    }
}