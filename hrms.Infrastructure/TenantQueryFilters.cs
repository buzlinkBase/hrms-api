using BuzlinkRepository;
using Microsoft.EntityFrameworkCore;
using System.Linq.Expressions;

namespace Hrms.Infrastructure;

/// <summary>
/// Tenant + soft-delete global query filters that read the tenant from the executing context at
/// query time, instead of baking it into the model as a constant.
///
/// BuzlinkRepository's <c>UseTenantAndDateFilter(Guid)</c> embeds the tenant Guid as a constant, so
/// every tenant needed its own model (TenantModelCacheKeyFactory) -- EF kept one full compiled model
/// plus its own compiled-query cache per tenant for the life of the process, which is what grew the
/// server's memory with every tenant touched. Here the filter references a member of the context
/// instance; EF re-binds that to whichever context runs the query and passes the value as a SQL
/// parameter, so a single model serves every tenant.
/// </summary>
public static class TenantQueryFilters
{
    /// <summary>
    /// Same semantics as <c>UseTenantAndDateFilter</c>: <c>DeletedAt == null</c> for every
    /// <see cref="ITimeStamp"/> entity, AND <c>TenantId == current tenant</c> for those with a Guid
    /// TenantId -- except that an empty current tenant (background jobs, design time) applies no
    /// tenant restriction, exactly like the old filter built for <see cref="Guid.Empty"/>.
    /// </summary>
    public static void ApplyTenantAndDateFilters<TContext>(this ModelBuilder modelBuilder, TContext context)
        where TContext : DbContext, ICurrentTenantContext, IConfidentialityContext
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

            // EF allows one query filter per entity, so the confidential-employee rule joins here.
            if (ConfidentialityFilters.For(entityType, parameter, context) is { } confidentiality)
                filter = Expression.AndAlso(filter, confidentiality);

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
