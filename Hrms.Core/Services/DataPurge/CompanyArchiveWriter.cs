using System.Data.Common;
using System.Globalization;
using System.IO.Compression;
using System.Text;
using System.Text.Json;

namespace Hrms.Core.Services.DataPurge;

/// <summary>How many rows of one table went into the archive.</summary>
public sealed record ArchivedTable(string Table, long Rows);

/// <summary>What was written: per-table counts, and the latest entry in any of them.</summary>
public sealed record CompanyArchiveSummary(Guid TenantId, DateTime CreatedAtUtc, IReadOnlyList<ArchivedTable> Tables, DateTime? LastEntryUtc)
{
    public long TotalRows => Tables.Sum(t => t.Rows);
}

/// <summary>
/// Writes a company's records as one zip: a CSV per table (<c>data/{table}.csv</c>, header row
/// first) and a <c>manifest.json</c> with the row counts. Plain CSV so the archive can be read
/// years from now with nothing but a spreadsheet -- and by this code's counterpart, if a company
/// asks for its records back. Streams each table, so a large company doesn't sit in memory.
/// </summary>
public sealed class CompanyArchiveWriter
{
    private static readonly UTF8Encoding Utf8 = new(false);
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { WriteIndented = true };

    public async Task<CompanyArchiveSummary> WriteAsync(
        DbConnection connection, CompanyDataCatalog catalog, Guid tenantId, Stream destination, DateTime nowUtc, CancellationToken token)
    {
        var tables = new List<ArchivedTable>();
        DateTime? lastEntry = null;

        using (var zip = new ZipArchive(destination, ZipArchiveMode.Create, leaveOpen: true))
        {
            foreach (var table in catalog.ToArchive)
            {
                var rows = await WriteTableAsync(connection, zip, table, tenantId, token);
                if (rows == 0) continue;
                tables.Add(new ArchivedTable(table.Name, rows));

                if (table.MaxCreatedSql != null && await LatestAsync(connection, table.MaxCreatedSql, tenantId, token) is { } latest
                    && (lastEntry == null || latest > lastEntry))
                    lastEntry = latest;
            }

            var summary = new CompanyArchiveSummary(tenantId, nowUtc, tables, lastEntry);
            var manifest = zip.CreateEntry("manifest.json", CompressionLevel.Optimal);
            await using var stream = manifest.Open();
            await JsonSerializer.SerializeAsync(stream, new
            {
                summary.TenantId,
                summary.CreatedAtUtc,
                summary.LastEntryUtc,
                summary.TotalRows,
                Tables = summary.Tables,
                Note = "One CSV per table under data/. Dates are UTC. Binary values are base64.",
            }, Json, token);
            return summary;
        }
    }

    private static async Task<long> WriteTableAsync(
        DbConnection connection, ZipArchive zip, CompanyTable table, Guid tenantId, CancellationToken token)
    {
        await using var command = CompanyDataCatalog.CreateCommand(connection, table.SelectSql, tenantId);
        await using var reader = await command.ExecuteReaderAsync(token);
        if (!reader.HasRows) return 0;

        var entry = zip.CreateEntry($"data/{table.Name}.csv", CompressionLevel.Optimal);
        await using var stream = entry.Open();
        await using var writer = new StreamWriter(stream, Utf8);

        var columns = Enumerable.Range(0, reader.FieldCount).Select(reader.GetName).ToList();
        await writer.WriteLineAsync(string.Join(',', columns.Select(Escape)));

        long rows = 0;
        var values = new object[reader.FieldCount];
        while (await reader.ReadAsync(token))
        {
            reader.GetValues(values);
            await writer.WriteLineAsync(string.Join(',', values.Select(v => Escape(Format(v)))));
            rows++;
        }
        return rows;
    }

    private static async Task<DateTime?> LatestAsync(DbConnection connection, string sql, Guid tenantId, CancellationToken token)
    {
        await using var command = CompanyDataCatalog.CreateCommand(connection, sql, tenantId);
        return ToUtc(await command.ExecuteScalarAsync(token));
    }

    /// <summary>A date from the database (a DateTime, or text on providers without a date type), as UTC.</summary>
    public static DateTime? ToUtc(object? value) => value switch
    {
        null or DBNull => null,
        DateTime date => DateTime.SpecifyKind(date, DateTimeKind.Utc),
        _ when DateTime.TryParse(value.ToString(), CultureInfo.InvariantCulture,
            DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var parsed) => parsed,
        _ => null,
    };

    private static string Format(object? value) => value switch
    {
        null or DBNull => "",
        DateTime date => date.ToString("O", CultureInfo.InvariantCulture),
        DateTimeOffset offset => offset.ToString("O", CultureInfo.InvariantCulture),
        byte[] bytes => Convert.ToBase64String(bytes),
        bool flag => flag ? "true" : "false",
        IFormattable formattable => formattable.ToString(null, CultureInfo.InvariantCulture),
        _ => value.ToString() ?? "",
    };

    private static string Escape(string value) =>
        value.AsSpan().IndexOfAny(",\"\r\n") >= 0 ? $"\"{value.Replace("\"", "\"\"")}\"" : value;
}
