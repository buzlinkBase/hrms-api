using Hrms.adms.Models.DTO;

namespace Hrms.adms.Services;
public class DeviceService : BaseService<BiometricDevice>
{
    public DeviceService(IUnitOfWorkService uow) : base(uow)
    {
    }
    public async Task<UpdateBiometricDevice> AddAsync(CreateBiometricDevice payload, Guid TenantId, CancellationToken token)
    {
        var model = new BiometricDevice
        {
            SN = payload.SN,
            TenantId = TenantId,
        };
        await CreateAsync(model, token);
        await CommitChangesAsync(token);
        return new UpdateBiometricDevice
        {
            Id = model.Id,
            SN = payload.SN,
            Status = model.Status,
        };
    }
    public async Task UpdateDeviceInfo(ZkDeviceModel payload, CancellationToken token)
    {
        string sn = payload.DeviceInfo.SN;
        // 1. Update the root Biometric Device identity table

        await ModifyAsync(payload.DeviceInfo, token);

        // 2. Perform flat table updates using highly efficient ExecuteUpdateAsync
        await UpsertSystemCounters(sn, payload.SystemCounters, token);
        await UpsertPhotosAndMedias(sn, payload.PhotosAndMedia, token);
        await UpsertFeaturesAndProtocols(sn, payload.FeaturesAndProtocols, token);

        // 3. Handle the deep-nested structures that are independent DB entities
        await UpsertQrCodeConfig(sn, payload.FeaturesAndProtocols.QrCode, token);
        await UpsertThermalAndMaskConfig(sn, payload.FeaturesAndProtocols.ThermalAndMask, token);
        await UpsertMultiBioSupport(sn, payload.FeaturesAndProtocols.ConfigSupport, token);

        // 4. Update the structural Parent Biometrics grouping table
        var biometricsRows = await Context.Biometrics
            .Where(b => b.SN == sn)
            .ExecuteUpdateAsync(setters => setters.SetProperty(b => b.SN, sn), token);

        if (biometricsRows == 0)
        {
            Context.Biometrics.Add(payload.Biometrics);
            await Context.SaveChangesAsync(token);
        }

        // 5. Update or insert specific records into the relational BiometricDetails table
        await UpsertBiometricDetailAsync(sn, BiometricType.Fingerprint, payload.Biometrics.Fingerprint, token);
        await UpsertBiometricDetailAsync(sn, BiometricType.Face, payload.Biometrics.Face, token);
        await UpsertBiometricDetailAsync(sn, BiometricType.FingerVein, payload.Biometrics.FingerVein, token);
        await UpsertBiometricDetailAsync(sn, BiometricType.PalmVein, payload.Biometrics.PalmVein, token);

        // Final save for tracking operations falling back to insertions
        await Context.SaveChangesAsync(token);
        await CommitChangesAsync(token);
    }

    private async Task UpsertBiometricDetailAsync(string sn, BiometricType type, BiometricDetail detail, CancellationToken token)
    {
        var rowsAffected = await Context.BiometricDetails
            .Where(b => b.SN == sn && b.Type == type)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(b => b.Enabled, detail.Enabled)
                .SetProperty(b => b.Version, detail.Version)
                .SetProperty(b => b.Count, detail.Count)
                .SetProperty(b => b.MaxCount, detail.MaxCount),
                token);

