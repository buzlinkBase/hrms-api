namespace Hrms.Core.Pipelines;

public class RegularHolidayPipeLine : BasicPipelineBase
{
    public override BasicPipelineData Run(PayrollContext context)
    {
        pipeline
            .AddPolicy(new RegularHolidayPolicy())
            ;
        return pipeline.Execute(new BasicPipelineData(), context);
    }
}
