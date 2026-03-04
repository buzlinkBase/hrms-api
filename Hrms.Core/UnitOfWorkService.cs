namespace Hrms.Core.Services;
public interface IUnitOfWorkService : IUnitOfWork<HrmsContext> { }
public class UnitOfWorkService : UnitOfWork<HrmsContext>, IUnitOfWorkService
{
    public UnitOfWorkService(HrmsContext context) : base(context) { }
}
