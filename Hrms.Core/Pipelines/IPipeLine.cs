
namespace Hrms.Core.Pipelines;

public interface IPipeLine<Context, Model>
    where Context : IPayloadContext
{
    Model Run(Context context);
}