        if (rowsAffected == 0)
        {
            detail.SN = sn;
            detail.Type = type;
            Context.BiometricDetails.Add(detail);
        }
    }

    private async Task UpsertSystemCounters(string sn, SystemCounters counters, CancellationToken token)
    {
        var rowsAffected = await Context.SystemCounters
            .Where(u => u.SN == sn)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(u => u.TransactionCount, counters.TransactionCount)
                .SetProperty(u => u.MaxAttLogCount, counters.MaxAttLogCount)
                .SetProperty(u => u.UserCount, counters.UserCount)
                .SetProperty(u => u.MaxUserCount, counters.MaxUserCount),
                token);

        if (rowsAffected == 0)
        {
            counters.SN = sn;
            Context.SystemCounters.Add(counters);
        }
    }

    private async Task UpsertPhotosAndMedias(string sn, PhotosAndMedia media, CancellationToken token)
    {
        var rowsAffected = await Context.PhotosAndMedias
            .Where(u => u.SN == sn)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(u => u.MaxUserPhotoCount, media.MaxUserPhotoCount)
                .SetProperty(u => u.PhotoFunctionEnabled, media.PhotoFunctionEnabled)
                .SetProperty(u => u.UserPicUrlFunctionEnabled, media.UserPicUrlFunctionEnabled),
                token);

        if (rowsAffected == 0)
        {
            media.SN = sn;
            Context.PhotosAndMedias.Add(media);
        }
    }

    private async Task UpsertFeaturesAndProtocols(string sn, FeaturesAndProtocols config, CancellationToken token)
    {
        var rowsAffected = await Context.FeaturesAndProtocols
            .Where(u => u.SN == sn)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(u => u.VisilightFun, config.VisilightFun)
                .SetProperty(u => u.VisualIntercomFunOn, config.VisualIntercomFunOn)
                .SetProperty(u => u.VideoTid, config.VideoTid)
                .SetProperty(u => u.VideoProtocol, config.VideoProtocol)
                .SetProperty(u => u.SubcontractingUpgradeFunOn, config.SubcontractingUpgradeFunOn),
                token);

        if (rowsAffected == 0)
        {
            config.SN = sn;
            Context.FeaturesAndProtocols.Add(config);
        }
    }

    private async Task UpsertQrCodeConfig(string sn, QrCodeConfig qrConfig, CancellationToken token)
    {
        var rowsAffected = await Context.QrCodeConfigs
            .Where(u => u.SN == sn)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(u => u.IsSupported, qrConfig.IsSupported)
                .SetProperty(u => u.Enabled, qrConfig.Enabled)
                .SetProperty(u => u.DecryptFunList, qrConfig.DecryptFunList),
                token);

        if (rowsAffected == 0)
        {
            qrConfig.SN = sn;
            Context.QrCodeConfigs.Add(qrConfig);
        }
    }

    private async Task UpsertThermalAndMaskConfig(string sn, ThermalAndMaskConfig thermalConfig, CancellationToken token)
    {
        var rowsAffected = await Context.ThermalAndMaskConfigs
            .Where(u => u.SN == sn)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(u => u.IrTempDetectionFunOn, thermalConfig.IrTempDetectionFunOn)
                .SetProperty(u => u.MaskDetectionFunOn, thermalConfig.MaskDetectionFunOn),
                token);

        if (rowsAffected == 0)
        {
            thermalConfig.SN = sn;
            Context.ThermalAndMaskConfigs.Add(thermalConfig);
        }
    }

    private async Task UpsertMultiBioSupport(string sn, MultiBioSupport bioSupport, CancellationToken token)
    {
        // ExecuteUpdateAsync cannot perform multi-row updates for tracking List<int> directly.
        // We evaluate existence via normal query tracking check for complex entity structures.
        var existing = await Context.MultiBioSupports
            .FirstOrDefaultAsync(u => u.SN == sn, token);

        if (existing != null)
        {
            existing.DataSupport = bioSupport.DataSupport;
            existing.PhotoSupport = bioSupport.PhotoSupport;
        }
        else
        {
            bioSupport.SN = sn;
            Context.MultiBioSupports.Add(bioSupport);
        }
    }

    public async Task<UpdateBiometricDevice> UpdateStatusAsync(UpdateBiometricDevice payload, CancellationToken token)
    {
        var model = new BiometricDevice
        {
            Id = payload.Id,
            SN = payload.SN,
            Status = payload.Status
        };
        await ModifyAsync(model, token);
        await CommitChangesAsync(token);
        return payload;
    }
    public async Task<List<BiometricDevice>> FindAllAsync(CancellationToken token)
    {
        return await GetQueryable()
            .ToListAsync(token);
    }
    public async Task<BiometricDevice?> FineOneAsync(Guid Id, CancellationToken token)
    {
        return await GetOneAsync(Id, token);
    }

    public async Task<BiometricDevice?> GetBySerial(string sn, CancellationToken token)
    {
        return await GetQueryable(x => x.SN == sn).FirstOrDefaultAsync();
    }

    public async Task<BiometricDevice?> FindSnAsync(string SN, CancellationToken token)
    {
        return await GetQueryable(x =>
                x.SN == SN &&
                x.Status == "Active" &&
                x.DeletedAt == null)
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(token);
    }

    public async Task DeleteAsync(Guid Id, CancellationToken token)
    {
        await RemoveAsync(Id, token);
        await CommitChangesAsync(token);
    }
}
