namespace Hrms.Core.Pipelines;

public class DoubleLegalPipeLine : BasicPipelineBase
{
    public override BasicPipelineData Run(PayrollContext context)
    {
        pipeline
            .AddPolicy(new DoubleLegalPolicy())
            ;
        return pipeline.Execute(new BasicPipelineData(), context);
    }
}
