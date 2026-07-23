namespace Hrms.adms.Services;

public interface IUnitOfWorkService : IUnitOfWork<AdmsContext> { }
public class UnitOfWorkService : UnitOfWork<AdmsContext>, IUnitOfWorkService
{
    public UnitOfWorkService(AdmsContext context) : base(context) { }
}
