using Hrms.Domain.Entities;

namespace DTR.Core;

public record struct SettingRecordKey(string IdentityType, string? IdentityTypeId, string Description);
public class GeneralSettingService : BaseService<GeneralSetting>
{
    public GeneralSettingService(IUnitOfWorkService uow) : base(uow) { }
    public async Task<Dictionary<string, GeneralSettingModel>> GetSettingsAsync(string identityType)
    {
        var settingsList = await Uow.Repository
            .FindAll<GeneralSetting>()
            .Where(x => x.IdentityType == identityType)
            .ToListAsync();

        return settingsList
            .GroupBy(x => x.Description)
            .ToDictionary(
                group => group.Key,
                group =>
                {
                    var firstItem = group.First();
                    return new GeneralSettingModel
                    {
                        Key = group.Key,
                        Metadata = firstItem.Metadata,
                        IdentityId = firstItem.IdentityTypeId,
                        Value = firstItem.Value
                    };
                });
    }

    public async Task<Dictionary<SettingGroupKey, Dictionary<string, GeneralSettingModel>>> GetSettingsAsync(
    string identityType,
    HashSet<string>? validIdentities)
    {
        if (validIdentities == null || !validIdentities.Any())
            return new Dictionary<SettingGroupKey, Dictionary<string, GeneralSettingModel>>();

        // 1. Fetch filtered entities from database into memory
        var settingsList = await Uow.Repository
            .FindAll<GeneralSetting>()
            .Where(x => x.IdentityType == identityType && x.IdentityTypeId != null && validIdentities.Contains(x.IdentityTypeId))
            .ToListAsync();

        // 2. Build the nested dictionary safely in-memory
        return settingsList
            .Where(x => !string.IsNullOrEmpty(x.IdentityTypeId) && Guid.TryParse(x.IdentityTypeId, out _))
            .GroupBy(x => x.IdentityTypeId!)
            .ToDictionary(
                group => new SettingGroupKey(Guid.Parse(group.Key)),
                group => group
                    .GroupBy(x => x.Description)
                    .ToDictionary(
                        subGroup => subGroup.Key,
                        subGroup =>
                        {
                            var firstItem = subGroup.First();
                            return new GeneralSettingModel
                            {
                                Key = subGroup.Key,
                                Metadata = firstItem.Metadata,
                                IdentityId = firstItem.IdentityTypeId,
                                Value = firstItem.Value
                            };
                        })
            );
    }

    public async Task ReplaceByIdentityTypeAsync(string identityType, List<GeneralSetting> incoming, string? IdentityTypeId, CancellationToken token = default)
    {
        var existing = await GetQueryable(x => x.IdentityType == identityType && x.IdentityTypeId == IdentityTypeId)
            .ToListAsync(token);
        Context.GeneralSettings.RemoveRange(existing);
        if (incoming.Any())
        {
            await Context.GeneralSettings.AddRangeAsync(incoming, token);
        }
        await CommitChangesAsync(token);
    }
    public async Task DeleteAsync(string identityType, string identityId, CancellationToken token = default)
    {
        await ExecuteDeleteAsync(x => x.IdentityType == identityType && x.IdentityTypeId == identityId, token);
    }
}

public record struct SettingGroupKey(Guid IdentityId);
public record SettingCompositeKey(string Description, string? IdentityTypeId);