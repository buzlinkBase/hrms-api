using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Storage;

namespace hrms.test.TestSupport;

/// <summary>
/// Puts one minimal row for a company into EVERY table of a context's EF model, read straight from
/// the model -- so a table added next year is seeded (and must then be erased) without anyone
/// touching a test. Foreign keys are switched off while seeding: the rows only have to exist, not to
/// make business sense. A table without a TenantId column gets its single-column foreign key to a
/// table that has one pointed at that company's seeded parent, the way the purge catalog scopes it.
/// SQLite only (the test database).
/// </summary>
public static class UniversalRowSeeder
{
    /// <summary>Tables nothing is seeded into: messaging and the audit trail, which aren't company tables.</summary>
    public static IReadOnlySet<string> InfrastructureTables => Hrms.Core.Services.DataPurge.CompanyDataCatalog.InfrastructureTables;

    private const string TenantColumn = "TenantId";

    /// <summary>Seeds one row per company per table and returns the tables seeded.</summary>
    public static IReadOnlyList<string> Seed(DbContext db, IReadOnlyList<Guid> companies, IReadOnlySet<string>? skip = null)
    {
        skip ??= InfrastructureTables;
        var tables = db.Model.GetRelationalModel().Tables
            .Where(t => !skip.Contains(t.Name))
            // Tables with the company column first, so a child-only table finds its parent's key.
            .OrderBy(t => t.FindColumn(TenantColumn) == null)
            .ThenBy(t => t.Name, StringComparer.Ordinal)
            .ToList();

        var connection = db.Database.GetDbConnection();
        db.Database.OpenConnection();
        var keys = new Dictionary<(string Table, Guid Company), Dictionary<string, object?>>();
        var counter = 0;
        try
        {
            Execute(connection, "PRAGMA foreign_keys = OFF");
            foreach (var table in tables)
            {
                foreach (var company in companies)
                {
                    using var command = connection.CreateCommand();
                    var columns = new List<string>();
                    var values = new Dictionary<string, object?>();
                    foreach (var column in table.Columns)
                    {
                        var property = column.PropertyMappings.First().Property;
                        if (IsStoreGenerated(property)) continue;

                        var value = ValueFor(table, column, property, company, keys, ref counter);
                        values[column.Name] = value;
                        var name = $"@p{columns.Count}";
                        var parameter = (property.GetRelationalTypeMapping()).CreateParameter(command, name, value, column.IsNullable);
                        command.Parameters.Add(parameter);
                        columns.Add(column.Name);
                    }

                    var sql = db.GetService<ISqlGenerationHelper>();
                    command.CommandText =
                        $"INSERT INTO {sql.DelimitIdentifier(table.Name, table.Schema)} ({string.Join(", ", columns.Select(c => sql.DelimitIdentifier(c)))}) " +
                        $"VALUES ({string.Join(", ", columns.Select((_, i) => $"@p{i}"))})";
                    try
                    {
                        command.ExecuteNonQuery();
                    }
                    catch (Exception ex)
                    {
                        throw new InvalidOperationException($"Couldn't seed table {table.Name}: {ex.Message}", ex);
                    }

                    foreach (var key in table.PrimaryKey?.Columns ?? [])
                    {
                        if (!values.ContainsKey(key.Name))
                            values[key.Name] = Scalar(connection, "SELECT last_insert_rowid()");
                    }
                    keys[(table.Name, company)] = values;
                }
            }
        }
        finally
        {
            Execute(connection, "PRAGMA foreign_keys = ON");
            db.Database.CloseConnection();
        }
        return tables.Select(t => t.Name).ToList();
    }

    /// <summary>Rows left in a table, whoever they belong to.</summary>
    public static long Count(DbContext db, string table)
    {
        var sql = db.GetService<ISqlGenerationHelper>();
        var schema = db.Model.GetRelationalModel().Tables.First(t => t.Name == table).Schema;
        var connection = db.Database.GetDbConnection();
        db.Database.OpenConnection();
        try
        {
            return Convert.ToInt64(Scalar(connection, $"SELECT COUNT(*) FROM {sql.DelimitIdentifier(table, schema)}"),
                System.Globalization.CultureInfo.InvariantCulture);
        }
        finally
        {
            db.Database.CloseConnection();
        }
    }

