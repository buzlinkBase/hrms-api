namespace Hrms.adms.Core;
public class BioTemplateService : BaseService<BiometricTemplate>
{
    public BioTemplateService(IUnitOfWorkService uow ) : base(uow)
    {
    }
    public async Task AddRangeTemplate(List<CreateBiometricTemplate> models, CancellationToken token)
    {
        foreach (var model in models)
        {
            // Look for existing record to update instead of duplicate insert
            var existing = await Context.BiometricTemplates
                .FirstOrDefaultAsync(x => x.BioId == model.BioId &&
                                         x.BioType == model.BioType &&
                                         x.BioIndex == model.BioIndex, token);

            if (existing != null)
            {
                existing.TemplateData = model.TemplateData;
                existing.TemplateSize = model.TemplateSize;
            }
            else
            {
                Context.BiometricTemplates.Add(new BiometricTemplate
                {
                    BioId = model.BioId,
                    BioType = model.BioType,
                    BioIndex = model.BioIndex,
                    TemplateSize = model.TemplateSize,
                    TemplateData = model.TemplateData
                });
            }
        }
        await Context.SaveChangesAsync(token);
        await CommitChangesAsync(token);
    }


    public async Task DeleteAsync(Guid Id, CancellationToken token)
    {
        await RemoveAsync(Id, token);
        await CommitChangesAsync(token);
    }
}

