
namespace Hrms.Core.Pipelines;

public abstract class BasicPipelineBase : BasePipelineBase<PayrollContext, BasicPipelineData> { }
public abstract class BasicPipelineCollectionBase : BasePipelineBase<PayrollContext, LineCollection<BasicPipelineData>> { }
public abstract class BasePipelineBase<Context, lineData> : IPipeLine<Context, lineData>
    where lineData : IPipeData, new()
    where Context : IPayloadContext
{
    protected abstract void ConfigurePolicies(PayrollPipeLine<Context, lineData> pipeline);
    protected virtual lineData CreateSeed(Context context) => new lineData();

    public lineData Run(Context context)
    {
        var pipeline = new PayrollPipeLine<Context, lineData>();
        ConfigurePolicies(pipeline);
        return pipeline.Execute(CreateSeed(context), context);
    }
}



