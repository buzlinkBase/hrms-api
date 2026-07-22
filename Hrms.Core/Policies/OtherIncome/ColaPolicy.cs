namespace Hrms.Core.Policies.OtherIncome;

internal class ColaPolicy : PayrollPolicyBase<AllowancePipeData, PayrollContext>
{
    public override AllowancePipeData ApplyIfSatisfied(AllowancePipeData line, PayrollContext context)
    {
        //TODO add logic here if cola will divided per frequency or by payroll
        // currently we add cola per payroll basis
        //var days = context.Payload.DaysDiffPayrollPeriod(context) 
        //    + 1
        //    - (context.Payload.);
        var cola = context.Employee.Cola;
        //switch (context.Employee.PayrollFrequency)
        //{
        //    case PayrollFrequency.DAILY:
        //        line.Cola = cola * days;
        //        line.RunningTotal += cola;
        //        return line;
        //    case PayrollFrequency.WEEKLY:
        //        line.Cola = cola;
        //        line.RunningTotal += cola;
        //        return line;
        //    case PayrollFrequency.SEMI_MONTHLY:
        //        break;
        //    case PayrollFrequency.MONTHLY:
        //        break;
        //    default:
        //        break;
        //}
        line.Cola = cola;
        line.RunningTotal += cola;
        return line;
    }
}

