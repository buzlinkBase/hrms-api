using Hrms.Core.Services.Approvals;
using Hrms.Domain.Entities.EmployeeEntities;
using hrms.test.TestSupport;
using Mapster;
using MapsterMapper;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using NSubstitute;

namespace hrms.test.ServiceTests;

/// <summary>
/// Loans &amp; Deductions installments, against a production-like context. Same failures Other
/// Income had (see IncomeAplMainServiceTests), plus loan-specific ones: creating a loan looked up
/// "existing" installments by the new loan's still-empty Id and deleted every other loan's; the
/// payroll loader let installments with that empty parent Id skip the approval gate; an edit
/// rebuilt Balance from Amount, re-opening installments payroll had already paid.
/// </summary>
public class DeductionApplicationScheduleTests : IDisposable
{
    private static readonly DateOnly Start = new(2026, 10, 15);

    private readonly SqliteHrmsContext _db = new();
    private readonly Guid _employeeId;
    private readonly Guid _deductionId;

    public DeductionApplicationScheduleTests()
    {
        using var seed = _db.NewContext();
        var groupId = Guid.NewGuid();
        seed.PayrollGroups.Add(new PayrollGroup { Id = groupId });
        _employeeId = Guid.NewGuid();
        seed.Employees.Add(new Employee { Id = _employeeId, PayrollGroupId = groupId });
        _deductionId = Guid.NewGuid();
        seed.Deductions.Add(new Deduction { Id = _deductionId, Code = "COLOAN", Name = "Company Loan" });
        seed.SaveChanges();
    }

    public void Dispose() => _db.Dispose();

    private static DeductionApplicationService BuildService(IUnitOfWorkService uow)
    {
        _ = TestMapping.GlobalSettings; // registers MappingProfile once per run (see TestMapping)
        var employeeService = new EmployeeService(
            uow, Substitute.For<IMapper>(), TypeAdapterConfig.GlobalSettings,
            new DepartmentService(uow), new PayrollGroupService(uow), new BranchService(uow),
            new CostCenterService(uow), new PositionService(uow), new SectionService(uow));
        return new DeductionApplicationService(uow, new DeductionService(uow), employeeService,
            new Mapper(TypeAdapterConfig.GlobalSettings), new ApprovalEngineService(uow, Substitute.For<IPublishEndpoint>()));
    }

    private async Task<T> RunAsync<T>(Func<DeductionApplicationService, Task<T>> action)
    {
        await using var context = _db.NewProductionLikeContext();
        return await action(BuildService(new UnitOfWorkService(context)));
    }

    private async Task RunAsync(Func<DeductionApplicationService, Task> action)
    {
        await using var context = _db.NewProductionLikeContext();
        await action(BuildService(new UnitOfWorkService(context)));
    }

    private CreateDeductionApplication CreateForm(decimal amount = 1000m, int terms = 3) => new()
    {
        EmployeeId = _employeeId,
        DeductionId = _deductionId,
        EncodeDate = Start,
        StartDate = Start,
        EndDate = Start.AddMonths(terms),
        Terms = terms,
        TotalPrincipal = amount * terms,
        TotalAmount = amount * terms,
        Breakdown = Enumerable.Range(0, terms)
            .Select(i => new CreateDeductionApplicationDetail { Date = Start.AddMonths(i), Principal = amount, Amount = amount })
            .ToList(),
    };

    private Task<DeductionApplication> CreateAsync(decimal amount = 1000m, int terms = 3) =>
        RunAsync(s => s.AddAsync(CreateForm(amount, terms), ApprovalStatus.Approved, CancellationToken.None));

    private static UpdateDeductionApplication UpdateForm(DeductionApplication stored,
        IEnumerable<UpdateDeductionApplicationDetail> details) => new()
    {
        Id = stored.Id,
        EmployeeId = stored.EmployeeId,
        DeductionId = stored.DeductionId,
        EncodeDate = stored.EncodeDate,
        StartDate = stored.StartDate,
        EndDate = stored.EndDate,
        Terms = stored.Terms,
        TotalPrincipal = stored.TotalPrincipal,
        TotalAmount = stored.TotalAmount,
        Details = details.ToList(),
    };

    private static UpdateDeductionApplicationDetail Echo(DeductionApplicationDetail row, decimal? amount = null) => new()
    {
        Id = row.Id, Date = row.Date, Principal = row.Principal, Interest = row.Interest, Amount = amount ?? row.Amount,
    };

