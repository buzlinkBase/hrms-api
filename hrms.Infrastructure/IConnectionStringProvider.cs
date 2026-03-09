namespace Hrms.Infrastructure;

public interface IConnectionStringProvider
{
    string? GetConnectionString(Guid tenantId);
}