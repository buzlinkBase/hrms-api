namespace Hrms.Core.Pipelines;

public class RegularNDOTPipeLine : BasicPipelineBase
{
    protected override void ConfigurePolicies(PayrollPipeLine<PayrollContext, BasicPipelineData> pipeline)
    {
        pipeline.AddPolicy(new RegularNDOTPolicy());
    }
}

public class RestDayNDOTPipeLine : BasicPipelineBase
{
    protected override void ConfigurePolicies(PayrollPipeLine<PayrollContext, BasicPipelineData> pipeline)
    {
        pipeline.AddPolicy(new RestDayNDOTPolicy());
    }
}

public class LegalHolNDOTPipeLine : BasicPipelineBase
{
    protected override void ConfigurePolicies(PayrollPipeLine<PayrollContext, BasicPipelineData> pipeline)
    {
        pipeline.AddPolicy(new LegalHolNDOTPolicy());
    }
}

public class RestLegalDayNDOTPipeLine : BasicPipelineBase
{
    protected override void ConfigurePolicies(PayrollPipeLine<PayrollContext, BasicPipelineData> pipeline)
    {
        pipeline.AddPolicy(new RestLegalDayNDOTPolicy());
    }
}


public class SpecialNonWorkingNDOTPipeLine : BasicPipelineBase
{
    protected override void ConfigurePolicies(PayrollPipeLine<PayrollContext, BasicPipelineData> pipeline)
    {
        pipeline.AddPolicy(new SpecialNonWorkingNDOTPolicy());
    }
}

public class RestSpecialDayNDOTPipeLine : BasicPipelineBase
{
    protected override void ConfigurePolicies(PayrollPipeLine<PayrollContext, BasicPipelineData> pipeline)
    {
        pipeline.AddPolicy(new RestSpecialDayNDOTPolicy());
    }
}

public class DoubleLegalNDOTPipeLine : BasicPipelineBase
{
    protected override void ConfigurePolicies(PayrollPipeLine<PayrollContext, BasicPipelineData> pipeline)
    {
        pipeline.AddPolicy(new DoubleLegalNDOTPolicy());
    }
}

public class RestDoubleLegalNDOTPipeLine : BasicPipelineBase
{
    protected override void ConfigurePolicies(PayrollPipeLine<PayrollContext, BasicPipelineData> pipeline)
    {
        pipeline.AddPolicy(new RestDoubleLegalNDOTPolicy());
    }
}
