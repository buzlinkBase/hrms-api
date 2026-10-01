using System.Text;
using System.Text.RegularExpressions;

namespace Hrms.Core.Services.Storage;

/// <summary>
/// Builds and checks storage keys. Every company's files live under
/// <c>tenants/{companyId}/</c>, so a key from one company can never point into another's.
/// A new file gets a unique id in its key, so two uploads with the same name never overwrite
/// each other, and the original name is kept (made safe) so the file is recognizable.
/// </summary>
public static partial class StorageKeys
{
    public const int MaxKeyLength = 512;

    /// <summary>
    /// A key for a new file of <paramref name="tenantId"/>:
    /// <c>tenants/{tenantId}/{area}/{yyyy}/{MM}/{id}-{name}</c>, e.g.
    /// <c>tenants/5f…/employee-documents/2026/10/9c…-tor.pdf</c>.
    /// </summary>
    /// <param name="area">What the file is for, e.g. "employee-documents" or "exports".</param>
    public static string ForNewFile(Guid tenantId, string area, string fileName, DateTime? utcNow = null)
    {
        if (tenantId == Guid.Empty)
            throw new ArgumentException("A file must belong to a company.", nameof(tenantId));
        var safeArea = Segment(area);
        if (safeArea.Length == 0)
            throw new ArgumentException("A file needs an area, e.g. \"employee-documents\".", nameof(area));

        var now = utcNow ?? DateTime.UtcNow;
        var key = $"{TenantPrefix(tenantId)}{safeArea}/{now:yyyy}/{now:MM}/{Guid.NewGuid():N}-{SafeFileName(fileName)}";
        return key.Length <= MaxKeyLength ? key : key[..MaxKeyLength];
    }

    /// <summary>The folder all of a company's files are under.</summary>
    public static string TenantPrefix(Guid tenantId) => $"tenants/{tenantId:D}/";

    /// <summary>
    /// Whether <paramref name="key"/> is a well-formed key of <paramref name="tenantId"/> -- check
    /// this before acting on a key that came from a request, never trust it as-is.
    /// </summary>
    public static bool BelongsTo(string? key, Guid tenantId) =>
        IsValid(key) && key!.StartsWith(TenantPrefix(tenantId), StringComparison.Ordinal);

    /// <summary>Relative, no "..", no backslashes or control characters, not too long.</summary>
    public static bool IsValid(string? key) =>
        !string.IsNullOrWhiteSpace(key) &&
        key.Length <= MaxKeyLength &&
        !key.StartsWith('/') &&
        !key.Contains('\\') &&
        !key.Split('/').Any(part => part is "" or "." or "..") &&
        !key.Any(char.IsControl);

    /// <summary>
    /// A file name that's safe in a key and a URL: letters, digits, dot, dash and underscore;
    /// anything else becomes a dash. Keeps the extension; never empty.
    /// </summary>
    public static string SafeFileName(string? fileName)
    {
        var name = Path.GetFileName((fileName ?? string.Empty).Replace('\\', '/'));
        var extension = Path.GetExtension(name);
        var stem = Path.GetFileNameWithoutExtension(name);

        var safeStem = Segment(stem);
        if (safeStem.Length > 80) safeStem = safeStem[..80].TrimEnd('-');
        var safeExtension = Segment(extension.TrimStart('.'));
        if (safeExtension.Length > 10) safeExtension = safeExtension[..10];

        if (safeStem.Length == 0) safeStem = "file";
        return safeExtension.Length == 0 ? safeStem : $"{safeStem}.{safeExtension.ToLowerInvariant()}";
    }

    private static string Segment(string? value)
    {
        var normalized = (value ?? string.Empty).Normalize(NormalizationForm.FormD);
        var ascii = new string(normalized.Where(c => c < 128).ToArray());
        return UnsafeCharacters().Replace(ascii, "-").Trim('-', '.', '_');
    }

    [GeneratedRegex("[^A-Za-z0-9._-]+")]
    private static partial Regex UnsafeCharacters();
}
