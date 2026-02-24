
namespace Hrms.Core.Pipelines;

public abstract class BasicPipelineBase : BasePipelineBase<PayrollContext, BasicPipelineData> { }
public abstract class BasicPipelineCollectionBase : BasePipelineBase<PayrollContext, LineCollection<BasicPipelineData>> { }
public abstract class BasePipelineBase<Context, lineData> : IPipeLine<Context, lineData>
    where lineData : IPipeData, new()
    where Context : IPayloadContext
{
    protected PayrollPipeLine<Context, lineData> pipeline;
    protected BasePipelineBase()
    {
        pipeline = new PayrollPipeLine<Context, lineData>();
    }
    public abstract lineData Run(Context context);
}