    /// <summary>Rows of one company left in a table that has the company column (null when it hasn't).</summary>
    public static long? CountFor(DbContext db, string table, Guid company)
    {
        var relational = db.Model.GetRelationalModel().Tables.First(t => t.Name == table);
        var column = relational.FindColumn(TenantColumn);
        if (column == null) return null;
        var sql = db.GetService<ISqlGenerationHelper>();
        var connection = db.Database.GetDbConnection();
        db.Database.OpenConnection();
        try
        {
            using var command = connection.CreateCommand();
            command.CommandText =
                $"SELECT COUNT(*) FROM {sql.DelimitIdentifier(table, relational.Schema)} WHERE {sql.DelimitIdentifier(column.Name)} = @company";
            command.Parameters.Add(column.PropertyMappings.First().Property.GetRelationalTypeMapping()
                .CreateParameter(command, "@company", company, nullable: false));
            return Convert.ToInt64(command.ExecuteScalar(), System.Globalization.CultureInfo.InvariantCulture);
        }
        finally
        {
            db.Database.CloseConnection();
        }
    }

    private static bool IsStoreGenerated(IProperty property) =>
        property.ValueGenerated == ValueGenerated.OnAdd
        && property.IsPrimaryKey()
        && property.ClrType is var type
        && (Nullable.GetUnderlyingType(type) ?? type) is var t
        && (t == typeof(int) || t == typeof(long) || t == typeof(short));

    private static object? ValueFor(
        ITable table, IColumn column, IProperty property, Guid company,
        Dictionary<(string Table, Guid Company), Dictionary<string, object?>> keys, ref int counter)
    {
        if (column.Name == TenantColumn && (Nullable.GetUnderlyingType(property.ClrType) ?? property.ClrType) == typeof(Guid))
            return company;

        if (table.FindColumn(TenantColumn) == null && ParentKey(table, column, company, keys) is { } parentKey)
            return parentKey;

        if (property.DeclaringType is IEntityType entity && entity.FindDiscriminatorProperty() == property)
            return entity.GetDiscriminatorValue();

        // A nullable column stays empty, unless it is part of a key.
        if (column.IsNullable && !property.IsKey()) return null;

        counter++;
        return Sample(property.ClrType, counter);
    }

    // The seeded parent's key, for a child-only table's single-column foreign key to a company table.
    private static object? ParentKey(
        ITable table, IColumn column, Guid company,
        Dictionary<(string Table, Guid Company), Dictionary<string, object?>> keys)
    {
        var fk = table.ForeignKeyConstraints.FirstOrDefault(f =>
            f.Columns.Count == 1 && f.Columns[0].Name == column.Name && f.PrincipalTable.FindColumn(TenantColumn) != null);
        if (fk == null || !keys.TryGetValue((fk.PrincipalTable.Name, company), out var parent)) return null;
        return parent.GetValueOrDefault(fk.PrincipalColumns[0].Name);
    }

    private static object Sample(Type type, int n)
    {
        var t = Nullable.GetUnderlyingType(type) ?? type;
        if (t.IsEnum) return Enum.GetValues(t).GetValue(0)!;
        if (t == typeof(string)) return $"s{n}";
        if (t == typeof(Guid)) return Guid.NewGuid();
        if (t == typeof(bool)) return false;
        if (t == typeof(int)) return n;
        if (t == typeof(long)) return (long)n;
        if (t == typeof(short)) return (short)(n % short.MaxValue);
        if (t == typeof(byte)) return (byte)(n % byte.MaxValue);
        if (t == typeof(decimal)) return (decimal)n;
        if (t == typeof(double)) return (double)n;
        if (t == typeof(float)) return (float)n;
        if (t == typeof(DateTime)) return new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc).AddMinutes(n);
        if (t == typeof(DateTimeOffset)) return new DateTimeOffset(2025, 1, 1, 0, 0, 0, TimeSpan.Zero).AddMinutes(n);
        if (t == typeof(DateOnly)) return new DateOnly(2025, 1, 1).AddDays(n % 300);
        if (t == typeof(TimeOnly)) return new TimeOnly(8, 0);
        if (t == typeof(TimeSpan)) return TimeSpan.FromHours(8);
        if (t == typeof(byte[])) return new byte[] { 1 };
        if (t.FullName?.StartsWith("NetTopologySuite.Geometries", StringComparison.Ordinal) == true)
            return new NetTopologySuite.Geometries.Point(0, 0) { SRID = 4326 };
        return Activator.CreateInstance(t)
            ?? throw new NotSupportedException($"No sample value for {t.Name}; teach {nameof(UniversalRowSeeder)} about it.");
    }

    private static void Execute(DbConnection connection, string sql)
    {
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        command.ExecuteNonQuery();
    }

    private static object? Scalar(DbConnection connection, string sql)
    {
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        return command.ExecuteScalar();
    }
}
