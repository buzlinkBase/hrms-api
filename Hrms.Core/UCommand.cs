using Hrms.Infrastructure.Data;
namespace Hrms.Core.Services;
public interface IUnitOfWorkService : IUnitOfWork<HrmsContext> { }
public class UCommand : UnitOfWork<HrmsContext>, IUnitOfWorkService
{
    public UCommand(HrmsContext context) : base(context)
    {
        OnCommitChanges += UCommand_OnCommitChanges;
    }
    private void UCommand_OnCommitChanges(object? sender, CommitChangesResponse e)
    {
    }
}
