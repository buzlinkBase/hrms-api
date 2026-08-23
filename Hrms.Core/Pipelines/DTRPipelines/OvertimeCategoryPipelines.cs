namespace Hrms.Core.Pipelines;

public class RegularOTPipeLine : BasicPipelineBase
{
    public override BasicPipelineData Run(PayrollContext context)
    {
        pipeline.AddPolicy(new RegularOTPolicy());
        return pipeline.Execute(new BasicPipelineData(), context);
    }
}

public class RestDayOTPipeLine : BasicPipelineBase
{
    public override BasicPipelineData Run(PayrollContext context)
    {
        pipeline.AddPolicy(new RestDayOTPolicy());
        return pipeline.Execute(new BasicPipelineData(), context);
    }
}

public class LegalHolOTPipeLine : BasicPipelineBase
{
    public override BasicPipelineData Run(PayrollContext context)
    {
        pipeline.AddPolicy(new LegalHolOTPolicy());
        return pipeline.Execute(new BasicPipelineData(), context);
    }
}

public class RestLegalDayOTPipeLine : BasicPipelineBase
{
    public override BasicPipelineData Run(PayrollContext context)
    {
        pipeline.AddPolicy(new RestLegalDayOTPolicy());
        return pipeline.Execute(new BasicPipelineData(), context);
    }
}

public class SpecialNonWorkingOTPipeLine : BasicPipelineBase
{
    public override BasicPipelineData Run(PayrollContext context)
    {
        pipeline.AddPolicy(new SpecialNonWorkingOTPolicy());
        return pipeline.Execute(new BasicPipelineData(), context);
    }
}

public class RestSpecialDayOTPipeLine : BasicPipelineBase
{
    public override BasicPipelineData Run(PayrollContext context)
    {
        pipeline.AddPolicy(new RestSpecialDayOTPolicy());
        return pipeline.Execute(new BasicPipelineData(), context);
    }
}



public class DoubleLegalOTPipeLine : BasicPipelineBase
{
    public override BasicPipelineData Run(PayrollContext context)
    {
        pipeline.AddPolicy(new DoubleLegalOTPolicy());
        return pipeline.Execute(new BasicPipelineData(), context);
    }
}

public class RestDoubleLegalOTPipeLine : BasicPipelineBase
{
    public override BasicPipelineData Run(PayrollContext context)
    {
        pipeline.AddPolicy(new RestDoubleLegalOTPolicy());
        return pipeline.Execute(new BasicPipelineData(), context);
    }
}
