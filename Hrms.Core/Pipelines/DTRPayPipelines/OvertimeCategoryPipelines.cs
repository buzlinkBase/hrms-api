namespace Hrms.Core.Pipelines;

public class RegularOTPipeLine : BasicPipelineBase
{
    protected override void ConfigurePolicies(PayrollPipeLine<PayrollContext, BasicPipelineData> pipeline)
    {
        pipeline.AddPolicy(new RegularOTPolicy());
    }
}

public class RestDayOTPipeLine : BasicPipelineBase
{
    protected override void ConfigurePolicies(PayrollPipeLine<PayrollContext, BasicPipelineData> pipeline)
    {
        pipeline.AddPolicy(new RestDayOTPolicy());
    }
}

public class LegalHolOTPipeLine : BasicPipelineBase
{
    protected override void ConfigurePolicies(PayrollPipeLine<PayrollContext, BasicPipelineData> pipeline)
    {
        pipeline.AddPolicy(new LegalHolOTPolicy());
    }
}

public class RestLegalDayOTPipeLine : BasicPipelineBase
{
    protected override void ConfigurePolicies(PayrollPipeLine<PayrollContext, BasicPipelineData> pipeline)
    {
        pipeline.AddPolicy(new RestLegalDayOTPolicy());
    }
}

public class SpecialNonWorkingOTPipeLine : BasicPipelineBase
{
    protected override void ConfigurePolicies(PayrollPipeLine<PayrollContext, BasicPipelineData> pipeline)
    {
        pipeline.AddPolicy(new SpecialNonWorkingOTPolicy());
    }
}

public class RestSpecialDayOTPipeLine : BasicPipelineBase
{
    protected override void ConfigurePolicies(PayrollPipeLine<PayrollContext, BasicPipelineData> pipeline)
    {
        pipeline.AddPolicy(new RestSpecialDayOTPolicy());
    }
}



public class DoubleLegalOTPipeLine : BasicPipelineBase
{
    protected override void ConfigurePolicies(PayrollPipeLine<PayrollContext, BasicPipelineData> pipeline)
    {
        pipeline.AddPolicy(new DoubleLegalOTPolicy());
    }
}

public class RestDoubleLegalOTPipeLine : BasicPipelineBase
{
    protected override void ConfigurePolicies(PayrollPipeLine<PayrollContext, BasicPipelineData> pipeline)
    {
        pipeline.AddPolicy(new RestDoubleLegalOTPolicy());
    }
}