    private async Task<DeductionApplication> StoredAsync(Guid id) =>
        (await RunAsync(s => s.FineOneAsync(id, CancellationToken.None)))!;

    private async Task<List<DeductionApplicationDetail>> AllRowsAsync()
    {
        await using var check = _db.NewContext();
        return await check.DeductionApplicationDetails.IgnoreQueryFilters().AsNoTracking().ToListAsync();
    }

    private async Task PayDownAsync(Guid rowId, decimal paid)
    {
        await using var context = _db.NewContext();
        var row = await context.DeductionApplicationDetails.SingleAsync(x => x.Id == rowId);
        row.Balance -= paid;
        await context.SaveChangesAsync();
    }

    // A generated (not yet posted) payroll run referencing the installment. The Payroll row itself
    // isn't needed to lock the installment, so the FK to it is switched off for this seed only.
    private async Task ReferenceFromPayrollAsync(Guid rowId)
    {
        await using var context = _db.NewContext();
        await context.Database.OpenConnectionAsync();
        await context.Database.ExecuteSqlRawAsync("PRAGMA foreign_keys = OFF");
        context.Add(new PayrollDeductionDetail
        {
            Id = Guid.NewGuid(), PayrollId = Guid.NewGuid(), DeductionApplicationDetailId = rowId,
            DeductionId = _deductionId, Amount = 1000m,
        });
        await context.SaveChangesAsync();
    }

    [Fact]
    public async Task AddAsync_LeavesEveryOtherLoansInstallmentsAlone()
    {
        var first = await CreateAsync();
        var second = await CreateAsync(500m, 2);

        var rows = await AllRowsAsync();
        rows.Should().HaveCount(5).And.OnlyContain(x => x.DeletedAt == null, "creating one loan must not delete another's installments");
        rows.Count(x => x.DeductionApplicationId == first.Id).Should().Be(3);
        rows.Count(x => x.DeductionApplicationId == second.Id).Should().Be(2);
    }

    [Fact]
    public async Task AddAsync_BuildsEachInstallmentOnceWithItsLoanAndAnOpenBalance()
    {
        var loan = await CreateAsync();

        var rows = (await AllRowsAsync()).OrderBy(x => x.RecordOrder).ToList();
        rows.Should().HaveCount(3, "Mapster must not copy the breakdown in a second time")
            .And.OnlyContain(x => x.DeductionApplicationId == loan.Id && x.EmployeeId == _employeeId &&
                                  x.DeductionId == _deductionId && x.Balance == x.Amount && x.Amount == 1000m);
        rows.Select(x => x.RecordOrder).Should().Equal(1, 2, 3);
    }

    [Fact]
    public async Task FineOneAsync_ReturnsEachInstallmentsIdAndBalance()
    {
        var loan = await CreateAsync();

        var model = (await StoredAsync(loan.Id)).Adapt<DeductionApplicationModel>();

        model.Breakdown.Should().HaveCount(3)
            .And.OnlyContain(x => x.Id != Guid.Empty && x.Balance == 1000m && x.Status == "Unpaid");
    }

    [Fact]
    public async Task UpdateAsync_WithTheStoredIds_EditsInstallmentsInPlace()
    {
        var loan = await CreateAsync();
        var stored = await StoredAsync(loan.Id);

        var saved = await RunAsync(s => s.UpdateAsync(UpdateForm(stored, stored.Breakdown.Select(x => Echo(x, 800m))), CancellationToken.None));

        saved.Breakdown.Select(x => x.Id).Should().BeEquivalentTo(stored.Breakdown.Select(x => x.Id));
        (await AllRowsAsync()).Should().HaveCount(3).And.OnlyContain(x => x.Amount == 800m && x.Balance == 800m);
    }

    [Fact]
    public async Task UpdateAsync_WithARegeneratedSchedule_DeletesTheOldInstallments()
    {
        var loan = await CreateAsync();
        var stored = await StoredAsync(loan.Id);

        var regenerated = Enumerable.Range(0, 2)
            .Select(i => new UpdateDeductionApplicationDetail { Date = Start.AddDays(15 * i), Principal = 1500m, Amount = 1500m });
        await RunAsync(s => s.UpdateAsync(UpdateForm(stored, regenerated), CancellationToken.None));

        var rows = await AllRowsAsync();
        rows.Select(x => x.Id).Should().NotIntersectWith(stored.Breakdown.Select(x => x.Id), "replaced installments are deleted outright");
        rows.Should().HaveCount(2).And.OnlyContain(x => x.DeductionApplicationId == loan.Id && x.Balance == 1500m);
    }

