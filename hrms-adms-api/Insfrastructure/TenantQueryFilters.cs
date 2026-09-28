using System.Linq.Expressions;

namespace Hrms.adms.Insfrastructure;

/// <summary>
/// Mirror of Hrms.Infrastructure.TenantQueryFilters (adms doesn't reference that project).
///
/// Tenant + soft-delete global query filters that read the tenant from the executing context at
/// query time, instead of baking it into the model as a constant (BuzlinkRepository's
/// <c>UseTenantAndDateFilter(Guid)</c>), which forced one cached model per tenant via
/// TenantModelCacheKeyFactory and grew memory with every tenant seen. A single model now serves
/// every tenant; the tenant travels as a SQL parameter.
/// </summary>
public static class TenantQueryFilters
{
    /// <summary>
    /// <c>DeletedAt == null</c> for every <see cref="ITimeStamp"/> entity, AND
    /// <c>TenantId == current tenant</c> for those with a Guid TenantId -- an empty current tenant
    /// (device /iclock calls, background jobs, design time) applies no tenant restriction, exactly
    /// like the old filter built for <see cref="Guid.Empty"/>.
    /// </summary>
    public static void ApplyTenantAndDateFilters<TContext>(this ModelBuilder modelBuilder, TContext context)
        where TContext : DbContext, ICurrentTenantContext
    {
        var entityTypes = modelBuilder.Model
            .GetEntityTypes()
            .Select(e => e.ClrType)
            .Where(t => typeof(ITimeStamp).IsAssignableFrom(t))
            .ToList();

        var currentTenant = Expression.Property(
            Expression.Constant(context, typeof(TContext)),
            nameof(ICurrentTenantContext.CurrentTenantId));
        var noTenant = Expression.Equal(currentTenant, Expression.Constant(Guid.Empty));

        foreach (var entityType in entityTypes)
        {
            var parameter = Expression.Parameter(entityType, "x");
            Expression filter = CreateDeletedFilter(entityType, parameter);

            var tenantIdProperty = entityType.GetProperty(nameof(IEntityTenant.TenantId));
            if (tenantIdProperty?.PropertyType == typeof(Guid))
            {
                var sameTenant = Expression.Equal(
                    Expression.Call(
                        typeof(EF), nameof(EF.Property), [typeof(Guid)],
                        parameter, Expression.Constant(nameof(IEntityTenant.TenantId))),
                    currentTenant);
                filter = Expression.AndAlso(filter, Expression.OrElse(noTenant, sameTenant));
            }

            modelBuilder.Entity(entityType).HasQueryFilter(Expression.Lambda(filter, parameter));
        }
    }

    private static Expression CreateDeletedFilter(Type entityType, ParameterExpression parameter)
    {
        var deletedAtProperty = entityType.GetProperty(nameof(ITimeStamp.DeletedAt));
        if (deletedAtProperty?.PropertyType != typeof(DateTime?))
            return Expression.Constant(true);

        return Expression.Equal(
            Expression.Call(
                typeof(EF), nameof(EF.Property), [typeof(DateTime?)],
                parameter, Expression.Constant(nameof(ITimeStamp.DeletedAt))),
            Expression.Constant(null, typeof(DateTime?)));
    }
}

/// <summary>A DbContext whose query filters read the tenant at query time.</summary>
public interface ICurrentTenantContext
{
    Guid CurrentTenantId { get; }
}
