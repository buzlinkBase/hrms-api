using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Storage;

namespace Hrms.Core.Services.DataPurge;

/// <summary>What the purge does with a table.</summary>
public enum CompanyTableKind
{
    /// <summary>Payroll, employee, attendance, tax and setup records: written to the archive, then deleted.</summary>
    Compliance,
    /// <summary>Notifications and announcements: deleted, not archived -- nothing the law asks to keep.</summary>
    Operational,
    /// <summary>Archived but left in place (the company's own row), so the company still opens.</summary>
    Kept,
}

/// <summary>
/// One table holding a company's rows, with the SQL to read, count and delete just that company's
/// part of it. A table with a TenantId column is filtered on it; a table without one (a join table)
/// is filtered through its foreign key to a table that has.
/// </summary>
public sealed record CompanyTable(
    string Name,
    CompanyTableKind Kind,
    string SelectSql,
    string CountSql,
    string DeleteSql,
    string? MaxCreatedSql,
    int Depth);

/// <summary>
/// Finds every table of the HR database that holds a company's data, by reading the EF model -- so a
/// table added next year is archived and purged without anyone remembering to list it. Not tied to
/// MySQL: identifiers are quoted by the provider's own SQL helper.
/// </summary>
public sealed class CompanyDataCatalog
{
    /// <summary>The parameter every statement filters on.</summary>
    public const string TenantParameter = "@tenant";

    // Entities that aren't records the law asks employers to keep.
    private static readonly HashSet<string> OperationalEntities =
        ["UserNotification", "Announcement", "AnnouncementAudienceRule", "AnnouncementRecipient"];

    // The company's own row stays, so signing in to a purged company still shows its name.
    private static readonly HashSet<string> KeptEntities = ["Company"];

    /// <summary>The only tables expected in <see cref="Unscoped"/>: messaging, and the audit trail (an
    /// erasure deletes it by organization). Anything else there is company data the purge would leave.</summary>
    public static readonly IReadOnlySet<string> InfrastructureTables =
        new HashSet<string>(StringComparer.Ordinal) { "InboxState", "OutboxMessage", "OutboxState", "AuditEvents" };

    public IReadOnlyList<CompanyTable> Tables { get; }

    /// <summary>Tables that hold company data but couldn't be tied to a company -- not touched, so
    /// someone can look at them.</summary>
    public IReadOnlyList<string> Unscoped { get; }

    private CompanyDataCatalog(IReadOnlyList<CompanyTable> tables, IReadOnlyList<string> unscoped)
    {
        Tables = tables;
        Unscoped = unscoped;
    }

    public IEnumerable<CompanyTable> ToArchive => Tables.Where(t => t.Kind != CompanyTableKind.Operational);

    /// <summary>Children before parents, so foreign keys are never left pointing at a deleted row.</summary>
    public IEnumerable<CompanyTable> ToDelete => Tables
        .Where(t => t.Kind != CompanyTableKind.Kept)
        .OrderByDescending(t => t.Depth)
        .ThenBy(t => t.Name, StringComparer.Ordinal);

    /// <summary>Every table, the kept company row included (last, as everything refers to it) -- for an
    /// operator's erasure, which leaves nothing behind.</summary>
    public IEnumerable<CompanyTable> ToErase => Tables
        .OrderByDescending(t => t.Depth)
        .ThenBy(t => t.Name, StringComparer.Ordinal);

