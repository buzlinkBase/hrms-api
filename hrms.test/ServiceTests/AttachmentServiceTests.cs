using BuzlinkRepository;
using Hrms.Core.Services;
using Hrms.Core.Services.Attachments;
using Hrms.Core.Services.Attachments.Handlers;
using Hrms.Core.Services.Storage;
using Hrms.Core.Services.Subscriptions;
using Hrms.Domain.Entities;
using Hrms.Domain.Entities.Attachments;
using Hrms.Domain.Entities.EmployeeEntities;
using Hrms.Infrastructure;
using hrms.test.InfrastructureTests;
using hrms.test.TestSupport;
using Microsoft.EntityFrameworkCore;
using Onepunch.Common.Lib.Exceptions;

namespace hrms.test.ServiceTests;

/// <summary>What an attachment may be: the file's own bytes decide, never its name or claimed type.</summary>
public class AttachmentFilesTests
{
    private static readonly byte[] Pdf = [.. "%PDF-1.7"u8, 1, 2, 3];

    [Fact]
    public void RecognizesImagesAndPdf_AndNothingElse()
    {
        AttachmentFiles.DetectContentType(ImageUploadsTests.Png).Should().Be("image/png");
        AttachmentFiles.DetectContentType(Pdf).Should().Be("application/pdf");
        AttachmentFiles.DetectContentType("<svg xmlns=\"http://www.w3.org/2000/svg\"/>"u8).Should().BeNull();
        AttachmentFiles.DetectContentType("PK\u0003\u0004 a docx is a zip"u8).Should().BeNull();
        AttachmentFiles.DetectContentType("MZ an exe renamed .pdf"u8).Should().BeNull();
    }

    [Fact]
    public async Task RejectsEmpty_TooLarge_AndOtherFiles_WithAMessageForTheUser()
    {
        var empty = () => AttachmentFiles.ReadAsync(new MemoryStream(), 0, default);
        var declaredTooLarge = () => AttachmentFiles.ReadAsync(new MemoryStream(Pdf), AttachmentFiles.MaxBytes + 1, default);
        var actuallyTooLarge = () => AttachmentFiles.ReadAsync(new MemoryStream([.. Pdf, .. new byte[AttachmentFiles.MaxBytes]]), null, default);
        var notAllowed = () => AttachmentFiles.ReadAsync(new MemoryStream("hello"u8.ToArray()), 5, default);

        await empty.Should().ThrowAsync<InvalidAttachmentException>();
        (await declaredTooLarge.Should().ThrowAsync<InvalidAttachmentException>()).WithMessage("*5 MB*");
        await actuallyTooLarge.Should().ThrowAsync<InvalidAttachmentException>();
        (await notAllowed.Should().ThrowAsync<InvalidAttachmentException>()).WithMessage("*PDF*");
    }

    [Fact]
    public void ExtensionFollowsTheDetectedType()
    {
        AttachmentFiles.ExtensionFor("application/pdf").Should().Be(".pdf");
        AttachmentFiles.ExtensionFor("image/png").Should().Be(".png");
    }
}

/// <summary>
/// Attachments live in file storage under the company; every feature is on until the company switches it off,
/// access follows the record's owner rules, and staged files can only be claimed by their uploader.
/// </summary>
public class AttachmentServiceTests : IDisposable
{
    private static readonly Guid Company = Guid.NewGuid();
    private static readonly Guid HrUser = Guid.NewGuid();
    private static readonly Guid OtherUser = Guid.NewGuid();
    private static readonly byte[] Pdf = [.. "%PDF-1.7"u8, 1, 2, 3];

    private readonly string _root = Path.Combine(Path.GetTempPath(), $"hrms-attachments-{Guid.NewGuid():N}");
    private readonly SqliteHrmsContext _db = new();

    public void Dispose()
    {
        _db.Dispose();
        if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
    }

    private sealed class CompanyTenant : ITenantProvider
    {
        public Guid TenantId { get; private set; } = Company;
        public void SetTenantId(Guid tenantId) => TenantId = tenantId;
    }

    private sealed class RoomForEverything : IStorageQuotaGuard
    {
        public int StoredCount { get; private set; }
        public int RemovedCount { get; private set; }
        public Task EnsureCanStoreAsync(Guid tenantId, long bytes, CancellationToken token) => Task.CompletedTask;
        public void Stored(Guid tenantId, long bytes) => StoredCount++;
        public void Removed(Guid tenantId) => RemovedCount++;
    }

