using Hrms.Domain.Entities;
using Onepunch.Common.Lib.Exceptions;

namespace Hrms.Core.Services.Storage;

/// <summary>
/// The company logo and employee profile photos, kept in file storage under the company
/// (<c>tenants/{companyId}/company-logo/…</c>, <c>…/employee-photos/…</c>); the database keeps
/// only the key. Replacing or removing one deletes the old file once the change is saved.
/// Reading an employee goes through the normal query filters, so a confidential employee the
/// caller can't see is "not found" here too.
/// </summary>
public class OrganizationImageService
{
    public const string LogoArea = "company-logo";
    public const string PhotoArea = "employee-photos";
    public const string ClientLogoArea = "client-logos";

    private readonly IUnitOfWorkService _uow;
    private readonly IFileStorage _storage;
    private readonly ITenantProvider _tenantProvider;
    private readonly ImageUrlResolver _urls;

    public OrganizationImageService(IUnitOfWorkService uow, IFileStorage storage, ITenantProvider tenantProvider, ImageUrlResolver urls)
    {
        _uow = uow;
        _storage = storage;
        _tenantProvider = tenantProvider;
        _urls = urls;
    }

    /// <summary>Stores a new photo for the employee and returns a link to show it.</summary>
    public async Task<string> SetEmployeePhotoAsync(Guid employeeId, Stream upload, long? length, string? fileName, CancellationToken token)
    {
        var employee = await FindEmployeeAsync(employeeId, token);
        var key = await ReplaceAsync(PhotoArea, upload, length, fileName ?? "photo", employee.ProfileImg,
            newKey => employee.ProfileImg = newKey, token);
        return await _urls.DisplayUrlAsync(key, token);
    }

    public async Task RemoveEmployeePhotoAsync(Guid employeeId, CancellationToken token)
    {
        var employee = await FindEmployeeAsync(employeeId, token);
        var previous = employee.ProfileImg;
        employee.ProfileImg = string.Empty;
        await _uow.CommitChangesAsync("", token);
        await DeleteQuietlyAsync(previous, token);
    }

    /// <summary>Stores a new company logo and returns a link to show it.</summary>
    public async Task<string> SetCompanyLogoAsync(Stream upload, long? length, string? fileName, CancellationToken token)
    {
        var company = await FindOrCreateCompanyAsync(token);
        var key = await ReplaceAsync(LogoArea, upload, length, fileName ?? "logo", company.LogoKey,
            newKey => company.LogoKey = newKey, token);
        return await _urls.DisplayUrlAsync(key, token);
    }

    public async Task RemoveCompanyLogoAsync(CancellationToken token)
    {
        var company = await _uow.Context.Companies.FirstOrDefaultAsync(token);
        if (company?.LogoKey == null) return;
        var previous = company.LogoKey;
        company.LogoKey = null;
        await _uow.CommitChangesAsync("", token);
        await DeleteQuietlyAsync(previous, token);
    }

    /// <summary>Stores a new logo for the client and returns a link to show it.</summary>
    public async Task<string> SetClientLogoAsync(Guid clientId, Stream upload, long? length, string? fileName, CancellationToken token)
    {
        var client = await FindClientAsync(clientId, token);
        var key = await ReplaceAsync(ClientLogoArea, upload, length, fileName ?? "logo", client.LogoKey,
            newKey => client.LogoKey = newKey, token);
        return await _urls.DisplayUrlAsync(key, token);
    }

    public async Task RemoveClientLogoAsync(Guid clientId, CancellationToken token)
    {
        var client = await FindClientAsync(clientId, token);
        if (client.LogoKey == null) return;
        var previous = client.LogoKey;
        client.LogoKey = null;
        await _uow.CommitChangesAsync("", token);
        await DeleteQuietlyAsync(previous, token);
    }

    // Upload first, then save the key: a failed save deletes the new file again, and the old one
    // is only deleted once nothing points at it any more.
    private async Task<string> ReplaceAsync(
        string area, Stream upload, long? length, string fileName, string? previous,
        Action<string> point, CancellationToken token)
    {
        var tenantId = _tenantProvider.TenantId;
        if (tenantId == Guid.Empty) throw new GuardException("No company is selected.");

        var (content, contentType) = await ImageUploads.ReadAsync(upload, length, token);
        await using (content)
        {
            var name = Path.GetFileNameWithoutExtension(fileName) + ImageUploads.ExtensionFor(contentType);
            var key = StorageKeys.ForNewFile(tenantId, area, name);
            await _storage.UploadAsync(new FileUpload(key, content, contentType), token);
            try
            {
                point(key);
                await _uow.CommitChangesAsync("", token);
            }
            catch
            {
                await DeleteQuietlyAsync(key, CancellationToken.None);
                throw;
            }
            await DeleteQuietlyAsync(previous, token);
            return key;
        }
    }

    private async Task<Hrms.Domain.Entities.EmployeeEntities.Employee> FindEmployeeAsync(Guid employeeId, CancellationToken token) =>
        await _uow.Context.Employees.FirstOrDefaultAsync(e => e.Id == employeeId, token)
        ?? throw new NotFoundException("Employee not found.");

    private async Task<Client> FindClientAsync(Guid clientId, CancellationToken token) =>
        await _uow.Context.Clients.FirstOrDefaultAsync(c => c.Id == clientId, token)
        ?? throw new NotFoundException("Client not found.");

    private async Task<Company> FindOrCreateCompanyAsync(CancellationToken token)
    {
        var company = await _uow.Context.Companies.FirstOrDefaultAsync(token);
        if (company != null) return company;
        // Same defaults as CompanyService.SaveAsync for a company with no info saved yet.
        company = new Company { Code = "Main" };
        await _uow.Context.Companies.AddAsync(company, token);
        return company;
    }

    /// <summary>
    /// Deletes a file this company owns. Best-effort: a leftover file is harmless, a failed
    /// request after the change was already saved isn't. Legacy data: URLs aren't files.
    /// </summary>
    private async Task DeleteQuietlyAsync(string? key, CancellationToken token)
    {
        if (!StorageKeys.BelongsTo(key, _tenantProvider.TenantId)) return;
        try
        {
            await _storage.DeleteAsync(key!, token);
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Couldn't delete replaced image {Key}", key);
        }
    }
}