    public static CompanyDataCatalog Build(IModel model, ISqlGenerationHelper sql)
    {
        var groups = model.GetEntityTypes()
            .Where(t => t.GetTableName() != null)
            .GroupBy(t => (Table: t.GetTableName()!, Schema: t.GetSchema()))
            .Select(g => new Group(g.Key.Table, g.Key.Schema, g.ToList()))
            .ToList();

        var direct = groups.Where(g => g.TenantColumn != null).ToDictionary(g => g.Key);
        var tables = new List<(Group Group, string Where)>();
        var unscoped = new List<string>();

        foreach (var group in groups)
        {
            if (group.TenantColumn is { } column)
            {
                tables.Add((group, $"{sql.DelimitIdentifier(column)} = {TenantParameter}"));
                continue;
            }

            // No TenantId: scoped through a single-column foreign key to a table that has one.
            var parent = group.Types
                .SelectMany(t => t.GetForeignKeys())
                .Where(fk => fk.Properties.Count == 1)
                .Select(fk => (Fk: fk, Principal: fk.PrincipalEntityType))
                .Where(x => x.Principal.GetTableName() is { } name && direct.ContainsKey(Key(name, x.Principal.GetSchema())))
                .Select(x => (x.Fk, Parent: direct[Key(x.Principal.GetTableName()!, x.Principal.GetSchema())]))
                .FirstOrDefault();
            if (parent.Fk == null)
            {
                unscoped.Add(group.Table);
                continue;
            }

            var fkColumn = parent.Fk.Properties[0].GetColumnName(StoreObjectIdentifier.Table(group.Table, group.Schema))!;
            var keyColumn = parent.Fk.PrincipalKey.Properties[0].GetColumnName(StoreObjectIdentifier.Table(parent.Parent.Table, parent.Parent.Schema))!;
            var subquery =
                $"SELECT {sql.DelimitIdentifier(keyColumn)} FROM {Quote(sql, parent.Parent.Table, parent.Parent.Schema)} " +
                $"WHERE {sql.DelimitIdentifier(parent.Parent.TenantColumn!)} = {TenantParameter}";
            tables.Add((group, $"{sql.DelimitIdentifier(fkColumn)} IN ({subquery})"));
        }

        var depths = Depths(groups.Where(g => tables.Any(t => t.Group == g)).ToList());
        var catalog = tables
            .Select(t =>
            {
                var quoted = Quote(sql, t.Group.Table, t.Group.Schema);
                var createdAt = t.Group.CreatedAtColumn;
                return new CompanyTable(
                    t.Group.Table,
                    t.Group.Kind,
                    $"SELECT * FROM {quoted} WHERE {t.Where}",
                    $"SELECT COUNT(*) FROM {quoted} WHERE {t.Where}",
                    $"DELETE FROM {quoted} WHERE {t.Where}",
                    createdAt == null ? null : $"SELECT MAX({sql.DelimitIdentifier(createdAt)}) FROM {quoted} WHERE {t.Where}",
                    depths.GetValueOrDefault(t.Group.Key));
            })
            .OrderBy(t => t.Name, StringComparer.Ordinal)
            .ToList();

        return new CompanyDataCatalog(catalog, unscoped);
    }

    /// <summary>A command filtered to one company.</summary>
    public static DbCommand CreateCommand(DbConnection connection, string sql, Guid tenantId)
    {
        var command = connection.CreateCommand();
        command.CommandText = sql;
        var parameter = command.CreateParameter();
        parameter.ParameterName = TenantParameter;
        parameter.Value = tenantId;
        command.Parameters.Add(parameter);
        return command;
    }

    /// <summary>The SQL as EF's raw-SQL API wants it: the company as {0}.</summary>
    public static string ForEf(string sql) => sql.Replace(TenantParameter, "{0}", StringComparison.Ordinal);

    private static string Quote(ISqlGenerationHelper sql, string table, string? schema) =>
        sql.DelimitIdentifier(table, schema);

    private static (string, string?) Key(string table, string? schema) => (table, schema);

    // How many foreign keys deep a table is below its parents; deleted deepest first.
    private static Dictionary<(string, string?), int> Depths(List<Group> groups)
    {
        var included = groups.ToDictionary(g => g.Key);
        var depths = new Dictionary<(string, string?), int>();

        int DepthOf((string, string?) key, HashSet<(string, string?)> path)
        {
            if (depths.TryGetValue(key, out var known)) return known;
            if (!path.Add(key)) return 0; // a cycle: stop here
            var parents = included[key].Types
                .SelectMany(t => t.GetForeignKeys())
                .Select(fk => Key(fk.PrincipalEntityType.GetTableName() ?? "", fk.PrincipalEntityType.GetSchema()))
                .Where(p => p != key && included.ContainsKey(p))
                .Distinct()
                .ToList();
            var depth = parents.Count == 0 ? 0 : parents.Max(p => DepthOf(p, path)) + 1;
            path.Remove(key);
            return depths[key] = depth;
        }

        foreach (var group in groups) DepthOf(group.Key, []);
        return depths;
    }

    private sealed class Group(string table, string? schema, List<IEntityType> types)
    {
        public string Table { get; } = table;
        public string? Schema { get; } = schema;
        public List<IEntityType> Types { get; } = types;
        public (string, string?) Key => (Table, Schema);

        public string? TenantColumn => Types
            .Select(t => t.FindProperty("TenantId"))
            .FirstOrDefault(p => p != null && p.ClrType == typeof(Guid))
            ?.GetColumnName(StoreObjectIdentifier.Table(Table, Schema));

        public string? CreatedAtColumn => Types
            .Select(t => t.FindProperty("CreatedAt"))
            .FirstOrDefault(p => p != null && (p.ClrType == typeof(DateTime) || p.ClrType == typeof(DateTime?)))
            ?.GetColumnName(StoreObjectIdentifier.Table(Table, Schema));

        public CompanyTableKind Kind =>
            Types.Any(t => KeptEntities.Contains(t.ClrType.Name)) ? CompanyTableKind.Kept
            : Types.Any(t => OperationalEntities.Contains(t.ClrType.Name)) ? CompanyTableKind.Operational
            : CompanyTableKind.Compliance;
    }
}
