namespace Hrms.Core.Pipelines;

public class SpecialWorkDayPipeLine : BasicPipelineBase
{
    public override BasicPipelineData Run(PayrollContext context)
    {
        pipeline
            .AddPolicy(new SpecialWorkDayPolicy())
            ;
        return pipeline.Execute(new BasicPipelineData(), context);
    }
}
