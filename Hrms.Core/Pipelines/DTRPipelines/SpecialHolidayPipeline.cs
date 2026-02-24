namespace Hrms.Core.Pipelines;

public class SpecialHolidayPipeline : BasicPipelineBase
{
    public override BasicPipelineData Run(PayrollContext context)
    {
        pipeline.AddPolicy(new SpecialHolidayPolicy());
        return pipeline.Execute(new BasicPipelineData(), context);
    }
}
