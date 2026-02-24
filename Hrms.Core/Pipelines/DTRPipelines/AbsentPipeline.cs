namespace Hrms.Core.Pipelines;

public class AbsentPipeline : BasicPipelineBase
{
    public override BasicPipelineData Run(PayrollContext context)
    {
        pipeline.AddPolicy(new AbsentDayPolicy());
        return pipeline.Execute(new BasicPipelineData(), context);
    }
}

public class LatesPipeLine : BasicPipelineBase
{
    public override BasicPipelineData Run(PayrollContext context)
    {
        pipeline
            .AddPolicy(new LatePolicy())
            ;
        return pipeline.Execute(new BasicPipelineData(), context);
    }
}

public class UnderTimePipeLine : BasicPipelineBase
{
    public override BasicPipelineData Run(PayrollContext context)
    {
        pipeline
            .AddPolicy(new UndertimePolicy())
            ;
        return pipeline.Execute(new BasicPipelineData(), context);
    }
}



