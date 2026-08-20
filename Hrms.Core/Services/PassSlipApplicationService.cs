using Hrms.Domain.Entities;
using Hrms.Domain.ValueObjects;
using Mapster;
using Microsoft.EntityFrameworkCore;

namespace Hrms.Core.Services;

public class PassSlipApplicationService : BaseService<PassSlipApplication>
{
    private readonly AttendanceService _attendanceService;

    public PassSlipApplicationService(IUnitOfWorkService uow, AttendanceService attendanceService) : base(uow)
    {
        _attendanceService = attendanceService;
    }

    public async Task AddAsync(PassSlipApplication model, CancellationToken token)
    {
        model.ApprovalStatus = ApprovalStatus.ForApproval;
        await CreateAsync(model, token);
        await CommitChangesAsync(token);
    }

    public async Task UpdateAsync(UpdatePassSlipApplication payload, CancellationToken token)
    {
        var existing = await Context.PassSlipApplications.FindAsync(new object[] { payload.Id }, token);
        if (existing == null) throw new NotFoundException("Record not found");
        payload.Adapt(existing);
        await ModifyAsync(existing, token);
        await CommitChangesAsync(token);
    }

    public async Task ApproveAsync(Guid id, CancellationToken token)
    {
        var passSlip = await Context.PassSlipApplications.FindAsync(new object[] { id }, token);
        if (passSlip == null) throw new NotFoundException("Record not found");

        var batchCode = $"PASS-{id:N}";
        passSlip.ApprovalStatus = ApprovalStatus.Approved;
        passSlip.BatchCode = batchCode;

        var records = new List<Attendance>
        {
            new Attendance
            {
                EmployeeId = passSlip.EmployeeId,
                WorkDateTime = passSlip.DepartureTime,
                Workstate = 1,
                LogSource = LOGSOURCE.MANUAL,
                LogRemarks = $"Pass Slip - {passSlip.Purpose}",
                BatchCode = batchCode,
                UserName = string.Empty,
            }
        };

        if (passSlip.ReturnTime.HasValue)
        {
            records.Add(new Attendance
            {
                EmployeeId = passSlip.EmployeeId,
                WorkDateTime = passSlip.ReturnTime.Value,
                Workstate = 0,
                LogSource = LOGSOURCE.MANUAL,
                LogRemarks = $"Pass Slip Return - {passSlip.Purpose}",
                BatchCode = batchCode,
                UserName = string.Empty,
            });
        }

        await _attendanceService.AddRangeAsync(records, token);
        await ModifyAsync(passSlip, token);
        await CommitChangesAsync(token);
    }

    public async Task RevokeAsync(Guid id, CancellationToken token)
    {
        var passSlip = await Context.PassSlipApplications.FindAsync(new object[] { id }, token);
        if (passSlip == null) throw new NotFoundException("Record not found");

        if (!string.IsNullOrEmpty(passSlip.BatchCode))
            await _attendanceService.RemoveBatch(passSlip.BatchCode, token);

        passSlip.ApprovalStatus = ApprovalStatus.Cancelled;
        passSlip.BatchCode = string.Empty;
        await ModifyAsync(passSlip, token);
        await CommitChangesAsync(token);
    }

    public async Task<List<PassSlipApplication>> FindAllAsync(DateOnly? from, DateOnly? to, Guid? employeeId, CancellationToken token)
    {
        var query = Context.PassSlipApplications
            .Include(x => x.Employee)
            .AsQueryable();

        if (from.HasValue) query = query.Where(x => x.ApplicationDate >= from.Value);
        if (to.HasValue) query = query.Where(x => x.ApplicationDate <= to.Value);
        if (employeeId.HasValue) query = query.Where(x => x.EmployeeId == employeeId.Value);

        return await query.OrderByDescending(x => x.ApplicationDate).ToListAsync(token);
    }

    public async Task<PassSlipApplication?> FineOneAsync(Guid id, CancellationToken token)
    {
        return await Context.PassSlipApplications
            .Include(x => x.Employee)
            .FirstOrDefaultAsync(x => x.Id == id, token);
    }

    public async Task Delete(Guid id, CancellationToken token)
    {
        await RemoveAsync(id, token);
        await CommitChangesAsync(token);
    }
}
