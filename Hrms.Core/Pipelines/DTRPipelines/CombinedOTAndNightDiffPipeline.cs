namespace Hrms.Core.Pipelines.DTRPipelines;

internal class CombinedOTAndNightDiffPipeline : BasicPipelineBase
{
    public override BasicPipelineData Run(PayrollContext context)
    {
        pipeline
            .AddPolicy(new OvertimePolicy())
            .AddPolicy(new NightDiffPolicy());

        return pipeline.Execute(new BasicPipelineData(), context);
    }
}

internal class CombinedWorkHoursAndNightDiffPipeline : BasicPipelineBase
{
    public override BasicPipelineData Run(PayrollContext context)
    {
        pipeline
            .AddPolicy(new OvertimePolicy())
            .AddPolicy(new NightDiffPolicy());

        return pipeline.Execute(new BasicPipelineData(), context);
    }
}


