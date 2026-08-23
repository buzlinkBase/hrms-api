namespace Hrms.Core.Pipelines;

public class RegularNDPipeLine : BasicPipelineBase
{
    public override BasicPipelineData Run(PayrollContext context)
    {
        pipeline.AddPolicy(new RegularNDPolicy());
        return pipeline.Execute(new BasicPipelineData(), context);
    }
}

public class RestDayNDPipeLine : BasicPipelineBase
{
    public override BasicPipelineData Run(PayrollContext context)
    {
        pipeline.AddPolicy(new RestDayNDPolicy());
        return pipeline.Execute(new BasicPipelineData(), context);
    }
}

public class LegalHolNDPipeLine : BasicPipelineBase
{
    public override BasicPipelineData Run(PayrollContext context)
    {
        pipeline.AddPolicy(new LegalHolNDPolicy());
        return pipeline.Execute(new BasicPipelineData(), context);
    }
}

public class RestLegalDayNDPipeLine : BasicPipelineBase
{
    public override BasicPipelineData Run(PayrollContext context)
    {
        pipeline.AddPolicy(new RestLegalDayNDPolicy());
        return pipeline.Execute(new BasicPipelineData(), context);
    }
}

 

public class SpecialNonWorkingNDPipeLine : BasicPipelineBase
{
    public override BasicPipelineData Run(PayrollContext context)
    {
        pipeline.AddPolicy(new SpecialNonWorkingNDPolicy());
        return pipeline.Execute(new BasicPipelineData(), context);
    }
}

public class RestSpecialDayNDPipeLine : BasicPipelineBase
{
    public override BasicPipelineData Run(PayrollContext context)
    {
        pipeline.AddPolicy(new RestSpecialDayNDPolicy());
        return pipeline.Execute(new BasicPipelineData(), context);
    }
}


public class DoubleLegalNDPipeLine : BasicPipelineBase
{
    public override BasicPipelineData Run(PayrollContext context)
    {
        pipeline.AddPolicy(new DoubleLegalNDPolicy());
        return pipeline.Execute(new BasicPipelineData(), context);
    }
}

public class RestDoubleLegalNDPipeLine : BasicPipelineBase
{
    public override BasicPipelineData Run(PayrollContext context)
    {
        pipeline.AddPolicy(new RestDoubleLegalNDPolicy());
        return pipeline.Execute(new BasicPipelineData(), context);
    }
}
