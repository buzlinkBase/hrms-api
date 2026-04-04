namespace Hrms.adms.Core.Services;

public class AttendanceService : BaseService<Attendance>
{
    public AttendanceService(IUnitOfWorkService service) : base(service)
    {
    }
    public async Task AddRangeAsync(List<Attendance> attendances)
    {
        await CreateRangeAsync(attendances);
    }

    public async Task UpdateSync(Guid batchId)
    {
        await Context.Attendances
             .Where(x => x.BatchId == batchId)
             .ExecuteUpdateAsync(setters =>
                 setters.SetProperty(a => a.Synced, true)
             );
    }
}