    private sealed class NoRoom : IStorageQuotaGuard
    {
        public Task EnsureCanStoreAsync(Guid tenantId, long bytes, CancellationToken token) =>
            throw new GuardException("Out of storage.");
        public void Stored(Guid tenantId, long bytes) { }
        public void Removed(Guid tenantId) { }
    }

    private static AttachmentCaller Hr(Guid? userId = null) =>
        new(userId ?? HrUser, null, codes => codes.Contains("Workforce Setup:Edit"));

    private static AttachmentCaller Nobody(Guid? employeeId = null) =>
        new(OtherUser, employeeId, _ => false);

    private async Task<T> WithServiceAsync<T>(Func<AttachmentService, Task<T>> act, IStorageQuotaGuard? quota = null)
    {
        await using var context = _db.NewContext();
        var uow = new UnitOfWorkService(context);
        var service = new AttachmentService(
            uow, new LocalFileStorage(_root), new CompanyTenant(), quota ?? new RoomForEverything(),
            [new AssignAssetAttachmentHandler(uow), new EmployeeRecordAttachmentHandler(uow)]);
        return await act(service);
    }

    private async Task<Guid> SeedAssetAsync()
    {
        var groupId = Guid.NewGuid();
        var employee = new Employee
        {
            Id = Guid.NewGuid(), PayrollGroupId = groupId, JobLevel = "", FirstName = "Ana", LastName = "Reyes",
            BioId = Random.Shared.Next(1, int.MaxValue), ProfileImg = "",
        };
        var asset = new AssignAsset
        {
            Id = Guid.NewGuid(), EmployeeId = employee.Id, AssetType = "Laptop", AssetDescription = "", Model = "",
            Brand = "", SerialNo = "", Status = "Issued", Remarks = "", File = "",
        };
        await using var seed = _db.NewContext();
        seed.PayrollGroups.Add(new PayrollGroup { Id = groupId, Name = "Main" });
        seed.Employees.Add(employee);
        seed.AssignAssets.Add(asset);
        await seed.SaveChangesAsync();
        return asset.Id;
    }

    private async Task EnableAsync(AttachmentOwnerType type, bool enabled = true) =>
        await WithServiceAsync(async s =>
        {
            await s.SaveSettingsAsync([new AttachmentSettingDto(type, enabled)], default);
            return 0;
        });

    private Task<AttachmentDto> UploadAsync(
        AttachmentOwnerType type, Guid? ownerId, AttachmentCaller caller, string name = "photo.pdf", IStorageQuotaGuard? quota = null) =>
        WithServiceAsync(s => s.UploadAsync(type, ownerId, new MemoryStream(Pdf), Pdf.Length, name, caller, default), quota);

    [Fact]
    public async Task EveryFeatureIsOnByDefault_AndTheSettingsOnlyListFeaturesThatCanTakeFiles()
    {
        var settings = await WithServiceAsync(s => s.GetSettingsAsync(default));

        settings.Select(x => x.OwnerType).Should().BeEquivalentTo(
            [AttachmentOwnerType.AssignAsset, AttachmentOwnerType.EmployeeRecord]);
        settings.Should().OnlyContain(x => x.Enabled);
    }

    [Fact]
    public async Task AnUpload_IsAllowedByDefault_RefusedOnceAnAdminTurnsTheFeatureOff_AndAllowedAgainWhenOn()
    {
        var assetId = await SeedAssetAsync();

        await EnableAsync(AttachmentOwnerType.AssignAsset, enabled: false);
        var off = () => UploadAsync(AttachmentOwnerType.AssignAsset, assetId, Hr());
        await off.Should().ThrowAsync<GuardException>().WithMessage("*turned off*");

        await EnableAsync(AttachmentOwnerType.AssignAsset);
        var saved = await UploadAsync(AttachmentOwnerType.AssignAsset, assetId, Hr());
        saved.ContentType.Should().Be("application/pdf");
        saved.OwnerId.Should().Be(assetId);
    }

