
namespace Hrms.Core.Pipelines;

public class RegularPipeline : BasicPipelineBase
{
    public override BasicPipelineData Run(PayrollContext context)
    {
        pipeline
            .AddPolicy(new RegularWorkDayPolicy())
            ;
        return pipeline.Execute(new BasicPipelineData(), context);
    }
}


