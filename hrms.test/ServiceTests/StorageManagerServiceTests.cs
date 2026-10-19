using Hrms.Core.Services;
using Hrms.Core.Services.Attachments;
using Hrms.Core.Services.Attachments.Handlers;
using Hrms.Core.Services.Storage;
using Hrms.Core.Services.StorageManager;
using Hrms.Domain.Entities;
using Hrms.Domain.Entities.Attachments;
using Hrms.Domain.Entities.EmployeeEntities;
using Hrms.Infrastructure;
using hrms.test.TestSupport;
using Microsoft.EntityFrameworkCore;
using Onepunch.AuditEngine;
using Onepunch.Common.Lib.Exceptions;

namespace hrms.test.ServiceTests;

public class StorageKeyPartsTests
{
    private static readonly Guid Company = Guid.NewGuid();

    [Fact]
    public void ReadsAreaYearAndOriginalName_FromANewFileKey()
    {
        var key = StorageKeys.ForNewFile(Company, "attachments-leave", "Medical Cert.pdf", new DateTime(2023, 4, 2));

        var parts = StorageKeyParts.Parse(key, Company);

        parts.Should().Be(new StorageKeyParts("attachments-leave", 2023, "Medical-Cert.pdf"));
    }

    [Fact]
    public void RefusesAnotherCompanysKey_AndKeepsANameWithoutAnId()
    {
        var other = StorageKeys.ForNewFile(Guid.NewGuid(), "employee-photos", "a.png");

        StorageKeyParts.Parse(other, Company).Should().BeNull();
        StorageKeyParts.Parse($"tenants/{Company:D}/legacy/notes.txt", Company)
            .Should().Be(new StorageKeyParts("legacy", null, "notes.txt"));
    }
}