    [Fact]
    public async Task AFile_IsStoredUnderTheCompany_AndCountedAgainstTheQuota()
    {
        var assetId = await SeedAssetAsync();
        await EnableAsync(AttachmentOwnerType.AssignAsset);
        var quota = new RoomForEverything();

        await UploadAsync(AttachmentOwnerType.AssignAsset, assetId, Hr(), "Laptop front.PDF", quota);

        await using var read = _db.NewContext();
        var row = await read.Attachments.AsNoTracking().SingleAsync();
        row.StorageKey.Should().StartWith($"tenants/{Company}/attachments-employee-assets/").And.EndWith("-Laptop-front.pdf");
        File.Exists(Path.Combine(_root, row.StorageKey)).Should().BeTrue();
        quota.StoredCount.Should().Be(1);
    }

    [Fact]
    public async Task WhenThereIsNoStorageRoom_NothingIsStored()
    {
        var assetId = await SeedAssetAsync();
        await EnableAsync(AttachmentOwnerType.AssignAsset);

        var act = () => UploadAsync(AttachmentOwnerType.AssignAsset, assetId, Hr(), quota: new NoRoom());

        await act.Should().ThrowAsync<GuardException>().WithMessage("Out of storage.");
        await using var read = _db.NewContext();
        (await read.Attachments.CountAsync()).Should().Be(0);
        (!Directory.Exists(_root) || Directory.GetFiles(_root, "*", SearchOption.AllDirectories).Length == 0)
            .Should().BeTrue("no file was written");
    }

    [Fact]
    public async Task ARecord_TakesAtMostFiveFiles()
    {
        var assetId = await SeedAssetAsync();
        await EnableAsync(AttachmentOwnerType.AssignAsset);
        for (var i = 0; i < AttachmentFiles.MaxPerRecord; i++)
            await UploadAsync(AttachmentOwnerType.AssignAsset, assetId, Hr());

        var sixth = () => UploadAsync(AttachmentOwnerType.AssignAsset, assetId, Hr());

        await sixth.Should().ThrowAsync<InvalidAttachmentException>().WithMessage("*at most 5*");
    }