    [Fact]
    public async Task UpdateAsync_KeepsAPartlyPaidInstallmentsBalance()
    {
        var loan = await CreateAsync();
        var stored = await StoredAsync(loan.Id);
        var paid = stored.Breakdown.OrderBy(x => x.RecordOrder).First();
        await PayDownAsync(paid.Id, 400m);

        // The form echoes every installment back unchanged except the later ones' amount.
        var form = UpdateForm(stored, stored.Breakdown.Select(x => Echo(x, x.Id == paid.Id ? null : 900m)));
        await RunAsync(s => s.UpdateAsync(form, CancellationToken.None));

        var rows = await AllRowsAsync();
        rows.Single(x => x.Id == paid.Id).Balance.Should().Be(600m, "an edit must not re-open what payroll already deducted");
        rows.Where(x => x.Id != paid.Id).Should().OnlyContain(x => x.Amount == 900m && x.Balance == 900m);
    }

    [Fact]
    public async Task UpdateAsync_RefusesToChangeOrDropAnInstallmentPayrollUsed()
    {
        var loan = await CreateAsync();
        var stored = await StoredAsync(loan.Id);
        var used = stored.Breakdown.OrderBy(x => x.RecordOrder).First();
        await ReferenceFromPayrollAsync(used.Id);

        var changed = UpdateForm(stored, stored.Breakdown.Select(x => Echo(x, 1.5m)));
        var dropped = UpdateForm(stored, stored.Breakdown.Where(x => x.Id != used.Id).Select(x => Echo(x)));

        await RunAsync(s => s.Invoking(x => x.UpdateAsync(changed, CancellationToken.None)).Should().ThrowAsync<Exception>().WithMessage("*already paid*"));
        await RunAsync(s => s.Invoking(x => x.UpdateAsync(dropped, CancellationToken.None)).Should().ThrowAsync<Exception>().WithMessage("*already paid*"));
        (await AllRowsAsync()).Should().HaveCount(3).And.OnlyContain(x => x.Amount == 1000m);
    }

    [Fact]
    public async Task DeleteParent_DeletesTheInstallmentsSoPayrollNoLongerDeductsThem()
    {
        var loan = await CreateAsync();

        await RunAsync(s => s.DeleteParent(loan.Id, CancellationToken.None));

        (await AllRowsAsync()).Should().BeEmpty("the installments are deleted outright with the loan");
    }

    [Fact]
    public async Task DeleteParent_IsRefusedOnceAnyInstallmentWasDeducted()
    {
        var loan = await CreateAsync();
        await PayDownAsync((await AllRowsAsync()).First().Id, 1000m);

        await RunAsync(s => s.Invoking(x => x.DeleteParent(loan.Id, CancellationToken.None)).Should().ThrowAsync<Exception>().WithMessage("*already deducted*"));
        (await AllRowsAsync()).Should().HaveCount(3);
    }

    [Fact]
    public async Task LoadAsync_DeductsOnlyLiveApprovedLoans()
    {
        var approved = await CreateAsync();
        var pending = await RunAsync(s => s.AddAsync(CreateForm(), ApprovalStatus.ForApproval, CancellationToken.None));
        var deleted = await CreateAsync();
        await using (var context = _db.NewContext())
        {
            // Soft-deleted the way a loan deleted before this fix was left: its installments live.
            var loan = await context.DeductionApplications.SingleAsync(x => x.Id == deleted.Id);
            loan.DeletedAt = DateTime.UtcNow;
            await context.SaveChangesAsync();
        }

        await using var read = _db.NewContext();
        var result = await new DeductionAplDtlService(new UnitOfWorkService(read))
            .LoadAsync([_employeeId], Start, Start.AddYears(1), CancellationToken.None);

        var approvedIds = (await AllRowsAsync()).Where(x => x.DeductionApplicationId == approved.Id).Select(x => x.Id);
        result[new EmployeeKey(_employeeId)].Select(x => x.Id).Should().BeEquivalentTo(approvedIds,
            "a pending loan's and a deleted loan's installments are not deducted");
        pending.ApprovalStatus.Should().Be(ApprovalStatus.ForApproval);
    }
}
