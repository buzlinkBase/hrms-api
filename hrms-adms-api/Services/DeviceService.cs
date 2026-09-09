using Hrms.adms.Models.DTO;
using Onepunch.Common.Lib.Exceptions;

namespace Hrms.adms.Services;

public class DeviceService : BaseService<BiometricDevice>
{
    public DeviceService(IUnitOfWorkService uow) : base(uow)
    {
    }
    // Runs on both Create (AddAsync) and Update (UpdateStatusAsync) via BaseService's
    // Guard.ModelGuardAsync(CreateValidatorAsync, ...) hook. Blocks registering/reassigning a
    // device (by SN) that's already Active under a DIFFERENT tenant — the same protection
    // AdmsController's runtime tenant-resolution (FindSnAsync) implicitly relies on staying
    // unambiguous. IgnoreQueryFilters() is required here for the same reason FindSnAsync uses
    // it: this is a shared-schema DB (every tenant's BiometricDevices rows live in one table,
    // isolated only by the row-level TenantId query filter), so seeing across tenants means
    // deliberately bypassing that filter. Also blocks a same-tenant duplicate SN, which was
    // previously unguarded entirely and is exactly what made FindSnAsync's FirstOrDefaultAsync
    // nondeterministic when duplicates existed. Excludes the model's own Id so updating a
    // device's own other fields (UpdateStatusAsync currently always re-sends SN) never
    // self-blocks. Only Active, non-deleted rows count — a released/decommissioned device
    // (Inactive or soft-deleted) elsewhere is free to be re-registered.
    protected override async Task<EvaluationResult> CreateValidatorAsync(BiometricDevice model, CancellationToken token = default)
    {
        var existing = await GetQueryable(x =>
                x.SN == model.SN &&
                x.DeletedAt == null &&
                x.Id != model.Id)
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(token);

        if (existing == null) return EvaluationResult.OK;

        return existing.TenantId != model.TenantId
            ? EvaluationResult.Fail("This device is already registered to another company.")
            : EvaluationResult.Fail("This device (serial number) is already registered.");
    }

    public async Task<UpdateBiometricDevice> AddAsync(CreateBiometricDevice payload, Guid TenantId, CancellationToken token)
    {
        var model = new BiometricDevice
        {
            SN = payload.SN,
            TenantId = TenantId,
            Description = payload.Description ?? "",
            DeviceName = payload.DeviceName ?? "",
            BranchId = payload.BranchId,
            ClientId = payload.ClientId,
            OperationAreaId = payload.AreaId,
            Status = payload.Status
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

    public async Task<UpdateBiometricDevice> UpdateStatusAsync(UpdateBiometricDevice payload, Guid tenantId, CancellationToken token)
    {
        await Context.BiometricDevices
        .Where(x => x.Id == payload.Id && x.TenantId == tenantId)
        .ExecuteUpdateAsync(x => x
            .SetProperty(b => b.Status, payload.Status)
            .SetProperty(b => b.SN, payload.SN)
            .SetProperty(b => b.Description, payload.Description ?? "")
            .SetProperty(b => b.BranchId, payload.BranchId)
            .SetProperty(b => b.ClientId, payload.ClientId)
            .SetProperty(b => b.OperationAreaId, payload.AreaId),
            token);
        await CommitChangesAsync(token);
        return payload;
    }
    public async Task<List<BiometricDeviceModel>> FindAllAsync(Guid tenantId, CancellationToken token)
    {
        return await GetQueryable(x => x.TenantId == tenantId)
            .Select(x => new BiometricDeviceModel
            {
                Id = x.Id,
                SN = x.SN,
                DeviceName = x.DeviceName,
                Description = x.Description,
                FwVersion = x.FwVersion,
                LanguageCode = x.LanguageCode,
                PushVersion = x.PushVersion,
                RegDeviceType = x.RegDeviceType,
                BranchId = x.BranchId,
                ClientId = x.ClientId,
                DepartmentId = x.DepartmentId,
                OperationAreaId = x.OperationAreaId,
                Platform = x.Platform,
                OemVendor = x.OemVendor,
                State = x.State,
                Status = x.Status
            })
            .ToListAsync(token);
    }
    public async Task<BiometricDeviceModel?> FineOneAsync(Guid Id, Guid tenantId, CancellationToken token)
    {
        var result = await GetQueryable(x => x.Id == Id && x.TenantId == tenantId)
            .Select(x => new BiometricDeviceModel
            {
                Id = x.Id,
                SN = x.SN,
                DeviceName = x.DeviceName,
                Description = x.Description,
                FwVersion = x.FwVersion,
                LanguageCode = x.LanguageCode,
                PushVersion = x.PushVersion,
                RegDeviceType = x.RegDeviceType,
                BranchId = x.BranchId,
                ClientId = x.ClientId,
                DepartmentId = x.DepartmentId,
                OperationAreaId = x.OperationAreaId,
                Platform = x.Platform,
                OemVendor = x.OemVendor,
                State = x.State,
                Status = x.Status
            })
            .FirstOrDefaultAsync(token)
            ;

        return result;
    }

    public async Task<BiometricDeviceModel?> GetBySerial(string sn, Guid tenantId, CancellationToken token)
    {
        return await GetQueryable(x => x.SN == sn && x.TenantId == tenantId)
            .Select(x => new BiometricDeviceModel
            {
                Id = x.Id,
                SN = x.SN,
                DeviceName = x.DeviceName,
                Description = x.Description,
                FwVersion = x.FwVersion,
                LanguageCode = x.LanguageCode,
                PushVersion = x.PushVersion,
                RegDeviceType = x.RegDeviceType,
                BranchId = x.BranchId,
                ClientId = x.ClientId,
                DepartmentId = x.DepartmentId,
                OperationAreaId = x.OperationAreaId,
                Platform = x.Platform,
                OemVendor = x.OemVendor,
                State = x.State,
                Status = x.Status
            })
            .FirstOrDefaultAsync(token);
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
