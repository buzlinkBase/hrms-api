namespace Hrms.Core.Pipelines;

public class RegularNDOTPipeLine : BasicPipelineBase
{
    public override BasicPipelineData Run(PayrollContext context)
    {
        pipeline.AddPolicy(new RegularNDOTPolicy());
        return pipeline.Execute(new BasicPipelineData(), context);
    }
}

public class RestDayNDOTPipeLine : BasicPipelineBase
{
    public override BasicPipelineData Run(PayrollContext context)
    {
        pipeline.AddPolicy(new RestDayNDOTPolicy());
        return pipeline.Execute(new BasicPipelineData(), context);
    }
}

public class LegalHolNDOTPipeLine : BasicPipelineBase
{
    public override BasicPipelineData Run(PayrollContext context)
    {
        pipeline.AddPolicy(new LegalHolNDOTPolicy());
        return pipeline.Execute(new BasicPipelineData(), context);
    }
}

public class RestLegalDayNDOTPipeLine : BasicPipelineBase
{
    public override BasicPipelineData Run(PayrollContext context)
    {
        pipeline.AddPolicy(new RestLegalDayNDOTPolicy());
        return pipeline.Execute(new BasicPipelineData(), context);
    }
}


public class SpecialNonWorkingNDOTPipeLine : BasicPipelineBase
{
    public override BasicPipelineData Run(PayrollContext context)
    {
        pipeline.AddPolicy(new SpecialNonWorkingNDOTPolicy());
        return pipeline.Execute(new BasicPipelineData(), context);
    }
}

public class RestSpecialDayNDOTPipeLine : BasicPipelineBase
{
    public override BasicPipelineData Run(PayrollContext context)
    {
        pipeline.AddPolicy(new RestSpecialDayNDOTPolicy());
        return pipeline.Execute(new BasicPipelineData(), context);
    }
}

public class DoubleLegalNDOTPipeLine : BasicPipelineBase
{
    public override BasicPipelineData Run(PayrollContext context)
    {
        pipeline.AddPolicy(new DoubleLegalNDOTPolicy());
        return pipeline.Execute(new BasicPipelineData(), context);
    }
}

public class RestDoubleLegalNDOTPipeLine : BasicPipelineBase
{
    public override BasicPipelineData Run(PayrollContext context)
    {
        pipeline.AddPolicy(new RestDoubleLegalNDOTPolicy());
        return pipeline.Execute(new BasicPipelineData(), context);
    }
}
