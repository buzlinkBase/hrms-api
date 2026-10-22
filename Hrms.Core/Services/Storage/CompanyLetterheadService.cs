using Hrms.Domain.Entities;

namespace Hrms.Core.Services.Storage;

/// <summary>
/// The company logo as picture bytes for printed documents (payslips), which are drawn
/// synchronously and so can't open file storage themselves. Nothing is returned -- and the printout
/// keeps its plain text header -- when the company switched the letterhead off, has no logo, or the
/// stored file can't be read or is unreasonably large.
/// </summary>
public class CompanyLetterheadService
{
    /// <summary>Logos are limited to 2 MB on upload; anything bigger than this is not printed.</summary>
    public const int MaxLogoBytes = 2 * 1024 * 1024;

    private readonly IFileStorage _storage;

    public CompanyLetterheadService(IFileStorage storage) => _storage = storage;

    public async Task<byte[]?> LoadLogoAsync(Company? company, CancellationToken token = default)
    {
        if (company is not { ShowLetterhead: true } || !StorageKeys.IsValid(company.LogoKey))
            return null;

        try
        {
            await using var content = await _storage.OpenReadAsync(company.LogoKey!, token);
            if (content is null) return null;

            using var copy = new MemoryStream();
            await content.CopyToAsync(copy, token);
            return copy.Length is > 0 and <= MaxLogoBytes ? copy.ToArray() : null;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // A payslip must still print when the logo can't be fetched.
            return null;
        }
    }
}
