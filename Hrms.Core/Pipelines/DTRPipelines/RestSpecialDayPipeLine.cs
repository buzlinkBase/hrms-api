namespace Hrms.Core.Pipelines;

public class RestSpecialDayPipeLine : BasicPipelineBase
{
    public override BasicPipelineData Run(PayrollContext context)
    {
        pipeline
            .AddPolicy(new RestSpecialDayPolicy())
            ;
        return pipeline.Execute(new BasicPipelineData(), context);
    }
}
