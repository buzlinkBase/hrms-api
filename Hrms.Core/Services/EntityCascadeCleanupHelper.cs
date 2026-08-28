namespace Hrms.Core.Services;

// Shared by dev/testing seeders (EmployeeSeederService, TimeShiftSeederService, ...) to
// hard-delete a batch of rows plus everything that references them. Discovers every FK
// pointing at TEntity from EF's own model metadata rather than a hand-maintained table
// list, which drifts out of sync the moment a new referencing entity is added.
//
// Required vs optional FKs are handled differently: a required FK means the referencing
// row is meaningless without the parent (e.g. EmployeeFixedSchedule.TimeShiftId) — delete
// it. An optional FK means the referencing row is an independent entity that merely points
// at the parent (e.g. Employee.TimeShiftId, nullable) — deleting it would destroy unrelated
// data, so the reference is cleared instead.
public static class EntityCascadeCleanupHelper
{
    public static async Task<int> RemoveWithDependentsAsync<TEntity>(
        HrmsContext context,
        List<Guid> ids,
        CancellationToken token) where TEntity : class
    {
        if (ids.Count == 0) return 0;

        var idList = string.Join(",", ids.Select(id => $"'{id}'"));

        var entityType = context.Model.FindEntityType(typeof(TEntity))
            ?? throw new InvalidOperationException($"{typeof(TEntity).Name} entity type not found in EF model.");

        var dependentForeignKeys = entityType.GetReferencingForeignKeys()
            .Where(fk => fk.DeclaringEntityType.ClrType != typeof(TEntity))
            .ToList();

        // ExecuteSqlRawAsync bypasses the change tracker's SaveChanges pipeline entirely,
        // so it doesn't automatically ride along with a caller's later CommitChangesAsync()
        // — own the transaction here explicitly (only if one isn't already ambient) so this
        // is self-contained and guaranteed to commit.
        var ownsTransaction = context.Database.CurrentTransaction == null;
        var transaction = ownsTransaction
            ? await context.Database.BeginTransactionAsync(token)
            : null;
        try
        {
            foreach (var fk in dependentForeignKeys)
            {
                var tableName = fk.DeclaringEntityType.GetTableName();
                var columnName = fk.Properties[0].GetColumnName();
                if (tableName == null) continue;

                var sql = fk.IsRequired
                    ? $"DELETE FROM `{tableName}` WHERE `{columnName}` IN ({idList})"
                    : $"UPDATE `{tableName}` SET `{columnName}` = NULL WHERE `{columnName}` IN ({idList})";

                await context.Database.ExecuteSqlRawAsync(sql, token);
            }

            var selfTableName = entityType.GetTableName();
            await context.Database.ExecuteSqlRawAsync(
                $"DELETE FROM `{selfTableName}` WHERE `Id` IN ({idList})", token);

            if (ownsTransaction) await transaction!.CommitAsync(token);
        }
        catch
        {
            if (ownsTransaction) await transaction!.RollbackAsync(token);
            throw;
        }
        finally
        {
            if (transaction != null) await transaction.DisposeAsync();
        }

        return ids.Count;
    }
}
