
namespace Hrms.Core.Pipelines;

public class RegularPipeline : BasicPipelineBase
{
    protected override void ConfigurePolicies(PayrollPipeLine<PayrollContext, BasicPipelineData> pipeline)
    {
        pipeline.AddPolicy(new RegularWorkDayPolicy());
    }
}


