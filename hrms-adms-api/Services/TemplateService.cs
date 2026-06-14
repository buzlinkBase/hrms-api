namespace Hrms.adms.Services;

public class TemplateService : BaseService<BiometricTemplate>
{
    private readonly DeviceService _deviceService;
    public TemplateService(IUnitOfWorkService uow,
        DeviceService deviceService) : base(uow)
    {
        _deviceService = deviceService;
    }
    public async Task AddRangeTemplate(List<CreateBiometricTemplate> models, CancellationToken token)
    {
        if (models == null || models.Count == 0) return;

        // Extract individual filter sets
        var tenantId = (models.First().TenantId);
        var bioIds = models.Select(m => m.BioId).Distinct().ToList();
        var bioTypes = models.Select(m => m.BioType).Distinct().ToList();
        var bioIndexes = models.Select(m => m.BioIndex).Distinct().ToList();

        // EF Core can translate this into SQL IN clauses
        var existing = await Context.BiometricTemplates
            .Where(x => x.TenantId == tenantId &&
                        bioIds.Contains(x.BioId))
            .ToListAsync(token);

        // Build dictionary for O(1) lookup
        var existingDict = existing.ToDictionary(
            x => (x.TenantId, x.BioId, x.BioType, x.BioIndex)
        );

        foreach (var model in models)
        {
            var key = (model.TenantId, model.BioId, model.BioType, model.BioIndex);

            if (existingDict.TryGetValue(key, out var record))
            {
                record.TemplateData = model.TemplateData;
                record.TemplateSize = model.TemplateSize;
            }
            else
            {
                var newTemplate = new BiometricTemplate
                {
                    TenantId = model.TenantId,
                    BioId = model.BioId,
                    BioType = model.BioType,
                    BioIndex = model.BioIndex,
                    TemplateSize = model.TemplateSize,
                    TemplateData = model.TemplateData
                };
                Context.BiometricTemplates.Add(newTemplate);
                existingDict[key] = newTemplate;
            }
        }

        await Context.SaveChangesAsync(token);
        await CommitChangesAsync(token);
    }

    public Task<List<BiometricTemplate>> FindAll(string sn, CancellationToken token)
    {
        return GetQueryable(x => x.SN == sn).ToListAsync(token);
    }
    public async Task Transfer(string SN, Guid tenantId, CancellationToken token)
    {
        //TODO create command for transfer
        var device = await _deviceService.FindSnAsync(SN, token);
        if (device == null) return;

        //var command = new DeviceCommandPayload
        //{
        //    CommandMessage = "",
        //    DeviceSN = SN,
        //    TenantId = device.TenantId
        //};
        //await _deviceService.CreateCommand(new List<DeviceCommandPayload> { command });
    }
    public async Task DeleteAsync(Guid Id, CancellationToken token)
    {
        await RemoveAsync(Id, token);
        await CommitChangesAsync(token);
    }
}