    [Fact]
    public async Task SomeoneWithoutPermission_CannotSeeOrAddFilesToARecord_AndGetsNotFound()
    {
        var assetId = await SeedAssetAsync();
        await EnableAsync(AttachmentOwnerType.AssignAsset);
        var file = await UploadAsync(AttachmentOwnerType.AssignAsset, assetId, Hr());

        var upload = () => UploadAsync(AttachmentOwnerType.AssignAsset, assetId, Nobody());
        var list = () => WithServiceAsync(s => s.ListAsync(AttachmentOwnerType.AssignAsset, assetId, Nobody(), default));
        var open = () => WithServiceAsync(s => s.OpenAsync(file.Id, false, Nobody(), default));
        var remove = () => WithServiceAsync(async s => { await s.DeleteAsync(file.Id, Nobody(), default); return 0; });

        await upload.Should().ThrowAsync<NotFoundException>();
        await list.Should().ThrowAsync<NotFoundException>();
        await open.Should().ThrowAsync<NotFoundException>();
        await remove.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task ARecordFromAnotherCompany_IsNotFound()
    {
        await EnableAsync(AttachmentOwnerType.AssignAsset);

        var act = () => UploadAsync(AttachmentOwnerType.AssignAsset, Guid.NewGuid(), Hr());

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task DeletingAFile_RemovesTheRowAndTheStoredFile_AndFreesTheQuota()
    {
        var assetId = await SeedAssetAsync();
        await EnableAsync(AttachmentOwnerType.AssignAsset);
        var quota = new RoomForEverything();
        var file = await UploadAsync(AttachmentOwnerType.AssignAsset, assetId, Hr(), quota: quota);
        await using var before = _db.NewContext();
        var key = (await before.Attachments.AsNoTracking().SingleAsync()).StorageKey;

        await WithServiceAsync(async s => { await s.DeleteAsync(file.Id, Hr(), default); return 0; }, quota);

        await using var read = _db.NewContext();
        (await read.Attachments.CountAsync()).Should().Be(0);
        File.Exists(Path.Combine(_root, key)).Should().BeFalse();
        quota.RemovedCount.Should().Be(1);
    }

    [Fact]
    public async Task AStagedFile_CanBeClaimedByItsUploader_ButNotSeenOrClaimedBySomeoneElse()
    {
        var assetId = await SeedAssetAsync();
        await EnableAsync(AttachmentOwnerType.AssignAsset);
        var staged = await UploadAsync(AttachmentOwnerType.AssignAsset, null, Hr());
        staged.OwnerId.Should().BeNull();

        var others = await WithServiceAsync(s => s.ListStagedAsync(AttachmentOwnerType.AssignAsset, Hr(Guid.NewGuid()), default));
        var steal = () => WithServiceAsync(async s =>
        {
            await s.LinkAsync(AttachmentOwnerType.AssignAsset, assetId, [staged.Id], Hr(Guid.NewGuid()), default);
            return 0;
        });
        others.Should().BeEmpty();
        await steal.Should().ThrowAsync<GuardException>();

        // The record's own save commits the link.
        await using (var context = _db.NewContext())
        {
            var uow = new UnitOfWorkService(context);
            var service = new AttachmentService(
                uow, new LocalFileStorage(_root), new CompanyTenant(), new RoomForEverything(), [new AssignAssetAttachmentHandler(uow)]);
            await service.LinkAsync(AttachmentOwnerType.AssignAsset, assetId, [staged.Id], Hr(), default);
            await uow.CommitChangesAsync("", default);
        }
        var listed = await WithServiceAsync(s => s.ListAsync(AttachmentOwnerType.AssignAsset, assetId, Hr(), default));
        listed.Should().ContainSingle().Which.Id.Should().Be(staged.Id);
    }

    [Fact]
    public async Task LinkingStagedFiles_ToARecordTheCallerCannotChange_IsRefused()
    {
        var assetId = await SeedAssetAsync();
        await EnableAsync(AttachmentOwnerType.AssignAsset);
        var staged = await UploadAsync(AttachmentOwnerType.AssignAsset, null, Hr());

        var refused = () => WithServiceAsync(async s =>
        {
            await s.LinkStagedAsync(AttachmentOwnerType.AssignAsset, assetId, [staged.Id], Nobody(), default);
            return 0;
        });
        await refused.Should().ThrowAsync<NotFoundException>();

        await WithServiceAsync(async s =>
        {
            await s.LinkStagedAsync(AttachmentOwnerType.AssignAsset, assetId, [staged.Id], Hr(), default);
            return 0;
        });
        var listed = await WithServiceAsync(s => s.ListAsync(AttachmentOwnerType.AssignAsset, assetId, Hr(), default));
        listed.Should().ContainSingle();
    }

    private async Task<(Guid EmployeeId, Guid OrderId, Guid SlipId)> SeedApplicationsAsync(ApprovalStatus status)
    {
        var groupId = Guid.NewGuid();
        var employee = new Employee
        {
            Id = Guid.NewGuid(), PayrollGroupId = groupId, JobLevel = "", FirstName = "Ana", LastName = "Reyes",
            BioId = Random.Shared.Next(1, int.MaxValue), ProfileImg = "",
        };
        var order = new TravelOrderApplication { Id = Guid.NewGuid(), EmployeeId = employee.Id, ApprovalStatus = status };
        var slip = new PassSlipApplication { Id = Guid.NewGuid(), EmployeeId = employee.Id, ApprovalStatus = status };
        await using var seed = _db.NewContext();
        seed.PayrollGroups.Add(new PayrollGroup { Id = groupId, Name = "Main" });
        seed.Employees.Add(employee);
        seed.TravelOrderApplications.Add(order);
        seed.PassSlipApplications.Add(slip);
        await seed.SaveChangesAsync();
        return (employee.Id, order.Id, slip.Id);
    }

    private Task<T> WithRequestServiceAsync<T>(Func<AttachmentService, Task<T>> act) =>
        WithServiceInternalAsync(act);

    private async Task<T> WithServiceInternalAsync<T>(Func<AttachmentService, Task<T>> act)
    {
        await using var context = _db.NewContext();
        var uow = new UnitOfWorkService(context);
        var service = new AttachmentService(
            uow, new LocalFileStorage(_root), new CompanyTenant(), new RoomForEverything(),
            [new TravelOrderAttachmentHandler(uow), new PassSlipAttachmentHandler(uow)]);
        return await act(service);
    }

    [Theory]
    [InlineData(AttachmentOwnerType.TravelOrder)]
    [InlineData(AttachmentOwnerType.PassSlip)]
    public async Task AnEmployee_CanAttachToTheirOwnPendingRequest_ButNotSomeoneElses(AttachmentOwnerType type)
    {
        var (employeeId, orderId, slipId) = await SeedApplicationsAsync(ApprovalStatus.ForApproval);
        var ownerId = type == AttachmentOwnerType.TravelOrder ? orderId : slipId;
        await WithRequestServiceAsync(async s =>
        {
            await s.SaveSettingsAsync([new AttachmentSettingDto(type, true)], default);
            return 0;
        });
        var mine = new AttachmentCaller(HrUser, employeeId, _ => false);
        var theirs = new AttachmentCaller(OtherUser, Guid.NewGuid(), _ => false);

        var saved = await WithRequestServiceAsync(s =>
            s.UploadAsync(type, ownerId, new MemoryStream(Pdf), Pdf.Length, "proof.pdf", mine, default));
        var intruder = () => WithRequestServiceAsync(s =>
            s.UploadAsync(type, ownerId, new MemoryStream(Pdf), Pdf.Length, "x.pdf", theirs, default));

        saved.OwnerId.Should().Be(ownerId);
        await intruder.Should().ThrowAsync<NotFoundException>();
    }

    [Theory]
    [InlineData(AttachmentOwnerType.TravelOrder)]
    [InlineData(AttachmentOwnerType.PassSlip)]
    public async Task ADecidedRequest_TakesNoNewFiles(AttachmentOwnerType type)
    {
        var (employeeId, orderId, slipId) = await SeedApplicationsAsync(ApprovalStatus.Approved);
        var ownerId = type == AttachmentOwnerType.TravelOrder ? orderId : slipId;
        await WithRequestServiceAsync(async s =>
        {
            await s.SaveSettingsAsync([new AttachmentSettingDto(type, true)], default);
            return 0;
        });
        var mine = new AttachmentCaller(HrUser, employeeId, _ => false);

        var act = () => WithRequestServiceAsync(s =>
            s.UploadAsync(type, ownerId, new MemoryStream(Pdf), Pdf.Length, "late.pdf", mine, default));

        await act.Should().ThrowAsync<GuardException>().WithMessage("*can no longer be changed*");
    }

    [Fact]
    public async Task The201File_ListsTheFilesOfTheEmployeesDocumentsAndAssets_AndNothingElse()
    {
        var assetId = await SeedAssetAsync();
        Guid employeeId;
        await using (var read = _db.NewContext())
            employeeId = (await read.AssignAssets.AsNoTracking().SingleAsync(a => a.Id == assetId)).EmployeeId!.Value;
        var recordId = Guid.NewGuid();
        await using (var seed = _db.NewContext())
        {
            seed.EmployeeRecords.Add(new EmployeeRecord
            {
                Id = recordId, EmployeeId = employeeId, RecordType = "NBI", Description = "", File = "",
            });
            await seed.SaveChangesAsync();
        }
        await EnableAsync(AttachmentOwnerType.AssignAsset);
        await EnableAsync(AttachmentOwnerType.EmployeeRecord);
        await UploadAsync(AttachmentOwnerType.AssignAsset, assetId, Hr(), "laptop.pdf");
        await UploadAsync(AttachmentOwnerType.EmployeeRecord, recordId, Hr(), "nbi.pdf");
        await UploadAsync(AttachmentOwnerType.AssignAsset, null, Hr(), "staged.pdf");

        await using var context = _db.NewContext();
        var full = await ConfidentialAccessTests.EmployeeServiceFor(context, null)
            .GetFullByIdAsync(employeeId, default);

        full!.Attachments.Select(a => (a.OwnerType, a.OwnerId, a.FileName.Split('-').Last())).Should().BeEquivalentTo(
            [
                (AttachmentOwnerType.AssignAsset, assetId, "laptop.pdf"),
                (AttachmentOwnerType.EmployeeRecord, recordId, "nbi.pdf"),
            ],
            "a staged file isn't attached to anything yet");
    }

    [Fact]
    public async Task SwitchingAFeatureOff_KeepsTheFilesItAlreadyHas()
    {
        var assetId = await SeedAssetAsync();
        await EnableAsync(AttachmentOwnerType.AssignAsset);
        await UploadAsync(AttachmentOwnerType.AssignAsset, assetId, Hr());

        await EnableAsync(AttachmentOwnerType.AssignAsset, enabled: false);

        var listed = await WithServiceAsync(s => s.ListAsync(AttachmentOwnerType.AssignAsset, assetId, Hr(), default));
        listed.Should().HaveCount(1);
        var again = () => UploadAsync(AttachmentOwnerType.AssignAsset, assetId, Hr());
        await again.Should().ThrowAsync<GuardException>().WithMessage("*turned off*");
    }
}
