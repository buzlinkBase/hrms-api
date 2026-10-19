using System.Globalization;
using Hrms.Core.Services.Storage;

namespace Hrms.Core.Services.StorageManager;

/// <summary>
/// What a company file's key says about it -- <c>tenants/{companyId}/{area}/{yyyy}/{MM}/{id}-{name}</c>
/// (see <see cref="StorageKeys.ForNewFile"/>): the area (which feature stored it), the year it was
/// stored and its original name. The Storage Manager groups by area and year straight from this.
/// </summary>
public sealed record StorageKeyParts(string Area, int? Year, string FileName)
{
    /// <summary>The key's parts, or null when it isn't a file of <paramref name="tenantId"/>.</summary>
    public static StorageKeyParts? Parse(string key, Guid tenantId)
    {
        if (!StorageKeys.BelongsTo(key, tenantId)) return null;
        var parts = key[StorageKeys.TenantPrefix(tenantId).Length..].Split('/');
        if (parts.Length < 2) return null;

        var area = parts[0];
        var name = parts[^1];
        int? year = parts.Length >= 4
            && int.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out var y)
            && y is >= 2000 and <= 9999
                ? y
                : null;
        return new StorageKeyParts(area, year, WithoutUniqueId(name));
    }

    // "9c0f…(32 hex)-tor.pdf" -> "tor.pdf"; names stored some other way are kept as they are.
    private static string WithoutUniqueId(string name)
    {
        var dash = name.IndexOf('-');
        return dash == 32 && name[..dash].All(Uri.IsHexDigit) && name.Length > dash + 1
            ? name[(dash + 1)..]
            : name;
    }
}
