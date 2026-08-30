using Hrms.Core.Validations;
using Hrms.Domain.Entities;
using Hrms.Domain.ValueObjects;
using Mapster;

namespace Hrms.Core.Services;

public class CompanyService : BaseService<Company>
{
    public CompanyService(IUnitOfWorkService uow) : base(uow)
    {
    }

    protected override async Task<EvaluationResult> CreateValidatorAsync(Company model, CancellationToken token)
    {
        var validator = new CompanyValidator().Validate(model);
        if (!validator.IsValid)
        {
            return EvaluationResult.Fail(validator.Errors);
        }
        return await base.CreateValidatorAsync(model, token);
    }
    public async Task AddAsync(Company model, CancellationToken token)
    {
        await CreateAsync(model, token);
        _uow.SaveChanges();
    }
    public async Task UpdateAsync(Company model, CancellationToken token)
    {
        await ModifyAsync(model, token);
    }
    public async Task AddOrUpdateAsync(Company model, CancellationToken token)
    {
        await CreateOrUpdateAsync(model, token);
    }
    public async Task<List<Company>> FindAllAsync()
    {
        return await GetQueryable().ToListAsync();
    }
    public async Task<Company?> FineOneAsync(CancellationToken token)
    {
        return await GetQueryable().FirstOrDefaultAsync(token);
    }

    // Get-or-default + upsert for the Company Setup screen — one Company row per tenant.
    // Adapts onto the existing entity (not a fresh one) so payroll-policy fields not
    // carried by UpdateCompany (TotalWorkingDays/TakehomePercentage/ApplyStatutoryOnActualMonth)
    // aren't silently zeroed out — this codebase's established update convention.
    // Code isn't shown to the user; it's only there because the legacy validator requires
    // it, so it's auto-defaulted here.
    public async Task<Company?> SaveAsync(CreateCompany payload, CancellationToken token)
    {
        var existing = await FineOneAsync(token);
        if (existing != null)
        { 
            payload.Adapt(existing);
            if (string.IsNullOrEmpty(existing.Code))
            {
                existing.Code = "Main";
            }
            await ModifyAsync(existing, token);
        }
        else
        {
            var company = new Company();
            payload.Adapt(company);
            if (string.IsNullOrEmpty(company.Code))
            {
                company.Code = "Main";
            }
            await CreateAsync(company, token);
        }
        await CommitChangesAsync(token);
        return await Context.Companies.FirstOrDefaultAsync(token);
    }
    public async Task DeleteAync(Guid Id, CancellationToken token)
    {
        await RemoveAsync(Id, token);
    }

    // Single source of truth for what a brand-new tenant's default Company row looks
    // like — called from both provisioning strategies in TenantCreationCompletedWorker so
    // that logic lives here once instead of being duplicated across them. Code isn't
    // user-facing; it's only there because CompanyValidator requires it.
    //
    // Idempotent by design: TenantCreationCompleted can be redelivered by MassTransit's
    // retry policy (DedicatedProvisioner explicitly rethrows to trigger a retry on
    // failure), so this must be safe to run more than once for the same tenant. Skipping
    // when a row already exists both prevents a duplicate Company and avoids clobbering
    // any Company Info the tenant may have already saved before a delayed retry fires.
    public async Task SeedDefaultAsync(string? tenantName, CancellationToken token)
    {
        var existing = await FineOneAsync(token);
        if (existing != null) return;

        await AddAsync(new Company
        {
            Code = "MAIN",
            Description = tenantName ?? "My Workspace",
        }, token);
    }
}