/// <summary>
/// The Storage Manager groups a company's files by category and year straight from the folder
/// listing, offers unclaimed files as unused once they are a day old, and deletes files (never the
/// records) with an audit entry -- files of decided records only after an explicit confirmation.
/// </summary>
public class StorageManagerServiceTests : IDisposable
{
    private static readonly Guid Company = Guid.NewGuid();
    private static readonly DateTime Y2023 = new(2023, 3, 1, 0, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime Y2025 = new(2025, 6, 1, 0, 0, 0, DateTimeKind.Utc);
    private static readonly byte[] Bytes100 = new byte[100];

    private readonly string _root = Path.Combine(Path.GetTempPath(), $"hrms-storage-manager-{Guid.NewGuid():N}");
    private readonly SqliteHrmsContext _db = new();
    private readonly LocalFileStorage _storage;
    private readonly CountingMeter _meter = new();
    private readonly RecordingAudit _audit = new();

    public StorageManagerServiceTests() => _storage = new LocalFileStorage(_root);

    public void Dispose()
    {
        _db.Dispose();
        if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
    }

    private sealed class FixedClock(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    private sealed class CountingMeter : IStorageUsageMeter
    {
        public int Removals { get; private set; }
        public Task<long> GetBytesAsync(Guid tenantId, bool fresh = false, CancellationToken token = default) => Task.FromResult(0L);
        public Task<long> GetTotalBytesAsync(IReadOnlyCollection<Guid> tenantIds, bool fresh = false, CancellationToken token = default) => Task.FromResult(0L);
        public Task<Dictionary<Guid, int>> GetMegabytesAsync(IReadOnlyCollection<Guid> tenantIds, CancellationToken token = default) => Task.FromResult(new Dictionary<Guid, int>());
        public void Stored(Guid tenantId, long bytes) { }
        public void Removed(Guid tenantId) => Removals++;
    }

    private sealed class RecordingAudit : IAuditWriter
    {
        public List<AuditEvent> Events { get; } = [];
        public Task WriteAsync(AuditEvent auditEvent, CancellationToken token = default)
        {
            Events.Add(auditEvent);
            return Task.CompletedTask;
        }
    }

    // Two days ahead: every unclaimed file is past the grace period.
    private static TimeProvider Later => new FixedClock(DateTimeOffset.UtcNow.AddDays(2));

    private async Task<T> WithServiceAsync<T>(Func<StorageManagerService, Task<T>> act, TimeProvider? clock = null)
    {
        await using var context = _db.NewContext(Company);
        var uow = new UnitOfWorkService(context);
        var chain = new StoredFileCategoryChain(
            uow,
            [new LeaveApplicationAttachmentHandler(uow), new EmployeeRecordAttachmentHandler(uow)],
            [new EmployeePhotoCategory(uow), new CompanyLogoCategory(uow), new ClientLogoCategory(uow)]);
        var service = new StorageManagerService(uow, _storage, _meter, chain, _audit, clock ?? Later);
        return await act(service);
    }

    private async Task<string> StoreAsync(string area, string name, DateTime storedAt, Guid? company = null)
    {
        var key = StorageKeys.ForNewFile(company ?? Company, area, name, storedAt);
        await _storage.UploadAsync(new FileUpload(key, new MemoryStream(Bytes100), "application/pdf"));
        return key;
    }

    private async Task<Employee> SeedEmployeeAsync(HrmsContext seed, string profileImg = "")
    {
        var groupId = Guid.NewGuid();
        var employee = new Employee
        {
            Id = Guid.NewGuid(), TenantId = Company, PayrollGroupId = groupId, JobLevel = "", FirstName = "Ana", LastName = "Reyes",
            EmployeeNo = "E-001", BioId = Random.Shared.Next(1, int.MaxValue), ProfileImg = profileImg,
        };
        seed.PayrollGroups.Add(new PayrollGroup { Id = groupId, TenantId = Company, Name = "Main" });
        seed.Employees.Add(employee);
        await seed.SaveChangesAsync();
        return employee;
    }

    /// <summary>A leave application with one attached file; returns the file's key.</summary>
    private async Task<string> SeedLeaveAttachmentAsync(ApprovalStatus status, DateTime storedAt)
    {
        var key = await StoreAsync("attachments-leave", "cert.pdf", storedAt);
        await using var seed = _db.NewContext(Company);
        var employee = await SeedEmployeeAsync(seed);
        var leave = new Leave { Id = Guid.NewGuid(), TenantId = Company, Code = "SL", Description = "Sick Leave" };
        seed.Add(leave);
        var application = new LeaveApplication
        {
            Id = Guid.NewGuid(), TenantId = Company, EmployeeId = employee.Id, LeaveId = leave.Id, Leave = leave,
            LeaveDateFrom = new DateOnly(2023, 3, 1), LeaveDateTo = new DateOnly(2023, 3, 1), ApprovalStatus = status,
        };
        seed.leaveApplications.Add(application);
        seed.Attachments.Add(new Attachment
        {
            Id = Guid.NewGuid(), TenantId = Company, OwnerType = AttachmentOwnerType.LeaveApplication, OwnerId = application.Id,
            StorageKey = key, FileName = "cert.pdf", ContentType = "application/pdf", SizeBytes = Bytes100.Length,
        });
        await seed.SaveChangesAsync();
        return key;
    }

    [Fact]
    public async Task Breakdown_GroupsByCategoryAndYear_AndAddsUpToTheFolderSize()
    {
        await SeedLeaveAttachmentAsync(ApprovalStatus.ForApproval, Y2023);
        var photo = await StoreAsync("employee-photos", "ana.png", Y2025);
        await using (var seed = _db.NewContext(Company)) await SeedEmployeeAsync(seed, photo);
        await StoreAsync("attachments-leave", "never-saved.pdf", Y2023); // no record points at it

        var breakdown = await WithServiceAsync(s => s.GetBreakdownAsync(Company, default));

        breakdown.FileCount.Should().Be(3);
        breakdown.Bytes.Should().Be(await _storage.GetPrefixSizeAsync(StorageKeys.TenantPrefix(Company)));
        breakdown.Categories.Should().ContainSingle(c => c.Category == "attachments-leave")
            .Which.Years.Should().Equal(new StorageYearDto(2023, 1, 100));
        breakdown.Categories.Should().ContainSingle(c => c.Category == "employee-photos")
            .Which.Years.Should().Equal(new StorageYearDto(2025, 1, 100));
        breakdown.Categories.Should().ContainSingle(c => c.Category == UnlinkedFileCategory.CategoryKey)
            .Which.FileCount.Should().Be(1);
    }

    [Fact]
    public async Task AnUnclaimedFileFromToday_StaysInItsCategory_AndCantBeDeleted()
    {
        var key = await StoreAsync("attachments-leave", "uploading.pdf", DateTime.UtcNow);
        var now = new FixedClock(DateTimeOffset.UtcNow);

        var page = await WithServiceAsync(s => s.GetFilesAsync(Company, "attachments-leave", DateTime.UtcNow.Year, 1, 20, default), now);
        var preview = await WithServiceAsync(
            s => s.PreviewDeleteAsync(Company, new StorageDeleteRequest(null, null, [key]), default), now);

        page.Items.Should().ContainSingle().Which.Deletable.Should().BeFalse();
        preview.FileCount.Should().Be(0);
        (await _storage.ExistsAsync(key)).Should().BeTrue();
    }

    [Fact]
    public async Task DeletingFilesOfADecidedRecord_NeedsConfirmation()
    {
        var key = await SeedLeaveAttachmentAsync(ApprovalStatus.Approved, Y2023);
        var request = new StorageDeleteRequest("attachments-leave", 2023, null);

        var preview = await WithServiceAsync(s => s.PreviewDeleteAsync(Company, request, default));
        var unconfirmed = () => WithServiceAsync(s => s.DeleteAsync(Company, request, StorageManagerCaller.SignedInUser, default));

        preview.Should().Be(new StorageDeletePreview(1, 100, 1));
        (await unconfirmed.Should().ThrowAsync<GuardException>()).WithMessage("*Confirm*");
        (await _storage.ExistsAsync(key)).Should().BeTrue();
    }

    [Fact]
    public async Task DeletingAYear_RemovesTheFilesAndAttachmentRows_KeepsTheRecords_AndAudits()
    {
        var key = await SeedLeaveAttachmentAsync(ApprovalStatus.Approved, Y2023);

        var result = await WithServiceAsync(s => s.DeleteAsync(
            Company, new StorageDeleteRequest("attachments-leave", 2023, null, ConfirmLocked: true),
            StorageManagerCaller.SignedInUser, default));

        result.Should().Be(new StorageDeleteResult(1, 100, 0));
        (await _storage.ExistsAsync(key)).Should().BeFalse();
        await using var verify = _db.NewContext(Company);
        (await verify.Attachments.IgnoreQueryFilters().CountAsync()).Should().Be(0);
        (await verify.leaveApplications.IgnoreQueryFilters().CountAsync()).Should().Be(1);
        _meter.Removals.Should().Be(1);
        var audit = _audit.Events.Should().ContainSingle().Subject;
        audit.EventType.Should().Be(AuditEventTypes.Delete);
        audit.OrganizationId.Should().Be(Company.ToString());
        audit.Metadata["LockedRecordFiles"].Should().Be("1");
        audit.Metadata["Channel"].Should().Be("HRIS");
    }

    [Fact]
    public async Task DeletingAPhoto_ClearsTheEmployeesPhoto()
    {
        var photo = await StoreAsync("employee-photos", "ana.png", Y2025);
        Guid employeeId;
        await using (var seed = _db.NewContext(Company)) employeeId = (await SeedEmployeeAsync(seed, photo)).Id;

        await WithServiceAsync(s => s.DeleteAsync(
            Company, new StorageDeleteRequest(null, null, [photo]), StorageManagerCaller.SignedInUser, default));

        (await _storage.ExistsAsync(photo)).Should().BeFalse();
        await using var verify = _db.NewContext(Company);
        (await verify.Employees.IgnoreQueryFilters().SingleAsync(e => e.Id == employeeId)).ProfileImg.Should().BeEmpty();
    }

    [Fact]
    public async Task KeysOfAnotherCompany_AreNeverDeleted()
    {
        var other = Guid.NewGuid();
        var theirs = await StoreAsync("attachments-leave", "theirs.pdf", Y2023, other);
        var ours = await StoreAsync("attachments-leave", "ours.pdf", Y2023);

        var result = await WithServiceAsync(s => s.DeleteAsync(
            Company, new StorageDeleteRequest(null, null, [theirs, ours]), StorageManagerCaller.SignedInUser, default));

        result.DeletedCount.Should().Be(1);
        (await _storage.ExistsAsync(theirs)).Should().BeTrue();
        (await _storage.ExistsAsync(ours)).Should().BeFalse();
    }

    [Fact]
    public async Task AnOperatorsDelete_IsAuditedAsTheOperator_WithTheReason()
    {
        await StoreAsync("client-logos", "old.png", Y2023);

        await WithServiceAsync(s => s.DeleteAsync(
            Company, new StorageDeleteRequest(UnlinkedFileCategory.CategoryKey, 2023, null),
            StorageManagerCaller.PlatformOperator("op-1", "Jo", "Owner asked to free space"), default));

        var audit = _audit.Events.Should().ContainSingle().Subject;
        audit.Actor!.Id.Should().Be("op-1");
        audit.Metadata["Reason"].Should().Be("Owner asked to free space");
        audit.Metadata["Channel"].Should().Be("Platform console");
    }

    [Fact]
    public async Task ARequestWithNeitherAGroupNorKeys_IsRefused()
    {
        var act = () => WithServiceAsync(s => s.PreviewDeleteAsync(Company, new StorageDeleteRequest(null, null, null), default));

        await act.Should().ThrowAsync<GuardException>();
    }
}

public class LocalFileStorageListingTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), $"hrms-listing-{Guid.NewGuid():N}");

    public void Dispose()
    {
        if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
    }

    [Fact]
    public async Task ListsOneCompanysFiles_AndDeletesSeveralAtOnce()
    {
        var storage = new LocalFileStorage(_root);
        var company = Guid.NewGuid();
        var a = StorageKeys.ForNewFile(company, "x", "a.pdf");
        var b = StorageKeys.ForNewFile(company, "x", "b.pdf");
        var other = StorageKeys.ForNewFile(Guid.NewGuid(), "x", "c.pdf");
        foreach (var key in new[] { a, b, other })
            await storage.UploadAsync(new FileUpload(key, new MemoryStream(new byte[10]), "application/pdf"));

        var listed = await storage.ListAsync(StorageKeys.TenantPrefix(company));
        await storage.DeleteManyAsync([a, b]);

        listed.Select(f => f.Key).Should().BeEquivalentTo([a, b]);
        listed.Should().OnlyContain(f => f.Size == 10);
        (await storage.ListAsync(StorageKeys.TenantPrefix(company))).Should().BeEmpty();
        (await storage.ExistsAsync(other)).Should().BeTrue();
        var notAFolder = () => storage.ListAsync("tenants/");
        await notAFolder.Should().ThrowAsync<ArgumentException>();
    }
}
